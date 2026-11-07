"use client";

import { useEffect, useMemo, useState, type FormEvent } from "react";
import Link from "next/link";
import { ArrowRight, LockKeyhole } from "lucide-react";
import {
  createWorkspaceClient,
  type WorkspaceClient,
} from "@/lib/workspace-api";
import { loginForm } from "@/lib/forms";
import type { Session } from "@/lib/contracts";
import { SystemStatus } from "@/components/system-status";
import { WorkspaceScreen } from "./workspace-screen";

export function WorkspaceApp() {
  const client = useMemo(() => createWorkspaceClient(), []);
  const [session, setSession] = useState<Session | null | undefined>(undefined);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let active = true;
    client
      .session()
      .then((value) => {
        if (active) {
          setSession(value);
          setError("");
        }
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [client, attempt]);
  if (session === undefined)
    return (
      <main className="session-state">
        <div className="brand">
          <span className="brandmark" aria-hidden="true" />
          RUSHCTRL
        </div>
        {error ? (
          <>
            <p role="alert">{error}</p>
            <button
              className="button"
              onClick={() => {
                setError("");
                setAttempt((n) => n + 1);
              }}
            >
              Retry connection
            </button>
          </>
        ) : (
          <p role="status">Opening your workspace…</p>
        )}
        <SystemStatus />
      </main>
    );
  if (!session)
    return <SignIn client={client} notice={error} onSignedIn={setSession} />;
  return (
    <WorkspaceScreen
      key={session.id}
      client={client}
      session={session}
      onSignedOut={() => setSession(null)}
      onExpired={() => {
        setError("Your session expired. Sign in again.");
        setSession(null);
      }}
    />
  );
}

function SignIn({
  client,
  notice,
  onSignedIn,
}: {
  client: WorkspaceClient;
  notice: string;
  onSignedIn: (session: Session) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(notice);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const input = loginForm.safeParse({
      email: String(data.get("email") ?? "").trim(),
      password: data.get("password"),
    });
    if (!input.success) {
      setError(input.error.issues[0].message);
      return;
    }
    setBusy(true);
    setError("");
    try {
      await client.save("/api/v1/auth/login", input.data);
      const session = await client.session();
      if (!session)
        throw new Error("Sign-in did not establish a session. Try again.");
      onSignedIn(session);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Unable to sign in.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="auth-shell">
      <section className="auth-story">
        <div className="brand">
          <span className="brandmark" aria-hidden="true" />
          RUSHCTRL
        </div>
        <p className="eyebrow">A workspace for the rush</p>
        <h1>
          Your team.
          <br />
          <span>In sync.</span>
        </h1>
        <p className="auth-intro">
          Make room for the day ahead. Plan the floor, find the gaps, and keep
          everyone on the same schedule.
        </p>
        <div className="auth-note">
          <span className="connection-dot" aria-hidden="true" />
          <div>
            <strong>Local demo workspace</strong>
            <p>Synthetic people. Real scheduling rules.</p>
          </div>
        </div>
        <Link href="/preview">
          Explore the sample preview <ArrowRight size={14} aria-hidden="true" />
        </Link>
      </section>
      <section className="auth-card glass" aria-labelledby="sign-in-heading">
        <div className="auth-icon">
          <LockKeyhole aria-hidden="true" />
        </div>
        <p className="eyebrow">Welcome back</p>
        <h2 id="sign-in-heading">Sign in to your workspace</h2>
        <p className="muted">
          Use a manager or employee account from local setup.
        </p>
        <form className="workspace-form" onSubmit={submit}>
          <label>
            Email
            <input
              name="email"
              type="email"
              autoComplete="username"
              required
              maxLength={256}
              placeholder="you@your-team.example"
            />
          </label>
          <label>
            Password
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              required
              maxLength={256}
            />
          </label>
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          <button className="button primary" disabled={busy}>
            {busy ? "Signing in…" : "Sign in"}
            <ArrowRight size={16} aria-hidden="true" />
          </button>
        </form>
        <div className="auth-connection">
          <SystemStatus />
        </div>
      </section>
    </main>
  );
}
