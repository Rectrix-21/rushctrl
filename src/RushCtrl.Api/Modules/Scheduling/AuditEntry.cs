namespace RushCtrl.Api.Modules.Scheduling;

public sealed class AuditEntry
{
    public Guid OrganizationId { get; set; }
    public long Number { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required string Action { get; set; }
    public Guid ResourceId { get; set; }
    public Guid? Generation { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
}

public sealed record AuditDraft(Guid OrganizationId, string Action, Guid ResourceId, string? Before, string? After, Guid? SubjectUserId = null);
