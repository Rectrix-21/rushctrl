import {
  LayoutGrid,
  CalendarDays,
  Users,
  ArrowLeftRight,
  Zap,
  ScrollText,
} from "lucide-react";
import Link from "next/link";

const links = [
  { label: "Schedule", icon: CalendarDays, milestone: 1 },
  { label: "Team", icon: Users, milestone: 1 },
  { label: "Requests", icon: ArrowLeftRight, milestone: 1 },
  { label: "Simulations", icon: Zap, milestone: 2 },
  { label: "Activity", icon: ScrollText, milestone: 2 },
];

export function Sidebar() {
  return (
    <aside className="sidebar" aria-label="Workspace navigation">
      <div className="brand">
        <span className="brandmark" aria-hidden="true" />
        RUSHCTRL
      </div>
      <p className="eyebrow nav-label">Workspace</p>
      <nav className="nav" aria-label="Main navigation">
        <Link className="active" href="/" aria-current="page">
          <LayoutGrid aria-hidden="true" />
          Overview
        </Link>
        {links.map(({ label, icon: Icon, milestone }) => (
          <button
            key={label}
            disabled
            title={`Available in Milestone ${milestone}`}
          >
            <Icon aria-hidden="true" />
            {label}
            {label === "Requests" && <span className="count">2</span>}
          </button>
        ))}
      </nav>
      <div className="side-footer">
        <div className="workspace-note">
          <strong>A workspace for the rush.</strong>Keep your people in sync.
          <br />
          Keep your floor covered.
        </div>
        <div className="profile">
          <div className="avatar">AK</div>
          <div>
            <strong>Manager preview</strong>
            <small>Synthetic workspace</small>
          </div>
        </div>
      </div>
    </aside>
  );
}
