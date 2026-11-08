using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Scheduling;

public sealed class AssignmentService(RushCtrlDbContext db, TenantScope tenant, EligibilityService eligibility)
{
    public async Task<Shift> LockShiftAsync(Guid shiftId, CancellationToken ct) =>
        await db.Shifts.FromSqlInterpolated($"SELECT * FROM \"Shifts\" WHERE \"Id\" = {shiftId} AND \"OrganizationId\" = {tenant.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new ScheduleRuleException("Shift not found.", 404);
    public async Task AssignMemberAsync(Shift shift, Guid userId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE", ct);
        await eligibility.RequireAsync(shift, userId, ct);
        shift.AssigneeUserId = userId; shift.Version = Guid.NewGuid();
    }
    public async Task<Shift> AssignAsync(Guid shiftId, Guid? userId, Guid version, CancellationToken cancellationToken)
    {
        var shift = await LockShiftAsync(shiftId, cancellationToken);
        if (shift.Version != version) throw new ScheduleRuleException("This shift changed. Refresh it before assigning.", 409);
        if (await db.CoverageRequests.AnyAsync(x => x.ShiftId == shift.Id && x.Status == "Pending", cancellationToken)) throw new ScheduleRuleException("Resolve the pending coverage request before changing this assignment.", 409);
        if (userId is not null) await AssignMemberAsync(shift, userId.Value, cancellationToken);
        else { shift.AssigneeUserId = null; shift.Version = Guid.NewGuid(); }
        await db.SaveChangesAsync(cancellationToken);
        return shift;
    }
}
