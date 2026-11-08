namespace RushCtrl.Api.Modules.Scheduling;

public sealed class SimulationWorkspace
{
    public Guid OrganizationId { get; set; }
    public Guid SourceOrganizationId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid Generation { get; set; } = Guid.NewGuid();
    public DateOnly Week { get; set; }
    public bool Applied { get; set; }
    public bool Pristine { get; set; } = true;
    public Guid LocationId { get; set; }
    public Guid OtherLocationId { get; set; }
    public Guid SkillId { get; set; }
    public Guid LeadSkillId { get; set; }
    public Guid AbsenceShiftId { get; set; }
    public Guid StandbyShiftId { get; set; }
    public Guid TargetId { get; set; }
    public string MemberIds { get; set; } = "[]";
}
