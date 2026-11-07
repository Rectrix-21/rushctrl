using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public static class SchedulingEndpoints
{
    public static void MapSchedulingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}")
            .AddEndpointFilter<MembershipFilter>().AddEndpointFilter<CsrfFilter>();
        group.MapGet("/workspace", async (RushCtrlDbContext db, TenantScope tenant) =>
        {
            var organization = await db.Organizations.SingleAsync(x => x.Id == tenant.OrganizationId);
            var locations = await db.Locations.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.TimeZoneId }).ToListAsync();
            return Results.Ok(new { organizationName = organization.Name, permission = tenant.IsManager ? "Manager" : "Employee", locations });
        });
    }
}
