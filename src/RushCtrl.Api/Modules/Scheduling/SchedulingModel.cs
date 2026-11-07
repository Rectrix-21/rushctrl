using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Modules.Identity;
using RushCtrl.Api.Modules.Organizations;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Modules.Scheduling;

public static class SchedulingModel
{
    public static void Configure(ModelBuilder model, RushCtrlDbContext tenant)
    {
        model.HasPostgresExtension("btree_gist");
        model.Entity<Skill>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            e.HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
            e.Property(x => x.Name).HasMaxLength(80);
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<EmployeeSkill>(e =>
        {
            e.HasKey(x => new { x.OrganizationId, x.UserId, x.SkillId });
            e.HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
            e.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Skill>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SkillId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AvailabilityWindow>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
            e.HasIndex(x => new { x.OrganizationId, x.UserId, x.StartsAt });
            e.HasOne<Location>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LocationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("AvailabilityWindows", t => t.HasCheckConstraint("CK_Availability_Range", "\"EndsAt\" > \"StartsAt\""));
        });
        model.Entity<Shift>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            e.HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.OrganizationId, x.LocationId, x.StartsAt });
            e.HasIndex(x => new { x.AssigneeUserId, x.StartsAt });
            e.HasOne<Location>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LocationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Skill>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SkillId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.AssigneeUserId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Shifts", t => t.HasCheckConstraint("CK_Shift_Range", "\"EndsAt\" > \"StartsAt\""));
        });
        model.Entity<StaffingRequirement>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
            e.HasIndex(x => new { x.OrganizationId, x.LocationId, x.StartsAt });
            e.HasOne<Location>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LocationId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Skill>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SkillId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("StaffingRequirements", t =>
            {
                t.HasCheckConstraint("CK_Requirement_Range", "\"EndsAt\" > \"StartsAt\"");
                t.HasCheckConstraint("CK_Requirement_Count", "\"RequiredCount\" > 0 AND \"RequiredCount\" <= 100");
            });
        });
        model.Entity<CoverageRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
            e.HasIndex(x => x.ShiftId).IsUnique();
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne<Shift>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ShiftId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RequesterUserId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("CoverageRequests", t => t.HasCheckConstraint("CK_Coverage_Status", "\"Status\" = 'Pending'"));
        });
    }
}
