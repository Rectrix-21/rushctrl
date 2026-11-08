"use client";

import { useEffect, useRef, useState } from "react";
import { Plus, CalendarDays } from "lucide-react";
import type { Workspace } from "@/lib/contracts";
import {
  ApiError,
  loadSchedule,
  type ScheduleData,
  type WorkspaceClient,
} from "@/lib/workspace-api";
import {
  addDays,
  dayLabel,
  localDay,
  occursOn,
  timeRange,
} from "@/lib/schedule-view";
import { ActionDialog, type WorkspaceAction } from "./workspace-dialog";

type Props = {
  client: WorkspaceClient;
  org: string;
  location: Workspace["locations"][number];
  workspace: Workspace;
  week: string;
  onExpired: () => void;
};

export function ScheduleBoard({
  client,
  org,
  location,
  workspace,
  week,
  onExpired,
}: Props) {
  const [data, setData] = useState<ScheduleData | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [revision, setRevision] = useState(0);
  const [view, setView] = useState<"day" | "week">("week");
  const [day, setDay] = useState(week);
  const [action, setAction] = useState<WorkspaceAction | null>(null);
  const noticeRef = useRef<HTMLParagraphElement>(null);
  const manager = workspace.permission === "Manager";
  const days = Array.from({ length: 7 }, (_, n) => addDays(week, n));
  const skill = (id: string) =>
    workspace.skills.find((s) => s.id === id)?.name ?? "Unknown skill";
  const person = (id: string) =>
    workspace.people.find((p) => p.id === id)?.displayName ?? "Team member";
  useEffect(() => {
    let active = true;
    loadSchedule(client, org, location.id, week, manager)
      .then((result) => {
        if (active) setData(result);
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
  }, [client, org, location.id, week, manager, revision, onExpired]);
  useEffect(() => {
    if (data && notice) noticeRef.current?.focus();
  }, [data, notice]);
  function reload(message = "") {
    setNotice(message);
    setData(null);
    setError("");
    setRevision((n) => n + 1);
  }
  if (!data)
    return (
      <section className="glass workspace-loading">
        {error ? (
          <>
            <p role="alert">{error}</p>
            <button className="button" onClick={() => reload()}>
              Retry schedule
            </button>
          </>
        ) : (
          <p role="status">Loading saved schedule…</p>
        )}
      </section>
    );
  return (
    <>
      {notice && (
        <p className="save-notice" role="status" tabIndex={-1} ref={noticeRef}>
          {notice}
        </p>
      )}
      <div className="stats workspace-stats">
        <article className="glass">
          <span>Scheduled shifts</span>
          <strong>{data.shifts.length}</strong>
          <small>
            {manager
              ? "Across the selected location and week"
              : "Assigned to you this week"}
          </small>
        </article>
        <article className="glass">
          <span>{manager ? "Open shifts" : "Availability windows"}</span>
          <strong>
            {manager
              ? data.shifts.filter((s) => !s.assigneeUserId).length
              : data.availability.length}
          </strong>
          <small>
            {manager
              ? "Waiting for a qualified teammate"
              : "Saved declarations this week"}
          </small>
        </article>
        <article className="glass">
          <span>Coverage requests</span>
          <strong>{data.requests.length}</strong>
          <small>Pending requests for this week</small>
        </article>
      </div>
      <div className="workspace-columns">
        <div className="workspace-primary">
          <section id="schedule" className="glass workspace-panel">
            <div className="panel-header">
              <div>
                <h2>{manager ? "Team schedule" : "Your schedule"}</h2>
                <p>
                  {location.name} · {location.timeZoneId}
                </p>
              </div>
              <div className="workspace-actions">
                <div className="segmented" aria-label="Schedule view">
                  <button
                    aria-pressed={view === "day"}
                    onClick={() => setView("day")}
                  >
                    Day
                  </button>
                  <button
                    aria-pressed={view === "week"}
                    onClick={() => setView("week")}
                  >
                    Week
                  </button>
                </div>
                {manager && (
                  <button
                    className="button primary"
                    onClick={() => setAction({ kind: "shift" })}
                  >
                    <Plus aria-hidden="true" />
                    Create shift
                  </button>
                )}
              </div>
            </div>
            {view === "day" && (
              <div className="day-picker" aria-label="Choose schedule day">
                {days.map((date) => (
                  <button
                    className="button"
                    aria-pressed={day === date}
                    key={date}
                    onClick={() => setDay(date)}
                  >
                    {dayLabel(date)}
                  </button>
                ))}
              </div>
            )}
            <div
              className={`saved-schedule ${view}`}
              aria-label="Saved schedule"
            >
              {(view === "week" ? days : [day]).map((date) => (
                <section className="saved-day" key={date}>
                  <h3>
                    <CalendarDays size={14} aria-hidden="true" />
                    {dayLabel(date)}
                  </h3>
                  {data.shifts
                    .filter((s) =>
                      occursOn(s.startsAt, s.endsAt, date, location.timeZoneId),
                    )
                    .map((shift) => {
                      const pending = data.requests.some(
                        (r) => r.shiftId === shift.id,
                      );
                      return (
                        <article
                          className={`saved-shift ${shift.assigneeUserId ? "assigned" : "open"}`}
                          key={shift.id}
                          data-shift-id={shift.id}
                        >
                          <span className="shift-skill">
                            {skill(shift.skillId)}
                          </span>
                          <strong>{shift.assigneeName ?? "Open shift"}</strong>
                          <p>
                            {timeRange(
                              shift.startsAt,
                              shift.endsAt,
                              location.timeZoneId,
                            )}
                          </p>
                          <small>
                            {pending
                              ? "Coverage requested · Pending"
                              : shift.assigneeUserId
                                ? "Assigned"
                                : "Unassigned"}
                          </small>
                          {manager ? (
                            <button
                              className="button"
                              disabled={pending}
                              onClick={() =>
                                setAction({ kind: "assignment", shift })
                              }
                            >
                              {pending ? "Request pending" : "Assign employee"}
                            </button>
                          ) : (
                            <button
                              className="button"
                              disabled={
                                pending ||
                                new Date(shift.startsAt).getTime() <= Date.now()
                              }
                              onClick={() =>
                                setAction({ kind: "request", shift })
                              }
                            >
                              {pending ? "Request pending" : "Request coverage"}
                            </button>
                          )}
                        </article>
                      );
                    })}
                  {!data.shifts.some((s) =>
                    occursOn(s.startsAt, s.endsAt, date, location.timeZoneId),
                  ) && <p className="empty-day">No shifts</p>}
                </section>
              ))}
            </div>
          </section>
          <section id="availability" className="glass workspace-panel">
            <div className="panel-header">
              <div>
                <h2>{manager ? "Team availability" : "Your availability"}</h2>
                <p>Dated availability in {location.timeZoneId}</p>
              </div>
              <button
                className="button"
                onClick={() => setAction({ kind: "availability" })}
              >
                <Plus aria-hidden="true" />
                Add availability
              </button>
            </div>
            <div className="workspace-list">
              {!data.availability.length && (
                <p className="empty-state">
                  No availability declared for this week.
                </p>
              )}
              {data.availability.map((a) => (
                <div className="workspace-list-row" key={a.id}>
                  <div>
                    <strong>{person(a.userId)}</strong>
                    <p>
                      {dayLabel(localDay(a.startsAt, location.timeZoneId))} ·{" "}
                      {timeRange(a.startsAt, a.endsAt, location.timeZoneId)}
                    </p>
                  </div>
                  <span className="soft-badge">Available</span>
                </div>
              ))}
            </div>
          </section>
        </div>
        <aside className="workspace-secondary">
          {manager && (
            <section id="coverage" className="glass workspace-panel">
              <div className="panel-header">
                <div>
                  <h2>Coverage gaps</h2>
                  <p>Compared with saved staffing targets</p>
                </div>
              </div>
              <div className="workspace-list">
                {!data.requirements.length && (
                  <p className="empty-state">
                    Add a staffing target to measure coverage.
                  </p>
                )}
                {data.requirements.length > 0 && !data.gaps.length && (
                  <p className="empty-state">
                    All staffing targets for this week are covered.
                  </p>
                )}
                {data.gaps.map((g) => (
                  <div
                    className="coverage-gap"
                    key={`${g.requirementId}/${g.startsAt}`}
                  >
                    <span className="soft-badge warning">
                      {g.missingCount}{" "}
                      {g.missingCount === 1 ? "person" : "people"} short
                    </span>
                    <strong>{skill(g.skillId)}</strong>
                    <p>
                      {dayLabel(localDay(g.startsAt, location.timeZoneId))} ·{" "}
                      {timeRange(g.startsAt, g.endsAt, location.timeZoneId)}
                    </p>
                    <small>
                      {g.assignedCount} assigned / {g.requiredCount} needed
                    </small>
                  </div>
                ))}
                <button
                  className="button"
                  onClick={() => setAction({ kind: "target" })}
                >
                  <Plus aria-hidden="true" />
                  Add staffing target
                </button>
                {data.requirements.length > 0 && (
                  <details>
                    <summary>
                      View staffing targets ({data.requirements.length})
                    </summary>
                    {data.requirements.map((r) => (
                      <p key={r.id}>
                        {r.requiredCount} × {skill(r.skillId)} ·{" "}
                        {dayLabel(localDay(r.startsAt, location.timeZoneId))} ·{" "}
                        {timeRange(r.startsAt, r.endsAt, location.timeZoneId)}
                      </p>
                    ))}
                  </details>
                )}
              </div>
            </section>
          )}
          <section id="requests" className="glass workspace-panel">
            <div className="panel-header">
              <div>
                <h2>Coverage requests</h2>
                <p>Requests keep the original employee assigned</p>
              </div>
            </div>
            <div className="workspace-list">
              {!data.requests.length && (
                <p className="empty-state">
                  No coverage requests for this week.
                </p>
              )}
              {data.requests.map((r) => (
                <article className="coverage-gap" key={r.id}>
                  <span className="soft-badge">Pending</span>
                  <strong>{r.requesterName}</strong>
                  <p>
                    {skill(r.skillId)} ·{" "}
                    {dayLabel(localDay(r.startsAt, location.timeZoneId))}
                  </p>
                  <small>
                    {timeRange(r.startsAt, r.endsAt, location.timeZoneId)}
                  </small>
                </article>
              ))}
              <p className="workspace-hint">
                Request resolution and replacement approval arrive in Milestone
                2. Pending shifts stay assigned.
              </p>
            </div>
          </section>
          {manager && (
            <section className="glass workspace-panel">
              <div className="panel-header">
                <div>
                  <h2>Your team</h2>
                  <p>Qualifications and configured hour limits</p>
                </div>
              </div>
              <div className="workspace-list">
                {workspace.people.map((p) => (
                  <div className="workspace-list-row" key={p.id}>
                    <div>
                      <strong>{p.displayName}</strong>
                      <p>
                        {workspace.qualifications
                          .filter((q) => q.userId === p.id)
                          .map((q) => skill(q.skillId))
                          .join(" · ") || "No qualifications"}
                      </p>
                      <small>{p.maxWeeklyHours} hours / week limit</small>
                    </div>
                  </div>
                ))}
              </div>
            </section>
          )}
        </aside>
      </div>
      {action && (
        <ActionDialog
          action={action}
          client={client}
          org={org}
          location={location}
          workspace={workspace}
          day={view === "day" ? day : week}
          onClose={() => setAction(null)}
          onSaved={(message) => {
            setAction(null);
            reload(message);
          }}
          onExpired={onExpired}
        />
      )}
    </>
  );
}
