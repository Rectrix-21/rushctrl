# Milestone 2 API additions

All paths below are relative to `/api/v1/organizations/{organizationId}`. Existing authentication, membership, manager and fresh CSRF checks still apply. Writes in a simulation additionally require `X-Workspace-Generation` captured when the form opened: absent/stale is 409; malformed is 400. A conflict asks the caller to refresh, never to retry automatically.

| Method and path | Input / response | Scope |
| --- | --- | --- |
| GET `/shifts/{id}/candidates` | `policy`, `generatedAt`, `shiftVersion`, optional `pendingRequest {id,version}`, ranked `eligible`, explained `rejected` | Manager |
| POST `/coverage-requests/{id}/approve` | `{version,shiftVersion,userId}` → resolved request and new shift version | Manager; future shift and revalidated replacement |
| POST `/coverage-requests/{id}/reject` | `{version}` → resolved request | Manager |
| POST `/coverage-requests/{id}/cancel` | `{version}` → resolved request | Original requester only |
| GET `/audit?limit=50&beforeNumber=123` | Newest numbered records first, actor name, UTC time, action, resource, generation, small before/after JSON strings | Manager; limit 1–100, positive optional cursor |
| POST `/simulation` | `{}` → owner's existing or newly created lab metadata | Manager of reserved synthetic source; Development plus `Simulation:Enabled=true` |
| GET `/simulation` | Lab metadata | Lab owner |
| POST `/simulation/apply` | `{}` → same generation, applied metadata | Lab owner; pristine future baseline only |
| POST `/simulation/reset` | `{}` → new generation, baseline metadata | Lab owner; only this lab's staffing data replaced; audit preserved |

Requests now return `status` (Pending/Approved/Rejected/Cancelled), `kind` (Standard/SimulationCallout), `version`, `shiftVersion`, `replacementUserId` and `resolvedAt`, alongside the existing display fields. Only Pending requests block a new request or direct assignment. Standard requests retain the original assignment until approval. A simulation callout explicitly opens the absent fixture member's shift and removes their lab availability. Public request creation always creates Standard requests for the signed-in assignee.

Workspace metadata adds `canOpenSimulation` and nullable `simulation`. Lab metadata contains `organizationId`, `sourceOrganizationId`, `generation`, Monday `week`, `applied`, `pristine`, `locationId`, `absenceShiftId` and `standbyShiftId`. Reset reuses users, locations and skills. Another owner's lab is inaccessible.

Candidate eligibility uses the assignment rules, then `least-load-v1` orders eligible people by assigned minutes in all touched location-Monday weeks, approved covers in the inclusive last 28 days, then UUID. Rejected members have reason codes/messages. Discovery is advisory: approval locks and checks the rules again. See [ADR 0003](../adr/0003-explained-replacements.md).

`/hubs/workspace` is an authenticated SignalR endpoint, initially Long Polling only, with a 15-second poll timeout. `JoinWorkspace(organizationId)` validates current membership and leaves the connection's previous organization group. `WorkspaceChanged {organizationId,generation}` is sent after commit. It carries no staffing records. The client rejoins and reloads after reconnect, checks that the shared cookie still belongs to the displayed person, queues ordinary refresh during editing, and expires old-generation forms on reset. Notification is best-effort without an outbox; Refresh remains available. See [ADR 0004](../adr/0004-workspace-transactions-and-live-updates.md).

The organization row serializes writes; employee row locks and PostgreSQL's overlap constraint still guard assignments across organizations. The change and append-only audit commit together. A notification failure is logged after commit and cannot change a successful HTTP save into an error.
