using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RushCtrl.Api.Modules.Scheduling;

public static class AuditCapture
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static AuditDraft? From(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) return null;
        var (organization, id, name, subject) = entry.Entity switch
        {
            Shift x => (x.OrganizationId, x.Id, entry.State == EntityState.Added ? "Shift.Created" : entry.Property(nameof(Shift.AssigneeUserId)).IsModified ? "Shift.AssignmentChanged" : "Shift.Changed", x.AssigneeUserId),
            CoverageRequest x => (x.OrganizationId, x.Id, entry.State == EntityState.Added ? "CoverageRequest.Created" : "CoverageRequest." + x.Status, x.ReplacementUserId),
            AvailabilityWindow x => (x.OrganizationId, x.Id, "Availability.Declared", (Guid?)x.UserId),
            StaffingRequirement x => (x.OrganizationId, x.Id, entry.State == EntityState.Added ? "StaffingTarget.Created" : "StaffingTarget.Changed", (Guid?)null),
            _ => (Guid.Empty, Guid.Empty, "", (Guid?)null)
        };
        if (organization == Guid.Empty) return null;
        return new AuditDraft(organization, name, id,
            entry.State == EntityState.Added ? null : JsonSerializer.Serialize(entry.OriginalValues.ToObject(), Json),
            entry.State == EntityState.Deleted ? null : JsonSerializer.Serialize(entry.CurrentValues.ToObject(), Json), subject);
    }
}
