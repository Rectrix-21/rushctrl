using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;
using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Tests;

public sealed class CandidateTests
{
    [Fact, Trait("Category", "Database")]
    public async Task OrderingUsesLoadThenInclusiveApprovalHistoryThenStableIdAndExplainsRejections()
    {
        await using var f = await WorkflowFixture.Create(); await f.AddSchedulingData();
        using var manager = await f.Login(); using var employee = await f.Login("employee@example.test");
        var request = await RequestResolutionTests.Pending(f, manager, employee);
        var ids = Enumerable.Range(1, 4).Select(i => Guid.Parse($"20000000-0000-0000-0000-{i:000000000000}")).ToArray();
        using (var scope = f.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
            foreach (var id in ids)
            {
                db.Users.Add(new AppUser { Id = id, DisplayName = "Candidate " + id });
                db.Memberships.Add(new Membership { OrganizationId = f.OrganizationId, UserId = id });
                db.EmployeeSkills.Add(new EmployeeSkill { OrganizationId = f.OrganizationId, UserId = id, SkillId = f.SkillId });
                db.AvailabilityWindows.Add(new AvailabilityWindow { OrganizationId = f.OrganizationId, UserId = id, LocationId = f.LocationId, StartsAt = DateTimeOffset.Parse("2027-01-11T00:00Z"), EndsAt = DateTimeOffset.Parse("2027-01-19T00:00Z") });
            }
            db.Shifts.Add(new Shift { OrganizationId = f.OrganizationId, LocationId = f.LocationId, SkillId = f.SkillId, AssigneeUserId = ids[0], StartsAt = DateTimeOffset.Parse("2027-01-12T13:00Z"), EndsAt = DateTimeOffset.Parse("2027-01-12T17:00Z") });
            var now = DateTimeOffset.Parse("2026-11-07T12:00Z");
            var history = new[] { (ids[1], now.AddDays(-28)), (ids[1], now), (ids[2], now.AddDays(-28).AddTicks(-10)), (ids[2], now.AddSeconds(1)) };
            var number = 100L;
            foreach (var (person, at) in history) db.AuditEntries.Add(new AuditEntry { OrganizationId = f.OrganizationId, Number = number++, ActorUserId = f.ManagerId, CreatedAt = at, Action = "CoverageRequest.Approved", ResourceId = Guid.NewGuid(), SubjectUserId = person });
            await db.SaveChangesAsync();
        }
        var route = f.Route($"shifts/{request.GetProperty("shiftId").GetGuid()}/candidates");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync(route)).StatusCode);
        var response = await manager.GetAsync(route); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("least-load-v1", result.GetProperty("policy").GetString());
        var eligible = result.GetProperty("eligible").EnumerateArray().ToArray();
        Assert.Equal(new[] { ids[2], ids[3], ids[1], ids[0] }, eligible.Select(c => c.GetProperty("userId").GetGuid()));
        Assert.Equal(2, eligible[2].GetProperty("recentCovers").GetInt32());
        Assert.Equal(240, eligible[3].GetProperty("assignedMinutes").GetDouble());
        Assert.Contains(result.GetProperty("rejected").EnumerateArray(), c => c.GetProperty("userId").GetGuid() == f.EmployeeId && c.GetProperty("reasons")[0].GetProperty("code").GetString() == "self");
        Assert.Contains(result.GetProperty("rejected").EnumerateArray(), c => c.GetProperty("userId").GetGuid() == f.ManagerId && c.GetProperty("reasons").EnumerateArray().Any(r => r.GetProperty("code").GetString() == "qualification"));
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/v1/organizations/{f.OtherOrganizationId}/shifts/{request.GetProperty("shiftId").GetGuid()}/candidates")).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task APreviouslyEligibleRecommendationCannotBypassANewOverlapAtApproval()
    {
        await using var f = await WorkflowFixture.Create(); await f.AddSchedulingData(); await RequestResolutionTests.QualifyManager(f);
        using var manager = await f.Login(); using var employee = await f.Login("employee@example.test");
        var request = await RequestResolutionTests.Pending(f, manager, employee);
        var response = await manager.GetAsync(f.Route($"shifts/{request.GetProperty("shiftId").GetGuid()}/candidates"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidates = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(candidates.GetProperty("eligible").EnumerateArray());
        using (var scope = f.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
            db.Shifts.Add(new Shift { OrganizationId = f.OrganizationId, LocationId = f.LocationId, SkillId = f.SkillId, AssigneeUserId = f.ManagerId, StartsAt = DateTimeOffset.Parse("2027-01-11T13:00Z"), EndsAt = DateTimeOffset.Parse("2027-01-11T17:00Z") });
            await db.SaveChangesAsync();
        }
        var approval = await manager.PostAsJsonAsync(f.Route($"coverage-requests/{request.GetProperty("id").GetGuid()}/approve"), new { userId = f.ManagerId, version = RequestResolutionTests.Version(request), shiftVersion = candidates.GetProperty("shiftVersion").GetGuid() });
        Assert.Equal(HttpStatusCode.Conflict, approval.StatusCode);
        Assert.Contains("overlapping", await approval.Content.ReadAsStringAsync());
    }
}
