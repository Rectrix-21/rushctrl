import assert from "node:assert/strict";
import test from "node:test";
import { act, StrictMode, useState } from "react";
import { JSDOM } from "jsdom";
import { ActionDialog } from "../src/components/workspace/workspace-dialog";
import { createWorkspaceClient } from "../src/lib/workspace-api";
import type { Workspace } from "../src/lib/contracts";

test("a form survives Strict Mode effect replay and still dismisses on native close", async () => {
  const dom = new JSDOM('<div id="root"></div>');
  const previous = new Map<string, PropertyDescriptor | undefined>();
  for (const [key, value] of Object.entries({
    window: dom.window,
    document: dom.window.document,
    IS_REACT_ACT_ENVIRONMENT: true,
  })) {
    previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key));
    Object.defineProperty(globalThis, key, { configurable: true, value });
  }
  // jsdom omits dialog methods. Model the native queued close event at this boundary.
  dom.window.HTMLDialogElement.prototype.showModal = function () {
    this.open = true;
  };
  dom.window.HTMLDialogElement.prototype.close = function () {
    if (!this.open) return;
    this.open = false;
    setTimeout(() => this.dispatchEvent(new dom.window.Event("close")), 0);
  };
  const location = {
    id: "10000000-0000-0000-0000-000000000002",
    name: "Harbour",
    timeZoneId: "America/Toronto",
  };
  const workspace: Workspace = {
    organizationName: "Northline Coffee",
    permission: "Manager",
    suggestedWeek: "2027-01-11",
    simulation: null,
    canOpenSimulation: false,
    locations: [location],
    skills: [],
    people: [],
    qualifications: [],
  };
  let dismissals = 0;
  function Form() {
    const [open, setOpen] = useState(true);
    return open ? (
      <ActionDialog
        action={{ kind: "shift" }}
        client={createWorkspaceClient()}
        org="10000000-0000-0000-0000-000000000001"
        location={location}
        workspace={workspace}
        day="2027-01-11"
        updatesQueued={true}
        onClose={() => {
          dismissals++;
          setOpen(false);
        }}
        onSaved={() => assert.fail("Opening a form must not save it")}
        onExpired={() =>
          assert.fail("Opening a form must not expire a session")
        }
      />
    ) : null;
  }
  const { createRoot } = await import("react-dom/client");
  const root = createRoot(dom.window.document.getElementById("root")!);
  try {
    await act(async () =>
      root.render(
        <StrictMode>
          <Form />
        </StrictMode>,
      ),
    );
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 10));
    });
    const dialog = dom.window.document.querySelector("dialog");
    assert.ok(
      dialog?.open,
      "The create-shift form should remain open after effect replay",
    );
    assert.equal(dismissals, 0);
    assert.match(dialog.textContent!, /Schedule changed while you were editing/);
    await act(async () => {
      const cancelled = !dialog.dispatchEvent(
        new dom.window.Event("cancel", { cancelable: true }),
      );
      if (!cancelled) dialog.close();
      await new Promise((resolve) => setTimeout(resolve, 10));
    });
    assert.equal(dom.window.document.querySelector("dialog"), null);
    assert.equal(dismissals, 1, "A real native close should dismiss the form");
  } finally {
    await act(async () => root.unmount());
    dom.window.close();
    for (const [key, descriptor] of previous) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else Reflect.deleteProperty(globalThis, key);
    }
  }
});
