using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Modules.Scheduling;

public sealed record Rejection(string Code, string Message);
public sealed record Eligibility(IReadOnlyList<Rejection> Reasons, double AssignedMinutes);

public sealed class EligibilityService(RushCtrlDbContext db)
{
    public async Task<Eligibility> EvaluateAsync(Shift shift, Guid userId, CancellationToken ct = default)
    {
        List<Rejection> reasons = [];
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == db.OrganizationId && x.UserId == userId, ct);
        if (member is null) return new Eligibility([new("not_member", "Employee not found.")], 0);
        if (!await db.EmployeeSkills.AnyAsync(x => x.UserId == userId && x.SkillId == shift.SkillId, ct)) reasons.Add(new("qualification", "This employee does not have the required qualification."));
        if (await db.Shifts.IgnoreQueryFilters().AnyAsync(x => x.Id != shift.Id && x.AssigneeUserId == userId && x.StartsAt < shift.EndsAt && shift.StartsAt < x.EndsAt, ct)) reasons.Add(new("overlap", "This employee already has an overlapping shift."));
        var availability = await db.AvailabilityWindows.AsNoTracking().Where(x => x.UserId == userId && x.LocationId == shift.LocationId && x.StartsAt < shift.EndsAt && shift.StartsAt < x.EndsAt).OrderBy(x => x.StartsAt).ToListAsync(ct);
        var cursor = shift.StartsAt;
        foreach (var window in availability) { if (window.StartsAt > cursor) break; if (window.EndsAt > cursor) cursor = window.EndsAt; }
        if (cursor < shift.EndsAt) reasons.Add(new("availability", "Declared availability must cover the entire shift."));
        var zone = (await db.Locations.AsNoTracking().SingleAsync(x => x.Id == shift.LocationId, ct)).TimeZoneId;
        var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(shift.StartsAt, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
        var monday = local.AddDays(-(((int)local.DayOfWeek + 6) % 7));
        var minutes = 0d;
        for (var week = monday; ; week = week.AddDays(7))
        {
            var bounds = ScheduleTime.Week(week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), zone);
            if (bounds.Start >= shift.EndsAt) break;
            var assigned = await db.Shifts.AsNoTracking().Where(x => x.Id != shift.Id && x.AssigneeUserId == userId && x.StartsAt < bounds.End && bounds.Start < x.EndsAt).ToListAsync(ct);
            var existing = assigned.Sum(x => (Min(x.EndsAt, bounds.End) - Max(x.StartsAt, bounds.Start)).TotalMinutes);
            minutes += existing;
            var proposed = (Min(shift.EndsAt, bounds.End) - Max(shift.StartsAt, bounds.Start)).TotalMinutes;
            if (existing + proposed > member.MaxWeeklyHours * 60 && reasons.All(x => x.Code != "hours")) reasons.Add(new("hours", $"Assignment exceeds the configured weekly limit of {member.MaxWeeklyHours} hours."));
        }
        return new Eligibility(reasons, minutes);
    }
    public async Task RequireAsync(Shift shift, Guid userId, CancellationToken ct)
    {
        var result = await EvaluateAsync(shift, userId, ct);
        if (result.Reasons.Count > 0) throw new ScheduleRuleException(result.Reasons[0].Message, result.Reasons[0].Code == "not_member" ? 404 : 409);
    }
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
