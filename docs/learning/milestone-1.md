# Milestone 1 learning journal

Milestone 1 is in progress. This journal records working increments; it does not claim the whole milestone is complete.

## 1. Sign-in, sessions and organization membership

**What changed:** Added ASP.NET Core Identity users, organization memberships, login/logout/session endpoints, CSRF tokens and an explicit Development-only seed command. The first Identity migration adds user and membership tables without deleting the existing organization/location data. Startup generates ignored local demo credentials and preserves existing database credentials.

**Why:** Authentication answers “who signed in?” Authorization answers “may this person change this organization's schedule?” A session cookie lets the browser prove it already signed in. The cookie is HttpOnly so page JavaScript cannot read it. Passwords are hashed through Identity rather than stored as readable text. A membership is the database relationship connecting a person to an organization and its permission.

**Files to explore:** `Modules/Identity/IdentityEndpoints.cs` handles HTTP requests; `AppUser.cs` defines users and memberships; `TenantScope.cs` loads the caller's membership; `DevelopmentSeed.cs` creates only synthetic local identities. `RushCtrlDbContext.cs` maps those classes to PostgreSQL tables. The migration is a versioned database change, not a second copy of the database.

**Request path:** Browser → same-origin Next.js proxy → API → Identity/membership query → PostgreSQL → JSON response. The browser cannot grant itself manager permission by changing a header.

**Observed checks:** Four new protocol tests first failed with 404 for the missing endpoints. After implementation, the full API suite passed 17/17 against isolated PostgreSQL databases. The checks include real hashed credentials, login/logout, refreshed CSRF tokens after sign-in, cross-organization 404, anonymous 401 and idempotent seed setup that refuses Production. Existing frontend status tests passed 8/8 before interface changes.

**Try after the interface increment:** Open `.local/demo-accounts.txt` in VS Code for the generated local accounts. Do not commit that file or `.env`. Compare manager and employee sessions. The login screen is not connected yet at this increment, so the old preview remains the visible UI.

**Small exercise:** Find `MembershipFilter`. Explain why it gets the user ID from the authenticated session instead of a JSON field named `userId`. Then find the database's composite membership key: the same person can have different permissions in different organizations.

**Limits:** These accounts are local synthetic development data. Public signup, password recovery and deployment configuration are outside this milestone. Changing a generated password in `.env` does not reset an existing account.

## 2. Saved shifts and assignment rules

**What changed:** Added organization-owned skills, employee qualifications, availability, shifts, staffing targets and request storage. Managers can create/list shifts and assign an employee. Assignment checks skills, full availability, other shifts and the configured weekly hours. The availability write was brought forward because it lets us test assignment against availability actually declared through HTTP.

**Why:** A foreign key links a shift to a real location, skill and member. Composite foreign keys additionally require those records to belong to the same organization. A transaction makes validation and saving one operation. Locking the employee record makes concurrent assignment requests take turns; a PostgreSQL exclusion constraint is an additional guarantee that overlapping assigned shifts cannot be saved. A version token detects a browser acting on an old shift.

**Time lesson:** An 08:00 Toronto shift in November is 13:00 UTC. Store the instant in UTC, but display it in the location's zone. A local week crossing daylight saving can have 167 or 169 actual hours. Half-open intervals let one shift end at exactly the instant the next begins. Overnight shifts contribute only the hours falling inside each local week.

**Files to explore:** `Modules/Scheduling/Entities.cs` describes records; `SchedulingModel.cs` defines ownership and indexes; `ScheduleTime.cs` interprets dates; `AssignmentService.cs` owns assignment rules; `SchedulingEndpoints.cs` maps HTTP to those operations. The staffing migration creates the tables and database overlap constraints.

**Observed checks:** The new tests first failed for missing time handling, routes and tables. The 27-test suite then passed, including persistent shifts, qualification/availability rejection, adjacency, overlaps, weekly caps, stale versions, concurrent requests and inaccessible references. Supplemental tests exercise real cross-organization IDs, bypass application validation to check PostgreSQL constraints, and check overnight/DST boundaries.

**Small exercise:** Read the availability loop in `AssignmentService`. With windows 08:00–10:00 and 10:00–12:00, explain why 08:00–12:00 is covered. Change the second window on paper to 10:01–12:00: the one-minute gap must reject assignment.

**Input bounds:** A single entered interval is limited to 31 days, and schedule weeks to 2000–2100. These bound data-entry mistakes and expensive range checks; they are application limits, not employment rules.

## 3. Availability, pending requests and coverage gaps

**What changed:** Employees declare dated availability and request coverage for their own future assigned shift. Managers see those pending requests and can create staffing targets. The coverage calculator compares each target against assigned shifts, splitting periods where staffing changes. The development seed now includes qualifications, future availability, assigned/open shifts and a visible shortage. Re-running the seed preserves existing shifts.

**Why:** A coverage request is a record of an employee asking for help; it does not remove their assignment. Request resolution and replacement approval come in Milestone 2. This first version explains that a pending request cannot yet be cancelled/resolved and blocks reassignment of that shift. Targets express how many people are needed for a role; open shifts do not count as staffed people.

**Algorithm exercise:** A target needs two baristas from 08:00–14:00. One is assigned 08:00–10:00 and a different person 10:00–12:00. The shortage is one from 08:00–12:00, then two from 12:00–14:00. Two adjacent periods with the same shortage are merged for readability. An unassigned shift covering the whole day changes neither number.

**Files to explore:** `CoverageEndpoints.cs` enforces who can request and who can set targets. `CoverageCalculator.cs` calculates shortage intervals without HTTP or database dependencies, so a small unit test can check the algorithm. `DemoScheduleSeed.cs` creates synthetic local scheduling data within the Development-only seed transaction.

**Observed checks:** New coverage tests first failed for missing routes/calculation. The full API suite passed 36/36 after implementation and seed extension, including duplicate/ownership checks, manager-only targets, overlapping-target rejection, partial coverage, and simultaneous cap/overlap assignments. Test time is fixed independently of the machine's date.

## 4. Real screens and typed requests

**What changed:** The main route now checks the session and shows sign-in or a role-specific workspace. Managers can create/assign shifts and add staffing targets. Employees can see their assigned shifts, add availability and request coverage. Day/week views, location/week selection, request lists and gap summaries use actual saved API data. The old static screen remains at `/preview`. Styling extends the approved blue glass system.

**How React fits:** A component is a reusable piece of the screen. State is the component's current memory, such as the selected week or open dialog. An effect loads data when its inputs change; cleanup ignores responses belonging to a screen the user has already left. Saving sends a request to the API, then reloads the authoritative data instead of pretending the change succeeded locally.

**Files to explore:** `WorkspaceApp` handles sessions/sign-in; `WorkspaceScreen` handles organization/location/week; `ScheduleBoard` shows saved data; `ActionDialog` handles forms. `contracts.ts` defines runtime-checked response shapes, `forms.ts` validates inputs, and `workspace-api.ts` handles same-origin requests and fresh CSRF tokens. [Zod's official schema reference](https://zod.dev/api) explains the validation library.

**Failure lesson:** A network timeout after submitting a change does not prove the save failed: the database may have committed before the response was lost. The client never silently retries a save, and asks the user to refresh before retrying an uncertain outcome. This behavior has a regression test that failed before its message was corrected.

**Observed checks:** Frontend tests passed 15/15, TypeScript passed, ESLint passed without warnings, and a production build succeeded. The real HTTP smoke script passed through Next.js → API → PostgreSQL: both identities signed in; a manager created/assigned a shift; a fresh session read the saved assignment; the employee declared availability/requested coverage; the manager saw the request; logout cleared the session. This is HTTP verification, not visual browser verification. The synthetic records remain locally for inspection.

**Small exercise:** Sign in using `.local/demo-accounts.txt`, choose a week, then reload. React's temporary state disappears, but the saved shift comes back from PostgreSQL. Compare this with `/preview`, whose schedule is a fixed sample. Never commit `.env` or the local accounts file.

## 5. Test the application yourself

Start from the repository root in the VS Code PowerShell terminal:

```powershell
./scripts/dev.ps1
```

If it reports a previous recorded launch, use `./scripts/stop.ps1` first. Setup is not needed again if dependencies/tools already exist. Open `http://localhost:3000`, and open `.local/demo-accounts.txt` in VS Code for credentials. `apps/web/next-env.d.ts` is generated by Next.js; application changes belong in the components, API modules and styles, not that generated file.

1. **Manager sign-in:** Use the manager account. Check Northline Coffee, Harbour, the Manager label and America/Toronto. The selected seed week should contain future shifts. The connection status should report API + database connected.
2. **Staffing target:** Pick a future Wednesday in that seeded week. Add a Barista staffing target from 14:00 to 16:00 needing one person. The Wednesday gap should say one person short. Other Monday seed gaps can remain visible.
3. **Create, assign and reload:** Create a Barista shift for the same Wednesday 14:00–16:00. It starts open, so it does not yet fill the gap. Choose Assign employee → Morgan Park → Save assignment. The Wednesday gap should disappear. Reload the browser: the assignment should remain. This is your practical persistence check.
4. **Try a conflict:** Create another Barista shift for Wednesday 15:00–17:00 and try assigning Morgan. Expect an overlap error; the first assignment must remain. The second shift remains open because creating a shift and assigning it are separate operations. Also try an end time before the start: the form should reject it without saving.
5. **Employee view:** Sign out, then use the employee account. You should see Morgan's assigned shift and no Create shift button. Add a dated availability window, save, and reload. It should remain. Seeded availability already covers 08:00–20:00, so another overlapping declaration is allowed.
6. **Request coverage:** Open Request coverage on the future assigned Wednesday shift. Read the limitation before submitting: this milestone cannot cancel/resolve the request, and the manager cannot reassign it while pending. After submitting, expect Pending and an unchanged assignment. Do not use this action on a shift you want to keep experimenting with assigning.
7. **Manager visibility:** Sign out/in as manager. The request should appear in Coverage requests and the shift should show Request pending. Use a private browser window if you want both roles open simultaneously; ordinary tabs share sign-in cookies. Schedule updates from a different window require Refresh schedule.
8. **Keyboard and mobile:** Tab through controls; focus should be visible. Open a form and press Escape: it should close and return focus to its trigger. A successful save moves focus to its confirmation. Narrow the browser or use mobile emulation: forms and schedule cards should stay inside the page. These are manual checks for you to observe, not checks claimed as already passed by automation here.

The [HTTP smoke script](../../scripts/smoke-workflow.mjs) already left synthetic requests/assignments on the seeded Monday. Pick a free period for your own exercise. There is no demo reset/delete action yet; that belongs to Milestone 2.

## Automated acceptance record

| Check | Observed result for this branch |
| --- | --- |
| Full local `./scripts/check.ps1` after review fixes | API 39/39; frontend 19/19; typecheck, lint and production build passed |
| Test-driven regressions | Missing endpoints/time/rules, gap algorithm, suggested-week privacy and uncertain save outcomes were observed failing before their fixes |
| Native startup after script updates | Build/migrate/seed/start succeeded; existing demo edits preserved |
| Actual web-proxy HTTP workflow | Passed after review fixes, including fresh-session persistence, both roles, stale shared-cookie write rejection and explicit re-login |
| EF model versus migration | No pending model changes |
| Compose configuration | Validated with the standalone Compose CLI; no secret-expanded output printed |
| Playwright discovery | Six desktop/mobile cases registered; discovery does not execute a browser |
| New branch browser + Compose startup | Pending CI; local browser automation is unavailable and no local Docker engine is installed |
| Independent code review | Two Important findings fixed with failing-then-passing regressions; no Critical or Minor findings raised; no second review claimed |

Run `./scripts/check.ps1` for local automated checks. Run `node scripts/smoke-workflow.mjs` only when you want it to add another synthetic assignment/request. GitHub Actions is configured to test the API, start a real seeded stack for Playwright, and run the HTTP workflow against Docker Compose. A successful historical Milestone 0 run does not verify these new changes.

For learning: a unit test checks a focused rule without the whole app; an integration test checks components working together, including the real database; an end-to-end browser test clicks the interface through a complete workflow. Each catches a different class of mistake.

## Independent review and fixes

A fresh reviewer checked the complete branch. Both Important findings were reproduced before changing code:

- **Stale account in another tab:** Ordinary browser tabs share cookies. A screen showing Morgan could otherwise acquire a new CSRF token for Alex after another tab switched accounts, and save availability under the wrong person. CSRF now returns its own session user ID; the client compares it with the displayed identity before saving. A mismatch blocks the mutation and clears the stale workspace for sign-in. Login explicitly permits rebinding. Older session responses cannot overwrite the latest identity binding. Unit tests first accepted the unintended write, then correctly rejected it after the fix; a real shared-cookie HTTP test also verified that availability stayed unchanged. The Playwright workflow includes this case but still needs CI execution.
- **Overnight labels:** A Sunday 23:00–Monday 01:00 shift appeared in Monday's day view without its Sunday start date. Intervals crossing a local date now show both dates. Regressions cover overnight, Sunday/Monday week boundaries and midnight endings; the original missing-start-date case failed before the fix.

The final rerun exposed an additional test-infrastructure issue: `OpenPastAndOtherPersonsShiftsCannotBeRequested`, `SeedRequiresDevelopmentAndExplicitCredentialsAndIsIdempotent`, and `ManagerCanSaveAndReloadShiftAndEmployeeCannotCreateOne` intermittently failed before their assertions, while asynchronously creating a temporary database on this Windows runner. The first case also failed by itself. PostgreSQL's `psql` client could create/drop a diagnostic database successfully. Keeping only administrative test-database creation synchronous made the failing case and the full 39-test API suite pass. No business-rule tests were skipped or retried automatically, and application database operations/concurrency tests remain asynchronous. The underlying Windows async-I/O cause is not claimed as diagnosed beyond this observed setup boundary.

A separate follow-up run of all 21 database integration cases also passed after that setup change. The current full local acceptance result is 39 API tests and 19 frontend tests, plus clean type checking/lint and a successful production build. Browser and container execution remain pending CI.

## Decisions and remaining boundaries

- Continue the previously approved narrow M1 architecture and the owner's authorization to begin it; the November 7 design elaborates builder details. A different intended workflow would require a follow-up adjustment.
- Keep work in the open repository on `milestone-1`; adapt shell-based progress helpers to native PowerShell. This changes bookkeeping format, not application behavior.
- Bring availability endpoints and the small request/target tables into the scheduling increment so assignment tests exercise declared availability with one coherent migration. Those tables arrived before their request/coverage endpoints.
- Bound input intervals to 31 days and schedule queries to week years 2000–2100. Longer intervals/dates require a later deliberate change.
- Keep request resolution, replacement approval, ranking, audit, simulation/reset and SignalR for M2. Pending requests cannot yet be resolved or cancelled; that limitation is visible before submission.
- Availability editing/deletion and qualification/hour-cap administration remain outside this milestone. Local setup owns those seeded settings for now.
- Public registration/recovery, deployment and production operational hardening remain outside the authorized local synthetic application. This is not a public service.
- Browser layout/keyboard execution and Docker startup cannot be certified from static review. They remain pending this branch's CI run and owner visual checks.
- Seed reruns preserve existing shifts instead of replenishing them automatically each week. A long-lived demo may need a future reset or fresh seed workflow.

No review minors were deferred. Milestone 1 remains awaiting browser/container acceptance, so retain its progress ledger until that evidence arrives. The older Milestone 0 run cannot substitute for those checks.

## First GitHub acceptance run

The owner published `milestone-1` at `465a095`. [The first run](https://github.com/Rectrix-21/rushctrl/actions/runs/42400724102) passed the API job but failed the browser workflow and Compose startup. These failures keep acceptance open. Detailed job logs require GitHub sign-in; their exact errors are still needed to diagnose those failures.

Inspecting the container separately revealed that its small Alpine base image omits time-zone files. The runtime image now installs `tzdata`, which supplies the IANA rules used by `America/Toronto`. Microsoft’s [base image definition](https://raw.githubusercontent.com/dotnet/dotnet-docker/main/src/runtime-deps/10.0/alpine3.24/amd64/Dockerfile) lists the omitted packages, and [.NET’s Unix implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Private.CoreLib/src/System/TimeZoneInfo.Unix.NonAndroid.cs) reads zone files from disk. No time-zone conversion rules in the application changed. Compose configuration validation passed after the edit; actual container startup still needs a new GitHub run. The original Compose failure is not attributed to this dependency until its logs confirm it.

For learning: a container is a separately assembled operating environment. A library can compile successfully while an operating-system data dependency is missing at runtime. That is why we test both the application and its packaged container.

## Browser failure: a form closed as soon as it opened

The browser log supplied by the owner showed four preview tests passing, with both real-workflow tests failing immediately after **Create shift**: the dialog could not be found. This was a form lifecycle failure before any shift was submitted.

React's development Strict Mode runs effect setup, cleanup, then setup again. Our effect opened the native dialog, closed it during cleanup, then reopened it. The cleanup's queued `close` event still reached the React handler, which removed the reopened form. The handler now checks the dialog's current `open` state before dismissing it. A close event from the earlier cleanup is ignored while the form is open; an actual close still dismisses it. Strict Mode stays enabled.

The new component regression in `apps/web/tests/workspace-dialog.test.tsx` renders the real form under Strict Mode. It failed with the original handler and passed after this change, including the subsequent native-close dismissal. `tsx` lets the existing Node test runner load JSX components, while jsdom supplies a simulated DOM. Because jsdom omits native dialog methods, the test models their open state and queued close event at that boundary. This is a component regression, not a claim that Chromium, keyboard focus or mobile layout has passed. The unchanged GitHub Playwright workflow must still verify those behaviors in a real browser.

After the dialog fix, `./scripts/check.ps1` passed all 39 API tests, all 20 frontend tests, type checking, lint and the production build. Playwright still discovers six cases. Its assertions and timeouts were not relaxed. The CI reporter now includes GitHub failure annotations so later failures can be inspected through the checks API.

The supplied Compose log shows that all images built and PostgreSQL became healthy, but the migration container exited with code 139. On Linux this conventionally indicates a native process crash. The excerpt contains no migration-container output, so the crash's cause is still unknown; the `tzdata` change is not claimed to fix it. A failure-only CI step now collects container output before shutdown, redacts the generated database/demo passwords, and includes the output in a GitHub annotation. The workflow YAML parses and Compose configuration validates, but the diagnostic step and packaged runtime require another GitHub run.

## Second GitHub acceptance run

[Run 42401622823](https://github.com/Rectrix-21/rushctrl/actions/runs/42401622823), testing `0224c8a`, passed the API and Compose jobs. The containers started, readiness passed and the full proxy HTTP staffing workflow passed. This establishes that the packaged stack works on that run; it does not establish the original native-crash cause.

The browser cases now passed the dialog-visibility step, confirming the lifecycle fix in Chromium. Both then timed out locating **Required skill** with an exact label-text query. Playwright's label query reads the wrapping label's text, including its nested select options. The control's accessible name remains **Required skill**. The workflow now locates the skill and employee dropdowns using their `combobox` role and exact accessible name, as recommended by [Playwright's locator documentation](https://playwright.dev/docs/locators#locate-by-role). This corrects the test's lookup without changing application behavior, weakening assertions, or increasing timeouts. Actual browser acceptance still requires the next CI run.

After the selector correction, all 20 frontend tests, type checking and lint passed. The corrected `npx playwright test --list` command from `apps/web` listed six cases without launching a browser. An earlier listing attempt incorrectly forwarded `--list` through the root npm wrapper: npm consumed that flag and attempted execution. All six desktop/mobile cases (sample dashboard, failed readiness and saved staffing workflow) failed at browser launch because the Chromium executable is absent; no test body ran and no browser was installed. This local tooling failure does not replace the pending GitHub browser run.

## Third GitHub acceptance run

[Run 42402225906](https://github.com/Rectrix-21/rushctrl/actions/runs/42402225906), testing `a5d4a63`, again passed API and Compose. Both browser workflows found the dropdown and submitted a shift, receiving HTTP 201. They then failed when Playwright requested the response body through Chromium's debugging protocol (`Network.getResponseBody`: no data found). Playwright appends generic navigation advice to this protocol error; that text does not prove that the application navigated. The save handler calls `preventDefault` and refreshes the schedule through API reads.

The workflow now verifies that the page remains at the workspace URL, locates the saved open Barista card by its day and exact entered time, and obtains its ID from the rendered card. It still requires HTTP 201, a unique matching card, the correct skill, subsequent assignment, persistence after reload and the complete employee/request workflow. API integration tests separately check JSON response contracts. The browser test no longer depends on recovering the unavailable mutation response body; the precise reason Chromium could not supply that body is not established. All 20 frontend tests, type checking and lint passed after this correction; discovery lists six browser cases. Browser acceptance remains pending another CI run.
