import assert from "node:assert/strict";
import { loadEnvFile } from "node:process";
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";
import {
  createWorkspaceClient,
  loadSchedule,
  workspaceSchema,
  ApiError,
} from "../apps/web/src/lib/workspace-api.ts";
import {
  simulationSchema,
  candidatesSchema,
  auditSchema,
} from "../apps/web/src/lib/contracts.ts";

loadEnvFile(new URL("../.env", import.meta.url));
const origin =
  process.env.SMOKE_BASE_URL ??
  `http://127.0.0.1:${process.env.WEB_PORT ?? 3000}`;
assert.ok(["localhost", "127.0.0.1"].includes(new URL(origin).hostname));
const cookies = new Map();
const cookieHeader = () =>
  [...cookies].map(([name, value]) => `${name}=${value}`).join("; ");
const client = createWorkspaceClient(async (path, options) => {
  const headers = new Headers(options?.headers);
  headers.set("Cookie", cookieHeader());
  const response = await fetch(new URL(String(path), origin), {
    ...options,
    headers,
  });
  for (const cookie of response.headers.getSetCookie()) {
    const pair = cookie.split(";")[0];
    const separator = pair.indexOf("=");
    cookies.set(pair.slice(0, separator), pair.slice(separator + 1));
  }
  return response;
});
await client.save("/api/v1/auth/login", {
  email: "manager@northline.example",
  password: process.env.DEMO_MANAGER_PASSWORD,
});
await client.session();
const source = "10000000-0000-0000-0000-000000000001";
let lab = simulationSchema.parse(
  await (
    await client.save(`/api/v1/organizations/${source}/simulation`, {})
  ).json(),
);
const base = `/api/v1/organizations/${lab.organizationId}/`;
// Repeated smoke runs reset only this manager's lab, preserving ordinary scheduling data.
lab = simulationSchema.parse(
  await (
    await client.save(base + "simulation/reset", {}, "POST", lab.generation)
  ).json(),
);
const workspace = await client.get(base + "workspace", workspaceSchema);
const schedule = () =>
  loadSchedule(client, lab.organizationId, lab.locationId, lab.week, true);
assert.equal((await schedule()).gaps.length, 0);
const hub = new HubConnectionBuilder()
  .withUrl(origin + "/hubs/workspace", {
    transport: HttpTransportType.LongPolling,
    headers: { Cookie: cookieHeader() },
  })
  .configureLogging(LogLevel.None)
  .build();
const announced = Promise.withResolvers();
hub.on("WorkspaceChanged", (change) => {
  if (change.organizationId === lab.organizationId) announced.resolve(change);
});
try {
  await hub.start();
  await hub.invoke("JoinWorkspace", lab.organizationId);
  await client.save(base + "simulation/apply", {}, "POST", lab.generation);
  const change = await Promise.race([
    announced.promise,
    new Promise((_, reject) => {
      const timer = setTimeout(
        () => reject(new Error("No committed invalidation arrived")),
        5000,
      );
      timer.unref();
    }),
  ]);
  assert.equal(change.generation, lab.generation);
  assert.equal((await schedule()).gaps[0].missingCount, 2);
  const request = (await schedule()).requests.find(
    (r) => r.status === "Pending",
  );
  assert.ok(request);
  const candidates = await client.get(
    base + `shifts/${request.shiftId}/candidates`,
    candidatesSchema,
  );
  assert.equal(candidates.eligible.length, 2);
  await client.save(
    base + `coverage-requests/${request.id}/approve`,
    {
      version: request.version,
      shiftVersion: candidates.shiftVersion,
      userId: candidates.eligible[0].userId,
    },
    "POST",
    lab.generation,
  );
  assert.equal((await schedule()).gaps[0].missingCount, 1);
  const standby = await client.get(
    base + `shifts/${lab.standbyShiftId}/candidates`,
    candidatesSchema,
  );
  await client.save(
    base + `shifts/${lab.standbyShiftId}/assignment`,
    { version: standby.shiftVersion, userId: standby.eligible[0].userId },
    "PATCH",
    lab.generation,
  );
  assert.equal((await schedule()).gaps.length, 0);
  const old = lab.generation;
  lab = simulationSchema.parse(
    await (
      await client.save(base + "simulation/reset", {}, "POST", old)
    ).json(),
  );
  assert.notEqual(lab.generation, old);
  await assert.rejects(
    client.save(base + "simulation/apply", {}, "POST", old),
    (error) => error instanceof ApiError && error.status === 409,
  );
  const audit = await client.get(base + "audit", auditSchema);
  assert.ok(audit.some((entry) => entry.action === "CoverageRequest.Approved"));
  assert.ok(audit.some((entry) => entry.action === "Simulation.Reset"));
  assert.ok(workspace.simulation);
} finally {
  await hub.stop();
  await client.save("/api/v1/auth/logout");
}
console.log(
  "Milestone 2 HTTP/hub workflow passed: baseline → rush (2) → approval (1) → standby (0) → guarded reset, preserved audit and real post-commit SignalR notification.",
);
