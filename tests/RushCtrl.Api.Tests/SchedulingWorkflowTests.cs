using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Tests;

public sealed class SchedulingWorkflowTests
{
    [Fact, Trait("Category", "Database")]
    public async Task ManagerCanSaveAndReloadShiftAndEmployeeCannotCreateOne()
    {
        await using var f = await WorkflowFixture.Create();
        using var manager = await f.Login();
        var invalid = await manager.PostAsJsonAsync(f.Route("shifts"), new { locationId = f.LocationId, skillId = Guid.NewGuid(), startsAt = "2027-01-11T12:00", endsAt = "2027-01-11T08:00" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await f.AddSchedulingData();
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        var listResponse = await manager.GetAsync(f.Route("shifts?locationId=" + f.LocationId + "&week=2027-01-11"));
        Assert.True(listResponse.IsSuccessStatusCode, await listResponse.Content.ReadAsStringAsync());
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.EnumerateArray(), x => x.GetProperty("id").GetGuid() == shift.GetProperty("id").GetGuid());
        using var employee = await f.Login("employee@example.test");
        employee.DefaultRequestHeaders.Add("X-Role", "Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync(f.Route("shifts"), new { locationId = f.LocationId, skillId = f.SkillId, startsAt = "2027-01-11T08:00", endsAt = "2027-01-11T12:00" })).StatusCode);
        Assert.Empty((await employee.GetFromJsonAsync<JsonElement>(f.Route("shifts?locationId=" + f.LocationId + "&week=2027-01-11"))).EnumerateArray());
    }

    [Fact, Trait("Category", "Database")]
    public async Task AssignmentRequiresQualificationAndFullAvailabilityAndPersists()
    {
        await using var f = await WorkflowFixture.Create();
        using var manager = await f.Login();
        await f.AddSchedulingData(qualified: false, available: false);
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        Assert.Equal(HttpStatusCode.Conflict, (await f.Assign(manager, shift)).StatusCode);
        await f.Qualify();
        Assert.Equal(HttpStatusCode.Conflict, (await f.Assign(manager, shift)).StatusCode);
        using var employee = await f.Login("employee@example.test");
        foreach (var (start, end) in new[] { ("08:00", "10:00"), ("10:00", "12:00") })
            Assert.Equal(HttpStatusCode.Created, (await employee.PostAsJsonAsync(f.Route("availability"), new { locationId = f.LocationId, startsAt = "2027-01-11T" + start, endsAt = "2027-01-11T" + end })).StatusCode);
        var assigned = await f.Assign(manager, shift);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Equal(f.EmployeeId, (await assigned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assigneeUserId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await f.Assign(manager, shift)).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task AdjacentShiftsAreAllowedButOverlapAndHourCapAreRejected()
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData(maxHours: 8);
        using var manager = await f.Login();
        var first = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        var adjacent = await f.CreateShift(manager, "2027-01-11T12:00", "2027-01-11T16:00");
        var overlap = await f.CreateShift(manager, "2027-01-11T11:00", "2027-01-11T13:00");
        var overCap = await f.CreateShift(manager, "2027-01-12T08:00", "2027-01-12T09:00");
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, adjacent)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Assign(manager, overlap)).StatusCode);
        var capped = await f.Assign(manager, overCap);
        Assert.Equal(HttpStatusCode.Conflict, capped.StatusCode);
        Assert.Contains("weekly", await capped.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory, Trait("Category", "Database")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentAssignmentsCannotDoubleBookOrExceedWeeklyCap(bool overlapping)
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData(maxHours: overlapping ? 40 : 4);
        using var a = await f.Login();
        using var b = await f.Login();
        var first = await f.CreateShift(a, "2027-01-11T08:00", "2027-01-11T12:00");
        var next = await f.CreateShift(a, overlapping ? "2027-01-11T10:00" : "2027-01-11T12:00", overlapping ? "2027-01-11T14:00" : "2027-01-11T16:00");
        var results = await Task.WhenAll(f.Assign(a, first), f.Assign(b, next));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact, Trait("Category", "Database")]
    public async Task CrossOrganizationReferencesAreHiddenOnReadAndWrite()
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData();
        using var manager = await f.Login();
        var wrong = await manager.PostAsJsonAsync(f.Route("shifts"), new { locationId = f.OtherLocationId, skillId = f.SkillId, startsAt = "2027-01-11T08:00", endsAt = "2027-01-11T12:00" });
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        var otherSkill = await manager.PostAsJsonAsync(f.Route("shifts"), new { locationId = f.LocationId, skillId = f.OtherSkillId, startsAt = "2027-01-11T08:00", endsAt = "2027-01-11T12:00" });
        Assert.Equal(HttpStatusCode.NotFound, otherSkill.StatusCode);
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        var response = await manager.PatchAsJsonAsync(f.Route($"shifts/{shift.GetProperty("id").GetGuid()}/assignment"), new { userId = Guid.NewGuid(), version = shift.GetProperty("version").GetGuid() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task DatabaseRejectsDoubleBookingEvenWhenApplicationValidationIsBypassed()
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData();
        using var manager = await f.Login();
        var shift = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T12:00");
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, shift)).StatusCode);
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Shifts\" (\"Id\",\"OrganizationId\",\"LocationId\",\"SkillId\",\"StartsAt\",\"EndsAt\",\"AssigneeUserId\",\"Version\") VALUES ({Guid.NewGuid()},{f.OrganizationId},{f.LocationId},{f.SkillId},{DateTimeOffset.Parse("2027-01-11T14:00Z")},{DateTimeOffset.Parse("2027-01-11T18:00Z")},{f.EmployeeId},{Guid.NewGuid()})"));
        Assert.Equal("23P01", error.SqlState);
        var foreignKey = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Shifts\" (\"Id\",\"OrganizationId\",\"LocationId\",\"SkillId\",\"StartsAt\",\"EndsAt\",\"Version\") VALUES ({Guid.NewGuid()},{f.OrganizationId},{f.LocationId},{f.OtherSkillId},{DateTimeOffset.Parse("2027-01-12T14:00Z")},{DateTimeOffset.Parse("2027-01-12T18:00Z")},{Guid.NewGuid()})"));
        Assert.Equal("23503", foreignKey.SqlState);
    }

    [Fact, Trait("Category", "Database")]
    public async Task OvernightShiftCountsOnlyTheHoursInsideEachLocalWeek()
    {
        await using var f = await WorkflowFixture.Create();
        await f.AddSchedulingData(maxHours: 4);
        using var manager = await f.Login();
        var overnight = await f.CreateShift(manager, "2027-01-10T23:00", "2027-01-11T01:00");
        var monday = await f.CreateShift(manager, "2027-01-11T08:00", "2027-01-11T11:00");
        var capped = await f.CreateShift(manager, "2027-01-12T08:00", "2027-01-12T09:00");
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, overnight)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Assign(manager, monday)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Assign(manager, capped)).StatusCode);
    }
}
