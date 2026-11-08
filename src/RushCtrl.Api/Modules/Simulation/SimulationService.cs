using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;
using RushCtrl.Api.Modules.Organizations;
using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Modules.Simulation;

public sealed class SimulationService(RushCtrlDbContext db, TenantScope tenant, TimeProvider clock, IConfiguration configuration, IHostEnvironment environment)
{
    public bool Enabled => environment.IsDevelopment() && configuration.GetValue<bool>("Simulation:Enabled");
    public bool CanOpen => Enabled && tenant.OrganizationId == DevelopmentSeed.OrganizationId && tenant.IsManager;
    private void RequireEnabled()
    {
        if (!Enabled) throw new ScheduleRuleException("Simulation not found.", 404);
    }
    public async Task<SimulationWorkspace> Owned(CancellationToken ct)
    {
        RequireEnabled();
        return await db.SimulationWorkspaces.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.OwnerUserId == tenant.UserId, ct)
            ?? throw new ScheduleRuleException("Simulation not found.", 404);
    }
    public async Task<SimulationWorkspace> Open(CancellationToken ct)
    {
        RequireEnabled();
        if (tenant.OrganizationId != DevelopmentSeed.OrganizationId) throw new ScheduleRuleException("Simulation not found.", 404);
        var existing = await db.SimulationWorkspaces.SingleOrDefaultAsync(x => x.SourceOrganizationId == tenant.OrganizationId && x.OwnerUserId == tenant.UserId, ct);
        if (existing is not null) return existing;
        db.CaptureAudit = false;
        var org = new Organization { Name = "Friday Rush Lab" };
        db.Organizations.Add(org);
        db.Memberships.Add(new Membership { OrganizationId = org.Id, UserId = tenant.UserId, Permission = "Manager" });
        var floor = new Location { OrganizationId = org.Id, Name = "Harbour Lab", TimeZoneId = SimulationSeed.Zone };
        var other = new Location { OrganizationId = org.Id, Name = "Junction Lab", TimeZoneId = SimulationSeed.Zone };
        db.Locations.AddRange(floor, other);
        var skill = new Skill { OrganizationId = org.Id, Name = "Barista" };
        var lead = new Skill { OrganizationId = org.Id, Name = "Lead" };
        db.Skills.AddRange(skill, lead);
        var names = new[] { "Cameron", "Lee", "Avery", "Riley", "Quinn", "Jordan", "Casey", "Taylor" };
        var ids = new Guid[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            var id = Guid.NewGuid(); ids[i] = id;
            // These fixture identities have no password or sign-in route.
            db.Users.Add(new AppUser { Id = id, DisplayName = names[i] + " (lab)", UserName = "lab-" + id, NormalizedUserName = "LAB-" + id.ToString().ToUpperInvariant(), SecurityStamp = Guid.NewGuid().ToString() });
            db.Memberships.Add(new Membership { OrganizationId = org.Id, UserId = id });
            db.EmployeeSkills.Add(new EmployeeSkill { OrganizationId = org.Id, UserId = id, SkillId = i == 4 ? lead.Id : skill.Id });
        }
        var lab = new SimulationWorkspace { OrganizationId = org.Id, SourceOrganizationId = tenant.OrganizationId, OwnerUserId = tenant.UserId,
            Week = SimulationSeed.FutureMonday(clock.GetUtcNow()), LocationId = floor.Id, OtherLocationId = other.Id, SkillId = skill.Id, LeadSkillId = lead.Id,
            MemberIds = JsonSerializer.Serialize(ids) };
        db.SimulationWorkspaces.Add(lab);
        SimulationSeed.Restore(db, lab);
        await db.SaveChangesAsync(ct);
        db.AuditChanges.Add(new AuditDraft(org.Id, "Simulation.Created", org.Id, null, JsonSerializer.Serialize(new { lab.Week, lab.Generation })));
        return lab;
    }
    public async Task<SimulationWorkspace> Apply(CancellationToken ct)
    {
        var lab = await Owned(ct);
        var absent = await db.Shifts.SingleOrDefaultAsync(x => x.Id == lab.AbsenceShiftId, ct);
        var target = await db.StaffingRequirements.SingleOrDefaultAsync(x => x.Id == lab.TargetId, ct);
        var ids = JsonSerializer.Deserialize<Guid[]>(lab.MemberIds)!;
        if (!lab.Pristine || lab.Applied || absent is null || target is null || absent.StartsAt <= clock.GetUtcNow()
            || absent.StartsAt != SimulationSeed.At(lab, 4, "14:00") || absent.EndsAt != SimulationSeed.At(lab, 4, "18:00")
            || absent.AssigneeUserId != ids[0] || target.RequiredCount != 2)
            throw new ScheduleRuleException("This baseline was edited, expired or already applied. Reset the lab first.", 409);
        target.RequiredCount = 3;
        absent.AssigneeUserId = null;
        absent.Version = Guid.NewGuid();
        db.AvailabilityWindows.RemoveRange(await db.AvailabilityWindows.Where(x => x.UserId == ids[0]).ToListAsync(ct));
        db.CoverageRequests.Add(new CoverageRequest { OrganizationId = lab.OrganizationId, ShiftId = absent.Id,
            RequesterUserId = ids[0], Kind = "SimulationCallout", CreatedAt = clock.GetUtcNow() });
        lab.Applied = true; lab.Pristine = false;
        await db.SaveChangesAsync(ct);
        db.AuditChanges.Add(new AuditDraft(lab.OrganizationId, "Simulation.Applied", lab.OrganizationId, null, "{\"scenario\":\"Friday Rush\"}"));
        return lab;
    }
    public async Task<SimulationWorkspace> Reset(CancellationToken ct)
    {
        var lab = await Owned(ct);
        db.CaptureAudit = false;
        await db.CoverageRequests.ExecuteDeleteAsync(ct);
        await db.Shifts.ExecuteDeleteAsync(ct);
        await db.AvailabilityWindows.ExecuteDeleteAsync(ct);
        await db.StaffingRequirements.ExecuteDeleteAsync(ct);
        var before = JsonSerializer.Serialize(new { lab.Generation, lab.Week });
        if (SimulationSeed.At(lab, 0, "00:00") <= clock.GetUtcNow()) lab.Week = SimulationSeed.FutureMonday(clock.GetUtcNow());
        lab.Generation = Guid.NewGuid();
        SimulationSeed.Restore(db, lab);
        await db.SaveChangesAsync(ct);
        db.AuditChanges.Add(new AuditDraft(lab.OrganizationId, "Simulation.Reset", lab.OrganizationId, before, JsonSerializer.Serialize(new { lab.Generation, lab.Week })));
        return lab;
    }
    public static object View(SimulationWorkspace lab) => new { lab.OrganizationId, lab.SourceOrganizationId, lab.Generation,
        week = lab.Week.ToString("yyyy-MM-dd"), lab.Applied, lab.Pristine, lab.LocationId, lab.AbsenceShiftId, lab.StandbyShiftId };
}
