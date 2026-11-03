"use client";

import { useEffect, useMemo, useState } from "react";
import { RefreshCw } from "lucide-react";
import { createConnectionCheck, type ConnectionState } from "@/lib/api";

const labels = {
  checking: "Checking connection",
  ready: "API + database connected",
  "api-unavailable": "API unavailable",
  "database-unavailable": "Database unavailable",
};

export function SystemStatus() {
  const [state, setState] = useState<ConnectionState | "checking">("checking");
  const checker = useMemo(() => createConnectionCheck(setState), []);
  useEffect(() => {
    const check = () => {
      void checker.refresh();
    };
    check();
    const timer = setInterval(check, 30000);
    return () => {
      checker.cancel();
      clearInterval(timer);
    };
  }, [checker]);

  async function refresh() {
    setState("checking");
    await checker.refresh();
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
