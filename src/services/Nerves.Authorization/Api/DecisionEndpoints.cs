using Microsoft.AspNetCore.Mvc;
using Nerves.Authorization.Caching;
using Nerves.Authorization.Policy;
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
        PdpClient pdp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Permission))
        {
            return Results.Problem("permission is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var accountId = request.AccountId ?? string.Empty;
        var legalEntityId = request.LegalEntityId ?? string.Empty;

        var (version, localAllowed) = await ResolveLocalAsync(
            permissions, accountId, legalEntityId, request.Permission, cancellationToken);

        if (pdp.Enabled)
        {
            var decision = await pdp.DecideAsync(accountId, legalEntityId, request.Permission, cancellationToken);
            if (decision is null)
            {
                // Fail closed: an unreachable PDP is an outage, not a permit.
                return Results.Problem(
                    "The policy decision point is unavailable.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                allowed = decision.Allowed,
                reason = decision.Reason,
                source = "pdp",
                authorizationVersion = version,
                permission = request.Permission,
            });
        }

        return Results.Ok(new
        {
            allowed = localAllowed,
            source = "local",
            authorizationVersion = version,
            permission = request.Permission,
        });
    }

    private static async Task<(long Version, bool Allowed)> ResolveLocalAsync(
        CachedPermissions permissions,
        string accountId,
        string legalEntityId,
        string permission,
        CancellationToken cancellationToken)
    {
        var (granted, version) = await permissions.ResolveAsync(accountId, legalEntityId, cancellationToken);
        var allowed = granted.Any(p => string.Equals(p, permission, StringComparison.OrdinalIgnoreCase));
        return (version, allowed);
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
