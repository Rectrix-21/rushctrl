using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Modules.Scheduling;

public sealed record Candidate(Guid UserId, string DisplayName, double AssignedMinutes, int RecentCovers, IReadOnlyList<Rejection> Reasons, int? Rank = null);
public sealed record PendingRequest(Guid Id, Guid Version);
public sealed record CandidateSet(string Policy, DateTimeOffset GeneratedAt, Guid ShiftVersion, PendingRequest? PendingRequest, IReadOnlyList<Candidate> Eligible, IReadOnlyList<Candidate> Rejected);

public sealed class CandidateService(RushCtrlDbContext db, EligibilityService eligibility, TimeProvider clock)
{
    public async Task<CandidateSet> DiscoverAsync(Guid shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shiftId, ct) ?? throw new ScheduleRuleException("Shift not found.", 404);
        var request = await db.CoverageRequests.AsNoTracking().SingleOrDefaultAsync(x => x.ShiftId == shiftId && x.Status == "Pending", ct);
        var now = clock.GetUtcNow();
        var generation = await db.SimulationWorkspaces.Where(x => x.OrganizationId == db.OrganizationId).Select(x => (Guid?)x.Generation).SingleOrDefaultAsync(ct);
        var since = now.AddDays(-28);
        var covers = await db.AuditEntries.AsNoTracking().Where(x => x.Action == "CoverageRequest.Approved" && x.Generation == generation && x.CreatedAt >= since && x.CreatedAt <= now && x.SubjectUserId != null)
            .GroupBy(x => x.SubjectUserId!.Value).Select(x => new { UserId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.UserId, x => x.Count, ct);
        var people = await (from member in db.Memberships.AsNoTracking() join user in db.Users on member.UserId equals user.Id
                            where member.OrganizationId == db.OrganizationId select new { user.Id, user.DisplayName }).ToListAsync(ct);
        List<Candidate> candidates = [];
        foreach (var person in people)
        {
            var evaluation = await eligibility.EvaluateAsync(shift, person.Id, ct);
            var reasons = person.Id == (request?.RequesterUserId ?? shift.AssigneeUserId)
                ? new List<Rejection> { new("self", "The current assignee or requester cannot replace themselves.") } : evaluation.Reasons;
            candidates.Add(new Candidate(person.Id, person.DisplayName, evaluation.AssignedMinutes, covers.GetValueOrDefault(person.Id), reasons));
        }
        var eligible = candidates.Where(x => x.Reasons.Count == 0).OrderBy(x => x.AssignedMinutes).ThenBy(x => x.RecentCovers).ThenBy(x => x.UserId)
            .Select((x, index) => x with { Rank = index + 1 }).ToArray();
        return new CandidateSet("least-load-v1", now, shift.Version, request is null ? null : new PendingRequest(request.Id, request.Version), eligible, candidates.Where(x => x.Reasons.Count > 0).OrderBy(x => x.UserId).ToArray());
    }
}
