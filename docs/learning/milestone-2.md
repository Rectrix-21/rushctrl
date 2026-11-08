# Milestone 2 learning journal

Milestone 2 is being built on `milestone-2` from the merged Milestone 1 code. The owner approved the November 8 design. This document records completed increments; the whole milestone is not yet accepted.

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
