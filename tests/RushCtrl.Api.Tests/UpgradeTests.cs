using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Tests;

public sealed class UpgradeTests
{
    [Fact, Trait("Category", "Database")]
    public async Task MilestoneOneRequestsSurviveUpgradeWithDistinctVersionsAndStandardKind()
    {
        await using var fixture = await WorkflowFixture.Create(); await fixture.AddSchedulingData();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261107172523_StaffingWorkflow");
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        for (var i = 0; i < ids.Length; i++)
        {
            var shift = Guid.NewGuid(); var start = DateTimeOffset.Parse("2027-01-11T13:00Z").AddDays(i);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Shifts\" (\"Id\",\"OrganizationId\",\"LocationId\",\"SkillId\",\"AssigneeUserId\",\"StartsAt\",\"EndsAt\",\"Version\") VALUES ({shift},{fixture.OrganizationId},{fixture.LocationId},{fixture.SkillId},{fixture.EmployeeId},{start},{start.AddHours(4)},{Guid.NewGuid()})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CoverageRequests\" (\"Id\",\"OrganizationId\",\"ShiftId\",\"RequesterUserId\",\"Status\",\"CreatedAt\") VALUES ({ids[i]},{fixture.OrganizationId},{shift},{fixture.EmployeeId},'Pending',{DateTimeOffset.UtcNow})");
        }
        await migrator.MigrateAsync();
        var requests = await db.CoverageRequests.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, requests.Count);
        Assert.All(requests, r => { Assert.Contains(r.Id, ids); Assert.Equal("Standard", r.Kind); Assert.Equal("Pending", r.Status); Assert.NotEqual(Guid.Empty, r.Version); Assert.Null(r.ResolvedAt); });
        Assert.Equal(2, requests.Select(r => r.Version).Distinct().Count());
    }
}
