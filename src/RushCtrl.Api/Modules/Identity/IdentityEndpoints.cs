using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Modules.Identity;

public sealed class CsrfFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            try { await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context.HttpContext); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "Refresh the page and try again.", detail: "A valid CSRF token is required.");
            }
        }
        return await next(context);
    }
}

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").AddEndpointFilter<CsrfFilter>();
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new
            {
                token = antiforgery.GetAndStoreTokens(context).RequestToken,
                userId = context.User.Identity?.IsAuthenticated == true ? (Guid?)context.User.UserId() : null
            });
        }).AllowAnonymous();
        auth.MapPost("/login", async (LoginInput input, SignInManager<AppUser> signIn, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 256 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 256)
                return Results.Problem(statusCode: 400, title: "Enter an email and password.");
            var result = await signIn.PasswordSignInAsync(input.Email.Trim(), input.Password, false, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Problem(statusCode: 401, title: "Unable to sign in with those credentials.");
        }).AllowAnonymous();
        auth.MapPost("/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        });
        auth.MapGet("/session", async (HttpContext context, RushCtrlDbContext db, UserManager<AppUser> users) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var user = await users.GetUserAsync(context.User);
            if (user is null) return Results.Unauthorized();
            var memberships = await (from member in db.Memberships
                                     join org in db.Organizations on member.OrganizationId equals org.Id
                                     where member.UserId == user.Id
                                     orderby org.Name
                                     select new { organizationId = org.Id, organizationName = org.Name, member.Permission }).ToListAsync();
            return Results.Ok(new { user.Id, user.DisplayName, memberships });
        });
    }
    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public sealed record LoginInput(string? Email, string? Password);
}
