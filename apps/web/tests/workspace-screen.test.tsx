import assert from "node:assert/strict";
import test from "node:test";
import { act } from "react";
import { JSDOM } from "jsdom";
import { WorkspaceScreen } from "../src/components/workspace/workspace-screen";
import { createWorkspaceClient } from "../src/lib/workspace-api";
import type { startWorkspaceUpdates } from "../src/lib/realtime";

test("same-generation metadata updates lab controls without losing selection or restarting live updates", async () => {
  const dom = new JSDOM('<div id="root"></div>', {
    url: "http://localhost:3000",
  });
  const previous = new Map<string, PropertyDescriptor | undefined>();
  const org = "10000000-0000-0000-0000-000000000001";
  const id = "10000000-0000-0000-0000-000000000003";
  const generation = "10000000-0000-0000-0000-000000000004";
  const session = {
    id,
    displayName: "Alex",
    memberships: [
      {
        organizationId: org,
        organizationName: "Friday Rush Lab",
        permission: "Manager" as const,
      },
    ],
  };
  const metadata = {
    organizationName: "Friday Rush Lab",
    permission: "Manager",
    suggestedWeek: "2027-01-11",
    canOpenSimulation: false,
    locations: [
      {
        id: "10000000-0000-0000-0000-000000000002",
        name: "Harbour",
        timeZoneId: "America/Toronto",
      },
      {
        id: "10000000-0000-0000-0000-000000000006",
        name: "Junction",
        timeZoneId: "America/Toronto",
      },
    ],
    people: [],
    skills: [],
    qualifications: [],
    simulation: {
      organizationId: org,
      sourceOrganizationId: org,
      generation,
      week: "2027-01-11",
      pristine: true,
      applied: false,
      locationId: "10000000-0000-0000-0000-000000000002",
      absenceShiftId: id,
      standbyShiftId: id,
    },
  };
  let delayedRead: ReturnType<typeof Promise.withResolvers<Response>> | null =
    null;
  const fetcher: typeof fetch = async (url) => {
    const path = String(url);
    if (path.endsWith("/session")) return Response.json(session);
    if (path.endsWith("/csrf"))
      return Response.json({ token: "csrf", userId: id });
    if (path.endsWith("/workspace")) {
      if (delayedRead) {
        const read = delayedRead;
        delayedRead = null;
        return read.promise;
      }
      return Response.json(metadata);
    }
    if (path.endsWith("/system"))
      return Response.json({
        name: "RUSHCTRL",
        version: "0.1.0",
        milestone: 2,
      });
    if (path.includes("/health/")) return Response.json({ status: "healthy" });
    return Response.json([]);
  };
  for (const [key, value] of Object.entries({
    window: dom.window,
    self: dom.window,
    document: dom.window.document,
    IS_REACT_ACT_ENVIRONMENT: true,
    fetch: fetcher,
  })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key));
    Object.defineProperty(globalThis, key, { configurable: true, value });
  }
  let notify: (reset: boolean) => void = () =>
    assert.fail("Live updates not started");
  let starts = 0;
  const updatesFactory: typeof startWorkspaceUpdates = (options) => {
    starts++;
    notify = options.onChange;
    options.onStatus("Live updates connected");
    return { stop() {}, setEditing() {} };
  };
  const client = createWorkspaceClient(fetcher);
  await client.session();
  const { createRoot } = await import("react-dom/client");
  const root = createRoot(dom.window.document.getElementById("root")!);
  const settle = async () => {
    await new Promise((resolve) => setTimeout(resolve, 30));
  };
  const apply = () =>
    [...dom.window.document.querySelectorAll("button")].find(
      (button) => button.textContent === "Apply Friday Rush",
    )!;
  try {
    await act(async () => {
      root.render(
        <WorkspaceScreen
          client={client}
          session={session}
          updatesFactory={updatesFactory}
          onExpired={() => assert.fail("Unexpected expiry")}
          onSignedOut={() => {}}
        />,
      );
      await settle();
    });
    await act(settle);
    assert.equal(apply().disabled, false);
    const select = dom.window.document.querySelector<HTMLSelectElement>(
      'select[aria-label="Location"]',
    )!;
    await act(async () => {
      select.value = metadata.locations[1].id;
      select.dispatchEvent(new dom.window.Event("change", { bubbles: true }));
      await settle();
    });
    metadata.simulation.pristine = false;
    await act(async () => {
      notify(false);
      await settle();
    });
    assert.equal(apply().disabled, true, "A saved lab edit must disable Apply");
    assert.match(
      dom.window.document.body.textContent!,
      /Reset before applying again/,
    );
    metadata.simulation.pristine = true;
    await act(async () => {
      notify(false);
      await settle();
    });
    assert.equal(apply().disabled, false);
    metadata.simulation.applied = true;
    metadata.suggestedWeek = "2027-01-18";
    await act(async () => {
      notify(false);
      await settle();
    });
    assert.equal(
      apply().disabled,
      true,
      "Another session's Apply must disable Apply here",
    );
    assert.equal(select.value, metadata.locations[1].id);
    assert.equal(
      dom.window.document.querySelector<HTMLInputElement>(
        'input[aria-label="Week beginning"]',
      )!.value,
      "2027-01-11",
    );
    assert.equal(
      starts,
      1,
      "Fresh metadata must not restart the live connection",
    );
    metadata.simulation.applied = false;
    const stale = Response.json(metadata);
    const older = Promise.withResolvers<Response>();
    delayedRead = older;
    await act(async () => {
      notify(false);
      await settle();
    });
    metadata.simulation.applied = true;
    await act(async () => {
      notify(false);
      await settle();
    });
    assert.equal(apply().disabled, true);
    await act(async () => {
      older.resolve(stale);
      await settle();
    });
    assert.equal(
      apply().disabled,
      true,
      "An older metadata response must not overwrite a newer refresh",
    );
  } finally {
    await act(async () => root.unmount());
    dom.window.close();
    for (const [key, descriptor] of previous) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else Reflect.deleteProperty(globalThis, key);
    }
  }
});
