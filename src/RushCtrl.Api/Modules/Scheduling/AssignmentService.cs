using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public sealed class AssignmentService(RushCtrlDbContext db, TenantScope tenant)
{
    public async Task<Shift> AssignAsync(Guid shiftId, Guid? userId, Guid version, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var shift = await db.Shifts.FromSqlInterpolated($"SELECT * FROM \"Shifts\" WHERE \"Id\" = {shiftId} AND \"OrganizationId\" = {tenant.OrganizationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ScheduleRuleException("Shift not found.", 404);
        if (shift.Version != version) throw new ScheduleRuleException("This shift changed. Refresh it before assigning.", 409);
        if (await db.CoverageRequests.AnyAsync(x => x.ShiftId == shift.Id, cancellationToken))
            throw new ScheduleRuleException("This shift has a pending coverage request. Request resolution arrives in Milestone 2.", 409);
        if (userId is not null)
        {
            var member = await db.Memberships.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.UserId == userId, cancellationToken)
                ?? throw new ScheduleRuleException("Employee not found.", 404);
            // One lock per person serializes validation across shifts and organizations.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE", cancellationToken);
            if (!await db.EmployeeSkills.AnyAsync(x => x.UserId == userId && x.SkillId == shift.SkillId, cancellationToken))
                throw new ScheduleRuleException("This employee does not have the required qualification.", 409);
            if (await db.Shifts.IgnoreQueryFilters().AnyAsync(x => x.Id != shift.Id && x.AssigneeUserId == userId && x.StartsAt < shift.EndsAt && shift.StartsAt < x.EndsAt, cancellationToken))
                throw new ScheduleRuleException("This employee already has an overlapping shift.", 409);
            var availability = await db.AvailabilityWindows.Where(x => x.UserId == userId && x.LocationId == shift.LocationId && x.StartsAt < shift.EndsAt && shift.StartsAt < x.EndsAt)
                .OrderBy(x => x.StartsAt).ToListAsync(cancellationToken);
            var cursor = shift.StartsAt;
            foreach (var window in availability)
            {
                if (window.StartsAt > cursor) break;
                if (window.EndsAt > cursor) cursor = window.EndsAt;
            }
            if (cursor < shift.EndsAt) throw new ScheduleRuleException("Declared availability must cover the entire shift.", 409);
            var zone = (await db.Locations.SingleAsync(x => x.Id == shift.LocationId, cancellationToken)).TimeZoneId;
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(zone);
            var localStart = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(shift.StartsAt, timezone).DateTime);
            var monday = localStart.AddDays(-(((int)localStart.DayOfWeek + 6) % 7));
            for (var week = monday; ; week = week.AddDays(7))
            {
                var bounds = ScheduleTime.Week(week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), zone);
                if (bounds.Start >= shift.EndsAt) break;
                var assigned = await db.Shifts.Where(x => x.Id != shift.Id && x.AssigneeUserId == userId && x.StartsAt < bounds.End && bounds.Start < x.EndsAt).ToListAsync(cancellationToken);
                var hours = assigned.Append(shift).Sum(x => (Min(x.EndsAt, bounds.End) - Max(x.StartsAt, bounds.Start)).TotalHours);
                if (hours > member.MaxWeeklyHours) throw new ScheduleRuleException($"Assignment exceeds the configured weekly limit of {member.MaxWeeklyHours} hours.", 409);
            }
        }
        shift.AssigneeUserId = userId;
        shift.Version = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return shift;
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
