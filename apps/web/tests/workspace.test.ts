import assert from "node:assert/strict";
import test from "node:test";
import { createWorkspaceClient, ApiError } from "../src/lib/workspace-api.ts";
import { intervalForm, loginForm } from "../src/lib/forms.ts";
import { localDay, occursOn } from "../src/lib/schedule-view.ts";

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
      return Response.json({ token: `token-${++issued}` });
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
      return Response.json({ token: "valid-token" });
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
