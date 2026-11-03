# Milestone 0 HTTP contract

The API listens locally at `http://127.0.0.1:5080`. The web app forwards the API and health paths from its own origin. No route accepts real staff data.

| Method/path | Success | Failure / scope |
| --- | --- | --- |
| GET `/health/live` | 200 `{"status":"healthy"}` | Public, process-only liveness |
| GET `/health/ready` | 200 `{"status":"healthy"}` | 503 `{"status":"unhealthy"}` if PostgreSQL is unreachable, migrations are pending or expected tables cannot be queried |
| GET `/api/v1/system` | 200 `{"name":"RUSHCTRL","version":"0.1.0","milestone":0}` | Public operational metadata only |
| GET `/openapi/v1.json` | 200 generated OpenAPI document | Development only; 404 in Production; direct API origin |

Readiness is not cached and its public payload does not expose connection strings, exception messages or tenant records. The generated [OpenAPI snapshot](openapi.json) documents implemented operational endpoints. Regenerate the snapshot from the running Development API after contract changes.

The request pipeline emits an `X-Request-Id` correlation header and structured request logs. Unhandled errors use Problem Details. Unknown routes return a 404 Problem Details response. Future protected routes inherit an authenticated-user policy and return 401 for anonymous callers; forbidden API calls return 403. The protected test route exists only in test-host configuration and is not shipped.

No staffing, identity, tenant listing, recommendation or simulation contract is claimed as implemented in this milestone.
