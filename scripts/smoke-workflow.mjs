import assert from "node:assert/strict";
import { loadEnvFile } from "node:process";
import {
  ApiError,
  createWorkspaceClient,
  loadSchedule,
  workspaceSchema,
} from "../apps/web/src/lib/workspace-api.ts";
import { addDays } from "../apps/web/src/lib/schedule-view.ts";

loadEnvFile(new URL("../.env", import.meta.url));
const origin =
  process.env.SMOKE_BASE_URL ??
  `http://127.0.0.1:${process.env.WEB_PORT ?? 3000}`;
assert.ok(
  ["127.0.0.1", "localhost"].includes(new URL(origin).hostname),
  "Smoke tests are restricted to the local development stack.",
);
const managerPassword = process.env.DEMO_MANAGER_PASSWORD;
const employeePassword = process.env.DEMO_EMPLOYEE_PASSWORD;
assert.ok(
  managerPassword && employeePassword,
  "Generate ignored local demo credentials before running this workflow.",
);

function clientWithCookies(cookies = new Map()) {
  return createWorkspaceClient(async (path, options) => {
    const headers = new Headers(options?.headers);
    headers.set(
      "Cookie",
      [...cookies].map(([name, value]) => `${name}=${value}`).join("; "),
    );
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
}
const manager = clientWithCookies();
const employee = clientWithCookies();
await manager.save("/api/v1/auth/login", {
  email: "manager@northline.example",
  password: managerPassword,
});
await employee.save("/api/v1/auth/login", {
  email: "employee@northline.example",
  password: employeePassword,
});
const session = await manager.session();
const employeeSession = await employee.session();
assert.ok(
  session && employeeSession,
  "Both demo sessions must be established.",
);
const org = session.memberships.find(
  (m) => m.organizationName === "Northline Coffee",
)?.organizationId;
assert.ok(org, "The seeded organization must exist.");
const base = `/api/v1/organizations/${org}/`;
const workspace = await manager.get(base + "workspace", workspaceSchema);
const location = workspace.locations[0];
const barista = workspace.skills.find((s) => s.name === "Barista");
assert.ok(
  location && barista,
  "The seed must include a location and barista qualification.",
);
const week = workspace.suggestedWeek;
const before = await loadSchedule(manager, org, location.id, week, true);
const existing = new Set(before.shifts.map((s) => s.id));
let interval;
for (let day = 0; day < 7 && !interval; day++) {
  for (let hour = 8; hour <= 18 && !interval; hour += 2) {
    const date = addDays(week, day);
    const startsAt = `${date}T${String(hour).padStart(2, "0")}:00`;
    const endsAt = `${date}T${String(hour + 2).padStart(2, "0")}:00`;
    // Seeded Toronto days are ordinary dates; compare local strings via Intl, not the host zone.
    const local = (instant) => {
      const parts = new Intl.DateTimeFormat("sv-SE", {
        timeZone: location.timeZoneId,
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        hour12: false,
      }).format(new Date(instant));
      return parts.replace(" ", "T");
    };
    const overlaps = before.shifts.some(
      (s) =>
        s.assigneeUserId === employeeSession.id &&
        local(s.startsAt) < endsAt &&
        startsAt < local(s.endsAt),
    );
    const available = before.availability.some(
      (a) =>
        a.userId === employeeSession.id &&
        local(a.startsAt) <= startsAt &&
        local(a.endsAt) >= endsAt,
    );
    if (!overlaps && available) interval = { startsAt, endsAt };
  }
}
assert.ok(
  interval,
  "No free seeded availability remains. Use a fresh local test database for this smoke workflow.",
);
await manager.save(base + "shifts", {
  locationId: location.id,
  skillId: barista.id,
  ...interval,
});
const createdData = await loadSchedule(manager, org, location.id, week, true);
const shift = createdData.shifts.find((s) => !existing.has(s.id));
assert.ok(shift, "Created shift must be readable from the API.");
await manager.save(
  base + `shifts/${shift.id}/assignment`,
  { userId: employeeSession.id, version: shift.version },
  "PATCH",
);
const freshManager = clientWithCookies();
await freshManager.save("/api/v1/auth/login", {
  email: "manager@northline.example",
  password: managerPassword,
});
assert.ok(
  (await loadSchedule(freshManager, org, location.id, week, true)).shifts.some(
    (s) => s.id === shift.id && s.assigneeUserId === employeeSession.id,
  ),
  "A fresh session must read the persisted assignment.",
);
assert.ok(
  (await loadSchedule(employee, org, location.id, week, false)).shifts.some(
    (s) => s.id === shift.id,
  ),
  "The employee must see the assigned shift.",
);
await employee.save(base + "availability", {
  locationId: location.id,
  ...interval,
});
await employee.save(base + "coverage-requests", { shiftId: shift.id });
assert.ok(
  (await loadSchedule(manager, org, location.id, week, true)).requests.some(
    (r) => r.shiftId === shift.id && r.requesterUserId === employeeSession.id,
  ),
  "The manager must see the employee's saved request.",
);
const sharedCookies = new Map();
const staleTab = clientWithCookies(sharedCookies);
const switchedTab = clientWithCookies(sharedCookies);
await staleTab.save("/api/v1/auth/login", {
  email: "employee@northline.example",
  password: employeePassword,
});
await staleTab.session();
await switchedTab.save("/api/v1/auth/login", {
  email: "manager@northline.example",
  password: managerPassword,
});
const availabilityBeforeSwitch = (
  await loadSchedule(manager, org, location.id, week, true)
).availability.length;
await assert.rejects(
  staleTab.save(base + "availability", {
    locationId: location.id,
    ...interval,
  }),
  (error) => error instanceof ApiError && error.status === 401,
);
assert.equal(
  (await loadSchedule(manager, org, location.id, week, true)).availability
    .length,
  availabilityBeforeSwitch,
);
await staleTab.save("/api/v1/auth/login", {
  email: "manager@northline.example",
  password: managerPassword,
});
assert.equal((await staleTab.session()).id, session.id);
for (const client of [manager, employee, freshManager, staleTab])
  await client.save("/api/v1/auth/logout");
assert.equal(await manager.session(), null);
console.log(
  "HTTP workflow passed through the web proxy: login → create → assign → fresh-session persistence → employee availability/request → manager visibility → stale-tab write blocked → explicit re-login → logout.",
);
console.log(
  "The synthetic shift and pending request remain in the local demo so you can inspect them.",
);
