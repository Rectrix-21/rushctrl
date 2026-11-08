# Milestone 2 learning journal

Milestone 2 is implemented locally on `milestone-2` from the merged Milestone 1 code. The owner approved the November 8 design. The increments below explain the build; final browser/container acceptance still needs this branch's GitHub run. Earlier check counts describe that increment; the final verification section records the latest result.

## 1. Resolving requests safely

Coverage requests now have four states: Pending, Approved, Rejected and Cancelled. The employee who made a request may cancel it; a manager may reject it or approve a different qualified person. Resolved requests stay in history. Only a Pending request blocks a direct assignment or another request on the same shift.

A **transaction** groups related database changes so they succeed together. The common organization mutation boundary locks that organization's row, runs the operation, saves its audit entries, then commits. Existing nested transactions moved into that boundary. The global employee lock and overlap constraint still protect against assignments made in different organizations. Serializing an organization's writes is a deliberate local-demo tradeoff; it is not a performance claim.

Approval requires the request version and shift version from the manager's screen. A **version token** answers “am I still changing the record I originally reviewed?” The server rechecks qualification, complete availability, overlap and every touched week's hour cap before assigning. Those checks now live in `EligibilityService`, shared with direct assignment. The future candidate-ranking screen will use those same rules, but approval always checks them again.

The audit log records the real actor, UTC time, action and small before/after scheduling snapshots. It contains no passwords or session tokens. Audit rows commit with the change and are visible only to managers, with numbered pages. The migration preserves old Pending requests as Standard requests and gives them individual version tokens. Simulation metadata is stored in this migration for the next increments; no simulator is claimed yet.

**Observed checks:** The three new request-lifecycle HTTP tests failed on missing routes first. After implementation, all 42 API tests passed against isolated PostgreSQL databases, including the original Milestone 1 scheduling/concurrency tests. New checks cover cancellation/history, role boundaries, stale versions, eligibility revalidation, competing approvals, duplicate actions, audit privacy/pagination and no new audit entry after a rejected approval. Baseline before implementation was 39 API and 20 frontend tests plus successful types/lint/build.

**Files to explore:** `WorkspaceMutationFilter.cs` owns the transaction; `EligibilityService.cs` checks the rules; `RequestResolutionEndpoints.cs` resolves requests; `AuditCapture.cs` captures scheduling changes. `RushCtrlDbContext.cs` collects those snapshots while saving. The local running app remains the previous accepted version until the new API and screens are ready to start together.

**Exercise:** Draw Pending with three outgoing arrows. Write the role allowed on each arrow. Then explain why pressing Approve twice must produce one successful approval and one conflict, rather than two audit records. Interface controls for these routes arrive in the screen increment.

## 2. Explaining candidate recommendations

`CandidateService` evaluates each organization member using the shared eligibility rules. Ineligible people have readable reasons. Eligible people are ordered by assigned minutes, then recent approved covers, then a stable ID for ties. The API returns the policy name and versions so the screen can explain what it reviewed. It does not approve anything.

The 28-day history window uses the time an approval was recorded, including both exact boundaries. It ignores future timestamps and work/approval history outside this organization. An overlap elsewhere only produces a generic conflict reason, not another organization's details. [ADR 0003](../adr/0003-explained-replacements.md) explains this policy and its limitations.

**Observed checks:** Candidate tests failed on missing routes, then the full API suite passed 44/44. Tests pin workload ordering, cover counts at both time boundaries, older/future exclusion, ID ties, permissions, self/qualification rejection and an eligible recommendation becoming invalid after a new overlap. Approval correctly refused that stale recommendation.

**Exercise:** Person A has four scheduled hours and no recent covers; B has zero hours and two covers; C has zero hours and no covers. Explain why the proposed order is C, B, A. Then explain why someone with zero hours but incomplete availability is not on that list at all.

## 3. A separate Friday Rush lab

The simulator creates an owner-only organization, two Toronto locations and eight fictional profiles. Those profiles have no password. They never share employee IDs with the ordinary workspace, so their overlapping schedules cannot interfere with it.

The baseline has two assigned people and a target of two. Apply raises the target to three and removes Cameron from the floor, creating a real simulation callout request. The shortage becomes two. Approving a replacement reduces it to one; assigning the standby shift fills the final gap. Reset restores the baseline and keeps the audit history.

A generation token identifies one run of the experiment. Every lab save must carry the token from the form it opened with. Reset changes that token. An old form therefore receives a conflict instead of accidentally saving into the new experiment. The organization transaction lock also makes a reset racing with approval finish in a consistent order.

**Observed checks:** Missing simulator routes failed first. All 49 API tests then passed, including the full 0 → 2 → 1 → 0 workflow, no-password identities, two locations, normal workspace preservation, audit retention, missing/malformed/old generations, edited/expired/repeated Apply, role/environment/source restrictions and concurrent reset/approval. At this increment the running app was still Milestone 1; section 4 records the later native startup and HTTP verification.

**Exercise:** Imagine opening an assignment form, resetting in another tab, then pressing Save. Explain why the server must compare the captured generation rather than simply using the newest one. Find that comparison in `WorkspaceMutationFilter.cs`.


## 4. Connected screens and live refresh

The manager can now review eligible replacements and rejection reasons, approve or reject pending requests, inspect activity pages and run the lab. Employees can cancel their own pending requests and read resolved history. Only Pending requests affect the pending count and assignment buttons.

SignalR sends a small invalidation after the database transaction commits. It does not send editable records or make scheduling changes. The client rejoins its authorized organization after reconnecting, then reloads authoritative data. Long Polling uses the existing same-origin web proxy with a 15-second server poll timeout. Connection status distinguishes connecting, connected, reconnecting and offline; manual Refresh remains available. See Microsoft's [client guide](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0) and [group guide](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-10.0).

An ordinary invalidation waits while a form is open. A new simulation generation closes old forms and reloads metadata, with an explanation. Each form keeps the generation it opened with. Identity checks compare the current cookie's user against the displayed user before saving and joining/rejoining; they never silently switch the displayed account. Entering the lab refreshes memberships for that same identity. Normal organizations stay first on a fresh sign-in.

**Observed checks:** The hub test failed on the missing endpoint, and the new client tests failed before the live-update module existed. All 51 API tests and 23 frontend tests now pass, with types, lint and production build passing. Hub tests exercise authentication, membership validation, leaving the old group, committed audit visibility and notification failure after commit. Client tests exercise coalescing, editing queues, reset invalidation and stale identity on reconnect/save. Native HTTP plus the official SignalR client passed through the actual Next proxy, including the full lab shortage progression, preserved audit and old-generation rejection. A separate regression caught lab names taking over the default workspace; normal-first ordering now passes.

**Exercise:** Disconnect the API while the page stays open. Explain why the saved schedule and a “connected” live indicator are different claims. Then explain why a notification failure after commit must not tell the user that their save failed.

## 5. Test it yourself

Open `http://localhost:3000`, or start the app with `./scripts/dev.ps1` from the repository's VS Code terminal. Your generated credentials are in the ignored `.local/demo-accounts.txt`. Keep passwords out of commits and screenshots.

### Start with Friday Rush

1. Sign in as manager and choose **Open Friday Rush lab**. If you have experimented before, choose **Reset lab**, read the confirmation, then **Restore baseline**.
2. The baseline target is covered. Choose **Apply Friday Rush**. Expect **2 people short** and Cameron's simulation callout. Apply removes Cameron's lab availability too, so the absent employee cannot be chosen for standby.
3. Choose **Review replacements**. Avery and Riley are eligible, with four and eight scheduled hours respectively. Expand **Why others are unavailable** and explain each reason in your own words. Choose Avery and approve. Expect **1 person short**.
4. Find the remaining open Friday shift. Choose **Assign employee**, select Riley and save. Expect all targets covered. The replacement filled the absence; standby filled the added demand.
5. Read **Activity history**, then expand **Change details**. Identify the actor, time and action. **Older activity** pages backwards; **Latest activity** returns to the newest page.
6. Open another normal browser tab with the same account. Select **Friday Rush Lab** in Organization, wait for **Live updates connected**, and open **Create shift**. Reset in the first tab. The other tab's old form should close with an explanation. The server also rejects old generation tokens if a screen misses the notification.
7. Return to the normal workspace. Ordinary shifts and requests remain saved. Reset preserves lab history and reuses its eight fictional profiles.

### Try the ordinary request flow

Use a private browser window for the employee; ordinary tabs share cookies. As manager, create a future Barista shift within Morgan's declared availability and assign Morgan. In the private window, Morgan can request coverage, cancel it, then request again. The manager can approve an eligible different person or reject the request. Approved history remains on Morgan's screen while the shift leaves Morgan's schedule. With both windows connected, updates should appear without Refresh.

If nobody is eligible, the explanation is the result. Choose an interval satisfying the rules or declare availability under the appropriate person's own sign-in. Overlap and hour limits still block saves. A version conflict asks you to review current data again.

### Run checks and understand Git

Run `./scripts/check.ps1` for API/frontend tests, types, lint and production build. With the app running, `node scripts/smoke-milestone-2.mjs` exercises HTTP and SignalR, resetting only your lab. The older `smoke-workflow.mjs` adds an ordinary synthetic shift/request, so run it deliberately.

Eight desktop/mobile browser cases register locally; discovery is not browser execution. Actual browser/container acceptance remains pending this branch's GitHub Actions run. No local browser automation or Docker engine is being installed to bypass existing limits.

A **commit** is a named snapshot. A **branch** keeps this milestone separate from accepted work. VS Code **Publish Branch** uploads it; later **Sync Changes** uploads additional commits. **CI** is GitHub running checks on its own clean machine. Inspect Actions for the exact latest commit before calling the milestone accepted. Merging is a later owner decision.

We used tests in the order “show missing behavior, implement, check again.” That is test-driven development. Independent review adds another reading of the whole change; neither replaces exercising the application.

## Final local verification and review

| Check | Observed result |
| --- | --- |
| API / PostgreSQL integration | 52 passed, including migration preservation, roles, concurrent resolution/reset and readable approval snapshots after reset |
| Frontend unit/component behavior | 25 passed, including real React dialog/screen rendering, stale metadata ordering, reconnect with an open form, queued notices and identity checks |
| Types / lint / production build | Passed |
| Native HTTP workflows | Original staffing workflow and full Milestone 2 HTTP/SignalR workflow passed through the actual Next proxy |
| Schema / configuration | EF model matches migration; official checksummed standalone Compose CLI validates configuration; workflow YAML parses |
| Browser discovery | Eight desktop/mobile cases registered; discovery only |
| Browser / container runtime | Pending this branch's GitHub run; historical runs do not verify this milestone |
| Independent review | One fresh whole-branch review; findings addressed in one fix pass, with no second review claimed |

The review found stale lab controls after same-generation changes, a missed reset during reconnect with an open form, historical audit snapshots lacking readable names/times, and three corrupted punctuation literals in the preserved browser test. Added regressions failed first, then passed after the fixes. A delayed-response check also proved that an older metadata response cannot overwrite a newer refresh. The encoding correction passed a character inspection; its actual browser workflow still needs CI. Employees now receive the replacement name only with their own request history, and queued refreshes show a notice inside open forms. No review minors remain deferred.

This is why a green test suite is evidence with limits: tests cover the cases written so far. Review found missing cases, so we added them. Browser tests check browser behavior; configuration validation alone cannot prove containers start.

Implementation decisions: native HTTP checks waited for the API/screens to start together (later passed); the callout removes the absent lab member's fixture availability (Reset restores it); the replacement name and queued notice were treated as required user behavior; production scale/durable delivery and full accessibility certification remain outside this milestone. Browser/container CI remains a required acceptance gate. These choices and their limits do not authorize a merge or deployment.
