using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;
using RushCtrl.Api.Modules.Organizations;

namespace RushCtrl.Api.Tests;

internal sealed class WorkflowFixture(IsolatedDatabase database, WebApplicationFactory<Program> factory) : IAsyncDisposable
{
    internal const string TestPassword = "Test-only!Password47";
    public WebApplicationFactory<Program> Factory => factory;
    public Guid OrganizationId { get; private set; } = Guid.NewGuid();
    public Guid OtherOrganizationId { get; } = Guid.NewGuid();
    public Guid LocationId { get; } = Guid.NewGuid();
    public Guid ManagerId { get; } = Guid.NewGuid();
    public Guid EmployeeId { get; } = Guid.NewGuid();
    public Guid SkillId { get; } = Guid.NewGuid();
    public Guid OtherLocationId { get; } = Guid.NewGuid();
    public Guid OtherSkillId { get; } = Guid.NewGuid();

    public static async Task<WorkflowFixture> Create(bool simulation = false, string environment = "Development")
    {
        var database = await IsolatedDatabase.Create();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FixedClock()));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = database.ConnectionString, ["Simulation:Enabled"] = simulation.ToString() }));
        });
        var fixture = new WorkflowFixture(database, factory);
        if (simulation) fixture.OrganizationId = DevelopmentSeed.OrganizationId;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        await db.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var (id, email, name) in new[] { (fixture.ManagerId, "manager@example.test", "Test Manager"), (fixture.EmployeeId, "employee@example.test", "Test Employee") })
        {
            var result = await users.CreateAsync(new AppUser { Id = id, UserName = email, Email = email, DisplayName = name }, TestPassword);
            Assert.True(result.Succeeded, string.Join(";", result.Errors.Select(x => x.Description)));
        }
        db.Organizations.AddRange(new Organization { Id = fixture.OrganizationId, Name = "Test cafe" }, new Organization { Id = fixture.OtherOrganizationId, Name = "Other cafe" });
        db.Locations.Add(new Location { Id = fixture.LocationId, OrganizationId = fixture.OrganizationId, Name = "Harbour", TimeZoneId = "America/Toronto" });
        db.Memberships.AddRange(new Membership { OrganizationId = fixture.OrganizationId, UserId = fixture.ManagerId, Permission = "Manager" },
            new Membership { OrganizationId = fixture.OrganizationId, UserId = fixture.EmployeeId });
        await db.SaveChangesAsync();
        return fixture;
    }

    public async Task<HttpClient> Login(string email = "manager@example.test")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await RefreshCsrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = TestPassword });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        await RefreshCsrf(client);
        return client;
    }

    public static async Task RefreshCsrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    public string Route(string path) => $"/api/v1/organizations/{OrganizationId}/{path}";

    public async Task AddSchedulingData(bool qualified = true, bool available = true, int maxHours = 40)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Skills\" (\"Id\", \"OrganizationId\", \"Name\") VALUES ({SkillId}, {OrganizationId}, 'Barista')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Skills\" (\"Id\", \"OrganizationId\", \"Name\") VALUES ({OtherSkillId}, {OtherOrganizationId}, 'Other skill')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Locations\" (\"Id\", \"OrganizationId\", \"Name\", \"TimeZoneId\", \"CreatedAt\") VALUES ({OtherLocationId}, {OtherOrganizationId}, 'Other location', 'America/Toronto', now())");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Memberships\" SET \"MaxWeeklyHours\" = {maxHours} WHERE \"OrganizationId\" = {OrganizationId} AND \"UserId\" = {EmployeeId}");
        if (qualified) await Qualify();
        if (available)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AvailabilityWindows\" (\"Id\", \"OrganizationId\", \"LocationId\", \"UserId\", \"StartsAt\", \"EndsAt\") VALUES ({Guid.NewGuid()}, {OrganizationId}, {LocationId}, {EmployeeId}, {DateTimeOffset.Parse("2027-01-11T00:00Z")}, {DateTimeOffset.Parse("2027-01-19T00:00Z")})");
    }

    public async Task Qualify()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"EmployeeSkills\" (\"OrganizationId\", \"UserId\", \"SkillId\") VALUES ({OrganizationId}, {EmployeeId}, {SkillId})");
    }

    public async Task<JsonElement> CreateShift(HttpClient client, string start, string end)
    {
        var response = await client.PostAsJsonAsync(Route("shifts"), new { locationId = LocationId, skillId = SkillId, startsAt = start, endsAt = end });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public Task<HttpResponseMessage> Assign(HttpClient client, JsonElement shift) => client.PatchAsJsonAsync(
        Route($"shifts/{shift.GetProperty("id").GetGuid()}/assignment"), new { userId = EmployeeId, version = shift.GetProperty("version").GetGuid() });

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-11-07T12:00Z");
    }
}
