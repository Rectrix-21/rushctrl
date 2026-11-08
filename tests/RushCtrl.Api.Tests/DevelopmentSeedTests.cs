using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Tests;

public sealed class DevelopmentSeedTests
{
    [Fact, Trait("Category", "Database")]
    public async Task SeedRequiresDevelopmentAndExplicitCredentialsAndIsIdempotent()
    {
        await using var fixture = await WorkflowFixture.Create();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Demo:Enabled"] = "true", ["Demo:ManagerPassword"] = WorkflowFixture.TestPassword,
            ["Demo:EmployeePassword"] = WorkflowFixture.TestPassword
        }).Build();
        var environment = new TestEnvironment { EnvironmentName = Environments.Production };
        await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeed.RunAsync(fixture.Factory.Services, configuration, environment));
        environment.EnvironmentName = Environments.Development;
        await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeed.RunAsync(fixture.Factory.Services, new ConfigurationBuilder().Build(), environment));
        await DevelopmentSeed.RunAsync(fixture.Factory.Services, configuration, environment);
        await DevelopmentSeed.RunAsync(fixture.Factory.Services, configuration, environment);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>();
        Assert.Equal(2, await db.Memberships.CountAsync(x => x.OrganizationId == DevelopmentSeed.OrganizationId));
        Assert.Single(await db.Locations.IgnoreQueryFilters().Where(x => x.OrganizationId == DevelopmentSeed.OrganizationId).ToListAsync());
        var originalShifts = await db.Shifts.IgnoreQueryFilters().Where(x => x.OrganizationId == DevelopmentSeed.OrganizationId).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        Assert.NotEmpty(originalShifts);
        await DevelopmentSeed.RunAsync(fixture.Factory.Services, configuration, environment);
        Assert.Equal(originalShifts, await db.Shifts.IgnoreQueryFilters().Where(x => x.OrganizationId == DevelopmentSeed.OrganizationId).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
