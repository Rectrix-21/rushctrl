import assert from "node:assert/strict";
import { test } from "node:test";
import { loadConnectionState } from "../src/lib/api.ts";

const metadata = { name: "RUSHCTRL", version: "0.1.0", milestone: 0 };
function respond(system: Response | Error, database: Response | Error): typeof fetch {
  return (async (input: RequestInfo | URL) => {
    const result = String(input).includes("/api/v1/system") ? system : database;
    if (result instanceof Error) throw result;
    return result;
  }) as typeof fetch;
}

test("reports ready only when the API contract and database readiness both succeed", async () => {
  const state = await loadConnectionState(respond(Response.json(metadata), Response.json({ status: "healthy" })));
  assert.equal(state, "ready");
});

test("reports an unavailable API after a failed HTTP response", async () => {
  assert.equal(await loadConnectionState(respond(new Response(null, { status: 500 }), Response.json({ status: "healthy" }))), "api-unavailable");
});

test("reports database unavailable when readiness returns 503", async () => {
  assert.equal(await loadConnectionState(respond(Response.json(metadata), Response.json({ status: "unhealthy" }, { status: 503 }))), "database-unavailable");
});

test("rejects a successful but malformed API payload", async () => {
  assert.equal(await loadConnectionState(respond(Response.json({ status: "ok" }), Response.json({ status: "healthy" }))), "api-unavailable");
});

test("does not show a malformed readiness response as healthy", async () => {
  assert.equal(await loadConnectionState(respond(Response.json(metadata), Response.json({ status: "unknown" }))), "database-unavailable");
});

test("handles an unreachable API without throwing into the UI", async () => {
  assert.equal(await loadConnectionState(respond(new TypeError("network unavailable"), Response.json({ status: "healthy" }))), "api-unavailable");
});
