# ADR 0001: Next.js frontend and a modular ASP.NET Core API

Status: Accepted for Milestone 0.

RUSHCTRL needs transactional staffing workflows, relational constraints and eventually real-time change notifications. A single ASP.NET Core API keeps those rules and transactions in one place while Next.js owns the interface. Feature directories provide boundaries without unused projects or distributed infrastructure.

PostgreSQL stores the authoritative data through EF Core. The first migration includes only Organization and Location. UUID keys, organization ownership, UTC timestamptz creation values, required names, a unique location name per organization and a restrictive foreign key establish the relational foundation. Future tenant-owned relationships should reference composite organization/resource keys. The current two-table schema does not implement employee isolation or scheduling rules by itself.

Frontend requests use same-origin Next.js rewrites for `/api/*` and explicit health paths. This avoids a separate browser CORS configuration and prepares for HttpOnly cookie authentication. The upstream origin is server configuration embedded in the Next build, not a public database secret.

Liveness deliberately ignores PostgreSQL. Readiness checks connectivity, pending migrations, and access to the expected tables with short timeouts. The public result contains only status. Apply migrations explicitly, using a one-shot local Compose service or the API `--migrate` command. Production migration orchestration must be designed separately before deployment.

Alternatives considered: frontend-only persistence would not demonstrate the required server-side rules; microservices would add failure modes and deployment work without a justified boundary. SignalR will later send invalidation events, not act as the data store.

Consequences: the frontend and API remain separate build units, and proxy origin changes require a frontend rebuild. The API remains one deployable process; module boundaries can be extracted only when real dependency pressure justifies it.
