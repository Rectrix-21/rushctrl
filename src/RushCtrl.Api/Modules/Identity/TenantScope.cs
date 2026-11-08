using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Modules.Identity;

public sealed class TenantScope
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public bool IsManager { get; set; }
}

public sealed class MembershipFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var organizationId = Guid.Parse((string)http.Request.RouteValues["organizationId"]!);
        var userId = http.User.UserId();
        var db = http.RequestServices.GetRequiredService<RushCtrlDbContext>();
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == userId);
        if (member is null) return Results.Problem(statusCode: 404, title: "Workspace not found.");
        var scope = http.RequestServices.GetRequiredService<TenantScope>();
        scope.OrganizationId = organizationId;
        scope.UserId = userId;
        scope.IsManager = member.Permission == "Manager";
        http.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }
}

public sealed class ManagerFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.RequestServices.GetRequiredService<TenantScope>().IsManager
            ? next(context) : ValueTask.FromResult<object?>(Results.Problem(statusCode: 403, title: "A manager must make this change."));
}
