import { sampleShifts } from "./sample-data";

export function Schedule({
  view,
  onViewChange,
}: {
  view: "day" | "week";
  onViewChange: (view: "day" | "week") => void;
}) {
  return (
    <section className="glass schedule" aria-labelledby="schedule-heading">
      <div className="panel-header">
        <div>
          <h2 id="schedule-heading">The floor, at a glance.</h2>
          <p>Your sample Friday schedule</p>
        </div>
        <div className="segmented" aria-label="Schedule view">
          <button
            aria-pressed={view === "day"}
            onClick={() => onViewChange("day")}
          >
            Day
          </button>
          <button
            aria-pressed={view === "week"}
            onClick={() => onViewChange("week")}
          >
            Week
          </button>
        </div>
      </div>
      <div className="datebar">
        <span>Friday, November 6, 2026</span>
        <span className="timezone">AMERICA/TORONTO</span>
      </div>
      {view === "day" ? (
        <div
          className="timeline-wrap"
          tabIndex={0}
          role="region"
          aria-label="Sample shift timeline; scroll horizontally on small screens"
        >
          <div className="timeline">
            <div className="time-header" aria-hidden="true">
              <span>TEAM MEMBER</span>
              {[
                "06:00",
                "08:00",
                "10:00",
                "12:00",
                "14:00",
                "16:00",
                "18:00",
                "20:00",
              ].map((time) => (
                <span key={time}>{time}</span>
              ))}
            </div>
            {sampleShifts.map((shift) => (
              <div className="shift-row" key={shift.name}>
                <div className="person">
                  <span className="avatar" aria-hidden="true">
                    {shift.initials}
                  </span>
                  <div>
                    <strong>{shift.name}</strong>
                    <small>{shift.role}</small>
                  </div>
                </div>
                <div
                  className={`shift ${shift.later ? "alt" : ""}`}
                  style={{ gridColumn: shift.column }}
                >
                  <strong>
                    {shift.start}–{shift.end}
                  </strong>
                  <small>{shift.detail}</small>
                </div>
              </div>
            ))}
            <div className="shift-row">
              <div className="person">
                <span className="avatar open-avatar" aria-hidden="true">
                  +
                </span>
                <div>
                  <strong>Open shift</strong>
                  <small>Barista needed</small>
                </div>
              </div>
              <div className="shift gap" style={{ gridColumn: "5 / 7" }}>
                <strong>12:00–16:00</strong>
                <small>Needs coverage</small>
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div className="week-view">
          <strong>Week of November 2–6</strong>
          <p>Layout preview only. Other days have no sample shifts.</p>
          <div className="week-grid">
            {["Mon 02", "Tue 03", "Wed 04", "Thu 05", "Fri 06"].map(
              (day, index) => (
                <div
                  className={`week-day ${index === 4 ? "today" : ""}`}
                  key={day}
                >
                  <strong>{day}</strong>
                  <span>
                    {index === 4 ? (
                      <>
                        4 assigned
                        <br />1 open
                      </>
                    ) : (
                      "No sample data"
                    )}
                  </span>
                </div>
              ),
            )}
          </div>
        </div>
      )}
      <div className="legend">
        <span>
          <i aria-hidden="true" />
          Assigned
        </span>
        <span>
          <i className="pending" aria-hidden="true" />
          Assigned · later start
        </span>
        <span>
          <i className="open" aria-hidden="true" />
          Open shift
        </span>
      </div>
    </section>
  );
}
