# ADR 0003: shared eligibility and explained replacement ordering

Status: implemented for the Milestone 2 candidate API.

Hard constraints and preference are separate. `EligibilityService` checks organization membership, qualifications, full positive availability, global overlaps and every affected location-Monday week's configured cap. Direct assignment and coverage approval call this same service after locking; discovery evaluates a read snapshot and never grants permission to save.

`least-load-v1` orders eligible people by existing assigned minutes across the weeks touched by the target shift, then successful replacement approvals in the last 28 days, then UUID. The history window uses approval audit time in `[server now - 28 days, server now]`, inclusive; it does not use the future shift's date. Counts and workload are scoped to the organization, and simulation history is scoped to its current generation. Rejected candidates have reason codes and readable explanations; another organization's overlap is described generically.

This avoids arbitrary weighted confidence scores and is straightforward to explain and test. It favours lower scheduled load, not inferred employee preference or a guaranteed optimal/fair schedule. Different contractual caps can still lead to unequal utilization percentages; changing that policy would require a deliberate versioned decision. No historic approvals are invented. A manager always chooses and confirms, and approval revalidates against current data.
