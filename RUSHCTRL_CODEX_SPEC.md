# RUSHCTRL — Codex Engineering Specification

**Status:** Proposed project, not yet built. No functionality, users, performance claims, or metrics should be described as existing.
**Owner:** AK. **Date:** 2026-11-02.
**Purpose:** A portfolio-defining, production-oriented full-stack engineering project demonstrating business logic, real-time collaboration, algorithms, testing, SQL, security, deployment, and product judgment.

## One-sentence product pitch

RUSHCTRL is a multi-location operations platform for cafés, restaurants, and retail teams that manages staffing availability, schedules, shift swaps, coverage gaps, and operational simulations in real time.

## Why this is worth building

AK has hands-on hospitality experience and can explain user pain points convincingly without claiming access to proprietary staff data or employer systems. KeyTone already demonstrates a creative AI/audio product with a Python backend. RUSHCTRL adds a **conventional business application** with C#, relational data, real-time workflows, security boundaries, and careful quality practices. It must have a complete core workflow before flashy extras.

## Showcase demo: "Simulate Friday Rush"

1. Visitor enters an isolated, seeded demo workspace (synthetic café/location, fake employees and transaction demand).
2. A manager dashboard displays current coverage by role/skill and upcoming shifts.
3. Visitor triggers a deterministic "Friday Rush" scenario: forecast demand spikes and one employee calls out.
4. Backend calculates coverage shortfalls, ranks possible substitutes based on availability, skill, no-overlap, maximum configured hours, and a transparent fairness policy.
5. UI shows why each candidate was selected and what constraints were enforced; it does NOT auto-assign staff or suggest that the algorithm guarantees optimal scheduling.
6. Visitor approves a replacement or leaves the gap unresolved; backend applies the update atomically with a concurrency check.
7. A second connected browser session sees the updated schedule and coverage via SignalR. Audit log records the action.
8. Visitor can reset the demo dataset safely. No real employee information required or accepted in the demo.

This end-to-end story is the goal. Build the minimal working version *before* charts, AI, fancy animation, or real external integrations.

## Architecture and default technology decisions

- **Frontend:** Next.js (App Router), React, TypeScript; accessible responsive UI; forms with schema validation; API client generated or strongly typed from documented contracts where useful.
- **Backend:** ASP.NET Core Web API in C#, preferably a maintainable **modular monolith** (not microservices). REST API with OpenAPI documentation.
- **Database:** PostgreSQL with Entity Framework Core migrations. Explicit relational constraints and indexes; UUID IDs. Tenant-aware queries.
- **Real-time:** ASP.NET Core SignalR hub for event notifications. The database remains the source of truth; clients refresh state from the API after receiving change notifications.
- **Auth and authorization:** Use a supported provider or ASP.NET Identity, with explicit manager/employee permissions. Enforce authorization and tenant isolation on the server. Prefer a shared seeded demo identity model over exposing unrestricted production admin accounts.
- **Testing:** xUnit for unit/integration tests; Playwright for critical end-to-end flow. Use an isolated test database; integration tests cover tenant separation and race conditions.
- **Quality/operations:** Docker Compose for local web/API/Postgres development, GitHub Actions for build/test/lint, structured logs, health checks and request tracing. Add OpenTelemetry or Sentry if appropriate after a working MVP.
- **Hosting:** Choose a secure, reasonably priced hosting provider after checking current options and limits. Never put secrets in Git. Do not claim the deployment is production-scale without evidence.
- **AI:** No LLM required in MVP. If added later, it may explain recommendations but must not override validated scheduling rules. Measure accuracy/utility before marketing it as predictive.

## User roles and core workflow

### Employee
- View assigned shifts in the correct location/time zone.
- Maintain availability and submit requests for time off.
- Offer a shift swap / request coverage; cannot directly approve or assign a replacement.
- View request statuses and relevant notifications.

### Manager
- Create/edit shifts and staffing requirements by role and time interval.
- Accept/reject availability and coverage requests.
- Review gaps, candidate recommendations, and explanations.
- Trigger simulations in an isolated demo environment.
- See an audit history for staffing changes.

### Organization admin (optional after MVP)
- Manage locations, roles, qualification tags, hours limits, and manager access.

## Minimal proposed entities

Organization, Location, User, Membership, Role/Permission, Skill, EmployeeSkill, AvailabilityWindow, Shift, StaffingRequirement, CoverageRequest, SwapRequest, RecommendationRun, AuditEvent, SimulationScenario.

Do not build all entities immediately. Implement the minimum relational model for the first vertical slice. Every shift needs tenant/location ID and timezone-aware timestamps. Define actual business invariants such as no overlapping shifts for a person and manager-only final approval.

## Feature roadmap and acceptance criteria

### Milestone 0 — Discovery + skeleton
- Repo structure, README, architecture decision record, API contract, local setup, CI checks.
- Docker Compose brings up API + database; frontend connects; health check passes.
- Decide how demo users, auth, and tenant isolation work before shipping endpoints.

### Milestone 1 — Functional MVP
- A manager can create and list shifts, assign employees, and view coverage gaps.
- An employee can declare availability and request shift coverage.
- Permission boundaries enforced server-side; conflicts rejected with useful messages.
- Basic seed data, responsive schedule, relevant backend tests, and at least one E2E workflow.

### Milestone 2 — The differentiator
- Simulation runs against an isolated synthetic dataset; changes are inspectable and resettable.
- Coverage engine identifies shortages, ranks eligible replacements using explicit scoring and rejection rules, and shows reasoning.
- Manager approval persists atomically; concurrent conflicting approvals cannot double-book an employee.
- SignalR updates appear in a second session after a successful change.
- Unit tests cover overlap, lack of qualifications, unavailable staff, configured hour caps, and concurrency/tenant boundaries.

### Milestone 3 — Presentation and operational maturity
- Live demo and 90-second walkthrough video, architecture and data-flow diagrams, useful README, instructions for reviewers.
- Observability/logging and error paths, security checks, accessible keyboard UI, mobile audit, realistic perf/load tests (with actual measurements only).
- Two to four engineering decision records explain tradeoffs, algorithms, and system design.
- Project added to portfolio as a case study with Demo, Code, Architecture, Tests, and Results links.

## Constraints and anti-patterns

- **Do not attempt an entire HR/payroll platform**, tax calculations, labour-law compliance engine, or real employee payroll processing.
- No scraped or confidential Starbucks employer data. No trademark implication or affiliation. Base the domain on general hospitality problems and public/synthetic data.
- Do not market rule-based ranking as machine learning, or simulated demand as actual demand predictions.
- No unnecessary microservices, Kafka, Kubernetes, LLM agents, or complex infrastructure solely for resume keywords.
- Do not store demo user passwords or private employee information in source control.
- Do not fabricate real users, reduced labour costs, throughput, or availability claims. If benchmarking, publish environment, methodology, and limitations.
- No automatic deployments or external paid service signups without AK's explicit approval.

## Portfolio story to demonstrate

> Designed a real-time multi-location staffing operations application with React/TypeScript, ASP.NET Core, PostgreSQL, and SignalR. Implemented role-based workflows, constraint-aware coverage recommendations, atomic approvals, audit logs, and automated API/E2E tests. [Replace this last sentence with honestly measured results after completing and testing the app.]

This is **proposed resume wording**, not currently achieved work.

## Required first Codex action

1. Read this spec and the existing repository instructions. Confirm whether the open VS Code workspace is the portfolio site or the RUSHCTRL application.
2. If it is the portfolio site, **do not put a new .NET application inside its codebase**. Create or open a separate RUSHCTRL repository/workspace when AK authorizes that action.
3. Propose a deliberately narrow Milestone 0 and Milestone 1 implementation plan, initial data model, architecture boundary, and test strategy.
4. Work in small reviewable increments and report the code changes and tests after each increment.
5. Preserve the current portfolio site visual identity; the new RUSHCTRL product may use its own clean UI, but must prioritize usability over decorative effects.

## Paste into Codex when ready to build the new application

Read `RUSHCTRL_CODEX_SPEC.md`. This is a proposed standalone application, not part of the existing portfolio app. Audit the current VS Code workspace and any existing instructions before editing. Plan a small modular-monolith MVP using Next.js/TypeScript, ASP.NET Core/C#, PostgreSQL/EF Core, SignalR, xUnit, Playwright, and Docker Compose. Start with the functional staffing workflow and testable business rules, not the simulator or visuals. Show me the proposed architecture, folder layout, data model, and milestones; then implement Milestone 0 in manageable steps. Do not deploy, buy services, or create external resources without approval.
