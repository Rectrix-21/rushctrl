using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Tests;

public sealed class RealtimeTests
{
    private static StringContent Frame(object data) => new(JsonSerializer.Serialize(data) + '\u001e', Encoding.UTF8, "text/plain");
    private static async Task<string> Join(HttpClient client, string token, Guid org, string id)
    {
        var path = "/hubs/workspace?id=" + Uri.EscapeDataString(token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path, Frame(new { type = 1, invocationId = id, target = "JoinWorkspace", arguments = new[] { org } }))).StatusCode);
        return await client.GetStringAsync(path);
    }

    [Fact, Trait("Category", "Database")]
    public async Task AuthenticatedHubValidatesMembershipAndLeavesThePreviousOrganization()
    {
        await using var f = await WorkflowFixture.Create(); await f.AddSchedulingData();
        using var anonymous = f.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/hubs/workspace/negotiate?negotiateVersion=1", null)).StatusCode);
        using var client = await f.Login();
        var negotiate = await client.PostAsync("/hubs/workspace/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
        var token = (await negotiate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("connectionToken").GetString()!;
        var path = "/hubs/workspace?id=" + Uri.EscapeDataString(token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path, Frame(new { protocol = "json", version = 1 }))).StatusCode);
        Assert.Contains("{}", await client.GetStringAsync(path));
        Assert.DoesNotContain("error", await Join(client, token, f.OrganizationId, "1"));
        Assert.Contains("error", await Join(client, token, f.OtherOrganizationId, "2"));
        using (var scope = f.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>().Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Memberships\" (\"OrganizationId\",\"UserId\",\"Permission\",\"MaxWeeklyHours\") VALUES ({f.OtherOrganizationId},{f.ManagerId},'Manager',40)");
        Assert.DoesNotContain("error", await Join(client, token, f.OtherOrganizationId, "3"));
        await f.CreateShift(client, "2027-01-11T08:00", "2027-01-11T12:00");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/v1/organizations/{f.OtherOrganizationId}/shifts", new { locationId = f.OtherLocationId, skillId = f.OtherSkillId, startsAt = "2027-01-11T08:00", endsAt = "2027-01-11T12:00" })).StatusCode);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = await client.GetStringAsync(path, timeout.Token);
        Assert.Contains("WorkspaceChanged", message); Assert.Contains(f.OtherOrganizationId.ToString(), message);
        Assert.DoesNotContain(f.OrganizationId.ToString(), message);
        await client.DeleteAsync(path);
    }

    [Fact, Trait("Category", "Database")]
    public async Task NotificationSeesCommittedAuditAndFailureCannotTurnASavedWriteIntoAnError()
    {
        var notifier = new FailingNotifier();
        await using var f = await WorkflowFixture.Create(configure: services => services.AddSingleton<IWorkspaceNotifier>(notifier));
        notifier.Services = f.Factory.Services;
        await f.AddSchedulingData(); using var manager = await f.Login();
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        Assert.Equal(1, notifier.Calls); Assert.True(notifier.Committed);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PatchAsJsonAsync(f.Route($"shifts/{shift.GetProperty("id").GetGuid()}/assignment"), new { version = Guid.NewGuid(), userId = f.EmployeeId })).StatusCode);
        Assert.Equal(1, notifier.Calls);
    }
    private sealed class FailingNotifier : IWorkspaceNotifier
    {
        public IServiceProvider Services { get; set; } = null!;
        public int Calls { get; private set; }
        public bool Committed { get; private set; }
        public async Task ChangedAsync(Guid organizationId, Guid? generation, CancellationToken ct)
        {
            Calls++;
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
            Committed = await db.AuditEntries.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId, ct);
            throw new IOException("Simulated notification failure after commit");
        }
    }
}
