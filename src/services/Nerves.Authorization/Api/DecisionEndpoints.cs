using Microsoft.AspNetCore.Mvc;
using Nerves.Authorization.Caching;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Authorization.Api;

/// <summary>Authorization checks with a version-aware permission cache.</summary>
public static class DecisionEndpoints
{
    public static RouteGroupBuilder MapDecisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/decisions")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapPost("/check", CheckAsync);
        group.MapGet("/stats", Stats);

        return group;
    }

    private static async Task<IResult> CheckAsync(
        [FromBody] DecisionRequest request,
        CachedPermissions permissions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Permission))
        {
            return Results.Problem("permission is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var (granted, version) = await permissions.ResolveAsync(
            request.AccountId ?? string.Empty,
            request.LegalEntityId ?? string.Empty,
            cancellationToken);

        var allowed = granted.Any(p => string.Equals(p, request.Permission, StringComparison.OrdinalIgnoreCase));

        return Results.Ok(new
        {
            allowed,
            authorizationVersion = version,
            permission = request.Permission,
        });
    }

    private static IResult Stats(PermissionCache cache)
    {
        var stats = cache.Snapshot();
        return Results.Ok(new
        {
            entries = stats.Entries,
            hits = stats.Hits,
            misses = stats.Misses,
            hitRate = Math.Round(stats.HitRate, 4),
        });
    }
}

/// <summary>Decision payload.</summary>
public sealed record DecisionRequest(string? AccountId, string? LegalEntityId, string Permission);
