namespace RushCtrl.Api.Modules.Scheduling;

public sealed class Skill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
}

public sealed class EmployeeSkill
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
}

public sealed class AvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid LocationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
}

public sealed class Shift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid LocationId { get; set; }
    public Guid SkillId { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public sealed class StaffingRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid LocationId { get; set; }
    public Guid SkillId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int RequiredCount { get; set; }
}

public sealed class CoverageRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid RequesterUserId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Kind { get; set; } = "Standard";
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public Guid? ReplacementUserId { get; set; }
}
