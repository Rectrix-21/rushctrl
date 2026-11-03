# Milestone 0 — implementation notes

**Current result:** Milestone 0's automated acceptance passed in [GitHub Actions for commit 8d48cfa](https://github.com/Rectrix-21/rushctrl/actions/runs/41718067240). API, frontend/browser and full Docker Compose jobs succeeded. The sections below explain the implementation and the limitations encountered at the initial local handoff; the final section records how the remaining runtime checks were verified. A manual visual/accessibility audit remains separate from these smoke tests.

## 1. API foundation

The repository is a standalone application on the local `milestone-0` branch. The downloaded .NET SDK is inside ignored `.tools/dotnet`, verified against Microsoft's SHA512 release metadata. Nothing has been pushed or deployed.

`Program.cs` assembles the HTTP pipeline: structured request logging and a correlation header, exception handling, cookie authentication, authorization, then endpoint dispatch. The fallback authorization policy protects new endpoints unless they explicitly opt into anonymous access. Cookie challenges return 401/403 for API clients rather than HTML redirects. No login or staffing endpoint is exposed yet.

`Modules/System/SystemEndpoints.cs` contains public operational contracts. `/health/live` answers without a database. `/api/v1/system` exposes only the service name, version, and milestone. Development OpenAPI describes these endpoints; production requests to that document return 404.

Verification: five xUnit tests passed. Before implementation, four failed on missing routes/unprotected access. The production OpenAPI test then caught a routing detail: ASP.NET's default fallback excludes file-like paths such as `.json`. An explicit catch-all now returns 404 without exposing a document.

Next: persistence and migration-aware readiness. Liveness alone must never be presented as proof that the application can use its database.

## 2. PostgreSQL and readiness

The two initial entities are Organization and Location. EF Core maps them to PostgreSQL UUID keys and timezone-aware creation timestamps. A location must belong to an existing organization, names cannot be blank, and duplicate location names within an organization are rejected. The `(OrganizationId, Id)` alternate key prepares for tenant-owned references in future workflows. No public tenant-data endpoint exists yet, so cross-tenant authorization is not claimed as implemented.

The initial migration is a versioned schema change, not a database dump. Apply it explicitly with the API's `--migrate` command. The readiness check requires a reachable database, no pending migrations, and readable organization/location tables. Its public response exposes only healthy/unhealthy; failed dependencies return 503. Queries and the overall check are bounded by short timeouts.

Verification: all nine API tests passed, including real PostgreSQL tests against freshly created `rushctrl_test_*` databases. The tests observed the expected failures before readiness/schema implementation. An invalid organization reference is rejected by PostgreSQL itself. The portable PostgreSQL 18.6 binaries came from the official EDB download linked by PostgreSQL.org; they run only on loopback and use generated ignored credentials. This does not verify Docker Compose.

Dependency setup encountered a local certificate-chain issue in Node. Using `NODE_USE_SYSTEM_CA=1` lets Node use the existing Windows trust store while retaining TLS verification. No certificate verification was disabled. See https://nodejs.org/learn/http/enterprise-network-configuration.

## 3. React dashboard and real connectivity

Next.js renders the page shell; focused React components own navigation, sample schedule, detail panels and operational status. `Dashboard` uses local state for Day/Week and which informational panel is open. A native HTML dialog supplies modal focus and Escape behavior; keyboard dismissal and focus restoration were later verified by the CI browser tests. The schedule data lives in one clearly named sample-data module and is never submitted as real staffing data.

The connection component calls a typed loader through same-origin paths. It validates the API metadata and then checks database readiness, bounds requests with an AbortSignal timeout, and returns separate API-unavailable/database-unavailable states. It refreshes every 30 seconds and has a manual refresh button. A successful but malformed response cannot turn the indicator healthy.

Verification: six status unit tests passed after observing three expected failures against a placeholder loader. TypeScript, ESLint and the Next.js production build passed. Actual HTTP calls through port 3000 returned the API metadata and healthy database status. The page response contains the expected heading and synthetic-data label. Those HTTP checks do not prove visual rendering or client interaction; Playwright desktop/mobile tests are written but have not run locally because browser access was declined.

## 4. Reproducible startup and checks

Setup generates an ignored random database password, restores dependencies and the project-local SDK, and optionally downloads portable PostgreSQL. Native startup applies migrations, launches API/web processes hidden, records their process IDs and writes local logs. Shutdown verifies the recorded processes belong to this repository before stopping their process trees. The database is stopped separately so a normal app restart does not discard data.

Docker Compose describes the same boundary: PostgreSQL becomes healthy, a one-shot migration succeeds, API readiness becomes healthy, then the frontend starts. Published ports use loopback and database storage uses a named volume. Dockerfiles use separate build/runtime stages and non-root runtime users. This configuration is for development, with administrative local database credentials and development OpenAPI, not production deployment.

The GitHub Actions workflow runs backend integration checks with disposable PostgreSQL, frontend tests/build, browser smoke tests and Compose startup. At the initial local handoff it had not run remotely. The owner subsequently connected GitHub and pushed the application; after the image-tag fix described below, all three jobs passed. The local machine still has no Docker engine or WSL installation; container startup was verified on GitHub's runner instead.

Use `./scripts/check.ps1` to repeat the locally available checks. At the initial handoff browser and Compose runtime checks were acceptance gaps. Those gaps were subsequently closed by actual execution on GitHub, rather than being marked complete from configuration inspection.

The combined check caught a Windows development issue: rebuilding Debug output while the API was running locked its DLL. The check script now builds/tests Release output, leaving the running Debug development process alone. This is an output-directory fix, not a suppressed test failure.

## 5. Independent review and regression fix

A fresh reviewer checked the complete foundation and found a stale-status race: automatic polling and manual refresh could finish out of order, letting an older healthy result overwrite a newer failure. Because the indicator claims to show real connectivity, this was fixed before handoff.

Automatic and manual checks now share a request-generation guard. Each refresh advances the generation; only the latest request may publish its result. Cleanup invalidates pending requests when the component unmounts. Two controlled asynchronous tests reproduced the incorrect older-success update and an update after cancellation before the guard was added; both then passed. The frontend suite now has eight tests.

The reviewer found no critical issue in the foundation. Staffing/authentication workflows and production deployment remain outside this milestone's implementation scope. The required Compose startup and browser keyboard/mobile smoke checks subsequently passed in CI; complete manual visual/accessibility review is not claimed.

Final available checks passed: 9 backend tests, 8 frontend tests, TypeScript, ESLint and production build. The native stop/restart script was also exercised: the recorded processes stopped, the database remained running, and the restarted API/dashboard returned HTTP 200. The full client-to-proxy-to-API-to-PostgreSQL health path returned ready.

## 6. GitHub verification and the Docker correction

The first Compose job failed because the API Dockerfile requested unpublished image tags ending in `-alpine`. A tag is an exact registry lookup name; Docker does not infer a compatible operating-system version. Both requests returned HTTP 404. Changing the references to the published `-alpine3.24` variants fixed the packaging error without changing application code.

After commit `8d48cfa` was pushed, [Checks run 41718067240](https://github.com/Rectrix-21/rushctrl/actions/runs/41718067240) passed all three jobs. The Compose build/start step and both HTTP checks passed. The frontend job also passed Playwright tests on desktop and mobile configurations. These results verify the Milestone 0 foundation on a fresh Linux test machine, in addition to the native Windows checks.

For a student, the distinction matters: a commit saves a checkpoint locally, pushing uploads it, and CI then tests that specific commit in a separate environment. A passing local test cannot prove that the container packaging works; that is why we tested both.
