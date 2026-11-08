# Milestone 1 sessions and staffing

Use the same-origin web proxy. JSON request/response properties use camelCase. UUIDs identify resources; they never grant permission. Responses contain DTOs, not Identity users/password fields. Errors use `{ title, status, ... }` Problem Details; callers should display `title` and treat it as text.

## Session protocol

| Method / path | Request | Response |
| --- | --- | --- |
| GET `/api/v1/auth/csrf` | None; anonymous allowed | 200 `{ token, userId }`, own authenticated UUID or null, HttpOnly CSRF cookie, `Cache-Control: no-store` |
| POST `/api/v1/auth/login` | `{ email, password }` and `X-CSRF-TOKEN` | 204 and HttpOnly session cookie; generic 401 on failed credentials/lockout; 400 on invalid input/token |
| GET `/api/v1/auth/session` | Session cookie | 200 `{ id, displayName, memberships: [{ organizationId, organizationName, permission }] }`; 401 when signed out |
| POST `/api/v1/auth/logout` | Session cookie and fresh CSRF token | 204, session cookie cleared |

Fetch a CSRF token again after login because tokens are identity-bound. The web client obtains a fresh token before every write. No public registration, password reset or trusted user/role/tenant headers exist. Failed login attempts use Identity lockout (five attempts / five minutes). Cookies are secure outside Development; local HTTP uses Development settings. Public hosting needs a separate production security/deployment design.

## Organization routes

The UI binds its client to the displayed session's user ID. Before tenant writes and logout, it compares the fresh CSRF response's identity with that displayed identity. A change in another tab blocks the write and clears the stale workspace for re-sign-in. Explicit login can rebind the client. Identity-bound antiforgery validation also rejects a cookie switch between token issuance and mutation. This identity comparison is an intent precondition; server membership remains the authorization authority.

Prefix: `/api/v1/organizations/{organizationId}`. Every route checks server-side membership. Unknown/inaccessible organizations and resources are 404; anonymous callers are 401; employee members calling manager-only routes are 403. Every POST/PATCH requires CSRF. Reads are not cached.

`locationId` and `week` query parameters are required on the list endpoints below. `week` is a Monday formatted `yyyy-MM-dd`, with start year 2000–2100. The selected location's stored IANA zone defines the week's UTC boundaries. A shift crossing into the week is included even if it starts earlier.

| Method / suffix | Permission | Contract |
| --- | --- | --- |
| GET `/workspace` | Member | Organization name, caller permission, locations `{ id, name, timeZoneId }`, skills `{ id, name }`, people `{ id, displayName, maxWeeklyHours }`, qualifications `{ userId, skillId }`, `suggestedWeek`. Employees receive only their own person/qualifications; their suggestion uses only their own shifts. |
| GET `/shifts?locationId=...&week=...` | Member | Array of Shift DTO; employee sees only their own assigned shifts. |
| POST `/shifts` | Manager | `{ locationId, skillId, startsAt, endsAt }`; 201 Shift DTO, initially unassigned. |
| PATCH `/shifts/{id}/assignment` | Manager | `{ userId: UUID or null, version: UUID }`; 200 refreshed Shift DTO. `null` unassigns, subject to version/pending-request checks. |
| GET `/availability?locationId=...&week=...` | Member | Array `{ id, userId, locationId, startsAt, endsAt }`; employee sees only their own. |
| POST `/availability` | Member, own identity | `{ locationId, startsAt, endsAt }`; 201 saved window. Client-supplied user IDs cannot select another employee. |
| GET `/staffing-requirements?locationId=...&week=...` | Manager | Array `{ id, locationId, skillId, startsAt, endsAt, requiredCount }`. |
| POST `/staffing-requirements` | Manager | `{ locationId, skillId, startsAt, endsAt, requiredCount }`; count 1–100; 201 `{ id }`. Same-location/same-skill overlapping targets return 409. |
| GET `/coverage?locationId=...&week=...` | Manager | Shortage intervals `{ requirementId, locationId, skillId, startsAt, endsAt, requiredCount, assignedCount, missingCount }`, clipped to the selected week. |
| GET `/coverage-requests?locationId=...&week=...` | Member | Array `{ id, shiftId, requesterUserId, requesterName, status, createdAt, startsAt, endsAt, skillId }`; employee sees only their own requests. |
| POST `/coverage-requests` | Member, own assigned shift | `{ shiftId }`; 201 `{ id, shiftId, requesterUserId, status: "Pending", createdAt }`. Unassigned/another person's shift is 404; already started/duplicate request is 409. |

Shift DTO: `{ id, locationId, skillId, startsAt, endsAt, assigneeUserId, assigneeName, version }`. Assignment responses include the new version. Creation has nullable assignee fields. The current client reloads collections after writes rather than navigating to the returned Location header.

## Time and assignment rules

Input `startsAt`/`endsAt` are location-local minute strings such as `2027-01-11T08:00`, without `Z` or a browser offset. The API derives the zone from the owned Location, rejects skipped/repeated daylight-saving times, and returns UTC ISO instants. All intervals are half-open `[start, end)` and positive. An individual input interval is at most 31 days.

Assignment requires membership, the required skill, full coverage by the union of positive dated availability windows, no overlap with any assigned shift for that person, and no configured weekly-hour violation. Overlap is checked across organizations without disclosing another tenant's records. Hour caps belong to the membership; weeks begin Monday in the target shift location's zone, counting actual overlapping duration. Overnight/multiple-week shifts are split across those weeks. These are configured scheduling limits, not labour-law compliance advice.

The assignment transaction locks the shift and employee, checks its version and saves a new version. PostgreSQL exclusion constraints reject double booking and overlapping same-skill staffing requirements even if application checks race. Stale/conflicting writes return 409. The UI must refresh; it must not silently retry a mutation whose response was lost.

Availability is immediate and additive; no general edit/delete/approval route is claimed. A Standard pending coverage request keeps the employee assigned and blocks direct reassignment/unassignment. [Milestone 2](milestone-2.md) adds cancellation, rejection, approval and retained resolution history; only Pending requests block new requests/assignments.

Coverage counts assigned shifts matching the target's organization, location and skill. It splits at shift boundaries, reports shortages only, and merges adjacent periods with the same shortage. Open shifts are separate unassigned work; they do not count as staffed coverage.
