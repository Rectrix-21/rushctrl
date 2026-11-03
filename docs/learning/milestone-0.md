# Milestone 0 — implementation notes

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
