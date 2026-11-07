using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public static class SchedulingEndpoints
{
    public static void MapSchedulingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}")
            .AddEndpointFilter<ScheduleErrors>().AddEndpointFilter<MembershipFilter>().AddEndpointFilter<CsrfFilter>();
        group.MapGet("/workspace", async (RushCtrlDbContext db, TenantScope tenant, TimeProvider clock) =>
        {
            var organization = await db.Organizations.SingleAsync(x => x.Id == tenant.OrganizationId);
            var locations = await db.Locations.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.TimeZoneId }).ToListAsync();
            var skills = await db.Skills.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync();
            var people = await (from m in db.Memberships join u in db.Users on m.UserId equals u.Id
                                where m.OrganizationId == tenant.OrganizationId && (tenant.IsManager || m.UserId == tenant.UserId)
                                orderby u.DisplayName select new { u.Id, u.DisplayName, m.MaxWeeklyHours }).ToListAsync();
            var qualifications = await db.EmployeeSkills.Where(x => tenant.IsManager || x.UserId == tenant.UserId).Select(x => new { x.UserId, x.SkillId }).ToListAsync();
            var next = await db.Shifts.Where(x => x.EndsAt > clock.GetUtcNow() && (tenant.IsManager || x.AssigneeUserId == tenant.UserId)).OrderBy(x => x.StartsAt).Select(x => (DateTimeOffset?)x.StartsAt).FirstOrDefaultAsync();
            var zone = TimeZoneInfo.FindSystemTimeZoneById(locations.FirstOrDefault()?.TimeZoneId ?? "UTC");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(next ?? clock.GetUtcNow(), zone).DateTime);
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            return Results.Ok(new { organizationName = organization.Name, permission = tenant.IsManager ? "Manager" : "Employee", locations, skills, people, qualifications, suggestedWeek = monday.ToString("yyyy-MM-dd") });
        });
        group.MapGet("/shifts", async (Guid locationId, string week, RushCtrlDbContext db, TenantScope tenant) =>
        {
            var location = await Location(db, locationId);
            var bounds = ScheduleTime.Week(week, location.TimeZoneId);
            var query = db.Shifts.AsNoTracking().Where(x => x.LocationId == locationId && x.StartsAt < bounds.End && bounds.Start < x.EndsAt && (tenant.IsManager || x.AssigneeUserId == tenant.UserId)).OrderBy(x => x.StartsAt);
            return Results.Ok(await Views(query, db).ToListAsync());
        });
        group.MapPost("/shifts", async (ShiftInput input, RushCtrlDbContext db, TenantScope tenant) =>
        {
            var location = await Location(db, input.LocationId);
            var interval = ScheduleTime.Interval(input.StartsAt, input.EndsAt, location.TimeZoneId);
            if (!await db.Skills.AnyAsync(x => x.Id == input.SkillId)) throw new ScheduleRuleException("Skill not found.", 404);
            var shift = new Shift { OrganizationId = tenant.OrganizationId, LocationId = location.Id, SkillId = input.SkillId, StartsAt = interval.Start, EndsAt = interval.End };
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/organizations/{tenant.OrganizationId}/shifts/{shift.Id}", View(shift));
        }).AddEndpointFilter<ManagerFilter>();
        group.MapPatch("/shifts/{id:guid}/assignment", async (Guid id, AssignmentInput input, AssignmentService service, RushCtrlDbContext db, CancellationToken ct) =>
        {
            var shift = await service.AssignAsync(id, input.UserId, input.Version, ct);
            return Results.Ok(await Views(db.Shifts.Where(x => x.Id == shift.Id), db).SingleAsync(ct));
        }).AddEndpointFilter<ManagerFilter>();
        group.MapAvailabilityEndpoints();
        group.MapCoverageEndpoints();
    }

    public static async Task<Organizations.Location> Location(RushCtrlDbContext db, Guid id) =>
        await db.Locations.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ScheduleRuleException("Location not found.", 404);

    public static IQueryable<ShiftView> Views(IQueryable<Shift> shifts, RushCtrlDbContext db) => shifts.Select(x => new ShiftView(
        x.Id, x.LocationId, x.SkillId, x.StartsAt, x.EndsAt, x.AssigneeUserId,
        db.Users.Where(u => u.Id == x.AssigneeUserId).Select(u => u.DisplayName).FirstOrDefault(), x.Version));
    private static ShiftView View(Shift x) => new(x.Id, x.LocationId, x.SkillId, x.StartsAt, x.EndsAt, x.AssigneeUserId, null, x.Version);
    public sealed record ShiftView(Guid Id, Guid LocationId, Guid SkillId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid? AssigneeUserId, string? AssigneeName, Guid Version);
    public sealed record ShiftInput(Guid LocationId, Guid SkillId, string? StartsAt, string? EndsAt);
    public sealed record AssignmentInput(Guid? UserId, Guid Version);
}
