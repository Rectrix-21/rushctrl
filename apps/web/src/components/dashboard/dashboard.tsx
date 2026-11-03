"use client";

import { useEffect, useRef, useState } from "react";
import { ArrowUpRight, ArrowLeftRight, Clock3, Info, X } from "lucide-react";
import { SystemStatus } from "@/components/system-status";
import { Schedule } from "./schedule";
import { sampleRequests } from "./sample-data";

export function Dashboard() {
  const [view, setView] = useState<"day" | "week">("day");
  const [panel, setPanel] = useState<"about" | "coverage" | null>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    if (panel && !dialog.current?.open) dialog.current?.showModal();
    if (!panel && dialog.current?.open) dialog.current?.close();
  }, [panel]);

  return (
    <>
      <header className="topbar">
        <div className="mobile-brand">
          <span className="brandmark" aria-hidden="true" />
          RUSHCTRL
        </div>
        <div className="location">
          <strong>Northline Coffee</strong>
          <span className="divider" />
          <span>Harbour location</span>
        </div>
        <SystemStatus />
      </header>
      <section className="heading">
        <div>
          <div className="eyebrow">Friday, November 6 · Sample day</div>
          <h1>Ready for the rush.</h1>
          <p>A clear view of your floor, your people, and what needs you.</p>
        </div>
        <button className="button" onClick={() => setPanel("about")}>
          <Info aria-hidden="true" />
          About this preview
        </button>
      </section>
      <section className="stats" aria-label="Sample staffing summary">
        <article className="glass stat">
          <div className="stat-top">
            Scheduled today
            <span className="stat-mark">
              <ArrowUpRight aria-hidden="true" size={16} />
            </span>
          </div>
          <div className="stat-value">
            4<small>people</small>
          </div>
          <div className="stat-bottom">
            <span className="dot" />
            Across 4 assigned shifts
          </div>
        </article>
        <article className="glass stat">
          <div className="stat-top">
            Floor coverage
            <span className="stat-mark">
              <Clock3 aria-hidden="true" size={16} />
            </span>
          </div>
          <div className="stat-value">
            1<small>open shift</small>
          </div>
          <div className="stat-bottom">
            <span className="dot amber" />
            Barista · 12:00–16:00
          </div>
        </article>
        <article className="glass stat">
          <div className="stat-top">
            Needs a decision
            <span className="stat-mark">
              <ArrowLeftRight aria-hidden="true" size={16} />
            </span>
          </div>
          <div className="stat-value">
            2<small>requests</small>
          </div>
          <div className="stat-bottom">
            <span className="dot" />1 coverage · 1 availability
          </div>
        </article>
      </section>
      <div className="content">
        <Schedule view={view} onViewChange={setView} />
        <aside
          className="right-column"
          aria-label="Sample items needing attention"
        >
          <section className="glass attention">
            <div className="eyebrow">Needs your attention</div>
            <h2>A gap in the afternoon.</h2>
            <p>
              One barista shift is still open.
              <br />
              Review the gap before the floor gets busy.
            </p>
            <div className="gap-meta">
              <span className="tag">12:00–16:00</span>
              <span className="tag">Espresso qualified</span>
            </div>
            <button className="button" onClick={() => setPanel("coverage")}>
              Review coverage <ArrowUpRight size={14} aria-hidden="true" />
            </button>
          </section>
          <section className="glass requests">
            <h2>
              Team requests <span className="count">2</span>
            </h2>
            {sampleRequests.map((request) => (
              <div className="request" key={request.name}>
                <span className="avatar" aria-hidden="true">
                  {request.initials}
                </span>
                <div>
                  <strong>{request.name}</strong>
                  <p>{request.description}</p>
                  <small>Sample request · awaiting review</small>
                </div>
              </div>
            ))}
            <div className="requests-footer">
              Request management arrives in Milestone 1
            </div>
          </section>
        </aside>
      </div>
      <footer className="footer">
        <span>
          <strong>Milestone 0 · Synthetic preview</strong>
          <br />
          All people, shifts, and requests are sample data. No staffing changes
          are saved.
        </span>
        <span>
          America/Toronto
          <br />
          Northline Coffee · Harbour
        </span>
      </footer>
      <dialog
        className="detail-dialog"
        ref={dialog}
        aria-labelledby="detail-heading"
        onClose={() => setPanel(null)}
        onClick={(event) => {
          if (event.target === event.currentTarget) dialog.current?.close();
        }}
      >
        <section className="drawer-inner">
          <div className="drawer-header">
            <span className="eyebrow">RUSHCTRL · Sample workspace</span>
            <form method="dialog">
              <button className="button" aria-label="Close panel">
                <X size={16} aria-hidden="true" />
              </button>
            </form>
          </div>
          <h2 id="detail-heading">
            {panel === "coverage"
              ? "One open barista shift."
              : "Your foundation, in focus."}
          </h2>
          {panel === "coverage" ? (
            <>
              <p>
                <strong>Friday, November 6 · 12:00–16:00</strong>
                <br />
                Northline Coffee · Harbour location
                <br />
                Time zone: America/Toronto
              </p>
              <p>
                This sample shift needs one espresso-qualified barista. The
                planned coverage engine will check availability, qualifications,
                shift overlaps, and configured hour limits.
              </p>
              <p>
                Recommendations and manager approval arrive in later milestones.
                This panel makes no staffing changes.
              </p>
            </>
          ) : (
            <>
              <p>
                The blue glass dashboard is a visual shell for the staffing
                application. Its schedule, team, and requests use synthetic
                sample data.
              </p>
              <p>
                The connection indicator is real: it checks the C# API and
                PostgreSQL readiness through same-origin requests.
              </p>
              <ul>
                <li>Day / Week switches the local schedule view.</li>
                <li>Review coverage opens a sample gap.</li>
                <li>Staffing workflows arrive in Milestone 1.</li>
              </ul>
            </>
          )}
          <form method="dialog">
            <button className="button primary">Back to dashboard</button>
          </form>
        </section>
      </dialog>
    </>
  );
}
