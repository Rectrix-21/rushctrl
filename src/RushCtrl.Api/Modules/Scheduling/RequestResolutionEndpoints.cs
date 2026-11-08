using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public static class RequestResolutionEndpoints
{
    public static void MapRequestResolutionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/audit", async (RushCtrlDbContext db, int? limit, long? beforeNumber) =>
        {
            if (limit is < 1 or > 100 || beforeNumber is <= 0) throw new ScheduleRuleException("Use an audit limit from 1 to 100 and a positive cursor.");
            return Results.Ok(await (from item in db.AuditEntries.AsNoTracking() join actor in db.Users on item.ActorUserId equals actor.Id
                                     where beforeNumber == null || item.Number < beforeNumber orderby item.Number descending
                                     select new { item.Number, item.ActorUserId, actorName = actor.DisplayName, item.CreatedAt, item.Action, item.ResourceId, item.Generation, item.Before, item.After }).Take(limit ?? 50).ToListAsync());
        }).AddEndpointFilter<ManagerFilter>();
        group.MapPost("/coverage-requests/{id:guid}/approve", async (Guid id, ApprovalInput input, RushCtrlDbContext db, TenantScope tenant, AssignmentService assignment, TimeProvider clock, CancellationToken ct) =>
        {
            var request = await Find(db, id, ct); var shift = await assignment.LockShiftAsync(request.ShiftId, ct);
            Check(request, input.Version);
            if (shift.Version != input.ShiftVersion || shift.StartsAt <= clock.GetUtcNow()) throw new ScheduleRuleException("This shift changed or has started. Refresh the schedule.", 409);
            if (input.UserId == request.RequesterUserId || (request.Kind == "Standard" ? shift.AssigneeUserId != request.RequesterUserId : shift.AssigneeUserId is not null)) throw new ScheduleRuleException("The current assignment no longer matches this request or replacement.", 409);
            await assignment.AssignMemberAsync(shift, input.UserId, ct);
            Resolve(request, "Approved", tenant.UserId, clock.GetUtcNow(), input.UserId);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { request.Id, request.Status, request.Version, shiftVersion = shift.Version });
        }).AddEndpointFilter<ManagerFilter>();
        group.MapPost("/coverage-requests/{id:guid}/reject", async (Guid id, ResolutionInput input, RushCtrlDbContext db, TenantScope tenant, AssignmentService assignment, TimeProvider clock, CancellationToken ct) =>
        {
            var request = await Find(db, id, ct); var shift = await assignment.LockShiftAsync(request.ShiftId, ct);
            Check(request, input.Version); Resolve(request, "Rejected", tenant.UserId, clock.GetUtcNow()); shift.Version = Guid.NewGuid(); await db.SaveChangesAsync(ct);
            return Results.Ok(new { request.Id, request.Status, request.Version });
        }).AddEndpointFilter<ManagerFilter>();
        group.MapPost("/coverage-requests/{id:guid}/cancel", async (Guid id, ResolutionInput input, RushCtrlDbContext db, TenantScope tenant, AssignmentService assignment, TimeProvider clock, CancellationToken ct) =>
        {
            var request = await Find(db, id, ct);
            if (request.RequesterUserId != tenant.UserId) throw new ScheduleRuleException("Request not found.", 404);
            var shift = await assignment.LockShiftAsync(request.ShiftId, ct);
            Check(request, input.Version); Resolve(request, "Cancelled", tenant.UserId, clock.GetUtcNow()); shift.Version = Guid.NewGuid(); await db.SaveChangesAsync(ct);
            return Results.Ok(new { request.Id, request.Status, request.Version });
        });
    }
    private static async Task<CoverageRequest> Find(RushCtrlDbContext db, Guid id, CancellationToken ct) => await db.CoverageRequests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ScheduleRuleException("Request not found.", 404);
    private static void Check(CoverageRequest request, Guid version) { if (request.Status != "Pending" || request.Version != version) throw new ScheduleRuleException("This request changed. Refresh before resolving it.", 409); }
    private static void Resolve(CoverageRequest request, string status, Guid actor, DateTimeOffset now, Guid? replacement = null)
    { request.Status = status; request.Version = Guid.NewGuid(); request.ResolvedAt = now; request.ResolvedByUserId = actor; request.ReplacementUserId = replacement; }
    public sealed record ResolutionInput(Guid Version);
    public sealed record ApprovalInput(Guid Version, Guid ShiftVersion, Guid UserId);
}
