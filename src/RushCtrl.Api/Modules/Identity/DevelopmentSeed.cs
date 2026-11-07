using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Organizations;

namespace RushCtrl.Api.Modules.Identity;

public static class DevelopmentSeed
{
    public static readonly Guid OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid LocationId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid ManagerId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid EmployeeId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Demo:Enabled"))
            throw new InvalidOperationException("Demo seeding requires Development and Demo:Enabled=true.");
        var managerPassword = configuration["Demo:ManagerPassword"];
        var employeePassword = configuration["Demo:EmployeePassword"];
        if (string.IsNullOrWhiteSpace(managerPassword) || string.IsNullOrWhiteSpace(employeePassword))
            throw new InvalidOperationException("Provide ignored local demo passwords before seeding.");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Organizations.AnyAsync(x => x.Id == OrganizationId))
            db.Organizations.Add(new Organization { Id = OrganizationId, Name = "Northline Coffee" });
        if (!await db.Locations.IgnoreQueryFilters().AnyAsync(x => x.Id == LocationId))
            db.Locations.Add(new Location { Id = LocationId, OrganizationId = OrganizationId, Name = "Harbour", TimeZoneId = "America/Toronto" });
        await db.SaveChangesAsync();
        foreach (var (id, email, name, permission, password) in new[]
        {
            (ManagerId, "manager@northline.example", "Alex Kim", "Manager", managerPassword),
            (EmployeeId, "employee@northline.example", "Morgan Park", "Employee", employeePassword)
        })
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new AppUser { Id = id, UserName = email, Email = email, DisplayName = name };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded) throw new InvalidOperationException("Demo password must satisfy the configured Identity password policy.");
            }
            else if (user.Id != id) throw new InvalidOperationException("A non-demo account already uses a reserved demo email.");
            if (!await db.Memberships.AnyAsync(x => x.OrganizationId == OrganizationId && x.UserId == id))
                db.Memberships.Add(new Membership { OrganizationId = OrganizationId, UserId = id, Permission = permission });
        }
        await db.SaveChangesAsync();
        await DemoScheduleSeed.RunAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow());
        await transaction.CommitAsync();
    }
}
