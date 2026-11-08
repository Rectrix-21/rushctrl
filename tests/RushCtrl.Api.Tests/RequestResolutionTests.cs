using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Tests;

public sealed class RequestResolutionTests
{
    internal static Guid Version(JsonElement value) => value.TryGetProperty("version", out var version) ? version.GetGuid() : Guid.Empty;
    internal static async Task<JsonElement> Pending(WorkflowFixture f, HttpClient manager, HttpClient employee)
    {
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, shift)).StatusCode);
        var response = await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = shift.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal static async Task QualifyManager(WorkflowFixture f)
    {
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"EmployeeSkills\" (\"OrganizationId\",\"UserId\",\"SkillId\") VALUES ({f.OrganizationId},{f.ManagerId},{f.SkillId})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AvailabilityWindows\" (\"Id\",\"OrganizationId\",\"UserId\",\"LocationId\",\"StartsAt\",\"EndsAt\") VALUES ({Guid.NewGuid()},{f.OrganizationId},{f.ManagerId},{f.LocationId},{DateTimeOffset.Parse("2027-01-11T00:00Z")},{DateTimeOffset.Parse("2027-01-19T00:00Z")})");
    }

    [Fact, Trait("Category", "Database")]
    public async Task OwnerCanCancelAndRequestAgainWhileHistoryAndAuditRemainPrivate()
    {
        await using var f = await WorkflowFixture.Create(); await f.AddSchedulingData();
        using var manager = await f.Login(); using var employee = await f.Login("employee@example.test");
        var request = await Pending(f, manager, employee);
        var route = f.Route($"coverage-requests/{request.GetProperty("id").GetGuid()}/cancel");
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync(route, new { version = Version(request) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync(route, new { version = Version(request) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await employee.PostAsJsonAsync(route, new { version = Version(request) })).StatusCode);
        var again = await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = request.GetProperty("shiftId").GetGuid() });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var list = await employee.GetFromJsonAsync<JsonElement>(f.Route($"coverage-requests?locationId={f.LocationId}&week=2027-01-11"));
        Assert.Equal(2, list.GetArrayLength());
        Assert.Contains(list.EnumerateArray(), r => r.GetProperty("status").GetString() == "Cancelled");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync(f.Route("audit"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/v1/organizations/{f.OtherOrganizationId}/audit")).StatusCode);
        var audit = await manager.GetFromJsonAsync<JsonElement>(f.Route("audit?limit=2"));
        Assert.Equal(2, audit.GetArrayLength());
        Assert.True(audit[0].GetProperty("number").GetInt64() > audit[1].GetProperty("number").GetInt64());
        var page = await manager.GetFromJsonAsync<JsonElement>(f.Route($"audit?beforeNumber={audit[1].GetProperty("number").GetInt64()}&limit=2"));
        Assert.All(page.EnumerateArray(), e => Assert.True(e.GetProperty("number").GetInt64() < audit[1].GetProperty("number").GetInt64()));
    }

    [Fact, Trait("Category", "Database")]
    public async Task ApprovalRevalidatesEligibilityAndCommitsRequestAssignmentAndOneAuditTogether()
    {
        await using var f = await WorkflowFixture.Create(); await f.AddSchedulingData();
        using var manager = await f.Login(); using var employee = await f.Login("employee@example.test");
        var request = await Pending(f, manager, employee);
        var shifts = await manager.GetFromJsonAsync<JsonElement>(f.Route($"shifts?locationId={f.LocationId}&week=2027-01-11"));
        var input = new { version = Version(request), shiftVersion = Version(shifts[0]), userId = f.ManagerId };
        var route = f.Route($"coverage-requests/{request.GetProperty("id").GetGuid()}/approve");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(route, input)).StatusCode);
        var auditBefore = await manager.GetAsync(f.Route("audit")); Assert.Equal(HttpStatusCode.OK, auditBefore.StatusCode);
        var count = (await auditBefore.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength();
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(route, input)).StatusCode);
        Assert.Equal(count, (await manager.GetFromJsonAsync<JsonElement>(f.Route("audit"))).GetArrayLength());
        await QualifyManager(f);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(route, new { input.userId, input.shiftVersion, version = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync(route, input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(route, input)).StatusCode);
        shifts = await manager.GetFromJsonAsync<JsonElement>(f.Route($"shifts?locationId={f.LocationId}&week=2027-01-11"));
        Assert.Equal(f.ManagerId, shifts[0].GetProperty("assigneeUserId").GetGuid());
        var history = await employee.GetFromJsonAsync<JsonElement>(f.Route($"coverage-requests?locationId={f.LocationId}&week=2027-01-11"));
        Assert.Equal("Approved", history[0].GetProperty("status").GetString());
        var audit = await manager.GetFromJsonAsync<JsonElement>(f.Route("audit"));
        Assert.Single(audit.EnumerateArray(), x => x.GetProperty("action").GetString() == "CoverageRequest.Approved");
    }

    [Fact, Trait("Category", "Database")]
    public async Task CompetingApprovalsHaveOneWinnerAndRejectAllowsLaterDirectAssignment()
    {
        await using var f = await WorkflowFixture.Create(); await f.AddSchedulingData(); await QualifyManager(f);
        using var manager = await f.Login(); using var otherManager = await f.Login(); using var employee = await f.Login("employee@example.test");
        var request = await Pending(f, manager, employee);
        var shifts = await manager.GetFromJsonAsync<JsonElement>(f.Route($"shifts?locationId={f.LocationId}&week=2027-01-11"));
        var input = new { version = Version(request), shiftVersion = Version(shifts[0]), userId = f.ManagerId };
        var route = f.Route($"coverage-requests/{request.GetProperty("id").GetGuid()}/approve");
        var responses = await Task.WhenAll(manager.PostAsJsonAsync(route, input), otherManager.PostAsJsonAsync(route, input));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        var next = await f.CreateShift(manager, "2027-01-12T08:00", "2027-01-12T12:00");
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, next)).StatusCode);
        var pendingResponse = await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = next.GetProperty("id").GetGuid() });
        var pending = await pendingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var reject = f.Route($"coverage-requests/{pending.GetProperty("id").GetGuid()}/reject");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(reject, new { version = Version(pending) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync(reject, new { version = Version(pending) })).StatusCode);
        shifts = await manager.GetFromJsonAsync<JsonElement>(f.Route($"shifts?locationId={f.LocationId}&week=2027-01-11"));
        var refreshed = shifts.EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == next.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, refreshed)).StatusCode);
    }
}
