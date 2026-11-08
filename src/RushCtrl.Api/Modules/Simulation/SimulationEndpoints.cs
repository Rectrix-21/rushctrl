using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Simulation;

public static class SimulationEndpoints
{
    public static void MapSimulationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/simulation", async (SimulationService service, CancellationToken ct) => Results.Ok(SimulationService.View(await service.Open(ct)))).AddEndpointFilter<ManagerFilter>();
        group.MapGet("/simulation", async (SimulationService service, CancellationToken ct) => Results.Ok(SimulationService.View(await service.Owned(ct))));
        group.MapPost("/simulation/apply", async (SimulationService service, CancellationToken ct) => Results.Ok(SimulationService.View(await service.Apply(ct))));
        group.MapPost("/simulation/reset", async (SimulationService service, CancellationToken ct) => Results.Ok(SimulationService.View(await service.Reset(ct))));
    }
}
