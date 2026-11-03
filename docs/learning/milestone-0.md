# Milestone 0 — implementation notes

## 1. API foundation

The repository is a standalone application on the local `milestone-0` branch. The downloaded .NET SDK is inside ignored `.tools/dotnet`, verified against Microsoft's SHA512 release metadata. Nothing has been pushed or deployed.

`Program.cs` assembles the HTTP pipeline: structured request logging and a correlation header, exception handling, cookie authentication, authorization, then endpoint dispatch. The fallback authorization policy protects new endpoints unless they explicitly opt into anonymous access. Cookie challenges return 401/403 for API clients rather than HTML redirects. No login or staffing endpoint is exposed yet.

`Modules/System/SystemEndpoints.cs` contains public operational contracts. `/health/live` answers without a database. `/api/v1/system` exposes only the service name, version, and milestone. Development OpenAPI describes these endpoints; production requests to that document return 404.

Verification: five xUnit tests passed. Before implementation, four failed on missing routes/unprotected access. The production OpenAPI test then caught a routing detail: ASP.NET's default fallback excludes file-like paths such as `.json`. An explicit catch-all now returns 404 without exposing a document.

Next: persistence and migration-aware readiness. Liveness alone must never be presented as proof that the application can use its database.
