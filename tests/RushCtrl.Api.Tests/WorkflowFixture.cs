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
    public Guid OrganizationId { get; } = Guid.NewGuid();
    public Guid OtherOrganizationId { get; } = Guid.NewGuid();
    public Guid LocationId { get; } = Guid.NewGuid();
    public Guid ManagerId { get; } = Guid.NewGuid();
    public Guid EmployeeId { get; } = Guid.NewGuid();

    public static async Task<WorkflowFixture> Create()
    {
        var database = await IsolatedDatabase.Create();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = database.ConnectionString }));
        });
        var fixture = new WorkflowFixture(database, factory);
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
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
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

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }
}
