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
