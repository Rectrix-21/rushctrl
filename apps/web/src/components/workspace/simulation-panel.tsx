"use client";

import { useEffect, useRef, useState } from "react";
import { simulationSchema, type Workspace } from "@/lib/contracts";
import { ApiError, type WorkspaceClient } from "@/lib/workspace-api";

export function SimulationPanel({
  client,
  org,
  workspace,
  onOrganization,
  onReload,
  onExpired,
  onEditing,
}: {
  client: WorkspaceClient;
  org: string;
  workspace: Workspace;
  onOrganization: (id: string) => void;
  onReload: () => void;
  onExpired: () => void;
  onEditing: (value: boolean) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [confirm, setConfirm] = useState<{ generation: string } | null>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    if (!confirm) return;
    const element = dialog.current;
    element?.showModal();
    onEditing(true);
    return () => {
      element?.close();
      onEditing(false);
    };
  }, [confirm, onEditing]);
  const lab = workspace.simulation;
  async function act(kind: "open" | "apply" | "reset", generation?: string) {
    setBusy(true);
    setError("");
    try {
      const response = await client.save(
        `/api/v1/organizations/${org}/simulation${kind === "open" ? "" : "/" + kind}`,
        {},
        "POST",
        generation,
      );
      const result = simulationSchema.parse(await response.json());
      setConfirm(null);
      if (kind === "open") onOrganization(result.organizationId);
      else onReload();
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onExpired();
      else
        setError(e instanceof Error ? e.message : "Unable to change the lab.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="glass simulation-panel" aria-labelledby="lab-title">
      <div>
        <p className="eyebrow">Friday Rush · practice lab</p>
        <h2 id="lab-title">
          {lab ? "Make room for the rush." : "Try a shift under pressure."}
        </h2>
        <p>
          {lab
            ? "Two missing people. Explained recommendations. Your decisions."
            : "Open your separate lab with eight fictional profiles. Your normal schedule stays saved."}
        </p>
        {lab && (
          <p className="workspace-hint">
            Baseline: 0 short → Apply: 2 short → Approve: 1 short → Assign
            standby: covered.{" "}
            {lab.pristine ? "Baseline ready." : "Reset before applying again."}
          </p>
        )}
      </div>
      <div className="workspace-actions">
        {lab ? (
          <>
            <button
              className="button primary"
              disabled={busy || !lab.pristine || lab.applied}
              onClick={() => act("apply", lab.generation)}
            >
              Apply Friday Rush
            </button>
            <button
              className="button"
              disabled={busy}
              onClick={() => setConfirm({ generation: lab.generation })}
            >
              Reset lab
            </button>
            <button
              className="button"
              disabled={busy}
              onClick={() => onOrganization(lab.sourceOrganizationId)}
            >
              Return to normal workspace
            </button>
          </>
        ) : (
          <button
            className="button primary"
            disabled={busy}
            onClick={() => act("open")}
          >
            Open Friday Rush lab
          </button>
        )}
      </div>
      {error && !confirm && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
      {confirm && (
        <dialog
          ref={dialog}
          className="workspace-dialog glass"
          aria-labelledby="reset-title"
          onCancel={(event) => {
            if (busy) event.preventDefault();
          }}
          onClose={(event) => {
            if (!event.currentTarget.open) setConfirm(null);
          }}
        >
          <h2 id="reset-title">Reset this lab?</h2>
          <p>
            This replaces this lab’s shifts, availability, targets and requests
            with the baseline. Activity history stays. Open forms from this
            experiment will expire.
          </p>
          {error && <p role="alert">{error}</p>}
          <div className="dialog-actions">
            <button
              className="button"
              disabled={busy}
              onClick={() => setConfirm(null)}
            >
              Keep experimenting
            </button>
            <button
              className="button primary"
              disabled={busy}
              onClick={() => act("reset", confirm.generation)}
            >
              {busy ? "Resetting…" : "Restore baseline"}
            </button>
          </div>
        </dialog>
      )}
    </section>
  );
}
