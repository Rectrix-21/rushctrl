using Microsoft.AspNetCore.SignalR;
using RushCtrl.Api.Modules.Scheduling;

namespace RushCtrl.Api.Modules.Realtime;

public sealed class WorkspaceNotifier(IHubContext<WorkspaceHub> hub) : IWorkspaceNotifier
{
    public Task ChangedAsync(Guid organizationId, Guid? generation, CancellationToken cancellationToken) =>
        hub.Clients.Group(WorkspaceHub.Group(organizationId)).SendAsync("WorkspaceChanged", new { organizationId, generation }, cancellationToken);
}
