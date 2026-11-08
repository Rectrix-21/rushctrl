"use client";

import { useCallback, useEffect, useRef, useState } from "react";
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
import { startWorkspaceUpdates } from "@/lib/realtime";
import { SimulationPanel } from "./simulation-panel";

type Props = {
  client: WorkspaceClient;
  session: Session;
  onSignedOut: () => void;
  onExpired: () => void;
};

export function WorkspaceScreen(props: Props) {
  const [session, setSession] = useState(props.session);
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
    <WorkspaceLoader
      key={org}
      {...props}
      session={session}
      org={org}
      onOrganization={(id) => {
        void props.client
          .refreshSession()
          .then((current) => {
            setSession(current);
            setOrg(id);
          })
          .catch((error) => {
            if (error instanceof ApiError && error.status === 401)
              props.onExpired();
          });
      }}
    />
  );
}

function WorkspaceLoader(
  props: Props & { org: string; onOrganization: (id: string) => void },
) {
  const { client, org, onExpired } = props;
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [error, setError] = useState("");
  const [revision, setRevision] = useState(0);
  const [notice, setNotice] = useState("");
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
  return (
    <LoadedWorkspace
      {...props}
      workspace={workspace}
      notice={notice}
      onReloadWorkspace={(message = "") => {
        setNotice(message);
        setWorkspace(null);
        setError("");
        setRevision((n) => n + 1);
      }}
    />
  );
}

function LoadedWorkspace({
  client,
  session,
  workspace,
  org,
  onOrganization,
  onSignedOut,
  onExpired,
  onReloadWorkspace,
  notice,
}: Props & {
  workspace: Workspace;
  org: string;
  onOrganization: (id: string) => void;
  onReloadWorkspace: (message?: string) => void;
  notice: string;
}) {
  const [locationId, setLocation] = useState(workspace.locations[0]?.id ?? "");
  const [week, setWeek] = useState(workspace.suggestedWeek);
  const [revision, setRevision] = useState(0);
  const [error, setError] = useState("");
  const [signingOut, setSigningOut] = useState(false);
  const [liveStatus, setLiveStatus] = useState("Connecting live updates");
  const [liveAttempt, setLiveAttempt] = useState(0);
  const updates = useRef<ReturnType<typeof startWorkspaceUpdates> | null>(null);
  const callbacks = useRef({ onExpired, onReloadWorkspace });
  useEffect(() => {
    callbacks.current = { onExpired, onReloadWorkspace };
  }, [onExpired, onReloadWorkspace]);
  const refreshAuthoritative = useCallback(async () => {
    try {
      await client.assertIdentity();
      const latest = await client.get(
        `/api/v1/organizations/${org}/workspace`,
        workspaceSchema,
      );
      if (latest.simulation?.generation !== workspace.simulation?.generation)
        callbacks.current.onReloadWorkspace(
          "The lab was reset. Old forms were closed and the new baseline is loaded.",
        );
      else setRevision((n) => n + 1);
    } catch (e) {
      if (e instanceof ApiError && e.status === 401)
        callbacks.current.onExpired();
      else setError(e instanceof Error ? e.message : "Unable to refresh.");
    }
  }, [client, org, workspace.simulation]);
  useEffect(() => {
    const live = startWorkspaceUpdates({
      org,
      generation: workspace.simulation?.generation ?? null,
      verifyIdentity: () => client.assertIdentity(),
      onStatus: setLiveStatus,
      onExpired: () => callbacks.current.onExpired(),
      onChange: (reset) => {
        if (reset)
          callbacks.current.onReloadWorkspace(
            "The lab was reset. Old forms were closed and the new baseline is loaded.",
          );
        else void refreshAuthoritative();
      },
    });
    updates.current = live;
    return () => {
      live.stop();
      updates.current = null;
    };
  }, [
    client,
    org,
    session.id,
    workspace.simulation?.generation,
    liveAttempt,
    refreshAuthoritative,
  ]);
  const onEditing = useCallback(
    (editing: boolean) => updates.current?.setEditing(editing),
    [],
  );
  const manager = workspace.permission === "Manager";
  const location = workspace.locations.find((x) => x.id === locationId);
  async function signOut() {
    setSigningOut(true);
    setError("");
    try {
      await client.save("/api/v1/auth/logout");
      updates.current?.stop();
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
              <span className="live-status" role="status">
                {liveStatus}
              </span>
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
            {(session.memberships.length > 1 || workspace.simulation) && (
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
                  {!session.memberships.some(
                    (m) => m.organizationId === org,
                  ) && (
                    <option value={org}>{workspace.organizationName}</option>
                  )}
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
              onClick={() => {
                void refreshAuthoritative();
                if (liveStatus.includes("offline"))
                  setLiveAttempt((n) => n + 1);
              }}
            >
              <RefreshCw aria-hidden="true" />
              Refresh schedule
            </button>
          </div>
          {notice && (
            <p className="save-notice" role="status">
              {notice}
            </p>
          )}
          {(workspace.canOpenSimulation || workspace.simulation) && (
            <SimulationPanel
              client={client}
              org={org}
              workspace={workspace}
              onOrganization={onOrganization}
              onReload={onReloadWorkspace}
              onExpired={onExpired}
              onEditing={onEditing}
            />
          )}
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          {location ? (
            <ScheduleBoard
              key={`${org}/${locationId}/${week}`}
              client={client}
              org={org}
              location={location}
              workspace={workspace}
              week={week}
              onExpired={onExpired}
              revision={revision}
              onEditing={onEditing}
            />
          ) : (
            <p className="empty-state">
              No locations have been added to this workspace.
            </p>
          )}
          <footer className="workspace-footer">
            Week of {dayLabel(week)} · All schedule times use the location time
            zone. Live updates reload saved data; Refresh is always available.
          </footer>
        </main>
      </div>
    </>
  );
}
