"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { X } from "lucide-react";
import {
  candidatesSchema,
  type Candidates,
  type CoverageRequest,
  type Shift,
  type Workspace,
} from "@/lib/contracts";
import { ApiError, type WorkspaceClient } from "@/lib/workspace-api";
import { intervalForm, requirementForm, shiftForm } from "@/lib/forms";
import { timeRange } from "@/lib/schedule-view";

export type WorkspaceAction =
  | { kind: "shift" | "target" | "availability" }
  | { kind: "assignment"; shift: Shift }
  | { kind: "request"; shift: Shift }
  | { kind: "approve"; request: CoverageRequest }
  | { kind: "reject"; request: CoverageRequest }
  | { kind: "cancel"; request: CoverageRequest };
const titles = {
  shift: "Create shift",
  target: "Add staffing target",
  availability: "Add availability",
  assignment: "Assign employee",
  request: "Request coverage",
  approve: "Approve replacement",
  reject: "Reject request",
  cancel: "Cancel coverage request",
};
const buttons = {
  shift: "Create shift",
  target: "Save staffing target",
  availability: "Save availability",
  assignment: "Save assignment",
  request: "Submit coverage request",
  approve: "Approve replacement",
  reject: "Reject request",
  cancel: "Cancel request",
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
  updatesQueued = false,
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
  updatesQueued?: boolean;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [generation] = useState(workspace.simulation?.generation);
  const [candidates, setCandidates] = useState<Candidates | null>(null);
  const candidateShift =
    action.kind === "assignment"
      ? action.shift.id
      : action.kind === "approve"
        ? action.request.shiftId
        : null;
  useEffect(() => {
    if (!candidateShift) return;
    let active = true;
    client
      .get(
        `/api/v1/organizations/${org}/shifts/${candidateShift}/candidates`,
        candidatesSchema,
      )
      .then((value) => {
        if (active) setCandidates(value);
      })
      .catch((e) => {
        if (active) {
          if (e instanceof ApiError && e.status === 401) onExpired();
          else setError(e.message);
        }
      });
    return () => {
      active = false;
    };
  }, [candidateShift, client, org, onExpired]);
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
    } else if (
      action.kind === "approve" ||
      action.kind === "reject" ||
      action.kind === "cancel"
    ) {
      path = base + `coverage-requests/${action.request.id}/${action.kind}`;
      if (action.kind === "approve" && (!candidates || !values.userId)) {
        setError("Choose an eligible replacement.");
        return;
      }
      body = {
        version: action.request.version,
        ...(action.kind === "approve"
          ? { userId: values.userId, shiftVersion: candidates!.shiftVersion }
          : {}),
      };
    } else return;
    setBusy(true);
    try {
      await client.save(path, body, method, generation);
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
        {updatesQueued && (
          <p role="status" className="request-warning">
            Schedule changed while you were editing. Close this form to refresh.
            Saving rechecks the current rules and versions.
          </p>
        )}
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
        {(action.kind === "assignment" || action.kind === "approve") && (
          <>
            <p className="muted">
              {timeRange(
                action.kind === "assignment"
                  ? action.shift.startsAt
                  : action.request.startsAt,
                action.kind === "assignment"
                  ? action.shift.endsAt
                  : action.request.endsAt,
                location.timeZoneId,
              )}{" "}
              ·{" "}
              {
                workspace.skills.find(
                  (s) =>
                    s.id ===
                    (action.kind === "assignment"
                      ? action.shift.skillId
                      : action.request.skillId),
                )?.name
              }
            </p>
            <label>
              {action.kind === "approve" ? "Replacement" : "Employee"}
              <select
                name="userId"
                defaultValue={
                  action.kind === "assignment"
                    ? (action.shift.assigneeUserId ?? "")
                    : ""
                }
                required={action.kind === "approve"}
                disabled={!candidates}
              >
                <option value="">
                  {action.kind === "assignment"
                    ? "Leave unassigned"
                    : "Choose a replacement"}
                </option>
                {action.kind === "assignment" &&
                  action.shift.assigneeUserId && (
                    <option value={action.shift.assigneeUserId}>
                      Keep {action.shift.assigneeName}
                    </option>
                  )}
                {candidates?.eligible.map((p) => (
                  <option key={p.userId} value={p.userId}>
                    {p.rank}. {p.displayName}
                  </option>
                ))}
              </select>
            </label>
            {candidates ? (
              <div className="candidate-explanation">
                <p className="workspace-hint">
                  Least load ·{" "}
                  {new Date(candidates.generatedAt).toLocaleTimeString()}
                  <br />
                  Eligible people are ordered by assigned hours, recent covers,
                  then stable ID. Approval checks every rule again.
                </p>
                {!candidates.eligible.length && (
                  <p>No eligible replacements right now.</p>
                )}
                <ol>
                  {candidates.eligible.map((p) => (
                    <li key={p.userId}>
                      <strong>{p.displayName}</strong>
                      <span>
                        {(p.assignedMinutes / 60).toFixed(1)} scheduled hours ·{" "}
                        {p.recentCovers} covers in 28 days
                      </span>
                    </li>
                  ))}
                </ol>
                <details>
                  <summary>
                    Why others are unavailable ({candidates.rejected.length})
                  </summary>
                  {candidates.rejected.map((p) => (
                    <p key={p.userId}>
                      <strong>{p.displayName}</strong>:{" "}
                      {p.reasons.map((r) => r.message).join(" ")}
                    </p>
                  ))}
                </details>
              </div>
            ) : (
              <p role="status">Loading eligibility…</p>
            )}
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
              Your manager can approve an eligible replacement or reject the
              request. You can cancel while it is pending.
            </p>
          </div>
        )}
        {(action.kind === "cancel" || action.kind === "reject") && (
          <p className="request-warning">
            This closes the pending request and preserves its history. The
            current assignment stays as it is.
          </p>
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
          <button
            className="button primary"
            disabled={busy || (!!candidateShift && !candidates)}
          >
            {busy ? "Saving…" : buttons[action.kind]}
          </button>
        </div>
      </form>
    </dialog>
  );
}
