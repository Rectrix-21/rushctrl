using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Tests;

public sealed class DatabaseReadinessTests
{
    [Fact]
    public async Task RefusedDatabaseConnectionIsUnhealthyWithoutLeakingConnectionDetails()
    {
        await using var factory = Factory("Host=127.0.0.1;Port=1;Database=unavailable;Username=private_user;Password=private_password;Timeout=1");
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unhealthy", body.GetProperty("status").GetString());
        Assert.Single(body.EnumerateObject());
    }

    [Fact, Trait("Category", "Database")]
    public async Task ReachableButUnmigratedDatabaseIsNotReady()
    {
        await using var database = await IsolatedDatabase.Create();
        await using var factory = Factory(database.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task MigratedDatabaseIsReady()
    {
        await using var database = await IsolatedDatabase.Create();
        await using var factory = Factory(database.ConnectionString);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
    }

    [Fact, Trait("Category", "Database")]
    public async Task LocationCannotReferenceANonexistentOrganization()
    {
        await using var database = await IsolatedDatabase.Create();
        await using var factory = Factory(database.ConnectionString);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>().Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "Locations" ("Id", "OrganizationId", "Name", "TimeZoneId", "CreatedAt")
            VALUES (@id, @organization, 'Harbour', 'America/Toronto', now())
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("organization", Guid.NewGuid());
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
    }

    private static WebApplicationFactory<Program> Factory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connectionString })));
}

internal sealed class IsolatedDatabase : IAsyncDisposable
{
    private readonly string adminConnection;
    private readonly string databaseName = "rushctrl_test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; }

    private IsolatedDatabase(string adminConnection)
    {
        this.adminConnection = adminConnection;
        ConnectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName }.ConnectionString;
    }

    public static Task<IsolatedDatabase> Create()
    {
        var connection = Environment.GetEnvironmentVariable("RUSHCTRL_TEST_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set RUSHCTRL_TEST_DATABASE_CONNECTION to an isolated local/CI PostgreSQL server, or filter Category!=Database.");
        var database = new IsolatedDatabase(connection);
        // Keep administrative creation on its initiating thread on the Windows test runner.
        // Application requests and the concurrency tests still use asynchronous database I/O.
        using var admin = new NpgsqlConnection(connection);
        admin.Open();
        using var command = new NpgsqlCommand($"CREATE DATABASE \"{database.databaseName}\"", admin);
        command.ExecuteNonQuery();
        return Task.FromResult(database);
    }

    public async ValueTask DisposeAsync()
    {
        if (!databaseName.StartsWith("rushctrl_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the test database namespace.");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }
}
