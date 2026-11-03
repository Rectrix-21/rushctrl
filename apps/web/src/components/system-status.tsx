"use client";

import { useEffect, useState } from "react";
import { RefreshCw } from "lucide-react";
import { loadConnectionState, type ConnectionState } from "@/lib/api";

const labels = {
  checking: "Checking connection",
  ready: "API + database connected",
  "api-unavailable": "API unavailable",
  "database-unavailable": "Database unavailable",
};

export function SystemStatus() {
  const [state, setState] = useState<ConnectionState | "checking">("checking");
  useEffect(() => {
    let active = true;
    const check = () => {
      void loadConnectionState().then((result) => {
        if (active) setState(result);
      });
    };
    check();
    const timer = setInterval(check, 30000);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, []);

  async function refresh() {
    setState("checking");
    setState(await loadConnectionState());
  }

  return (
    <div className={`connection-state ${state}`}>
      <span role="status" aria-live="polite">
        <span className="connection-dot" aria-hidden="true" />
        {labels[state]}
      </span>
      <button
        className="connection-refresh"
        aria-label="Refresh connection status"
        title="Refresh connection status"
        disabled={state === "checking"}
        onClick={refresh}
      >
        <RefreshCw size={12} aria-hidden="true" />
      </button>
    </div>
  );
}
