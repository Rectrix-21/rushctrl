"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { X } from "lucide-react";
import type { Shift, Workspace } from "@/lib/contracts";
import { ApiError, type WorkspaceClient } from "@/lib/workspace-api";
import { intervalForm, requirementForm, shiftForm } from "@/lib/forms";
import { timeRange } from "@/lib/schedule-view";

export type WorkspaceAction =
  | { kind: "shift" | "target" | "availability" }
  | { kind: "assignment" | "request"; shift: Shift };
const titles = {
  shift: "Create shift",
  target: "Add staffing target",
  availability: "Add availability",
  assignment: "Assign employee",
  request: "Request coverage",
};
const buttons = {
  shift: "Create shift",
  target: "Save staffing target",
  availability: "Save availability",
  assignment: "Save assignment",
  request: "Submit coverage request",
};

export function ActionDialog({
  action,
  client,
  org,
  location,
  workspace,
  day,
  onClose,
  onSaved,
  onExpired,
}: {
  action: WorkspaceAction;
  client: WorkspaceClient;
  org: string;
  location: Workspace["locations"][number];
  workspace: Workspace;
  day: string;
  onClose: () => void;
  onSaved: (message: string) => void;
  onExpired: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const interval =
    action.kind === "shift" ||
    action.kind === "target" ||
    action.kind === "availability";
  useEffect(() => {
    const element = dialog.current;
    element?.showModal();
    return () => element?.close();
  }, []);
  function close() {
    if (!busy) {
      dialog.current?.close();
      onClose();
    }
  }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    const values = Object.fromEntries(new FormData(event.currentTarget));
    const base = `/api/v1/organizations/${org}/`;
    let path: string;
    let body: unknown;
    let method = "POST";
    if (interval) {
      const schema =
        action.kind === "shift"
          ? shiftForm
          : action.kind === "target"
            ? requirementForm
            : intervalForm;
      const parsed = schema.safeParse({ ...values, locationId: location.id });
      if (!parsed.success) {
        setError(parsed.error.issues[0].message);
        return;
      }
      body = parsed.data;
      path =
        base +
        (action.kind === "shift"
          ? "shifts"
          : action.kind === "target"
            ? "staffing-requirements"
            : "availability");
    } else if (action.kind === "assignment") {
      path = base + `shifts/${action.shift.id}/assignment`;
      method = "PATCH";
      body = { userId: values.userId || null, version: action.shift.version };
    } else if (action.kind === "request") {
      path = base + "coverage-requests";
      body = { shiftId: action.shift.id };
    } else return;
    setBusy(true);
    try {
      await client.save(path, body, method);
      dialog.current?.close();
      onSaved(
        action.kind === "request"
          ? "Coverage request saved. Your shift remains assigned."
          : "Saved. The schedule has been refreshed.",
      );
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else
        setError(e instanceof Error ? e.message : "Unable to save. Try again.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <dialog
      className="workspace-dialog glass"
      ref={dialog}
      aria-labelledby="action-title"
      onCancel={(e) => {
        if (busy) e.preventDefault();
      }}
      onClose={(event) => {
        // Effect cleanup can queue this event before Strict Mode reopens the dialog.
        if (!event.currentTarget.open) onClose();
      }}
    >
      <div className="dialog-heading">
        <div>
          <p className="eyebrow">
            {location.name} · {location.timeZoneId}
          </p>
          <h2 id="action-title">{titles[action.kind]}</h2>
        </div>
        <button
          type="button"
          className="button"
          aria-label="Close form"
          onClick={close}
          disabled={busy}
        >
          <X aria-hidden="true" />
        </button>
      </div>
      <form className="workspace-form" onSubmit={submit}>
        {interval && (
          <>
            <p className="muted">
              Enter dates and times in {location.timeZoneId}.
            </p>
            <label>
              Starts (location time)
              <input
                type="datetime-local"
                name="startsAt"
                defaultValue={day + "T08:00"}
                required
              />
            </label>
            <label>
              Ends (location time)
              <input
                type="datetime-local"
                name="endsAt"
                defaultValue={day + "T12:00"}
                required
              />
            </label>
          </>
        )}
        {(action.kind === "shift" || action.kind === "target") && (
          <label>
            Required skill
            <select
              name="skillId"
              required
              defaultValue={workspace.skills[0]?.id ?? ""}
            >
              <option value="" disabled>
                Choose a skill
              </option>
              {workspace.skills.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {action.kind === "target" && (
          <label>
            People needed
            <input
              type="number"
              name="requiredCount"
              min={1}
              max={100}
              step={1}
              defaultValue={1}
              required
            />
          </label>
        )}
        {action.kind === "assignment" && (
          <>
            <p className="muted">
              {timeRange(
                action.shift.startsAt,
                action.shift.endsAt,
                location.timeZoneId,
              )}{" "}
              ·{" "}
              {
                workspace.skills.find((s) => s.id === action.shift.skillId)
                  ?.name
              }
            </p>
            <label>
              Employee
              <select
                name="userId"
                defaultValue={action.shift.assigneeUserId ?? ""}
              >
                <option value="">Leave unassigned</option>
                {workspace.people
                  .filter((p) =>
                    workspace.qualifications.some(
                      (q) =>
                        q.userId === p.id && q.skillId === action.shift.skillId,
                    ),
                  )
                  .map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.displayName}
                    </option>
                  ))}
              </select>
            </label>
            <p className="workspace-hint">
              Qualified teammates are listed here. Availability, other shifts
              and weekly hours are checked when you save.
            </p>
          </>
        )}
        {action.kind === "availability" && (
          <p className="workspace-hint">
            This adds availability immediately. Existing windows cannot be
            edited or removed in this milestone.
          </p>
        )}
        {action.kind === "request" && (
          <div className="request-warning">
            <strong>Your shift stays assigned to you.</strong>
            <p>
              This saves a pending request for your manager to see. Requests
              cannot yet be cancelled or resolved, and your manager cannot
              reassign this shift while it is pending. Resolution arrives in
              Milestone 2.
            </p>
          </div>
        )}
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <div className="dialog-actions">
          <button
            type="button"
            className="button"
            disabled={busy}
            onClick={close}
          >
            Cancel
          </button>
          <button className="button primary" disabled={busy}>
            {busy ? "Saving…" : buttons[action.kind]}
          </button>
        </div>
      </form>
    </dialog>
  );
}
