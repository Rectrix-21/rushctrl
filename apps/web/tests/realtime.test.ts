import assert from "node:assert/strict";
import test from "node:test";
import { startWorkspaceUpdates } from "../src/lib/realtime.ts";
import { createWorkspaceClient, ApiError } from "../src/lib/workspace-api.ts";

const org = "10000000-0000-0000-0000-000000000001";
const generation = "10000000-0000-0000-0000-000000000002";
const changedGeneration = "10000000-0000-0000-0000-000000000005";
const identity = "10000000-0000-0000-0000-000000000003";
class Connection {
  changed: (value: unknown) => void = () => {};
  reconnect: () => Promise<void> = async () => {};
  joining: string[] = [];
  stops = 0;
  async start() {}
  async stop() {
    this.stops++;
  }
  async invoke(_method: string, id: string) {
    this.joining.push(id);
  }
  on(_event: string, cb: (value: unknown) => void) {
    this.changed = cb;
  }
  onreconnected(cb: () => Promise<void>) {
    this.reconnect = cb;
  }
  onreconnecting() {}
  onclose() {}
}
const tick = () => new Promise((resolve) => setTimeout(resolve, 120));

test("rejoining discovers a missed reset even while an old form is open", async () => {
  const connection = new Connection();
  let current = generation;
  const changes: boolean[] = [];
  const updates = startWorkspaceUpdates({
    org,
    generation,
    connection,
    verifyIdentity: async () => {},
    readGeneration: async () => current,
    onChange: (reset) => changes.push(reset),
    onStatus: () => {},
    onExpired: () => assert.fail("Unexpected expiry"),
  });
  await tick();
  changes.length = 0;
  updates.setEditing(true);
  current = changedGeneration;
  await connection.reconnect();
  await tick();
  assert.deepEqual(changes, [true]);
  updates.stop();
});

test("live updates coalesce, wait for edits and invalidate forms on reset", async () => {
  const connection = new Connection();
  const changes: boolean[] = [];
  const statuses: string[] = [];
  const queued: boolean[] = [];
  const updates = startWorkspaceUpdates({
    org,
    generation,
    verifyIdentity: async () => {},
    readGeneration: async () => generation,
    connection,
    onChange: (reset) => changes.push(reset),
    onStatus: (s) => statuses.push(s),
    onQueued: (value) => queued.push(value),
    onExpired: () => assert.fail("Unexpected expiry"),
  });
  await tick();
  assert.deepEqual(connection.joining, [org]);
  assert.equal(statuses.at(-1), "Live updates connected");
  changes.length = 0;
  updates.setEditing(true);
  connection.changed({ organizationId: org, generation });
  connection.changed({ organizationId: org, generation });
  await tick();
  assert.deepEqual(changes, []);
  assert.equal(queued.at(-1), true);
  updates.setEditing(false);
  await tick();
  assert.deepEqual(changes, [false]);
  assert.equal(queued.at(-1), false);
  updates.setEditing(true);
  connection.changed({ organizationId: org, generation: changedGeneration });
  await tick();
  assert.deepEqual(changes, [false, true]);
  updates.stop();
  connection.changed({ organizationId: org, generation: changedGeneration });
  await tick();
  assert.equal(changes.length, 2);
  assert.ok(connection.stops > 0);
});

test("a cookie identity switch prevents reconnect joining and expires the rendered session", async () => {
  const connection = new Connection();
  let switched = false;
  let expired = 0;
  const updates = startWorkspaceUpdates({
    org,
    generation: null,
    connection,
    verifyIdentity: async () => {
      if (switched) throw new ApiError(401, "Session changed");
    },
    readGeneration: async () => null,
    onChange: () => {},
    onStatus: () => {},
    onExpired: () => expired++,
  });
  await tick();
  switched = true;
  await connection.reconnect();
  assert.equal(connection.joining.length, 1);
  assert.equal(expired, 1);
  assert.ok(connection.stops > 0);
  updates.stop();
});

test("writes keep the form's captured generation and identity checks never rebind the client", async () => {
  let owner = identity;
  let writes = 0;
  const client = createWorkspaceClient(async (url, options) => {
    if (String(url).endsWith("/session"))
      return Response.json({
        id: owner,
        displayName: "Alex",
        memberships: [],
      });
    if (String(url).endsWith("/csrf"))
      return Response.json({ token: "csrf", userId: owner });
    writes++;
    assert.equal(
      new Headers(options?.headers).get("X-Workspace-Generation"),
      generation,
    );
    return new Response(null, { status: 204 });
  });
  await client.session();
  await client.assertIdentity();
  await client.save("/lab/availability", {}, "POST", generation);
  owner = changedGeneration;
  await assert.rejects(
    client.refreshSession(),
    (e: unknown) => e instanceof ApiError && e.status === 401,
  );
  await assert.rejects(
    client.assertIdentity(),
    (e: unknown) => e instanceof ApiError && e.status === 401,
  );
  await assert.rejects(
    client.save("/lab/availability", {}, "POST", generation),
    /session.*changed/i,
  );
  assert.equal(writes, 1);
});
