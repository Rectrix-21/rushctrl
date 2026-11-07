# RUSHCTRL

A staffing operations application for hospitality teams, built with Next.js, ASP.NET Core and PostgreSQL.

**Current stage: Milestone 1 functional workflow — acceptance in progress.** Identity sign-in, organization permissions, saved shifts/assignments, dated availability, pending coverage requests and staffing gaps are implemented locally. The blue glass manager/employee screens use PostgreSQL-backed APIs. Local checks, Docker startup and a complete container HTTP workflow have passed; browser acceptance is still pending CI. All people are synthetic. No real users, operational improvements or production scale are claimed.

## Run locally on Windows

Prerequisite: Node.js 24. Setup downloads the pinned .NET SDK and optional PostgreSQL binaries into ignored project-local directories. It installs no Windows service and changes no system-wide PATH.

From the repository root in PowerShell:

```powershell
./scripts/setup.ps1 -PortableDatabase
./scripts/dev.ps1
```

Open [RUSHCTRL](http://localhost:3000). Open the ignored `.local/demo-accounts.txt` in VS Code for the generated manager and employee credentials. Never commit that file or `.env`. Existing nonempty credentials are preserved during setup; changing a password in `.env` does not reset an existing account.

Startup applies migrations and explicitly seeds Development with Northline Coffee / Harbour / America/Toronto, qualifications, availability, future shifts and a staffing target. Later launches preserve saved shifts. The [original Milestone 0 preview](http://localhost:3000/preview) remains a fixed November 6 sample. The main workspace saves changes locally.

Managers create shifts, assign employees and set staffing targets. Assignment rejects missing qualifications, incomplete availability, overlaps, excessive configured weekly hours and stale versions. Employees see their own shifts, declare availability and request coverage. A pending request keeps the original employee assigned and blocks reassignment. Cancellation/resolution and replacement approval arrive in Milestone 2; the UI explains this before submission. Availability is additive and immediate. Recurring availability, time-off administration and editing/deletion screens are deferred.

The API exposes [metadata](http://localhost:5080/api/v1/system), [readiness](http://localhost:5080/health/ready) and [Development OpenAPI](http://localhost:5080/openapi/v1.json). Connection status polls every 30 seconds. The schedule reloads after saves or with Refresh schedule; live notifications are not implemented yet.

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
```

It creates a synthetic shift and pending coverage request and leaves them in your local demo for inspection. It refuses non-loopback URLs. Repeated runs consume available demo scheduling slots and hours; it is not a reset command.

Playwright desktop/mobile tests are in `apps/web/tests/e2e`. They require the migrated/seeded API to be running. When browser execution is available, install Chromium with `npx playwright install chromium` from `apps/web`, then run `npm run test:e2e` from the root. The tests cover the sample preview and a real manager-create/assign/reload → employee-availability/request → manager-visibility flow, plus keyboard dialogs and mobile overflow. Credential-flow traces/screenshots/video are disabled. CI starts an isolated PostgreSQL/API and generates masked ephemeral credentials before these tests.

Read the [student walkthrough and self-test guide](docs/learning/milestone-1.md). It explains the code in five increments, why each part exists, and what the checks prove.

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
| `src/RushCtrl.Api/Infrastructure` | EF context and versioned migrations |
| `tests/RushCtrl.Api.Tests` | HTTP, domain and isolated PostgreSQL integration tests |
| `scripts` | Local setup/start/stop, checks and HTTP workflow |

Read the [HTTP contract](docs/api/README.md), [staffing API details](docs/api/milestone-1.md), [architecture ADR](docs/adr/0001-modular-monolith.md) and [identity/tenancy ADR](docs/adr/0002-identity-and-tenancy.md).

## Verification status

- Milestone 1 local API tests, frontend tests, typecheck, lint, production build and the actual HTTP manager/employee workflow have passed. Exact evidence and review status are recorded in the [learning journal](docs/learning/milestone-1.md).
- API and Docker Compose acceptance passed again on `a5d4a63` in [Checks run 42402225906](https://github.com/Rectrix-21/rushctrl/actions/runs/42402225906). Browser cases now submit a shift successfully, then fail reading its response body through Chromium's debugging protocol. The test now checks the rendered saved card instead and awaits a new run. A build or test listing does not establish that browser interactions work.
- Milestone 0's browser and Compose acceptance passed in [Checks run 41718067240](https://github.com/Rectrix-21/rushctrl/actions/runs/41718067240) for `8d48cfa`. That historical result does not verify Milestone 1 changes.
- No manual visual/accessibility audit, deployment or paid service is claimed. GitHub repository: `Rectrix-21/rushctrl`.

## After Milestone 1 acceptance

Milestone 2 adds request resolution, atomic replacement approval, candidate ranking with explanations, audit, an isolated Friday Rush simulation/reset workflow and SignalR notifications. Do not describe those as implemented yet.
