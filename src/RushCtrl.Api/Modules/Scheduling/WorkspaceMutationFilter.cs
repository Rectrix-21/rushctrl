using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public interface IWorkspaceNotifier
{
    Task ChangedAsync(Guid organizationId, Guid? generation, CancellationToken cancellationToken);
}

public sealed class NullWorkspaceNotifier : IWorkspaceNotifier
{
    public Task ChangedAsync(Guid organizationId, Guid? generation, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class WorkspaceMutationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method)) return await next(context);
        var db = http.RequestServices.GetRequiredService<RushCtrlDbContext>();
        var tenant = http.RequestServices.GetRequiredService<TenantScope>();
        var clock = http.RequestServices.GetRequiredService<TimeProvider>();
        await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Organizations\" WHERE \"Id\" = {tenant.OrganizationId} FOR UPDATE", http.RequestAborted);
        var simulation = await db.SimulationWorkspaces.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId, http.RequestAborted);
        if (simulation is not null)
        {
            var header = http.Request.Headers["X-Workspace-Generation"].ToString();
            if (!string.IsNullOrEmpty(header) && !Guid.TryParse(header, out _)) throw new ScheduleRuleException("Invalid simulation generation.");
            if (!Guid.TryParse(header, out var generation) || generation != simulation.Generation)
                throw new ScheduleRuleException("This simulation was reset. Reload the workspace before saving.", 409);
        }
        db.CaptureAudit = true;
        var result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 }) return result;
        if (simulation is not null && !http.Request.Path.Value!.Contains("/simulation/", StringComparison.Ordinal)) simulation.Pristine = false;
        db.CaptureAudit = false;
        var changedOrganizations = db.AuditChanges.Select(x => x.OrganizationId).Append(tenant.OrganizationId).Distinct().ToArray();
        foreach (var organization in db.AuditChanges.GroupBy(x => x.OrganizationId))
        {
            var number = await db.AuditEntries.IgnoreQueryFilters().Where(x => x.OrganizationId == organization.Key).MaxAsync(x => (long?)x.Number, http.RequestAborted) ?? 0;
            var generation = await db.SimulationWorkspaces.Where(x => x.OrganizationId == organization.Key).Select(x => (Guid?)x.Generation).SingleOrDefaultAsync(http.RequestAborted);
            // A reset's tracked generation has not been flushed yet.
            generation = db.SimulationWorkspaces.Local.FirstOrDefault(x => x.OrganizationId == organization.Key)?.Generation ?? generation;
            foreach (var change in organization)
                db.AuditEntries.Add(new AuditEntry { OrganizationId = organization.Key, Number = ++number, ActorUserId = tenant.UserId, CreatedAt = clock.GetUtcNow(), Action = change.Action, ResourceId = change.ResourceId, Before = change.Before, After = change.After, SubjectUserId = change.SubjectUserId, Generation = generation });
        }
        await db.SaveChangesAsync(http.RequestAborted);
        await transaction.CommitAsync(http.RequestAborted);
        foreach (var organizationId in changedOrganizations)
        {
            var generation = db.SimulationWorkspaces.Local.FirstOrDefault(x => x.OrganizationId == organizationId)?.Generation;
            try { await http.RequestServices.GetRequiredService<IWorkspaceNotifier>().ChangedAsync(organizationId, generation, http.RequestAborted); }
            catch (Exception error) { http.RequestServices.GetRequiredService<ILogger<WorkspaceMutationFilter>>().LogWarning(error, "Committed workspace change could not be announced"); }
        }
        return result;
    }
}
