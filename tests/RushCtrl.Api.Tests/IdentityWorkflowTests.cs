using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RushCtrl.Api.Tests;

public sealed class IdentityWorkflowTests
{
    [Fact, Trait("Category", "Database")]
    public async Task CsrfResponseIdentifiesItsCurrentSessionForStaleTabProtection()
    {
        await using var fixture = await WorkflowFixture.Create();
        using var anonymous = fixture.Factory.CreateClient();
        var signedOut = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        Assert.Equal(JsonValueKind.Null, signedOut.GetProperty("userId").ValueKind);
        using var manager = await fixture.Login();
        var signedIn = await manager.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        Assert.Equal(fixture.ManagerId, signedIn.GetProperty("userId").GetGuid());
    }
    [Fact, Trait("Category", "Database")]
    public async Task FiveFailedAttemptsLockTheAccountWithoutChangingThePublicFailureMessage()
    {
        await using var fixture = await WorkflowFixture.Create();
        using var client = fixture.Factory.CreateClient();
        await WorkflowFixture.RefreshCsrf(client);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "manager@example.test", password = "Incorrect!Password" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "manager@example.test", password = WorkflowFixture.TestPassword })).StatusCode);
    }
    [Fact, Trait("Category", "Database")]
    public async Task LoginSessionAndLogoutUseRealIdentityWithoutLeakingSecrets()
    {
        await using var fixture = await WorkflowFixture.Create();
        using var client = await fixture.Login();
        var session = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/session");
        Assert.Equal(fixture.ManagerId, session.GetProperty("id").GetGuid());
        var membership = Assert.Single(session.GetProperty("memberships").EnumerateArray());
        Assert.Equal(fixture.OrganizationId, membership.GetProperty("organizationId").GetGuid());
        Assert.Equal("Manager", membership.GetProperty("permission").GetString());
        Assert.DoesNotContain("password", session.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task InvalidCredentialsAreGenericAndAnonymousCsrfCannotBeReusedAfterLogin()
    {
        await using var fixture = await WorkflowFixture.Create();
        using var client = fixture.Factory.CreateClient();
        await WorkflowFixture.RefreshCsrf(client);
        var badLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "missing@example.test", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "manager@example.test", password = WorkflowFixture.TestPassword });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
    }

    [Fact, Trait("Category", "Database")]
    public async Task MembershipComesFromSessionAndCrossOrganizationAccessIsHidden()
    {
        await using var fixture = await WorkflowFixture.Create();
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(fixture.Route("workspace"))).StatusCode);
        using var employee = await fixture.Login("employee@example.test");
        employee.DefaultRequestHeaders.Add("X-Role", "Manager");
        employee.DefaultRequestHeaders.Add("X-Tenant", fixture.OtherOrganizationId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync(fixture.Route("workspace"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/v1/organizations/{fixture.OtherOrganizationId}/workspace")).StatusCode);
    }
    [Fact]
    public async Task AnonymousSessionReturns401WithoutRedirect()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task CsrfEndpointIssuesATokenAndHttpOnlyCookie()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("forged-token")]
    public async Task LoginRequiresValidCsrfBeforeReadingCredentials(string? token)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@example.test", password = "wrong" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
