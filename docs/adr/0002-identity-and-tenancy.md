# ADR 0002: Establish secure defaults before staffing endpoints

Status: Accepted; Identity sessions and tenant workflows implemented locally in Milestone 1. Browser/container acceptance for this branch is tracked separately.

Milestone 0 exposes no employee or organization records through HTTP. New endpoints require authentication by default. Public health, metadata, development OpenAPI and the not-found fallback explicitly opt out. Cookie authentication is a real registered handler; unauthenticated and forbidden API responses use 401/403 rather than login redirects. Cookies are HttpOnly, SameSite=Lax and Secure outside Development. No login or fabricated user is shipped.

Milestone 1 uses ASP.NET Core Identity behind the same-origin proxy. State-changing routes validate antiforgery tokens, including login and logout. Server-side membership derives organization permission from the authenticated user; client-supplied user, tenant or role headers cannot grant access. A resource identifier is scoped to an organization the caller can access. Tenant query filters and composite relational ownership constraints are complementary safeguards. Integration tests use a second organization and actual cross-tenant resource identifiers. Organization-specific permissions live in Membership rather than global Identity roles.

The Identity user-only EF context supplies password/session persistence. The application cookie is HttpOnly/SameSite and Secure outside Development. Login failures use one public message and Identity lockout; no public registration or recovery flow exists. Explicit `--seed-demo` requires Development, `Demo:Enabled` and ignored/generated passwords. Rerunning it preserves saved scheduling records. There is no automatic production demo identity provisioning.

Local synthetic identities must be restricted to development and use ignored configuration or generated credentials. A later public demo should allocate an expiring isolated synthetic workspace rather than share an unrestricted mutable administrator account. No real employee information should be collected by that demo.

Local configuration is generated into ignored `.env`; example configuration contains no working password. Downloaded tools, database files and process logs are ignored. No external identity provider or hosting account has been created. Production TLS, cookie/data-protection key persistence, least-privilege database roles and deployment secrets must be settled before deployment; the current local Compose setup is not production hosting.
