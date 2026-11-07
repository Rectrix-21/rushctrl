namespace RushCtrl.Api.Modules.Scheduling;

public static class CoverageCalculator
{
    public sealed record Gap(Guid RequirementId, Guid LocationId, Guid SkillId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int RequiredCount, int AssignedCount, int MissingCount);
    public static List<Gap> Calculate(IEnumerable<StaffingRequirement> requirements, IEnumerable<Shift> shifts)
    {
        var assigned = shifts.Where(x => x.AssigneeUserId is not null).ToArray();
        var result = new List<Gap>();
        foreach (var target in requirements.OrderBy(x => x.StartsAt))
        {
            var relevant = assigned.Where(x => x.OrganizationId == target.OrganizationId && x.LocationId == target.LocationId && x.SkillId == target.SkillId && x.StartsAt < target.EndsAt && target.StartsAt < x.EndsAt).ToArray();
            var points = relevant.SelectMany(x => new[] { x.StartsAt > target.StartsAt ? x.StartsAt : target.StartsAt, x.EndsAt < target.EndsAt ? x.EndsAt : target.EndsAt })
                .Append(target.StartsAt).Append(target.EndsAt).Distinct().Order().ToArray();
            for (var i = 0; i < points.Length - 1; i++)
            {
                var count = relevant.Count(x => x.StartsAt <= points[i] && x.EndsAt >= points[i + 1]);
                if (count >= target.RequiredCount) continue;
                if (result.Count > 0 && result[^1].RequirementId == target.Id && result[^1].EndsAt == points[i] && result[^1].AssignedCount == count)
                    result[^1] = result[^1] with { EndsAt = points[i + 1] };
                else result.Add(new Gap(target.Id, target.LocationId, target.SkillId, points[i], points[i + 1], target.RequiredCount, count, target.RequiredCount - count));
            }
        }
        return result;
    }
}
