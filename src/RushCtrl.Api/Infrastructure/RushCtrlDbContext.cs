using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using RushCtrl.Api.Modules.Identity;
using RushCtrl.Api.Modules.Scheduling;
using RushCtrl.Api.Modules.Organizations;

namespace RushCtrl.Api.Infrastructure;

public sealed class RushCtrlDbContext(DbContextOptions<RushCtrlDbContext> options, TenantScope tenant) : IdentityUserContext<AppUser, Guid>(options)
{
    public Guid OrganizationId => tenant.OrganizationId;
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<EmployeeSkill> EmployeeSkills => Set<EmployeeSkill>();
    public DbSet<AvailabilityWindow> AvailabilityWindows => Set<AvailabilityWindow>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<StaffingRequirement> StaffingRequirements => Set<StaffingRequirement>();
    public DbSet<CoverageRequest> CoverageRequests => Set<CoverageRequest>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SchedulingModel.Configure(modelBuilder, this);
        modelBuilder.Entity<AppUser>().Property(x => x.DisplayName).HasMaxLength(160);
        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(x => new { x.OrganizationId, x.UserId });
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Permission).HasMaxLength(20);
            entity.ToTable("Memberships", t =>
            {
                t.HasCheckConstraint("CK_Membership_Permission", "\"Permission\" IN ('Manager', 'Employee')");
                t.HasCheckConstraint("CK_Membership_Hours", "\"MaxWeeklyHours\" > 0 AND \"MaxWeeklyHours\" <= 168");
            });
        });
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.ToTable("Organizations", table => table.HasCheckConstraint("CK_Organizations_Name", "btrim(\"Name\") <> ''"));
        });
        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasQueryFilter(x => x.OrganizationId == OrganizationId);
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("Locations", table =>
            {
                table.HasCheckConstraint("CK_Locations_Name", "btrim(\"Name\") <> ''");
                table.HasCheckConstraint("CK_Locations_TimeZoneId", "btrim(\"TimeZoneId\") <> ''");
            });
        });
    }
}
