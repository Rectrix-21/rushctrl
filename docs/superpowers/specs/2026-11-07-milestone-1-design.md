# Milestone 1: a saved staffing workflow

Status: Implementation elaboration of the approved narrow Milestone 1 design, authorized by the owner's request to begin Milestone 1. Builder choices below are identified separately from owner decisions. No Milestone 1 product code had been written when this document was reviewed.

## Intent and success

Turn the existing blue glass preview into a working local application. A manager signs in, creates a shift and assigns a qualified, available employee. That employee signs in separately, sees the saved shift, declares availability and requests coverage. The manager sees the request and staffing shortages. Reloading the browser preserves saved changes.

This implements Milestone 1 from `RUSHCTRL_CODEX_SPEC.md` and the approved `2026-11-03-milestone-0-design.md`. The specification is a project reference, not additional authorization for deployment or external services. The owner's current request authorizes work on Milestone 1. This document records routine implementation choices within that approved scope.

## Approach and constraints

Keep the approved Next.js/TypeScript frontend, ASP.NET Core modular monolith, PostgreSQL and EF Core. Use ASP.NET Core Identity with a same-origin session cookie, as already selected in ADR 0002. A browser-only mock would not meet persistence or server permission requirements; an external identity service would introduce an account and dependency without helping the local milestone. No architecture replacement is proposed.

Preserve the approved navy glass surfaces, blue accents, keyboard focus and mobile layout. Preserve the Milestone 0 sample at `/preview`; the main page becomes the signed-in application. No AI branding, boilerplate authorship comments or artificial co-author trailers. Use the existing configured Git identity. Make small local commits, explain each working increment in beginner language, and record reproducible checks in `docs/learning/milestone-1.md`.

No deployment, paid service, external account, real employee data or production-readiness claim. No ranking, automatic replacement, simulation, audit product, SignalR, payroll, public registration, email delivery or password recovery. Coverage approval/replacement remains Milestone 2, consistent with the narrow roadmap. Milestone 1 coverage requests are visible pending requests, not promises of automatic reassignment.

## People, permissions and sessions

An Identity user has an organization membership with either Manager or Employee permission. These are organization permissions, not a role the browser may choose. A manager can read the organization's roster and schedule, create shifts and staffing requirements, and assign or unassign employees. An employee can read their own shifts, availability and coverage requests, declare their own availability, and request coverage for their own assigned shift. The API enforces these boundaries even when called without the UI.

The login screen accepts email and password. Identity hashes passwords and applies lockout to failed attempts. Use a generic failure message. No public account creation. Provide sign-out and a current-session endpoint; expired sessions return the user to sign-in with a useful message.

Cookies are HttpOnly, SameSite and secure outside local Development. Validate antiforgery tokens on state-changing requests, including login and logout. Refresh the token after signing in because its identity binding has changed. Login and authorization errors are JSON/API errors, not HTML redirects. Do not trust X-User, X-Tenant or role headers.

Every tenant route resolves the authenticated user's membership before querying data. Tenant-owned queries use a scoped organization filter, and relationships use composite ownership constraints. An inaccessible organization or resource returns 404; a member attempting a manager-only operation returns 403; an anonymous request returns 401. Session responses include only the caller's memberships. Tests create a second organization and attempt cross-organization reads and writes.

## Persisted model

Retain Organization and Location. Add only the following workflow entities:

- Identity user: UUID, email, display name and Identity's password/session fields.
- Membership: OrganizationId + UserId key, permission and configured maximum weekly hours (40 in synthetic seed data; a scheduling setting, not a labour-law claim).
- Skill: organization-owned named qualification; EmployeeSkill links a membership to a skill.
- AvailabilityWindow: organization, employee, location and UTC start/end. These are positive, dated availability declarations, not recurring preferences or time-off requests.
- Shift: organization, location, required skill, UTC start/end, optional assigned employee and a concurrency token.
- StaffingRequirement: organization, location, skill, UTC start/end and required headcount.
- CoverageRequest: organization, shift, requester, creation timestamp and Pending status. Permit one pending request per shift.

Locations, skills, memberships and referenced shifts must belong to the same organization. Database checks enforce positive time intervals and positive staffing headcounts. Use explicit indexes for organization, employee and time-range access. Do not add empty generic repositories or an unrelated admin interface.

## Scheduling rules

All intervals are half-open `[start, end)`: an 08:00–12:00 shift may be followed by a 12:00–16:00 shift. End must be after start. Inputs use the selected location's local date/time; the API resolves its stored IANA time zone and stores UTC timestamps. Reject nonexistent or ambiguous daylight-saving times with an actionable message rather than silently guessing an offset. Display the location time zone alongside the schedule.

Before assigning, the API verifies that the employee belongs to the organization, has the required skill, and has declared availability covering the entire shift. Adjacent or overlapping declarations may jointly cover a shift. The employee must have no overlapping assigned shift, including in another organization; any rejection must avoid revealing the other organization's details. Weekly hours must remain within that membership's configured cap. A week begins Monday in the shift location's time zone; count the part of each assigned shift falling within that week. Check every week the proposed shift touches.

Assignment is transactional and checks a concurrency token. Concurrent requests must not double-book a person or exceed the configured cap; use a database overlap constraint and serialize that employee's assignment validation. Return a useful 409 conflict for stale or conflicting changes. Validation errors use 400 Problem Details with field information where applicable. Skill, availability and hour-cap rejection messages explain the rule without exposing another tenant.

Availability declarations take effect immediately for Milestone 1. Employees may add dated windows; editing/deleting windows is deferred so a change cannot quietly invalidate an assigned shift. Qualification and hours administration are seeded configuration for this milestone, not new CRUD screens.

An employee may request coverage for a future shift currently assigned to them. The request does not unassign them. The manager sees the request as pending. A manager cannot reassign or unassign a shift with a pending request in Milestone 1: return 409 explaining that request resolution belongs to the next milestone. Before submission, explicitly explain that requests cannot yet be cancelled or resolved and will keep that shift assigned. This keeps pending request ownership truthful until the atomic resolution workflow exists.

## Coverage gaps

The manager creates a staffing requirement for a location, skill and interval. Compare its required headcount with assigned shifts for that same location and skill. Split the interval at shift boundaries and report each period's shortage, merging adjacent periods with the same shortage. Unassigned shifts are listed separately as open shifts; merely creating an unassigned shift does not count as staffed coverage.

Overlapping requirements for the same location and skill are rejected in this first version, avoiding an implicit choice between adding targets and replacing them. Requirements with different skills may overlap. The seed includes a clear shortage, and assigning an eligible employee visibly reduces it after a refresh.

## Screens and API boundary

The main page first loads the session. Signed-out visitors see a blue glass sign-in form. Signed-in users see their organization, location, role, location time zone, chosen week, sign-out action and the existing real connection status. Changing a view fetches current API state; no live push is claimed.

Manager workspace: saved weekly/day schedule; create-shift form; assignment form with server-validated choices; staffing requirement form; gaps and pending requests. Employee workspace: own saved schedule; dated availability form/list; coverage request action and status. Use labelled native controls, loading/empty/error states, visible confirmation after saves and disabled submit buttons while a save is pending. Keep forms small and dialogs keyboard-accessible. Use schema validation in the UI and authoritative validation on the API.

Auth lives under `/api/v1/auth` (CSRF token, login, logout, session). Workflow routes live under `/api/v1/organizations/{organizationId}` for workspace metadata, shifts, assignments, availability, staffing requirements, coverage gaps and coverage requests. The browser continues to use the existing same-origin Next.js proxy. Responses expose typed DTOs, not EF entities or password fields. Document exact contracts during implementation.

## Synthetic local seed and startup

Add an explicit, idempotent Development-only seed command, run after migrations by local startup. Generate manager and employee passwords into ignored local configuration; never commit credentials or print them in checked-in documentation. Provide an ignored local account file that the owner can open. Fail clearly if seed credentials are missing or a seed command is attempted outside Development. Normal production startup does not create demo identities.

Use Northline Coffee / Harbour / America/Toronto, synthetic staff, skills, future dated availability, example shifts and a staffing requirement. Rerunning startup creates missing seed records without wiping the user's changes. Mark the workspace as synthetic and explain that edits are saved locally. Preserve existing local database credentials when upgrading configuration. Compose and CI receive seed credentials through environment variables, not source files.

## Teaching and implementation order

1. **Sign-in and permissions:** Identity schema/migration, sessions, CSRF, membership scope, local seed and isolation tests. Explain authentication versus authorization, password hashing and cookies.
2. **Saved scheduling:** entities/migration, create/list/assign workflow, qualifications, availability, hours, time zones and concurrent conflict tests. Explain foreign keys, UTC storage and transactions.
3. **Employee requests and staffing gaps:** availability declarations, pending coverage requests, staffing requirements and interval-based gaps. Explain why the API owns the rules and why a request is separate from a shift.
4. **Working screens:** connect the existing visual system to real APIs and add forms and permission-specific views. Explain React state, fetch requests, validation and persistence across reloads.
5. **Acceptance:** full API/database tests, frontend checks, one real manager-to-employee Playwright workflow, Compose/CI updates and a student self-test guide. Explain what each kind of test proves and record any checks that cannot run locally.

After each increment report what changed, why, which files matter, observed test results, and a small exercise the owner can try. Use the existing inline implementation workflow and one independent final code review. Do not ask the owner to choose technical details the repository already decides.

## Acceptance evidence required

- Identity login/logout, cookie challenge and CSRF tests; manager/employee permissions; a separate organization's resource identifiers cannot be used to read or mutate data.
- Database integration tests for persistence, relational ownership, invalid intervals, qualifications, full availability coverage, adjacent versus overlapping shifts, hour caps, stale writes and simultaneous assignments.
- Time-zone tests for ordinary local input and daylight-saving gaps/ambiguities; coverage tests for partial intervals, unassigned shifts and conflicting requirements.
- Request tests for ownership, past/unassigned shifts, duplicates and the pending-request reassignment boundary.
- Existing Milestone 0 tests remain meaningful; frontend typecheck, lint, unit tests and production build pass.
- A browser flow signs in as manager, creates and assigns a future shift, reloads to prove persistence, signs in as employee, sees that shift, declares availability and requests coverage, then manager sees the pending request. Include narrow/mobile layout and keyboard checks.
- CI and Compose start the migrated/seeded stack with generated secrets. Local Docker/browser limitations are reported honestly; a green build alone is not end-to-end acceptance.

## Builder decisions and review

The owner has selected the stack, local-development scope, visual direction and teaching approach. Builder choices within that scope are immediate dated availability, pending-only coverage requests, explicit staffing targets, Monday-based hours and retaining `/preview`. These are implementation decisions, not separate owner requirements.

Exact DTO/type names, patch versions consistent with existing dependencies, migration names, component boundaries and minor responsive adjustments are left to implementation. No new infrastructure or owner product choice should be hidden in those details.

An independent builder check found no important unanswered product or architecture questions. It highlighted the pending-request limitation, which must be visible before submission, and confirmed that immediate availability and deferred request resolution follow the approved narrow roadmap. This review checks whether the design can be planned; it is not implementation or acceptance evidence.
