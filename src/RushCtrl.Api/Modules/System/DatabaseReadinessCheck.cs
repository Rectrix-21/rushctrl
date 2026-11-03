using Microsoft.EntityFrameworkCore;
using RushCtrl.Api.Infrastructure;

namespace RushCtrl.Api.Modules.System;

public sealed class DatabaseReadinessCheck(RushCtrlDbContext database, ILogger<DatabaseReadinessCheck> logger)
{
    public async Task<bool> IsReady(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            if (!await database.Database.CanConnectAsync(timeout.Token)) return false;
            if ((await database.Database.GetPendingMigrationsAsync(timeout.Token)).Any()) return false;
            await database.Organizations.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(timeout.Token);
            await database.Locations.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(timeout.Token);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogWarning("Database readiness failed ({ExceptionType})", exception.GetType().Name);
            return false;
        }
    }
}
