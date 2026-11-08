using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.System;
using RushCtrl.Api.Modules.Identity;
using RushCtrl.Api.Modules.Scheduling;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<RushCtrlDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database"), database => database.CommandTimeout(3)));
builder.Services.AddScoped<DatabaseReadinessCheck>();
builder.Services.AddScoped<TenantScope>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddScoped<EligibilityService>();
builder.Services.AddSingleton<IWorkspaceNotifier, NullWorkspaceNotifier>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 12;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<RushCtrlDbContext>().AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
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
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "rushctrl.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal) || args.Contains("--seed-demo", StringComparer.Ordinal))
{
    if (args.Contains("--migrate", StringComparer.Ordinal))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RushCtrlDbContext>().Database.MigrateAsync();
    }
    if (args.Contains("--seed-demo", StringComparer.Ordinal)) await DevelopmentSeed.RunAsync(app.Services, app.Configuration, app.Environment);
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
app.MapIdentityEndpoints();
app.MapSchedulingEndpoints();
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapFallback("/{**path}", () => Results.Problem(statusCode: 404, title: "Resource not found")).AllowAnonymous();
app.Run();

public partial class Program;
