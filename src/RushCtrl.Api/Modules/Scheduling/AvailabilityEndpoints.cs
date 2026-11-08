using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public static class AvailabilityEndpoints
{
    public static void MapAvailabilityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/availability", async (Guid locationId, string week, RushCtrlDbContext db, TenantScope tenant) =>
        {
            var location = await SchedulingEndpoints.Location(db, locationId);
            var bounds = ScheduleTime.Week(week, location.TimeZoneId);
            return Results.Ok(await db.AvailabilityWindows.AsNoTracking().Where(x => x.LocationId == locationId && x.StartsAt < bounds.End && bounds.Start < x.EndsAt && (tenant.IsManager || x.UserId == tenant.UserId))
                .OrderBy(x => x.StartsAt).Select(x => new { x.Id, x.UserId, x.LocationId, x.StartsAt, x.EndsAt }).ToListAsync());
        });
        group.MapPost("/availability", async (AvailabilityInput input, RushCtrlDbContext db, TenantScope tenant) =>
        {
            var location = await SchedulingEndpoints.Location(db, input.LocationId);
            var interval = ScheduleTime.Interval(input.StartsAt, input.EndsAt, location.TimeZoneId);
            var window = new AvailabilityWindow { OrganizationId = tenant.OrganizationId, LocationId = location.Id, UserId = tenant.UserId, StartsAt = interval.Start, EndsAt = interval.End };
            db.AvailabilityWindows.Add(window);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/organizations/{tenant.OrganizationId}/availability/{window.Id}", new { window.Id, window.UserId, window.LocationId, window.StartsAt, window.EndsAt });
        });
    }
    public sealed record AvailabilityInput(Guid LocationId, string? StartsAt, string? EndsAt);
}
