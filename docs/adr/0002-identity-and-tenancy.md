# ADR 0002: Establish secure defaults before staffing endpoints

Status: Accepted; foundation implemented, Identity/tenant workflows planned for Milestone 1.

Milestone 0 exposes no employee or organization records through HTTP. New endpoints require authentication by default. Public health, metadata, development OpenAPI and the not-found fallback explicitly opt out. Cookie authentication is a real registered handler; unauthenticated and forbidden API responses use 401/403 rather than login redirects. Cookies are HttpOnly, SameSite=Lax and Secure outside Development. No login or fabricated user is shipped.

Milestone 1 will use ASP.NET Core Identity behind the same-origin proxy. Before a staffing write exists, implement server-side organization membership, manager/employee authorization and antiforgery checks. Never accept client-supplied user, tenant or role headers as authority. A resource identifier must be scoped to an organization the authenticated user can access. Query scoping and relational ownership constraints are complementary safeguards; tests must exercise a second organization and cross-tenant identifiers.

Local synthetic identities must be restricted to development and use ignored configuration or generated credentials. A later public demo should allocate an expiring isolated synthetic workspace rather than share an unrestricted mutable administrator account. No real employee information should be collected by that demo.

Local configuration is generated into ignored `.env`; example configuration contains no working password. Downloaded tools, database files and process logs are ignored. No external identity provider or hosting account has been created. Production TLS, cookie/data-protection key persistence, least-privilege database roles and deployment secrets must be settled before deployment; the current local Compose setup is not production hosting.
