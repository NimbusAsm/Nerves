using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>Aggregates shown on the admin overview page.</summary>
public static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/stats").RequireAuthorization(Nerves.ServiceDefaults.Security.BearerAuthentication.AdminPolicy);

        group.MapGet("/", async (IdentityStore store, CancellationToken cancellationToken) =>
        {
            // Account counters fan out across shards; entities live in the global database.
            var accounts = await store.CountAccountsAsync(null, cancellationToken);
            var disabled = await store.CountAccountsByStatusAsync(AccountStatus.Disabled, cancellationToken);

            await using var global = store.Global();
            var entities = await global.LegalEntities.CountAsync(cancellationToken);
            var verified = await global.LegalEntities.CountAsync(x => x.VerificationState == VerificationState.Verified, cancellationToken);

            return Results.Ok(new
            {
                accounts,
                disabledAccounts = disabled,
                legalEntities = entities,
                verifiedLegalEntities = verified,
            });
        });

        return group;
    }
}
