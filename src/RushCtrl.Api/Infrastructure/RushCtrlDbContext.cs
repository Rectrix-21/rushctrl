using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Modules.Organizations;

namespace RushCtrl.Api.Infrastructure;

public sealed class RushCtrlDbContext(DbContextOptions<RushCtrlDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.ToTable("Organizations", table => table.HasCheckConstraint("CK_Organizations_Name", "btrim(\"Name\") <> ''"));
        });
        modelBuilder.Entity<Location>(entity =>
        {
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
