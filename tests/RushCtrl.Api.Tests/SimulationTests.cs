using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Tests;

public sealed class SimulationTests
{
    private static string Route(Guid org, string path) => $"/api/v1/organizations/{org}/{path}";
    private static async Task<JsonElement> Open(WorkflowFixture f, HttpClient manager)
    {
        var response = await manager.PostAsJsonAsync(f.Route("simulation"), new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lab = await response.Content.ReadFromJsonAsync<JsonElement>();
        manager.DefaultRequestHeaders.Remove("X-Workspace-Generation");
        manager.DefaultRequestHeaders.Add("X-Workspace-Generation", lab.GetProperty("generation").GetString());
        return lab;
    }

    [Fact, Trait("Category", "Database")]
    public async Task RealScenarioApprovalsFillTwoGapsAndResetPreservesNormalDataAndAudit()
    {
        await using var f = await WorkflowFixture.Create(simulation: true); await f.AddSchedulingData();
        using var manager = await f.Login(); using var employee = await f.Login("employee@example.test");
        await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        var normalBefore = await manager.GetStringAsync(f.Route($"shifts?locationId={f.LocationId}&week=2027-01-11"));
        var lab = await Open(f, manager); var org = lab.GetProperty("organizationId").GetGuid();
        var generation = lab.GetProperty("generation").GetGuid();
        var session = await manager.GetFromJsonAsync<JsonElement>("/api/v1/auth/session");
        Assert.Equal(f.OrganizationId, session.GetProperty("memberships")[0].GetProperty("organizationId").GetGuid());
        var query = $"?locationId={lab.GetProperty("locationId").GetGuid()}&week={lab.GetProperty("week").GetString()}";
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync(Route(org, "simulation"))).StatusCode);
        Assert.Empty((await manager.GetFromJsonAsync<JsonElement>(Route(org, "coverage" + query))).EnumerateArray());
        var apply = await manager.PostAsJsonAsync(Route(org, "simulation/apply"), new { });
        Assert.Equal(HttpStatusCode.OK, apply.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
        var gaps = await manager.GetFromJsonAsync<JsonElement>(Route(org, "coverage" + query));
        Assert.Equal(2, gaps[0].GetProperty("missingCount").GetInt32());
        var requests = await manager.GetFromJsonAsync<JsonElement>(Route(org, "coverage-requests" + query));
        var request = Assert.Single(requests.EnumerateArray());
        Assert.Equal("SimulationCallout", request.GetProperty("kind").GetString());
        var candidates = await manager.GetFromJsonAsync<JsonElement>(Route(org, $"shifts/{request.GetProperty("shiftId").GetGuid()}/candidates"));
        Assert.Equal(2, candidates.GetProperty("eligible").GetArrayLength());
        var reasons = candidates.GetProperty("rejected").EnumerateArray().SelectMany(c => c.GetProperty("reasons").EnumerateArray()).Select(r => r.GetProperty("code").GetString()).ToArray();
        foreach (var code in new[] { "self", "qualification", "availability", "overlap", "hours" }) Assert.Contains(code, reasons);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync(Route(org, $"coverage-requests/{request.GetProperty("id").GetGuid()}/approve"), new { version = RequestResolutionTests.Version(request), shiftVersion = candidates.GetProperty("shiftVersion").GetGuid(), userId = candidates.GetProperty("eligible")[0].GetProperty("userId").GetGuid() })).StatusCode);
        gaps = await manager.GetFromJsonAsync<JsonElement>(Route(org, "coverage" + query)); Assert.Equal(1, gaps[0].GetProperty("missingCount").GetInt32());
        var standby = lab.GetProperty("standbyShiftId").GetGuid();
        candidates = await manager.GetFromJsonAsync<JsonElement>(Route(org, $"shifts/{standby}/candidates"));
        Assert.Single(candidates.GetProperty("eligible").EnumerateArray());
        Assert.DoesNotContain(candidates.GetProperty("eligible").EnumerateArray(), x => x.GetProperty("userId").GetGuid() == request.GetProperty("requesterUserId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync(Route(org, $"shifts/{standby}/assignment"), new { version = candidates.GetProperty("shiftVersion").GetGuid(), userId = candidates.GetProperty("eligible")[0].GetProperty("userId").GetGuid() })).StatusCode);
        Assert.Empty((await manager.GetFromJsonAsync<JsonElement>(Route(org, "coverage" + query))).EnumerateArray());
        var reset = await manager.PostAsJsonAsync(Route(org, "simulation/reset"), new { }); Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var restored = await reset.Content.ReadFromJsonAsync<JsonElement>(); Assert.NotEqual(generation, restored.GetProperty("generation").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
        var audit = await manager.GetFromJsonAsync<JsonElement>(Route(org, "audit"));
        Assert.Contains(audit.EnumerateArray(), x => x.GetProperty("action").GetString() == "CoverageRequest.Approved");
        Assert.Contains(audit.EnumerateArray(), x => x.GetProperty("action").GetString() == "Simulation.Reset");
        Assert.Equal(normalBefore, await manager.GetStringAsync(f.Route($"shifts?locationId={f.LocationId}&week=2027-01-11")));
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        var generated = await (from member in db.Memberships join user in db.Users on member.UserId equals user.Id where member.OrganizationId == org && user.Id != f.ManagerId select user).ToListAsync();
        Assert.Equal(8, generated.Count); Assert.All(generated, u => Assert.Null(u.PasswordHash));
    }

    [Theory, InlineData("Development", false), InlineData("Production", true), Trait("Category", "Database")]
    public async Task SimulationRequiresExplicitDevelopmentOptIn(string environment, bool enabled)
    {
        await using var f = await WorkflowFixture.Create(enabled, environment); using var manager = await f.Login();
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync(f.Route("simulation"), new { })).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task EditedOrExpiredBaselineRequiresResetAndOtherOwnersCannotUseTheLab()
    {
        await using var f = await WorkflowFixture.Create(simulation: true); using var manager = await f.Login(); using var employee = await f.Login("employee@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(f.Route("simulation"), new { })).StatusCode);
        var lab = await Open(f, manager); var org = lab.GetProperty("organizationId").GetGuid();
        var date = lab.GetProperty("week").GetString();
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync(Route(org, "availability"), new { locationId = lab.GetProperty("locationId").GetGuid(), startsAt = date + "T08:00", endsAt = date + "T09:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.PostAsJsonAsync(Route(org, "simulation/reset"), new { })).StatusCode);
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"SimulationWorkspaces\" SET \"Pristine\" = true WHERE \"OrganizationId\" = {org}");
        var shiftId = lab.GetProperty("absenceShiftId").GetGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Shifts\" SET \"StartsAt\" = {DateTimeOffset.Parse("2026-01-01T13:00Z")}, \"EndsAt\" = {DateTimeOffset.Parse("2026-01-01T17:00Z")} WHERE \"Id\" = {shiftId}");
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task ResetSerializesWithApprovalAndEveryOldFormIsRejected()
    {
        await using var f = await WorkflowFixture.Create(simulation: true);
        using var manager = await f.Login(); using var second = await f.Login();
        var lab = await Open(f, manager); var org = lab.GetProperty("organizationId").GetGuid();
        var generation = lab.GetProperty("generation").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await second.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
        second.DefaultRequestHeaders.Add("X-Workspace-Generation", "broken");
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
        second.DefaultRequestHeaders.Remove("X-Workspace-Generation"); second.DefaultRequestHeaders.Add("X-Workspace-Generation", generation);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
        var candidates = await manager.GetFromJsonAsync<JsonElement>(Route(org, $"shifts/{lab.GetProperty("absenceShiftId").GetGuid()}/candidates"));
        var request = candidates.GetProperty("pendingRequest");
        var approve = second.PostAsJsonAsync(Route(org, $"coverage-requests/{request.GetProperty("id").GetGuid()}/approve"), new {
            version = request.GetProperty("version").GetGuid(), shiftVersion = candidates.GetProperty("shiftVersion").GetGuid(), userId = candidates.GetProperty("eligible")[0].GetProperty("userId").GetGuid() });
        var reset = manager.PostAsJsonAsync(Route(org, "simulation/reset"), new { });
        var responses = await Task.WhenAll(approve, reset);
        Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
        var fresh = await responses[1].Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(generation, fresh.GetProperty("generation").GetString());
        var shifts = await manager.GetFromJsonAsync<JsonElement>(Route(org, $"shifts?locationId={lab.GetProperty("locationId").GetGuid()}&week={lab.GetProperty("week").GetString()}"));
        Assert.Equal(2, shifts.EnumerateArray().Count(x => x.GetProperty("startsAt").GetDateTimeOffset().DayOfWeek == DayOfWeek.Friday && x.GetProperty("assigneeUserId").ValueKind != JsonValueKind.Null));
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(Route(org, "shifts"), new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(Route(org, "simulation/reset"), new { })).StatusCode);
        manager.DefaultRequestHeaders.Remove("X-Workspace-Generation"); manager.DefaultRequestHeaders.Add("X-Workspace-Generation", fresh.GetProperty("generation").GetString());
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync(Route(org, "simulation/apply"), new { })).StatusCode);
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        Assert.Equal(9, await db.Memberships.CountAsync(x => x.OrganizationId == org));
        Assert.Equal(2, await db.Locations.IgnoreQueryFilters().CountAsync(x => x.OrganizationId == org));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Memberships\" (\"OrganizationId\",\"UserId\",\"Permission\",\"MaxWeeklyHours\") VALUES ({f.OtherOrganizationId},{f.ManagerId},'Manager',40)");
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync(Route(f.OtherOrganizationId, "simulation"), new { })).StatusCode);
        using var otherOwner = await f.Login("employee@example.test");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Memberships\" SET \"Permission\" = 'Manager' WHERE \"OrganizationId\" = {f.OrganizationId} AND \"UserId\" = {f.EmployeeId}");
        Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync(Route(org, "simulation"))).StatusCode);
        var otherLab = await Open(f, otherOwner);
        Assert.NotEqual(org, otherLab.GetProperty("organizationId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync(Route(otherLab.GetProperty("organizationId").GetGuid(), "simulation"))).StatusCode);
    }
}
