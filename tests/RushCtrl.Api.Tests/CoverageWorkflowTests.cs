using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Tests;

public sealed class CoverageWorkflowTests
{
    [Fact]
    public void PartialAssignedCoverageSplitsAndMergesShortagePeriods()
    {
        var org = Guid.NewGuid(); var location = Guid.NewGuid(); var skill = Guid.NewGuid();
        var target = new StaffingRequirement { OrganizationId = org, LocationId = location, SkillId = skill, StartsAt = At(8), EndsAt = At(14), RequiredCount = 2 };
        var shifts = new[]
        {
            new Shift { OrganizationId = org, LocationId = location, SkillId = skill, StartsAt = At(8), EndsAt = At(10), AssigneeUserId = Guid.NewGuid() },
            new Shift { OrganizationId = org, LocationId = location, SkillId = skill, StartsAt = At(10), EndsAt = At(12), AssigneeUserId = Guid.NewGuid() },
            new Shift { OrganizationId = org, LocationId = location, SkillId = skill, StartsAt = At(8), EndsAt = At(14) }
        };
        var gaps = CoverageCalculator.Calculate([target], shifts);
        Assert.Equal(2, gaps.Count);
        Assert.Equal((At(8), At(12), 1), (gaps[0].StartsAt, gaps[0].EndsAt, gaps[0].MissingCount));
        Assert.Equal((At(12), At(14), 2), (gaps[1].StartsAt, gaps[1].EndsAt, gaps[1].MissingCount));
    }

    [Fact, Trait("Category", "Database")]
    public async Task EmployeeOwnsAvailabilityAndCoverageRequestAndManagerSeesPendingStatus()
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData();
        using var manager = await f.Login();
        using var employee = await f.Login("employee@example.test");
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        var assigned = await f.Assign(manager, shift);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignedShift = await assigned.Content.ReadFromJsonAsync<JsonElement>();
        var request = await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = shift.GetProperty("id").GetGuid(), requesterUserId = f.ManagerId });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var body = await request.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(f.EmployeeId, body.GetProperty("requesterUserId").GetGuid());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = shift.GetProperty("id").GetGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Assign(manager, assignedShift)).StatusCode);
        var list = await manager.GetFromJsonAsync<JsonElement>(f.Route("coverage-requests?locationId=" + f.LocationId + "&week=2027-01-11"));
        Assert.Single(list.EnumerateArray());
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync(f.Route("availability"), new { locationId = f.LocationId, startsAt = "2027-01-11T08:00", endsAt = "2027-01-11T12:00", userId = f.EmployeeId })).StatusCode);
        var availability = await employee.GetFromJsonAsync<JsonElement>(f.Route("availability?locationId=" + f.LocationId + "&week=2027-01-11"));
        Assert.All(availability.EnumerateArray(), x => Assert.Equal(f.EmployeeId, x.GetProperty("userId").GetGuid()));
    }

    [Fact, Trait("Category", "Database")]
    public async Task OpenPastAndOtherPersonsShiftsCannotBeRequested()
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData();
        using var manager = await f.Login();
        using var employee = await f.Login("employee@example.test");
        var open = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        Assert.Equal(HttpStatusCode.NotFound, (await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = open.GetProperty("id").GetGuid() })).StatusCode);
        var response = await f.Assign(manager, open);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = open.GetProperty("id").GetGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await employee.PostAsJsonAsync(f.Route("availability"), new { locationId = f.LocationId, startsAt = "2026-01-12T08:00", endsAt = "2026-01-12T12:00" })).StatusCode);
        var past = await f.CreateShift(manager, "2026-01-12T08:00", "2026-01-12T12:00");
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, past)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await employee.PostAsJsonAsync(f.Route("coverage-requests"), new { shiftId = past.GetProperty("id").GetGuid() })).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task ManagerTargetsProducePartialGapsAndRejectOverlappingTargets()
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData();
        using var manager = await f.Login();
        using var employee = await f.Login("employee@example.test");
        var input = new { locationId = f.LocationId, skillId = f.SkillId, startsAt = "2027-01-11T08:00", endsAt = "2027-01-11T16:00", requiredCount = 1 };
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(f.Route("staffing-requirements"), input)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync(f.Route("staffing-requirements"), input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.PostAsJsonAsync(f.Route("staffing-requirements"), input)).StatusCode);
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, shift)).StatusCode);
        var gaps = await manager.GetFromJsonAsync<JsonElement>(f.Route("coverage?locationId=" + f.LocationId + "&week=2027-01-11"));
        var gap = Assert.Single(gaps.EnumerateArray());
        Assert.Equal(DateTimeOffset.Parse("2027-01-11T17:00Z"), gap.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(1, gap.GetProperty("missingCount").GetInt32());
    }

    private static DateTimeOffset At(int hour) => new(2027, 1, 11, hour, 0, 0, TimeSpan.Zero);
}
