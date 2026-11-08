import assert from "node:assert/strict";
import test from "node:test";
import { createWorkspaceClient, ApiError } from "../src/lib/workspace-api.ts";
import { intervalForm, loginForm } from "../src/lib/forms.ts";
import { localDay, occursOn, timeRange } from "../src/lib/schedule-view.ts";

test("an anonymous session becomes signed out, while other failures remain errors", async () => {
  const anonymous = createWorkspaceClient(
    async () => new Response(null, { status: 401 }),
  );
  assert.equal(await anonymous.session(), null);
  const denied = createWorkspaceClient(async () =>
    Response.json({ title: "Access denied" }, { status: 403 }),
  );
  await assert.rejects(
    denied.session(),
    (e: unknown) => e instanceof ApiError && e.status === 403,
  );
});

test("every write obtains a fresh identity-bound CSRF token and does not retry a save", async () => {
  let issued = 0;
  let writes = 0;
  const client = createWorkspaceClient(async (url, options) => {
    if (String(url).endsWith("/csrf"))
      return Response.json({ token: `token-${++issued}`, userId: null });
    writes++;
    assert.equal(
      new Headers(options?.headers).get("X-CSRF-TOKEN"),
      `token-${issued}`,
    );
    assert.equal(options?.credentials, "same-origin");
    if (writes === 3)
      return Response.json(
        { title: "This shift changed. Refresh it before assigning." },
        { status: 409 },
      );
    return new Response(null, { status: 204 });
  });
  await client.save("/api/v1/auth/login", {
    email: "manager@northline.example",
    password: "test",
  });
  await client.save("/api/v1/auth/logout");
  await assert.rejects(
    client.save("/api/v1/organizations/example/shifts", {}),
    /This shift changed/,
  );
  assert.equal(issued, 3);
  assert.equal(writes, 3);
});

test("malformed session data cannot be rendered as a signed-in identity", async () => {
  const client = createWorkspaceClient(async () =>
    Response.json({ id: "invalid", displayName: "Name", memberships: [] }),
  );
  await assert.rejects(client.session(), /unexpected response/i);
});

test("interval validation accepts local times and rejects reversed invalid and offset inputs", () => {
  const valid = {
    locationId: "10000000-0000-0000-0000-000000000002",
    startsAt: "2027-01-11T08:00",
    endsAt: "2027-01-11T12:00",
  };
  assert.equal(intervalForm.safeParse(valid).success, true);
  assert.equal(
    intervalForm.safeParse({ ...valid, endsAt: "2027-01-11T07:00" }).success,
    false,
  );
  assert.equal(
    intervalForm.safeParse({ ...valid, startsAt: "2027-02-30T08:00" }).success,
    false,
  );
  assert.equal(
    intervalForm.safeParse({ ...valid, startsAt: "2027-01-11T08:00Z" }).success,
    false,
  );
});

test("sign-in validation rejects missing email and password without trimming a password", () => {
  assert.equal(loginForm.safeParse({ email: "", password: "" }).success, false);
  const result = loginForm.safeParse({
    email: "manager@northline.example",
    password: " text with spaces ",
  });
  assert.equal(result.success, true);
  if (result.success) assert.equal(result.data.password, " text with spaces ");
});

test("a lost save response is not retried and asks the user to refresh before retrying", async () => {
  let writes = 0;
  const client = createWorkspaceClient(async (url) => {
    if (String(url).endsWith("/csrf"))
      return Response.json({ token: "valid-token", userId: null });
    writes++;
    throw new Error("Connection interrupted after the server may have saved");
  });
  await assert.rejects(
    client.save("/api/v1/organizations/example/shifts", {}),
    /outcome is unknown.*refresh/i,
  );
  assert.equal(writes, 1);
});

test("schedule days use the location zone and an end at midnight does not add an extra day", () => {
  assert.equal(
    localDay("2027-01-12T02:00:00Z", "America/Toronto"),
    "2027-01-11",
  );
  assert.equal(
    occursOn(
      "2027-01-12T02:00:00Z",
      "2027-01-12T05:00:00Z",
      "2027-01-11",
      "America/Toronto",
    ),
    true,
  );
  assert.equal(
    occursOn(
      "2027-01-12T02:00:00Z",
      "2027-01-12T05:00:00Z",
      "2027-01-12",
      "America/Toronto",
    ),
    false,
  );
});

test("a stale rendered identity cannot save after another tab switches the shared cookie", async () => {
  const employee = "10000000-0000-0000-0000-000000000004";
  const manager = "10000000-0000-0000-0000-000000000003";
  let owner = employee;
  let writes = 0;
  const client = createWorkspaceClient(async (url) => {
    if (String(url).endsWith("/session"))
      return Response.json({
        id: owner,
        displayName: "Current person",
        memberships: [],
      });
    if (String(url).endsWith("/csrf"))
      return Response.json({ token: "current-identity-token", userId: owner });
    writes++;
    return new Response(null, { status: 204 });
  });
  assert.equal((await client.session())?.id, employee);
  owner = manager;
  await assert.rejects(
    client.save("/api/v1/organizations/example/availability", {}),
    (e: unknown) =>
      e instanceof ApiError &&
      e.status === 401 &&
      /session.*changed/i.test(e.message),
  );
  assert.equal(writes, 0);
  await client.save("/api/v1/auth/login", {
    email: "manager@northline.example",
    password: "test",
  });
  assert.equal((await client.session())?.id, manager);
  await client.save("/api/v1/organizations/example/availability", {});
  assert.equal(writes, 2);
});

test("overnight and Sunday-to-Monday labels include both location dates", () => {
  const boundary = timeRange(
    "2027-01-11T04:00:00Z",
    "2027-01-11T06:00:00Z",
    "America/Toronto",
  );
  assert.match(boundary, /Sun, Jan 10/);
  assert.match(boundary, /Mon, Jan 11/);
  assert.match(boundary, /23:00/);
  assert.match(boundary, /01:00/);
  const overnight = timeRange(
    "2027-01-12T04:00:00Z",
    "2027-01-12T06:00:00Z",
    "America/Toronto",
  );
  assert.match(overnight, /Mon, Jan 11/);
  assert.match(overnight, /Tue, Jan 12/);
  const midnight = timeRange(
    "2027-01-12T04:00:00Z",
    "2027-01-12T05:00:00Z",
    "America/Toronto",
  );
  assert.match(midnight, /Tue, Jan 12 00:00/);
});

test("an ignored older session response cannot change the identity bound to the displayed session", async () => {
  const manager = "10000000-0000-0000-0000-000000000003";
  const employee = "10000000-0000-0000-0000-000000000004";
  const old = Promise.withResolvers<Response>();
  const current = Promise.withResolvers<Response>();
  let reads = 0;
  let writes = 0;
  const client = createWorkspaceClient(async (url) => {
    if (String(url).endsWith("/session"))
      return ++reads === 1 ? old.promise : current.promise;
    if (String(url).endsWith("/csrf"))
      return Response.json({ token: "manager-token", userId: manager });
    writes++;
    return new Response(null, { status: 204 });
  });
  const ignoredLoad = client.session();
  const acceptedLoad = client.session();
  current.resolve(
    Response.json({
      id: employee,
      displayName: "Displayed employee",
      memberships: [],
    }),
  );
  assert.equal((await acceptedLoad)?.id, employee);
  old.resolve(
    Response.json({
      id: manager,
      displayName: "Ignored manager",
      memberships: [],
    }),
  );
  await ignoredLoad;
  await assert.rejects(
    client.save("/api/v1/organizations/example/availability", {}),
    (error: unknown) => error instanceof ApiError && error.status === 401,
  );
  assert.equal(writes, 0);
});
