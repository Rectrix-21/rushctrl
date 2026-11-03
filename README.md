# RUSHCTRL

A staffing operations application for hospitality teams, built with Next.js, ASP.NET Core and PostgreSQL.

**Current stage: Milestone 0 foundation.** The API, initial database schema, real readiness checks, local startup tooling, and blue glass dashboard shell are implemented. Staffing figures are visibly synthetic. Scheduling, login, availability and coverage workflows are planned for Milestone 1; no real users, performance improvements, or production scale are claimed.

## Run locally on Windows

Prerequisite: Node.js 24. The setup script downloads the pinned .NET SDK and optional PostgreSQL binaries into ignored project-local directories. It installs no Windows service and changes no system-wide PATH.

From the repository root in PowerShell:

```powershell
./scripts/setup.ps1 -PortableDatabase
./scripts/dev.ps1
```

Open [the dashboard](http://localhost:3000). The API is on [port 5080](http://localhost:5080/api/v1/system), with [readiness](http://localhost:5080/health/ready) and [development OpenAPI](http://localhost:5080/openapi/v1.json).

The sample schedule is fixed to Friday, November 6, 2026, in America/Toronto. It is not a live staff schedule or a claim about today's operations. Day/Week switching and coverage detail panels are local UI interactions; no staffing changes are persisted. The connection indicator polls the real API/database every 30 seconds and can be refreshed manually.

Stop the web/API processes with `./scripts/stop.ps1`. Stop portable PostgreSQL separately with `./scripts/database.ps1 stop`. Logs, process IDs and database files live in ignored `.local/`. Do not delete `.local/pgdata` if you want to keep local data.

If the project is already set up, run only `./scripts/dev.ps1`; setup reinstalls dependencies and is not required on every launch. Do not run portable and Docker stacks on the same ports simultaneously.

## Docker Compose

Prerequisites: Docker with a running Linux-container engine, Docker Compose, and Node.js 24 for local secret generation. Docker Desktop/WSL installation is not automated by this repository.

```text
npm run setup:env
docker compose up --build --wait
```

All published ports bind to 127.0.0.1. PostgreSQL is persistent in a named volume. The one-shot migration service applies the schema after PostgreSQL is ready; the API starts after migration succeeds; the web service starts after API readiness succeeds. This is a local development arrangement, not a production migration/deployment strategy. The API runs in Development to expose the local OpenAPI document.

`docker compose down` stops containers without removing the database volume. The local `.env` contains generated database credentials and must remain untracked. `.env.example` intentionally has no working password; missing values fail configuration rather than using a committed default. Treat the local PostgreSQL role as a development-only administrative role, not a production application credential.

The web image embeds its server-side API origin at build time through `API_BASE_URL`. Rebuild after changing that origin. The default standalone image uses `http://api:8080`; native development uses `http://127.0.0.1:5080`. No database connection string is shipped to the browser.

## Verify

```powershell
./scripts/check.ps1
```

This runs xUnit against isolated temporary PostgreSQL databases, then frontend unit tests, TypeScript, ESLint, and a production Next.js build. PostgreSQL must be running. Tests create and drop only uniquely named `rushctrl_test_*` databases; set test configuration only to a local or isolated CI PostgreSQL server, never a production server. `./scripts/check.ps1 -WithoutDatabase` runs the non-database API tests and frontend checks.

With a system .NET 10 SDK, equivalent commands are:

```text
dotnet build RushCtrl.slnx -c Release
dotnet test RushCtrl.slnx -c Release --filter "Category!=Database"
npm test
npm run typecheck
npm run lint
npm run build
```

To include database integration tests, set `RUSHCTRL_TEST_DATABASE_CONNECTION` to an isolated server with permission to create test databases, then run `dotnet test RushCtrl.slnx -c Release` without the filter. Checks use Release output so the native development API can keep running from Debug output without Windows file locks.

Playwright desktop/mobile smoke tests live in `apps/web/tests/e2e`. When browser execution is authorized, install Chromium with `npx playwright install chromium` from `apps/web`, then run `npm run test:e2e` from the root. Tests cover sample labels, view switching, modal keyboard behavior, mobile overflow and failed readiness. The CI workflow is configured to run them and a full Compose startup; it has not been run on a remote repository.

## Architecture

```text
Browser
  -> Next.js UI and same-origin API/health proxy
       -> ASP.NET Core API
            -> EF Core -> PostgreSQL
```

The API is a modular monolith: one deployable process with feature directories. It owns authorization and persistence. Milestone 0 exposes only operational data. Authentication services and a fallback authorization policy are configured now, but no public login, tenant-data endpoints or fake admin identity are exposed. Identity, membership checks and antiforgery enforcement arrive before staffing writes in Milestone 1.

| Path | Responsibility |
| --- | --- |
| `apps/web/src/app` | Routing, metadata and blue glass styles |
| `apps/web/src/components/dashboard` | Sample schedule, navigation and detail UI |
| `apps/web/src/components/system-status.tsx` | Real connection status |
| `apps/web/src/lib/api.ts` | Typed, bounded health/metadata requests |
| `src/RushCtrl.Api/Program.cs` | API configuration and request pipeline |
| `src/RushCtrl.Api/Modules` | System endpoints and organization/location models |
| `src/RushCtrl.Api/Infrastructure` | EF context and versioned migration |
| `tests/RushCtrl.Api.Tests` | API contracts and PostgreSQL integration tests |
| `scripts` | Local setup, startup, shutdown and checks |

Read the [API contract](docs/api/README.md), [architecture decision](docs/adr/0001-modular-monolith.md), [security boundary](docs/adr/0002-identity-and-tenancy.md), and [step explanations](docs/learning/milestone-0.md).

## Verification status

- Nine backend tests passed, including real PostgreSQL migration/readiness/foreign-key checks.
- Eight frontend connection-state tests passed; TypeScript, ESLint and production build passed.
- Actual frontend-to-API health forwarding and API-to-PostgreSQL readiness returned HTTP 200 locally.
- Compose configuration was validated with the official standalone Compose CLI. **Container startup remains unverified:** this machine has no Docker engine or WSL installation.
- **Browser rendering, keyboard and responsive checks remain unverified:** browser access to the earlier local prototype was declined. No alternate browser path was used to bypass that decision.

Milestone 0's remaining acceptance items are a successful full Compose startup and authorized browser smoke/visual checks. No deployment, remote repository or paid service has been created.

## Next milestone

Milestone 1 starts with ASP.NET Core Identity, organization membership and manager/employee policies. Then implement shift creation/listing/assignment, availability and coverage requests with no-overlap validation and tenant-isolation tests. Real-time notifications, ranking and Friday Rush simulation belong to Milestone 2.
