import assert from "node:assert/strict";
import { test } from "node:test";
import {
  loadConnectionState,
  createConnectionCheck,
  type ConnectionState,
} from "../src/lib/api.ts";

const metadata = { name: "RUSHCTRL", version: "0.1.0", milestone: 0 };
test("a new milestone remains healthy while invalid milestone values are rejected", async () => {
  assert.equal(await loadConnectionState(respond(Response.json({ ...metadata, milestone: 1 }), Response.json({ status: "healthy" }))), "ready");
  for (const milestone of [-1, "1", 1.5])
    assert.equal(await loadConnectionState(respond(Response.json({ ...metadata, milestone }), Response.json({ status: "healthy" }))), "api-unavailable");
});
function respond(
  system: Response | Error,
  database: Response | Error,
): typeof fetch {
  return (async (input: RequestInfo | URL) => {
    const result = String(input).includes("/api/v1/system") ? system : database;
    if (result instanceof Error) throw result;
    return result;
  }) as typeof fetch;
}

test("reports ready only when the API contract and database readiness both succeed", async () => {
  const state = await loadConnectionState(
    respond(Response.json(metadata), Response.json({ status: "healthy" })),
  );
  assert.equal(state, "ready");
});

test("reports an unavailable API after a failed HTTP response", async () => {
  assert.equal(
    await loadConnectionState(
      respond(
        new Response(null, { status: 500 }),
        Response.json({ status: "healthy" }),
      ),
    ),
    "api-unavailable",
  );
});

test("reports database unavailable when readiness returns 503", async () => {
  assert.equal(
    await loadConnectionState(
      respond(
        Response.json(metadata),
        Response.json({ status: "unhealthy" }, { status: 503 }),
      ),
    ),
    "database-unavailable",
  );
});

test("rejects a successful but malformed API payload", async () => {
  assert.equal(
    await loadConnectionState(
      respond(
        Response.json({ status: "ok" }),
        Response.json({ status: "healthy" }),
      ),
    ),
    "api-unavailable",
  );
});

test("does not show a malformed readiness response as healthy", async () => {
  assert.equal(
    await loadConnectionState(
      respond(Response.json(metadata), Response.json({ status: "unknown" })),
    ),
    "database-unavailable",
  );
});

test("handles an unreachable API without throwing into the UI", async () => {
  assert.equal(
    await loadConnectionState(
      respond(
        new TypeError("network unavailable"),
        Response.json({ status: "healthy" }),
      ),
    ),
    "api-unavailable",
  );
});

test("an older healthy check cannot overwrite a newer failed refresh", async () => {
  const health = Promise.withResolvers<Response>();
  const healthStarted = Promise.withResolvers<void>();
  let metadataCalls = 0;
  const fetcher = (async (input: RequestInfo | URL) => {
    if (String(input).includes("/api/v1/system")) {
      return metadataCalls++ === 0
        ? Response.json(metadata)
        : new Response(null, { status: 500 });
    }
    healthStarted.resolve();
    return health.promise;
  }) as typeof fetch;
  const states: ConnectionState[] = [];
  const checker = createConnectionCheck((state) => states.push(state), fetcher);
  const older = checker.refresh();
  await healthStarted.promise;
  await checker.refresh();
  health.resolve(Response.json({ status: "healthy" }));
  await older;
  assert.deepEqual(states, ["api-unavailable"]);
});

test("cancelled checks cannot update an unmounted status display", async () => {
  const health = Promise.withResolvers<Response>();
  const states: ConnectionState[] = [];
  const fetcher = (async (input: RequestInfo | URL) =>
    String(input).includes("/api/v1/system")
      ? Response.json(metadata)
      : health.promise) as typeof fetch;
  const checker = createConnectionCheck((state) => states.push(state), fetcher);
  const pending = checker.refresh();
  checker.cancel();
  health.resolve(Response.json({ status: "healthy" }));
  await pending;
  assert.deepEqual(states, []);
});
