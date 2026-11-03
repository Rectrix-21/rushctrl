namespace RushCtrl.Api.Modules.System;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => TypedResults.Ok(new HealthStatus("healthy")))
            .AllowAnonymous().WithName("Liveness").WithTags("System");
        app.MapGet("/api/v1/system", () => TypedResults.Ok(new SystemMetadata("RUSHCTRL", "0.1.0", 0)))
            .AllowAnonymous().WithName("SystemMetadata").WithTags("System");
    }
}

public sealed record HealthStatus(string Status);
public sealed record SystemMetadata(string Name, string Version, int Milestone);
