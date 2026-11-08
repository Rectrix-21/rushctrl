# Milestone 2: explain and resolve staffing shortages

Status: Approved by the owner on November 8, 2026: “Approve and build Milestone 2.” Milestone 3 is not being started. Branch `milestone-2` starts at merged commit `0c16536`, in the owner's existing VS Code folder. The approval covers this local synthetic workflow; it does not authorize deployment or external services.

## Intent and boundaries

Complete the staffing story: an employee requests coverage; a manager sees eligible replacements and understandable rejection reasons; the manager explicitly approves one; the assignment, request status and audit record commit together. A second signed-in session refreshes after the committed change. Separately, an isolated synthetic Friday Rush workspace demonstrates demand growth and an absence, and can be reset without changing the normal workspace's staffing data.

Continue the approved Next.js/TypeScript, ASP.NET Core modular monolith, Identity cookies, EF Core and PostgreSQL architecture. Preserve the navy liquid-glass/blue-neon direction, beginner explanations, small local commits and configured Git identity. No authorship branding or artificial co-author trailers. The project specification supplies requirements, not permission to deploy or create external accounts. No hosting, public demo, payments, real employee data or portfolio publishing in this milestone.

No payroll, labour-law claims, ML prediction, automatic replacement, reciprocal shift swaps, recurring availability, availability edit/delete or qualification administration. Public registration/recovery and production operational hardening remain outside this local synthetic application. Milestone 3 retains measured performance, full accessibility/mobile audits and presentation deliverables.

## Approach and tradeoffs

Recommended builder choice: extend the current scheduling modules and use a separate organization, generated synthetic members and a generation token for each manager's simulation workspace. The simulator uses the real scheduling, recommendation and approval services. Its reset affects only server-identified simulation records.

Separate simulation-only tables or a browser-only simulator would duplicate the rules and fail to demonstrate the real approval workflow. A separate service would add deployment and authentication boundaries without helping this local milestone. The recommendation does not change the chosen architecture; these alternatives explain why the existing application remains the implementation path. This is a proposal, not a claim that the owner has approved its detailed behavior.

## Existing behavior that must survive

Milestone 1 already persists shifts, qualifications, positive availability, headcount targets and pending requests. Membership checks, query filters and composite foreign keys enforce organization boundaries. Assignment locks the shift and destination employee, rejects overlaps globally, checks qualifications/full availability and enforces that membership's weekly cap. Input uses the location's time zone and half-open intervals; ambiguous or skipped DST times are rejected. Preserve those rules, the 31-day input limit and Monday week semantics.

Normal coverage requests continue to leave the requester assigned until a replacement is approved. Existing pending records migrate as ordinary requests. Existing saved demo edits and credentials must not be reset during migration or startup.

## Request states and approval

Extend requests with Kind (`Standard` or `SimulationCallout`), a Guid concurrency version, resolution timestamp/actor and optional replacement member. Status becomes `Pending`, `Approved`, `Rejected` or `Cancelled`. Database checks require coherent resolution fields. A partial unique index permits only one Pending request per shift, while retaining resolved history. Composite ownership constraints apply to referenced shifts and members.

- Employees can cancel their own Pending request. A manager can reject a Pending request. Both actions retain the current assignment, close the request and release its assignment block. They can close a request after the shift starts so expired requests do not remain stuck. They cannot reopen or modify a resolved request.
- A manager approves a replacement for a future shift using request version, shift version and replacement user ID. Approval verifies Pending status, versions, organization ownership, the expected current assignment and every eligibility rule again. The requester cannot replace themselves. A stale recommendation never authorizes a write.
- A Standard request expects the shift to remain assigned to its requester. A SimulationCallout expects the simulation's explicitly opened shift to remain unassigned; only the scenario service may create that kind. Ordinary employee inputs cannot select a kind or impersonate a requester.
- Approval updates assignee and both versions, marks the request Approved and writes the before/after audit entry in one transaction. Cancellation/rejection also bump the shift version. Request creation bumps the shift version. Conflicts and repeated resolutions return 409 without duplicate changes or audit entries; inaccessible records return 404 and employee manager-only attempts return 403.
- Direct assignment remains blocked only by a Pending request. Resolved history must not disable the shift card or prevent later requests. Employee request history remains visible to its original requester even when the shift has been reassigned; other employees' requests stay private.

No replacement is selected automatically. The manager reviews the explanation and chooses Approve. Rejection/cancellation do not silently unassign an employee.

## One transaction boundary and shared rules

Introduce one scoped mutation boundary for organization scheduling writes, after authentication/membership/CSRF checks. It starts a transaction and locks that organization row before endpoint work. Existing nested endpoint/service transactions move into this boundary. It commits only a successful mutation; exceptions/error results roll back. All normal scheduling writes and simulation apply/reset use it.

The lock order is organization, shift, then affected destination employee. Preserve the global employee lock and PostgreSQL overlap constraint for cross-organization assignments. This serializes writes within one organization, which is deliberately acceptable for the local demo and avoids reset/approval races; it is a throughput tradeoff to measure later, not a production scalability claim. Recommendation reads are advisory and do not acquire long-lived write locks.

Extract shared eligibility evaluation from AssignmentService. Both direct assignment and approval call it after locking. Candidate discovery uses the same evaluation rules on a read snapshot; eligibility is rechecked on approval. Preserve DST, overnight, adjacency, union-of-availability and every affected week's cap behavior.

## Recommendations and transparent ordering

Manager-only candidate discovery works for a pending request or an open shift. Return the shift version, any pending request version, a policy identifier, generated-at time, eligible members and rejected members. Never return records from another organization; a cross-organization overlap produces only a generic overlap reason.

Reject the requester/current assignee, missing qualification, incomplete availability, any overlapping assignment or a weekly cap exceeded by the proposed assignment. Return stable reason codes with human-readable explanations, not a fabricated confidence percentage. An empty eligible list is a valid result with reasons.

Proposed fairness policy `least-load-v1`: compare three values in order, lowest first: (1) currently assigned minutes within the location-Monday weeks touched by the target shift, clipped to those weeks and summed; (2) the number of approved replacement assignments to that member in the preceding 28 days; (3) member UUID as a deterministic tie-breaker. Capture server UTC now once per recommendation; count successful approval audit timestamps in the inclusive interval `[now - 28 days, now]`, not the replacement shift's date. Include an approval made at that exact server-now value, and exclude future timestamps. Only this organization's work contributes to load/history; global overlaps are still disqualifying. Exclude the target shift from existing load. Show the load, recent cover count and ordering explanation next to the rank. This is an explicit ordering policy, not an optimal schedule or a fairness guarantee.

Use audit records for successful replacement history from this milestone onward; do not invent historical approvals. In simulations, use only the current generation's history so Reset restores deterministic recommendations. Do not persist a separate recommendation run merely to serve a GET; approval performs fresh validation.

## Audit history

Add tenant-owned append-only audit entries for successful shift creation/assignment, availability declaration, staffing-target creation, request creation/resolution and simulation creation/apply/reset. Record the real signed-in actor, UTC time, action code, relevant resource identifiers, optional simulation generation and small before/after snapshots. Do not log passwords, cookies, CSRF tokens or whole HTTP bodies. Events start with this milestone; no invented backfill.

Write audit rows in the same database transaction as their mutation. Use an organization-local increasing audit number allocated under the organization write lock; `(OrganizationId, Number)` identifies an event. Manager-only reads use descending keyset pagination (`beforeNumber`, default 50, maximum 100). Employees do not receive the organization audit feed. Snapshots contain enough names/times to explain a deleted simulation record; resource identifiers are historical values, not restrictive shift/location foreign keys. Actor users persist. There is no audit edit/delete endpoint, and simulation reset retains the audit history.

## Isolated Friday Rush workspace

Creation is explicitly enabled only in Development with `Simulation:Enabled=true`. Native startup, Compose and CI opt in without passing demo passwords to the web process. Production and missing-enable tests must refuse simulation creation/reset. Creation is manager-only from a normal organization and allowed only for the reserved synthetic development organization. It never clones arbitrary tenant data.

Maintain at most one simulation workspace per source organization and owner user. Create a separate organization with a server-owned `SimulationWorkspace` record (source organization, owner, generation Guid, anchor Monday, scenario-applied flag and baseline-pristine flag). The real manager receives membership; generated synthetic employee users have no password/login and belong only to this sandbox. Do not reuse normal employees as simulated employees: that would interfere with global overlap checks. Other source-organization managers do not automatically receive access. A second session signed in as the same owner may open the same lab.

The fixture uses a future Monday and two synthetic locations in America/Toronto. Its Friday 14:00–18:00 baseline has two assigned Barista shifts, one open standby shift and a target of two. It includes eight synthetic employee profiles: the two floor staff, two qualified/available substitutes with different weekly loads, an unqualified member, a partially available member, a member already assigned at the other location and a member whose 38 existing weekly hours would exceed a 40-hour cap with this shift. Names and exact IDs are builder details. This deliberately demonstrates both eligibility and rejection reasons.

Apply Friday Rush once per generation, only while its Friday start is future and the baseline is pristine: raise the target from two to three, open one assigned synthetic shift to model an absence, and create a Pending SimulationCallout retaining the absent member's identity. Any successful ordinary staffing write in the lab marks the baseline non-pristine within the same transaction; read-only operations do not. Apply checks those flags and expected fixture records under the organization lock, and returns 409 asking for Reset if the lab was edited, the scenario was already applied, its records no longer match or Friday has started. It does not adapt the fixed scenario to edited data. The UI explains why Apply is unavailable. Reset restores the pristine flag. Normal writes after Apply remain available for resolving the scenario.

The audit actor is the manager invoking the scenario. Ordinary employee coverage requests still retain assignments. The panel explicitly explains that this simulated call-out opened the shift. Initially there are two people short; an approved replacement leaves one demand gap; assigning an eligible member to the standby shift can fill the remaining gap. Never claim that replacing one person also satisfies the added demand.

Reset requires a clear in-app confirmation identifying the simulation. Under the same organization transaction/lock, delete only its requests, shifts, availability and staffing targets, then restore the baseline. Reuse its generated members, skills and locations, preserve all audit entries, rotate the generation token and clear the applied flag. Keep a future anchor week; if the old week is no longer future, select a new future Monday and return it explicitly. No endpoint resets the normal organization, `.local/pgdata`, credentials or another owner's lab.

Every write to a simulation requires its current `X-Workspace-Generation` header, captured from the screen/form that produced it. The mutation boundary checks it after locking. Missing/stale generation returns 409 asking the client to reload; malformed tokens return 400. Reset and approval cannot race into a partly restored dataset. A form opened before Reset cannot quietly submit into the new generation. Do not silently rebind an old form's generation token when a background refresh completes.

## SignalR: notifications after commit

Add an authenticated hub at a same-origin `/hubs/workspace` route and the official pinned JavaScript client. The hub may join only organizations for which its authenticated user has a current membership, checked on the server. Group names derive from that validated organization; client-supplied roles/group strings grant nothing. Joining one workspace leaves the previous workspace group. Hub methods cannot assign, approve or reset anything.

After a successful database commit, send an invalidation containing only organization ID and simulation generation, not roster/request/audit details. Clients reload authoritative APIs with their usual authorization rules. A send failure is logged and does not turn an already committed save into a misleading save failure. No durable outbox or guaranteed notification delivery is claimed; manual Refresh and refresh-after-reconnect recover missed notifications.

Use SignalR's Long Polling transport initially, with a bounded server poll timeout below the Next.js proxy timeout. This uses ordinary same-origin HTTP rewrites without assuming a WebSocket upgrade works through the current Next.js standalone proxy. It remains a real SignalR connection, not a timer pretending to receive events. WebSocket hosting can be considered in Milestone 3. Preserve cookie authentication, close connections on authentication expiration, validate current identity before starting/rejoining and do not enable permissive cross-origin access.

The client owns one connection for the displayed identity/workspace, stops it on sign-out/navigation and rejoins after reconnect. Subscribe before loading the snapshot and reload after joining/reconnecting to close the subscribe/fetch gap. Compare the fresh CSRF endpoint's user ID with the displayed identity without silently rebinding an old screen. An account change clears the stale workspace and requires explicit sign-in, as with M1 writes.

Coalesce invalidations and ignore responses from obsolete workspace/week/connection loads. With a normal form open, queue incoming refreshes and show that the schedule changed; refresh after the form closes, while server versions protect its save. A simulation generation change invalidates old forms, explains the reset and reloads workspace metadata. Show Connecting/Live/Reconnecting/Disconnected honestly, with manual Refresh retained. Do not claim live delivery when the connection is down.

Primary references: [SignalR JavaScript client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0), [configuration and transports](https://learn.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-10.0), [security considerations](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-10.0). Exact supported client version and bundled Next.js proxy behavior will be verified before installation/implementation.

## Screens and contracts

Preserve the current manager/employee workspace and `/preview`. Add manager candidate/review panels to pending requests and open shifts, showing rank inputs, rejection reasons, empty/loading/error states and an explicit approval/assignment action. Show resolved request status and replacement summaries; employees can cancel only their own pending requests. Remove outdated “resolution arrives in Milestone 2” blocks once those actions exist.

Add a manager audit panel, a clearly labelled isolated Friday Rush lab with open/apply/reset/return actions, and live connection status. Entering the lab refreshes same-user membership state explicitly. Its manager controls run real services against sandbox data. Do not add a role-impersonation selector. Use labelled native dialogs, focus return/confirmation, visible keyboard focus and existing responsive glass surfaces. New UI code must follow the bundled Next.js guides.

Proposed API additions within the existing organization routes: candidate GET on a shift; approve/reject/cancel POST on a request; manager audit GET; create/open simulation POST from the normal synthetic organization; apply/reset POST and metadata GET on an owned simulation. Extend workspace and request DTOs with versions, kind/status/resolution and simulation metadata. Preserve runtime Zod validation, fresh CSRF on writes and stale-account guards. Exact file names and DTO spellings are builder details; the permission, version and generation contracts are not optional.

## Teaching increments and acceptance

1. Request lifecycle and transaction boundary: explain state transitions, transactions, row locks and optimistic versions. Verify existing M1 constraints still work.
2. Shared eligibility, candidate ordering, atomic approval and audit: explain hard rules versus ranking, revalidation and why a recommendation is not permission.
3. Isolated simulation/apply/reset: explain tenants, generated identities, generation tokens and safe reset boundaries.
4. SignalR and working screens: explain commit versus notification, reconnect, authoritative refresh and managing React effects/forms.
5. Full acceptance and handoff: update migration/OpenAPI/ADRs, native/Compose/CI setup, real HTTP workflow, browser tests and a beginner self-test guide. Use incremental local commits and let the owner sync through VS Code as before.

Use failing-then-passing tests for new behavior and an independent final whole-branch review. Database/API tests cover tenant/role/CSRF, statuses and duplicate actions, stale versions, all eligibility rules, exact ordering/ties/28-day boundaries, overnight/DST/multiweek caps, competing approvals, reset versus approval, rollback/audit atomicity, isolated reset and stale generations. Test hub authentication/group isolation, post-commit-only invalidations, disconnect/reconnect and stale identity. Real browser acceptance uses two sessions, proving that approval refreshes the second session without pressing Refresh; cover ordinary and simulation workflows plus keyboard and mobile behavior. Preserve meaningful preview tests and repair outdated M1 pending-only assertions rather than weakening them.

Native browser-origin automation was previously denied and no local Docker engine is installed; do not bypass those limits or install a system engine. HTTP/hub integration checks may run natively; actual browser/container acceptance runs in GitHub CI and remains pending until observed passing. Record exact measured results only. No deployment or Milestone 3 work is included.

## Builder choices for owner review

The proposed choices are organization-serialized writes, least-load-first ordering with recent covers as the secondary key, owner-only synthetic lab membership, a separate simulation call-out kind, generation-guarded reset, preserved audit history and SignalR Long Polling through the existing proxy. They aim for an explainable local workflow, not a production scaling claim. The owner may change these before implementation. Exact module boundaries, client patch version, generated fixture names and small responsive adjustments are left to implementation.

## Builder check

A fresh read-only builder check identified two unanswered details: applying the fixed scenario to an edited/expired lab, and the clock/boundaries for recent covers. The builder defaults above require pristine future baseline data and count approval audit times against captured UTC now with inclusive 28-day boundaries. These are proposed implementation choices; they are not recorded as owner decisions. Acceptance must include edited-baseline/expired-scenario refusals and exact history-window boundaries. The reviewer did not implement or approve the product. The owner approved this design on November 8 before implementation.
