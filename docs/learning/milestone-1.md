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
