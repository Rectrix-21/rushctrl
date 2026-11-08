using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Modules.Scheduling;

public static class AuditCapture
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task<AuditDraft?> FromAsync(RushCtrlDbContext db, EntityEntry entry, CancellationToken ct)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) return null;
        var (organization, id, name, subject) = entry.Entity switch
        {
            Shift x => (x.OrganizationId, x.Id, entry.State == EntityState.Added ? "Shift.Created" : entry.Property(nameof(Shift.AssigneeUserId)).IsModified ? "Shift.AssignmentChanged" : "Shift.Changed", x.AssigneeUserId),
            CoverageRequest x => (x.OrganizationId, x.Id, entry.State == EntityState.Added ? "CoverageRequest.Created" : "CoverageRequest." + x.Status, x.ReplacementUserId),
            AvailabilityWindow x => (x.OrganizationId, x.Id, entry.State == EntityState.Deleted ? "Availability.Removed" : "Availability.Declared", (Guid?)x.UserId),
            StaffingRequirement x => (x.OrganizationId, x.Id, entry.State == EntityState.Added ? "StaffingTarget.Created" : "StaffingTarget.Changed", (Guid?)null),
            _ => (Guid.Empty, Guid.Empty, "", (Guid?)null)
        };
        if (organization == Guid.Empty) return null;
        var before = entry.State == EntityState.Added ? null : await Snapshot(db, entry.OriginalValues.ToObject(), original: true, ct);
        var after = entry.State == EntityState.Deleted ? null : await Snapshot(db, entry.CurrentValues.ToObject(), original: false, ct);
        return new AuditDraft(organization, name, id, before is null ? null : JsonSerializer.Serialize(before, Json), after is null ? null : JsonSerializer.Serialize(after, Json), subject);
    }

    // Explicit snapshots retain context after a reset deletes the scheduling records.
    private static async Task<object?> Snapshot(RushCtrlDbContext db, object entity, bool original, CancellationToken ct)
    {
        switch (entity)
        {
            case Shift shift:
                return await ShiftSnapshot(db, shift, ct);
            case CoverageRequest request:
                var tracked = db.ChangeTracker.Entries<Shift>().FirstOrDefault(x => x.Entity.OrganizationId == request.OrganizationId && x.Entity.Id == request.ShiftId);
                var requestShift = tracked is null
                    ? await db.Shifts.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.OrganizationId == request.OrganizationId && x.Id == request.ShiftId, ct)
                    : original ? (Shift)tracked.OriginalValues.ToObject() : tracked.Entity;
                return new { request.Id, request.OrganizationId, request.Kind, request.Status, request.Version, request.CreatedAt, request.ResolvedAt,
                    requester = await Person(db, request.RequesterUserId, ct), replacement = await Person(db, request.ReplacementUserId, ct),
                    shift = await ShiftSnapshot(db, requestShift, ct) };
            case AvailabilityWindow window:
                return new { window.Id, window.OrganizationId, person = await Person(db, window.UserId, ct), location = await Location(db, window.OrganizationId, window.LocationId, ct), window.StartsAt, window.EndsAt };
            case StaffingRequirement target:
                return new { target.Id, target.OrganizationId, location = await Location(db, target.OrganizationId, target.LocationId, ct), skill = await Skill(db, target.OrganizationId, target.SkillId, ct), target.StartsAt, target.EndsAt, target.RequiredCount };
            default: return null;
        }
    }
    private static async Task<object> ShiftSnapshot(RushCtrlDbContext db, Shift shift, CancellationToken ct) => new {
        shift.Id, shift.OrganizationId, location = await Location(db, shift.OrganizationId, shift.LocationId, ct),
        skill = await Skill(db, shift.OrganizationId, shift.SkillId, ct), shift.StartsAt, shift.EndsAt,
        assignee = await Person(db, shift.AssigneeUserId, ct), shift.Version
    };
    private static async Task<object?> Person(RushCtrlDbContext db, Guid? id, CancellationToken ct) => id is null ? null
        : await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, name = x.DisplayName }).SingleAsync(ct);
    private static async Task<object> Location(RushCtrlDbContext db, Guid organization, Guid id, CancellationToken ct) =>
        await db.Locations.IgnoreQueryFilters().AsNoTracking().Where(x => x.OrganizationId == organization && x.Id == id).Select(x => new { x.Id, x.Name, x.TimeZoneId }).SingleAsync(ct);
    private static async Task<object> Skill(RushCtrlDbContext db, Guid organization, Guid id, CancellationToken ct) =>
        await db.Skills.IgnoreQueryFilters().AsNoTracking().Where(x => x.OrganizationId == organization && x.Id == id).Select(x => new { x.Id, x.Name }).SingleAsync(ct);
}
