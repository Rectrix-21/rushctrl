namespace RushCtrl.Api.Modules.System;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => TypedResults.Ok(new HealthStatus("healthy")))
            .AllowAnonymous().WithName("Liveness").WithTags("System");
        app.MapGet("/health/ready", async (DatabaseReadinessCheck readiness, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var ready = await readiness.IsReady(cancellationToken);
            return Results.Json(new HealthStatus(ready ? "healthy" : "unhealthy"), statusCode: ready ? 200 : 503);
        }).AllowAnonymous().WithName("Readiness").WithTags("System")
            .Produces<HealthStatus>().Produces<HealthStatus>(503);
        app.MapGet("/api/v1/system", () => TypedResults.Ok(new SystemMetadata("RUSHCTRL", "0.1.0", 1)))
            .AllowAnonymous().WithName("SystemMetadata").WithTags("System");
    }
}

public sealed record HealthStatus(string Status);
public sealed record SystemMetadata(string Name, string Version, int Milestone);
