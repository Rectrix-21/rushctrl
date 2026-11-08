# RUSHCTRL

A staffing operations application for hospitality teams, built with Next.js, ASP.NET Core and PostgreSQL.

**Current stage: Milestone 2 implemented locally; CI acceptance pending.** Coverage approval/rejection/cancellation, explained replacement ranking, transactional activity history, an isolated Friday Rush lab and SignalR updates extend the accepted Milestone 1 workspace. Local API/frontend checks and the real HTTP/hub workflow pass. Desktop/mobile browser and container acceptance require this branch's GitHub checks. All people are synthetic; no operational improvements or production scale are claimed.

## Run locally on Windows

Prerequisite: Node.js 24. Setup downloads the pinned .NET SDK and optional PostgreSQL binaries into ignored project-local directories. It installs no Windows service and changes no system-wide PATH.

From the repository root in PowerShell:

```powershell
./scripts/setup.ps1 -PortableDatabase
./scripts/dev.ps1
```

Open [RUSHCTRL](http://localhost:3000). Open the ignored `.local/demo-accounts.txt` in VS Code for the generated manager and employee credentials. Never commit that file or `.env`. Existing nonempty credentials are preserved during setup; changing a password in `.env` does not reset an existing account.

Startup applies migrations and explicitly seeds Development with Northline Coffee / Harbour / America/Toronto, qualifications, availability, future shifts and a staffing target. Later launches preserve saved shifts. The [original Milestone 0 preview](http://localhost:3000/preview) remains a fixed November 6 sample. The main workspace saves changes locally.

Managers create shifts, assign employees, set staffing targets and approve/reject coverage requests. Candidate lists explain eligibility and rank eligible people by scheduled load and recent covers; saving rechecks the rules. Employees see their own shifts, declare availability, request coverage and cancel their own pending requests. Standard pending requests retain the original assignment until approval. Resolved requests remain in history. Availability is additive and immediate; recurring availability and editing/deletion screens remain deferred.

The API exposes [metadata](http://localhost:5080/api/v1/system), [readiness](http://localhost:5080/health/ready) and [Development OpenAPI](http://localhost:5080/openapi/v1.json). API/database readiness polls every 30 seconds. A separate SignalR status reports live connectivity. The schedule reloads after saves, live invalidations, reconnect or Refresh schedule. Ordinary live refresh waits while you edit; a lab reset expires old forms.

The manager's **Open Friday Rush lab** button enters a separate organization with eight fictional profiles and two locations. Apply creates two missing people; approve a replacement, then fill the standby shift. Reset restores only this lab's baseline and preserves activity history. Native startup and Compose explicitly opt in with Development plus `Simulation__Enabled=true`; Production refuses the feature. Normal workspaces have no reset action.

If already set up, run only `./scripts/dev.ps1`. Stop the web/API with `./scripts/stop.ps1`; stop portable PostgreSQL separately with `./scripts/database.ps1 stop`. Logs and database files live in ignored `.local/`. Do not delete `.local/pgdata` if you want to keep local data. Do not run native and Docker stacks on the same ports simultaneously.

## Docker Compose

Prerequisites: Docker with a running Linux-container engine, Docker Compose, and Node.js 24 for local secret generation. Docker Desktop/WSL installation is not automated.

```text
npm run setup:env
docker compose up --build --wait
```

All published ports bind to 127.0.0.1. PostgreSQL persists in a named volume. The one-shot migration service applies the schema and explicitly seeds Development with generated passwords; the API and frontend start after their dependencies are ready. Production refuses demo seeding. This arrangement is for local development, not production deployment.

`docker compose down` retains the database volume. `.env.example` has no working password; missing required values fail configuration. The local PostgreSQL role is administrative for development/tests and is not a production application credential.

The web image embeds its server-side API origin at build time through `API_BASE_URL`. Rebuild after changing it. Compose uses `http://api:8080`; native development uses `http://127.0.0.1:5080`. Database credentials are not exposed to browser JavaScript.

## Verify and learn

With portable PostgreSQL running:

```powershell
./scripts/check.ps1
```

This runs xUnit against isolated temporary PostgreSQL databases, frontend unit tests, TypeScript, ESLint and a production Next.js build. Tests create/drop only randomly named `rushctrl_test_*` databases. Test configuration must point to an isolated local/CI server with permission to create databases. `./scripts/check.ps1 -WithoutDatabase` runs the checks that do not require PostgreSQL. Release test output lets the native Debug API keep running without Windows file locks.

With the seeded web/API running, this additional HTTP workflow exercises both roles through the Next.js proxy:

```text
node scripts/smoke-workflow.mjs
node scripts/smoke-milestone-2.mjs
```

The first creates a normal synthetic shift and pending request for inspection; repeated runs consume available slots/hours. The second resets only the manager's lab, exercises the full shortage progression and checks a real SignalR invalidation through the web proxy. Both refuse non-loopback URLs.

Playwright desktop/mobile tests are in `apps/web/tests/e2e`. They require the migrated/seeded API to be running. When browser execution is available, install Chromium with `npx playwright install chromium` from `apps/web`, then run `npm run test:e2e` from the root. Tests cover the preview, original staffing flow, employee cancellation, manager approval with automatic refresh in an independent employee session, and lab apply/resolve/standby/reset with another tab's open form. One worker avoids competing resets of the same owner's lab. Credential-flow traces/screenshots/video are disabled. CI starts isolated PostgreSQL/API and masked ephemeral credentials. Local browser automation is not part of this branch's observed verification.

Read the [Milestone 2 student walkthrough and self-test guide](docs/learning/milestone-2.md), alongside the [Milestone 1 foundations](docs/learning/milestone-1.md). They explain the increments, code responsibilities and observed checks.

## Architecture

```text
Browser → Next.js UI / same-origin API proxy → ASP.NET Core API → EF Core → PostgreSQL
```

One API deployment owns Identity sessions, organization membership, scheduling rules and database writes. Protected routes require authentication by default. Server membership checks, tenant query filters and composite ownership constraints enforce organization boundaries. State-changing routes validate antiforgery tokens. Assignment uses transactions, employee locks, version tokens and PostgreSQL overlap constraints.

| Path | Responsibility |
| --- | --- |
| `apps/web/src/components/workspace` | Real sessions, forms, saved schedule and coverage UI |
| `apps/web/src/components/dashboard` | Preserved static preview |
| `apps/web/src/lib/contracts.ts` | Runtime-checked API response shapes and TypeScript types |
| `apps/web/src/lib/workspace-api.ts` | Same-origin requests, CSRF and error handling |
| `src/RushCtrl.Api/Modules/Identity` | Users, memberships, sessions and explicit Development seed |
| `src/RushCtrl.Api/Modules/Scheduling` | Scheduling endpoints, assignment rules and gap calculation |
| `src/RushCtrl.Api/Modules/Simulation` | Owner-only fixture, scenario and generation-guarded reset |
| `src/RushCtrl.Api/Modules/Realtime` | Authorized SignalR groups and committed invalidations |
| `src/RushCtrl.Api/Infrastructure` | EF context and versioned migrations |
| `tests/RushCtrl.Api.Tests` | HTTP, domain and isolated PostgreSQL integration tests |
| `scripts` | Local setup/start/stop, checks and HTTP workflow |

Read the [HTTP contract](docs/api/README.md), [staffing APIs](docs/api/milestone-1.md), [Milestone 2 additions](docs/api/milestone-2.md), [architecture ADR](docs/adr/0001-modular-monolith.md), [tenancy ADR](docs/adr/0002-identity-and-tenancy.md), [ranking policy](docs/adr/0003-explained-replacements.md) and [transaction/live-update ADR](docs/adr/0004-workspace-transactions-and-live-updates.md).

## Verification status

- Milestone 2: local API/frontend checks, types, lint, production build and actual HTTP/hub workflow pass. See its journal for counts and limitations. This branch's browser/container CI is pending; earlier green runs do not verify these changes.
- Milestone 1 local API tests, frontend tests, typecheck, lint, production build and the actual HTTP manager/employee workflow have passed. Exact evidence and review status are recorded in the [learning journal](docs/learning/milestone-1.md).
- All three jobs (API, web and Compose) passed on `acf6c19` in [Checks run 42403546329](https://github.com/Rectrix-21/rushctrl/actions/runs/42403546329). This includes the six configured desktop/mobile browser cases, the complete manager/employee staffing workflow, stale-tab write rejection, keyboard/focus checks and mobile overflow assertions. Compose startup, readiness and the container HTTP workflow passed too.
- Milestone 0's browser and Compose acceptance passed in [Checks run 41718067240](https://github.com/Rectrix-21/rushctrl/actions/runs/41718067240) for `8d48cfa`. That historical result does not verify Milestone 1 changes.
- No manual visual/accessibility audit, deployment or paid service is claimed. GitHub repository: `Rectrix-21/rushctrl`.

## After Milestone 2 acceptance

Milestone 3 remains presentation, measured performance and broader polish/audits. No deployment is included in Milestone 2.
