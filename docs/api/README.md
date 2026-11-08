# RUSHCTRL HTTP contract

The API listens locally at `http://127.0.0.1:5080`. Next.js forwards `/api/*`, `/hubs/*`, `/health/live` and `/health/ready` from its own origin. All identities and staffing records used by this local demo are synthetic.

| Method/path | Success | Failure / scope |
| --- | --- | --- |
| GET `/health/live` | 200 `{"status":"healthy"}` | Public, process-only liveness |
| GET `/health/ready` | 200 `{"status":"healthy"}` | Public; 503 if PostgreSQL is unreachable, migrations are pending or expected tables cannot be queried |
| GET `/api/v1/system` | 200 `{"name":"RUSHCTRL","version":"0.1.0","milestone":2}` | Public operational metadata only |
| GET `/openapi/v1.json` | Generated OpenAPI document | Development only; direct API origin; 404 in Production |

Readiness is not cached and does not expose connection strings, exception details or tenant records. Requests carry an `X-Request-Id` correlation header. Unknown routes and rule failures use Problem Details. Protected routes return 401 without redirecting for anonymous callers; manager-only actions return 403 for an employee member; inaccessible organizations/resources return 404.

Milestone 1 implements [sessions, staffing and coverage APIs](milestone-1.md). That document specifies payloads, interval/time-zone semantics and permission boundaries. The [OpenAPI snapshot](openapi.json) is generated from the running Development API; refresh it after route changes. Anonymous-result response bodies are described in the Markdown contract rather than all being inferred into OpenAPI schemas.

The [Milestone 2 additions](milestone-2.md) describe ranking, versioned resolution, generation-guarded simulations, audit and SignalR invalidations.
