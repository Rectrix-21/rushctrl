"use client";

import { useEffect, useState } from "react";
import { auditSchema } from "@/lib/contracts";
import { ApiError, type WorkspaceClient } from "@/lib/workspace-api";
import type { z } from "zod";

export function AuditPanel({
  client,
  org,
  revision,
  onExpired,
}: {
  client: WorkspaceClient;
  org: string;
  revision: number;
  onExpired: () => void;
}) {
  const [entries, setEntries] = useState<z.infer<typeof auditSchema>>([]);
  const [cursor, setCursor] = useState<number | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    client
      .get(
        `/api/v1/organizations/${org}/audit?limit=10${cursor ? `&beforeNumber=${cursor}` : ""}`,
        auditSchema,
      )
      .then((value) => {
        if (active) {
          setEntries(value);
          setError("");
        }
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
  }, [client, org, revision, cursor, onExpired]);
  return (
    <section id="audit" className="glass workspace-panel">
      <div className="panel-header">
        <div>
          <h2>Activity history</h2>
          <p>Successful changes, recorded with the save</p>
        </div>
      </div>
      <div className="workspace-list">
        {error && <p role="alert">{error}</p>}
        {!entries.length && !error && (
          <p className="empty-state">No recorded changes on this page.</p>
        )}
        {entries.map((entry) => (
          <article key={entry.number} className="audit-entry">
            <strong>{entry.action.replaceAll(".", " · ")}</strong>
            <p>
              {entry.actorName} · {new Date(entry.createdAt).toLocaleString()}
            </p>
            <small>
              #{entry.number}
              {entry.generation ? " · Lab experiment" : ""}
            </small>
            <details>
              <summary>Change details</summary>
              <pre>{entry.before ?? "No previous value"}</pre>
              <pre>{entry.after ?? "No remaining value"}</pre>
            </details>
          </article>
        ))}
        <div className="workspace-actions">
          <button
            className="button"
            disabled={!cursor}
            onClick={() => setCursor(null)}
          >
            Latest activity
          </button>
          <button
            className="button"
            disabled={entries.length < 10}
            onClick={() => setCursor(entries.at(-1)?.number ?? null)}
          >
            Older activity
          </button>
        </div>
      </div>
    </section>
  );
}
