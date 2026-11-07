using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Api;

/// <summary>Aggregates shown on the admin overview page.</summary>
public static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/stats").AddEndpointFilter<AdminTokenFilter>();

        group.MapGet("/", async (IdentityDbContext db, CancellationToken cancellationToken) =>
        {
            var accounts = await db.NetworkAccounts.CountAsync(cancellationToken);
            var disabled = await db.NetworkAccounts.CountAsync(x => x.Status == AccountStatus.Disabled, cancellationToken);
            var entities = await db.LegalEntities.CountAsync(cancellationToken);
            var verified = await db.LegalEntities.CountAsync(x => x.VerificationState == VerificationState.Verified, cancellationToken);

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
