using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;
using RushCtrl.Api.Modules.Identity;

namespace RushCtrl.Api.Modules.Realtime;

[Authorize]
public sealed class WorkspaceHub(RushCtrlDbContext db) : Hub
{
    public static string Group(Guid organizationId) => "workspace:" + organizationId;
    public async Task JoinWorkspace(Guid organizationId)
    {
        var user = Context.User!.UserId();
        if (!await db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == user, Context.ConnectionAborted))
            throw new HubException("Workspace not found.");
        if (Context.Items.TryGetValue("organization", out var previous) && previous is Guid old)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(old), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(organizationId), Context.ConnectionAborted);
        Context.Items["organization"] = organizationId;
    }
}
