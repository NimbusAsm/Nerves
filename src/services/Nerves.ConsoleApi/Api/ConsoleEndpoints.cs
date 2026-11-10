using System.Globalization;
using Nerves.ConsoleApi.Aggregation;
using Nerves.ServiceDefaults.Security;

namespace Nerves.ConsoleApi.Api;

/// <summary>Read-only aggregation endpoints used by the admin console.</summary>
public static class ConsoleEndpoints
{
    public static RouteGroupBuilder MapConsoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/console")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("/overview", OverviewAsync);
        group.MapGet("/account/{id:guid}", AccountAsync);
        group.MapGet("/entity/{id:guid}", EntityAsync);

        return group;
    }

    private static async Task<IResult> OverviewAsync(
        ConsoleAggregator aggregator,
        CancellationToken cancellationToken) =>
        Results.Ok(await aggregator.OverviewAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> AccountAsync(
        Guid id,
        ConsoleAggregator aggregator,
        CancellationToken cancellationToken)
    {
        var response = await aggregator.AccountAsync(id, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.Problem(
                string.Create(CultureInfo.InvariantCulture, $"no account with id {id:D}"),
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(response);
    }

    private static async Task<IResult> EntityAsync(
        Guid id,
        ConsoleAggregator aggregator,
        CancellationToken cancellationToken)
    {
        var response = await aggregator.EntityAsync(id, cancellationToken).ConfigureAwait(false);

        return response is null
            ? Results.Problem(
                string.Create(CultureInfo.InvariantCulture, $"no legal entity with id {id:D}"),
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(response);
    }
}
