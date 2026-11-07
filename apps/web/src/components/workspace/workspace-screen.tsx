"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  CalendarDays,
  ArrowLeftRight,
  Users,
  LogOut,
  RefreshCw,
} from "lucide-react";
import type { Session, Workspace } from "@/lib/contracts";
import {
  ApiError,
  workspaceSchema,
  type WorkspaceClient,
} from "@/lib/workspace-api";
import { addDays, dayLabel } from "@/lib/schedule-view";
import { SystemStatus } from "@/components/system-status";
import { ScheduleBoard } from "./schedule-board";

type Props = {
  client: WorkspaceClient;
  session: Session;
  onSignedOut: () => void;
  onExpired: () => void;
};

export function WorkspaceScreen(props: Props) {
  const [org, setOrg] = useState(
    props.session.memberships[0]?.organizationId ?? "",
  );
  if (!org)
    return (
      <main className="session-state">
        <h1>No workspace assigned</h1>
        <p>Ask your organization manager to add a membership.</p>
        <button
          className="button"
          onClick={async () => {
            await props.client.save("/api/v1/auth/logout");
            props.onSignedOut();
          }}
        >
          Sign out
        </button>
      </main>
    );
  return (
    <WorkspaceLoader key={org} {...props} org={org} onOrganization={setOrg} />
  );
}

function WorkspaceLoader(
  props: Props & { org: string; onOrganization: (id: string) => void },
) {
  const { client, org, onExpired } = props;
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    client
      .get(`/api/v1/organizations/${org}/workspace`, workspaceSchema)
      .then((value) => {
        if (active) setWorkspace(value);
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
  }, [client, org, onExpired, revision]);
  if (!workspace)
    return (
      <main className="session-state">
        {error ? (
          <>
            <p role="alert">{error}</p>
            <button
              className="button"
              onClick={() => {
                setError("");
                setRevision((n) => n + 1);
              }}
            >
              Retry workspace
            </button>
          </>
        ) : (
          <p role="status">Loading your team…</p>
        )}
      </main>
    );
  return <LoadedWorkspace {...props} workspace={workspace} />;
}

function LoadedWorkspace({
  client,
  session,
  workspace,
  org,
  onOrganization,
  onSignedOut,
  onExpired,
}: Props & {
  workspace: Workspace;
  org: string;
  onOrganization: (id: string) => void;
}) {
  const [locationId, setLocation] = useState(workspace.locations[0]?.id ?? "");
  const [week, setWeek] = useState(workspace.suggestedWeek);
  const [revision, setRevision] = useState(0);
  const [error, setError] = useState("");
  const [signingOut, setSigningOut] = useState(false);
  const manager = workspace.permission === "Manager";
  const location = workspace.locations.find((x) => x.id === locationId);
  async function signOut() {
    setSigningOut(true);
    setError("");
    try {
      await client.save("/api/v1/auth/logout");
      onSignedOut();
    } catch (e) {
      if (e instanceof ApiError && e.status === 401) onSignedOut();
      else setError(e instanceof Error ? e.message : "Unable to sign out.");
    } finally {
      setSigningOut(false);
    }
  }
  return (
    <>
      <a className="skip-link" href="#main">
        Skip to schedule
      </a>
      <div className="app workspace-app">
        <aside className="sidebar" aria-label="Workspace navigation">
          <div className="brand">
            <span className="brandmark" aria-hidden="true" />
            RUSHCTRL
          </div>
          <p className="eyebrow nav-label">Workspace</p>
          <nav className="nav workspace-nav" aria-label="Main navigation">
            <a href="#schedule" className="active">
              <CalendarDays aria-hidden="true" />
              Schedule
            </a>
            <a href="#availability">
              <Users aria-hidden="true" />
              Availability
            </a>
            <a href="#requests">
              <ArrowLeftRight aria-hidden="true" />
              Requests
            </a>
            {manager && (
              <a href="#coverage">
                <Users aria-hidden="true" />
                Coverage
              </a>
            )}
          </nav>
          <div className="side-footer">
            <div className="workspace-note">
              <strong>A workspace for the rush.</strong>Keep your people in
              sync.
            </div>
            <div className="profile">
              <div className="avatar">
                {session.displayName
                  .split(" ")
                  .map((n) => n[0])
                  .slice(0, 2)
                  .join("")}
              </div>
              <div>
                <strong>{session.displayName}</strong>
                <small>{workspace.permission}</small>
              </div>
            </div>
            <Link href="/preview" className="preview-link">
              Sample preview
            </Link>
          </div>
        </aside>
        <main className="main" id="main">
          <header className="topbar workspace-topbar">
            <div>
              <p className="eyebrow">RUSHCTRL / {workspace.permission}</p>
              <strong>{workspace.organizationName}</strong>
            </div>
            <div className="top-right">
              <SystemStatus />
              <button
                className="button"
                onClick={signOut}
                disabled={signingOut}
              >
                <LogOut aria-hidden="true" />
                {signingOut ? "Signing out…" : "Sign out"}
              </button>
            </div>
          </header>
          <div className="heading">
            <div>
              <p className="eyebrow">
                Synthetic workspace · Changes saved locally
              </p>
              <h1>
                {manager
                  ? "Set the floor for a good day."
                  : "Your week, in view."}
              </h1>
              <p>
                {manager
                  ? "A clear schedule. A team that knows where to be."
                  : "Your shifts, availability, and requests in one place."}
              </p>
            </div>
          </div>
          <div className="workspace-toolbar glass">
            {session.memberships.length > 1 && (
              <label>
                Organization
                <select
                  aria-label="Organization"
                  value={org}
                  onChange={(e) => onOrganization(e.target.value)}
                >
                  {session.memberships.map((m) => (
                    <option key={m.organizationId} value={m.organizationId}>
                      {m.organizationName}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label>
              Location
              <select
                aria-label="Location"
                value={locationId}
                onChange={(e) => setLocation(e.target.value)}
              >
                {workspace.locations.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name}
                  </option>
                ))}
              </select>
            </label>
            <div className="week-picker">
              <button
                className="button"
                aria-label="Previous week"
                onClick={() => setWeek(addDays(week, -7))}
              >
                ←
              </button>
              <label>
                Week beginning
                <input
                  aria-label="Week beginning"
                  type="date"
                  value={week}
                  min="2000-01-03"
                  max="2100-12-20"
                  onChange={(e) => {
                    const day = e.target.value;
                    if (day) {
                      const weekday = new Date(day + "T12:00Z").getUTCDay();
                      setWeek(addDays(day, -((weekday + 6) % 7)));
                    }
                  }}
                />
              </label>
              <button
                className="button"
                aria-label="Next week"
                onClick={() => setWeek(addDays(week, 7))}
              >
                →
              </button>
            </div>
            <span className="zone-label">
              {location?.timeZoneId ?? "No location"}
            </span>
            <button
              className="button"
              onClick={() => setRevision((n) => n + 1)}
            >
              <RefreshCw aria-hidden="true" />
              Refresh schedule
            </button>
          </div>
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          {location ? (
            <ScheduleBoard
              key={`${org}/${locationId}/${week}/${revision}`}
              client={client}
              org={org}
              location={location}
              workspace={workspace}
              week={week}
              onExpired={onExpired}
            />
          ) : (
            <p className="empty-state">
              No locations have been added to this workspace.
            </p>
          )}
          <footer className="workspace-footer">
            Week of {dayLabel(week)} · All schedule times use the location time
            zone. Refresh to see changes from another session.
          </footer>
        </main>
      </div>
    </>
  );
}
