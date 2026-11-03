# RUSHCTRL foundation and dashboard design

Status: Approved by the owner in chat; builder review completed. Implementation and verification status are tracked in the README and learning notes.

## Intent

Build a standalone staffing application for hospitality teams and an engineering portfolio. The first deliverable is an understandable, reproducible foundation. A complete staffing workflow comes before simulations, recommendations, or presentation extras. No existing users, performance improvements, or production readiness are claimed.

The owner requested an Apple-inspired Liquid Glass appearance with blue neon accents, an intentional visual identity, and explanations after each implementation step. Repository comments should explain engineering decisions. Do not add GPT branding or artificial co-author trailers; use the existing configured Git identity without inventing authorship details.

## Approaches and boundary

The engineering specification selects one sensible architecture: a Next.js App Router frontend and an ASP.NET Core modular monolith backed by PostgreSQL. A frontend-only prototype cannot demonstrate the requested backend rules; microservices add operational cost without helping this application. Neither is proposed as an alternative for the product. A disposable HTML preview is used only to review the visual direction.

One API deployment owns identity, organization membership, scheduling rules, database writes, and audit events. The browser never connects to PostgreSQL. The frontend calls same-origin `/api/*` routes forwarded to the API. Add explicit same-origin `/health/live` and `/health/ready` rewrites as well; `/api/*` alone does not forward those paths. The API origin is server-only configuration. SignalR will eventually announce changes; clients then reload authoritative API state. No SignalR feature is claimed in Milestone 0.

Use the stable .NET 10 LTS SDK, Node.js 24, the currently supported stable Next.js release verified at setup, TypeScript, EF Core/Npgsql matching .NET 10, and a pinned supported PostgreSQL container image. Lock exact dependency versions when installing. Official support references:

- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://nextjs.org/docs/app/getting-started/installation

## Goals and non-goals

Milestone 0 delivers a runnable frontend/API/database skeleton, local setup, health checks, an initial migration, documented API contracts, automated build/test checks, and explicit future authentication/tenant decisions. It also establishes the approved blue glass visual system.

Milestone 0 does not implement shift writes, availability requests, coverage ranking, simulations, live notifications, a production login, or hosting. The dashboard schedule is synthetic preview content, visibly distinguished from live backend status. No real employee data is collected. No paid services, external repositories, accounts, or deployments are created.

## Visual design

Dark navy canvas; restrained radial blue light; translucent panels with subtle white upper edges; electric blue reserved for active navigation, focus, and primary actions. Native system typography keeps local setup independent of external font services. A left workspace navigation rail, compact workspace header, three staffing summaries, central day schedule, and attention/request column establish the dashboard hierarchy.

The sample organization is Northline Coffee, Harbour location, using America/Toronto as an explicitly synthetic location setting. A fixed sample Friday is labelled as sample data rather than presented as today's real schedule. Mobile layouts stack panels, keep touch targets usable, and allow the schedule to scroll inside its own labelled region. Provide keyboard focus, semantic headings, visible text for status, reduced-motion handling, and opaque fallback surfaces. The preview has only working local view switches and informational panels; unimplemented actions are disabled.

The application shell will include a real connection status component separately from the sample staffing display. Failed API or database checks must show an unavailable state rather than a fabricated green status.

## Folder layout

```text
apps/web/                  Next.js UI, components, typed API access, Playwright
src/RushCtrl.Api/           ASP.NET Core entry point and feature modules
  Infrastructure/          EF Core context and initial migration
  Modules/Organizations/   Organization and location entities
  Modules/System/          Public system metadata and health contract
tests/RushCtrl.Api.Tests/   xUnit API and database integration tests
docs/adr/                  Architecture and security decisions
docs/api/                  Implemented endpoint contract
docs/learning/             Step explanations and verification evidence
docs/superpowers/          Design and implementation plan
scripts/                   Local bootstrap/check helpers where needed
compose.yaml               Web, API, PostgreSQL local stack
.github/workflows/ci.yml    Frontend and backend checks
```

Avoid empty layers or placeholder projects with no behavior. The first backend can remain one API project with module directories. Split into assemblies only if dependencies justify it later.

## Initial data model

Implement only Organization and Location in Milestone 0:

- Organization: UUID Id, Name, CreatedAt as UTC timestamptz.
- Location: UUID Id, OrganizationId foreign key, Name, IANA TimeZoneId, CreatedAt. Index OrganizationId and enforce organization/location relational ownership.

The next vertical slice adds IdentityUser; Membership (OrganizationId, UserId, Manager/Employee permission); EmployeeSkill; AvailabilityWindow; Shift (OrganizationId, LocationId, optional AssigneeUserId, skill/role requirement, UTC StartsAt/EndsAt, concurrency token); StaffingRequirement; and CoverageRequest. Add other proposed entities only when their workflow needs them. Use composite ownership constraints for relationships across tenants. Time intervals are half-open `[start, end)` so adjacent shifts do not overlap. EndsAt must be after StartsAt. Final assignment is manager-only and must reject overlaps, missing qualifications, unavailable staff, and configured hour-cap violations.

The first migration establishes only the two implemented tables. No employee schema or sample personal details are persisted in Milestone 0.

## Authentication and tenant isolation

Milestone 0 exposes only public operational endpoints with no organization or employee records. Configure an actual ASP.NET Core cookie authentication handler and a fallback authorization policy requiring authenticated users; public endpoints opt out explicitly. Cookie challenges and access denials return API status codes 401/403, never HTML login redirects. A test-only protected route proves anonymous access returns 401. Do not ship placeholder bearer tokens, a client-selected role, trusted X-User/X-Tenant headers, or a fake production login. No public login or credential flow is exposed until Identity is implemented in Milestone 1.

Milestone 1 will use ASP.NET Core Identity with HttpOnly, SameSite cookies through the same-origin API proxy. State-changing authenticated requests require antiforgery validation. Resolve organization membership from authenticated server identity before loading tenant resources. Manager authorization checks membership for the requested organization. Apply tenant scoping in the data access layer and relational ownership constraints; test a second organization to verify isolation. Return a consistent inaccessible/not-found response for cross-tenant identifiers.

Local synthetic identities are development-only, with credentials provided through ignored local configuration or generated during setup. Do not commit passwords. A later public demo provisions an isolated, expiring synthetic organization per visitor/session rather than sharing unrestricted mutable admin data.

## Milestone 0 API

- GET `/health/live`: process liveness; HTTP 200 when the process is responsive. Does not require the database.
- GET `/health/ready`: HTTP 200 only if PostgreSQL is reachable and all expected EF migrations are applied; HTTP 503 otherwise, including a database with no migration history. Public response exposes status, not connection strings or exception details. `CanConnectAsync` alone is insufficient.
- GET `/api/v1/system`: public service name, version, and environment-neutral milestone metadata. No tenant data or secrets.
- GET `/openapi/v1.json`: development-only OpenAPI definition.

Use Problem Details for failures where appropriate, structured logs, request correlation via ASP.NET tracing, and bounded dependency-check timeouts. Do not equate liveness with database readiness.

## Local development and migrations

Install a .NET 10 SDK in a project-local ignored tool directory rather than changing the system-wide runtime. Node.js is already installed. Docker is currently absent. Prefer Docker Desktop/Compose when available; do not silently install Docker Desktop or modify WSL/system features. Provide Compose plus a clear prerequisite. If an isolated PostgreSQL process can be run without system changes, use it to verify API/database behavior, and report that this does not verify Docker Compose itself.

Use an ignored `.env` for local database credentials generated at setup, with a secret-free `.env.example`. Bind published local ports to 127.0.0.1. Use health-based Compose dependencies, persistent database storage, and explicit migration application during local startup. Production migration/deployment strategy is deferred and must not imply startup migrations are a universal production recommendation.

## Test and acceptance strategy

Backend xUnit: liveness works without PostgreSQL; anonymous requests cannot access protected routes; system metadata contains no tenant data; readiness is unhealthy with a failed connection; readiness is healthy against a migrated isolated test database. Verify an unapplied schema is not ready. Database tests use isolated configuration, never a production database.

Frontend: TypeScript checking, lint, production build, and a Playwright smoke test confirming the dashboard loads, sample-data labels are visible, live connection status reflects backend state, and keyboard-operated preview panels open/close. Inspect desktop/mobile layout and horizontal overflow. Build the backend and run xUnit. CI starts PostgreSQL and applies the migration before database integration tests.

Milestone 0 is complete only when frontend-to-API and API-to-PostgreSQL are verified, migrations/build/tests pass, and Docker Compose startup has been exercised. If Docker cannot run on this machine, explicitly report that acceptance item as pending; never label it verified by static inspection.

## Narrow Milestone 1

1. Identity cookies, development seed, organization membership and manager/employee policies, with tenant-isolation tests.
2. Manager creates/lists shifts and assigns an employee; database and service rules reject invalid ranges and overlaps.
3. Employee records availability and submits a coverage request; manager sees requests and coverage gaps.
4. One Playwright manager/employee flow and integration tests for permissions, isolation, and assignment conflicts.

Milestone 2 adds ranking, atomic approval, audit, simulation/reset, and SignalR. Milestone 3 adds measured operational and portfolio deliverables.

## Milestone 0 implementation increments

1. **Repository and API foundation:** initialize a local Git repository; add ignore rules for secrets, tool downloads, build outputs, and prototype state; install the SDK locally; create the API and xUnit project. Implement public liveness/system metadata, real cookie authentication with fallback authorization, development OpenAPI, and test-only protected routes. Verify build, liveness without a database, anonymous 401, and minimal public metadata. Explain the HTTP request path and why authentication is established before staffing endpoints.
2. **PostgreSQL foundation:** add Organization/Location entities, EF Core context, initial migration, explicit readiness checks, configuration examples, database container configuration, and a migration startup path for local use. Test healthy migrated, unreachable, and unmigrated database states against isolated test databases. Explain persistence, foreign keys, and the difference between liveness and readiness.
3. **Dashboard shell:** create the Next.js TypeScript application, adapt the reviewed visual direction into focused React components, and add typed server/API access plus same-origin rewrites. Include real connection status alongside visibly synthetic sample staffing content. Build, typecheck, lint, and exercise a Playwright smoke workflow including failed readiness and keyboard panel behavior. Explain rendering, components, state, and the frontend/API boundary.
4. **Reproducible local stack and CI:** add Dockerfiles and health-based Compose startup for all three services, a local secret-generation/setup command, GitHub Actions frontend/backend checks with PostgreSQL, API reference, architecture decisions, and run instructions. Run the complete available verification suite and Compose startup when Docker is available. Explain containers, migrations, and why automated checks run on each change.
5. **Acceptance and handoff:** record exact commands/results and any unavailable Docker/browser checks in the learning log. Provide working local URLs and a short walkthrough. Leave Milestone 0 explicitly incomplete if the required Compose startup has not been exercised. Do not begin Milestone 1 automatically.

The initial prototype is not production frontend code. Its layout can be adapted after review; its synthetic data must not be mistaken for backend-backed functionality.

## Review evidence

A separate builder review found no blocking product or architecture decision. It requested explicit health routing, migration-aware readiness, a real authentication handler with non-redirecting challenges, and honest Compose acceptance evidence; these are incorporated above.

The disposable HTML preview was authored locally. Browser automation was denied access to its local origin, so browser rendering, interactions, responsive behavior, and accessibility remain unverified. Static JavaScript checks are recorded separately from browser QA. The owner can inspect the preview directly.

## Explanation after each increment

Report what changed, why the approach was chosen, the relevant file/module responsibilities, commands and observed test results, how to try it, and remaining limitations. Keep a short learning log for revisiting the implementation. Make reviewable local commits using the configured identity only; do not create a remote or push.

## Decisions left to implementation

The builder chooses exact patch versions after checking official registries, concise names for helper components, the local free ports, and small layout adjustments needed for readability. The architecture and visual direction remain subject to owner review. No production provider, domain, real employee handling, or deployment has been authorized.
