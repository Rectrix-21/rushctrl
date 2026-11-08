using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public static class CoverageEndpoints
{
    public static void MapCoverageEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/staffing-requirements", async (Guid locationId, string week, RushCtrlDbContext db) =>
        {
            var location = await SchedulingEndpoints.Location(db, locationId);
            var bounds = ScheduleTime.Week(week, location.TimeZoneId);
            return Results.Ok(await db.StaffingRequirements.AsNoTracking().Where(x => x.LocationId == locationId && x.StartsAt < bounds.End && bounds.Start < x.EndsAt).OrderBy(x => x.StartsAt)
                .Select(x => new { x.Id, x.LocationId, x.SkillId, x.StartsAt, x.EndsAt, x.RequiredCount }).ToListAsync());
        }).AddEndpointFilter<ManagerFilter>();
        group.MapPost("/staffing-requirements", async (RequirementInput input, RushCtrlDbContext db, TenantScope tenant) =>
        {
            var location = await SchedulingEndpoints.Location(db, input.LocationId);
            var interval = ScheduleTime.Interval(input.StartsAt, input.EndsAt, location.TimeZoneId);
            if (input.RequiredCount is < 1 or > 100) throw new ScheduleRuleException("Required headcount must be between 1 and 100.");
            if (!await db.Skills.AnyAsync(x => x.Id == input.SkillId)) throw new ScheduleRuleException("Skill not found.", 404);
            if (await db.StaffingRequirements.AnyAsync(x => x.LocationId == location.Id && x.SkillId == input.SkillId && x.StartsAt < interval.End && interval.Start < x.EndsAt))
                throw new ScheduleRuleException("A staffing target already overlaps this skill and period.", 409);
            var target = new StaffingRequirement { OrganizationId = tenant.OrganizationId, LocationId = location.Id, SkillId = input.SkillId, StartsAt = interval.Start, EndsAt = interval.End, RequiredCount = input.RequiredCount };
            db.StaffingRequirements.Add(target);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/organizations/{tenant.OrganizationId}/staffing-requirements/{target.Id}", new { target.Id });
        }).AddEndpointFilter<ManagerFilter>();
        group.MapGet("/coverage", async (Guid locationId, string week, RushCtrlDbContext db) =>
        {
            var location = await SchedulingEndpoints.Location(db, locationId);
            var bounds = ScheduleTime.Week(week, location.TimeZoneId);
            var targets = await db.StaffingRequirements.AsNoTracking().Where(x => x.LocationId == locationId && x.StartsAt < bounds.End && bounds.Start < x.EndsAt).ToListAsync();
            foreach (var target in targets)
            {
                if (target.StartsAt < bounds.Start) target.StartsAt = bounds.Start;
                if (target.EndsAt > bounds.End) target.EndsAt = bounds.End;
            }
            var shifts = await db.Shifts.AsNoTracking().Where(x => x.LocationId == locationId && x.StartsAt < bounds.End && bounds.Start < x.EndsAt).ToListAsync();
            return Results.Ok(CoverageCalculator.Calculate(targets, shifts));
        }).AddEndpointFilter<ManagerFilter>();
        group.MapGet("/coverage-requests", async (Guid locationId, string week, RushCtrlDbContext db, TenantScope tenant) =>
        {
            var location = await SchedulingEndpoints.Location(db, locationId);
            var bounds = ScheduleTime.Week(week, location.TimeZoneId);
            return Results.Ok(await (from request in db.CoverageRequests.AsNoTracking()
                                     join shift in db.Shifts on request.ShiftId equals shift.Id
                                     join user in db.Users on request.RequesterUserId equals user.Id
                                     where shift.LocationId == locationId && shift.StartsAt < bounds.End && bounds.Start < shift.EndsAt && (tenant.IsManager || request.RequesterUserId == tenant.UserId)
                                     orderby request.CreatedAt descending
                                     select new { request.Id, request.ShiftId, request.RequesterUserId, requesterName = user.DisplayName, request.Status, request.Kind, request.Version, request.ReplacementUserId, replacementName = db.Users.Where(x => x.Id == request.ReplacementUserId).Select(x => x.DisplayName).FirstOrDefault(), request.ResolvedAt, request.CreatedAt, shift.StartsAt, shift.EndsAt, shift.SkillId, shiftVersion = shift.Version }).ToListAsync());
        });
        group.MapPost("/coverage-requests", async (RequestInput input, RushCtrlDbContext db, TenantScope tenant, TimeProvider clock) =>
        {
            var shift = await db.Shifts.FromSqlInterpolated($"SELECT * FROM \"Shifts\" WHERE \"Id\" = {input.ShiftId} AND \"OrganizationId\" = {tenant.OrganizationId} FOR UPDATE").SingleOrDefaultAsync();
            if (shift is null || shift.AssigneeUserId != tenant.UserId) throw new ScheduleRuleException("Assigned shift not found.", 404);
            if (shift.StartsAt <= clock.GetUtcNow()) throw new ScheduleRuleException("Coverage can only be requested before a shift starts.", 409);
            if (await db.CoverageRequests.AnyAsync(x => x.ShiftId == shift.Id && x.Status == "Pending")) throw new ScheduleRuleException("This shift already has a pending request.", 409);
            var request = new CoverageRequest { OrganizationId = tenant.OrganizationId, ShiftId = shift.Id, RequesterUserId = tenant.UserId, CreatedAt = clock.GetUtcNow() };
            db.CoverageRequests.Add(request);
            shift.Version = Guid.NewGuid();
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/organizations/{tenant.OrganizationId}/coverage-requests/{request.Id}", new { request.Id, request.ShiftId, request.RequesterUserId, request.Status, request.Kind, request.Version, request.CreatedAt });
        });
    }
    public sealed record RequirementInput(Guid LocationId, Guid SkillId, string? StartsAt, string? EndsAt, int RequiredCount);
    public sealed record RequestInput(Guid ShiftId);
}
