using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RushCtrl.Api.Tests;

public sealed class OperationalEndpointsTests
{
    [Fact]
    public async Task LivenessIsAvailableWithoutADatabase()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PublicMetadataDoesNotExposeTenantOrEnvironmentDetails()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/system");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RUSHCTRL", body.GetProperty("name").GetString());
        Assert.Equal(2, body.GetProperty("milestone").GetInt32());
        Assert.Equal(3, body.EnumerateObject().Count());
    }

    [Fact]
    public async Task ProtectedEndpointRejectsAnonymousAccessWithoutRedirect()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapGet("/test/protected", () => "private"));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/test/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task OpenApiIsOnlyExposedInDevelopment(string environment, HttpStatusCode expected)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment(environment));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(expected, response.StatusCode);
    }
}
