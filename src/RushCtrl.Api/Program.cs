using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.System;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<RushCtrlDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database"), database => database.CommandTimeout(3)));
builder.Services.AddScoped<DatabaseReadinessCheck>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "rushctrl.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>().Database.MigrateAsync();
    return;
}
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    var started = Stopwatch.GetTimestamp();
    context.Response.Headers["X-Request-Id"] = Activity.Current?.Id ?? context.TraceIdentifier;
    await next(context);
    app.Logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
        context.Request.Method, context.Request.Path, context.Response.StatusCode,
        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapSystemEndpoints();
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapFallback("/{**path}", () => Results.Problem(statusCode: 404, title: "Resource not found")).AllowAnonymous();
app.Run();

public partial class Program;
