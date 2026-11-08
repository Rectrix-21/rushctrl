using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RushCtrl.Api.Modules.Scheduling;

public sealed class ScheduleErrors : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (ScheduleRuleException e) { return Results.Problem(statusCode: e.Status, title: e.Message); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23P01" or "23505" or "40001" or "40P01" }) { return Conflict(); }
        catch (PostgresException e) when (e.SqlState is "40001" or "40P01") { return Conflict(); }
    }

    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "The schedule changed or conflicts with another saved entry. Refresh and try again.");
}
