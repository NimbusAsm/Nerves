using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>Incremental read of the identity domain event stream.</summary>
/// <remarks>
/// Outbox rows live next to the account they belong to, i.e. on that account's
/// shard. A list without an account id therefore pulls from every shard and
/// merges by occurrence time.
/// </remarks>
public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/events")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("/", ListAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        IdentityStore store,
        Guid? accountId = null,
        DateTimeOffset? after = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        List<Domain.OutboxEvent> items;
        if (accountId is not null)
        {
            // One shard owns the account, so this is a single index scan.
            await using var db = store.AccountShard(accountId.Value);
            var query = db.OutboxEvents.AsNoTracking().Where(x => x.AccountId == accountId);
            if (after is not null)
            {
                query = query.Where(x => x.OccurredAt > after);
            }

            items = await query.OrderBy(x => x.OccurredAt).Take(limit).ToListAsync(cancellationToken);
        }
        else
        {
            var merged = await store.QueryShardsAsync(async db =>
            {
                var query = db.OutboxEvents.AsNoTracking();
                if (after is not null)
                {
                    query = query.Where(x => x.OccurredAt > after);
                }

                return await query.OrderBy(x => x.OccurredAt).Take(limit).ToListAsync(cancellationToken);
            }, cancellationToken);

            items = merged.OrderBy(x => x.OccurredAt).Take(limit).ToList();
        }

        return Results.Ok(items.Select(x => new
        {
            id = x.Id,
            type = x.Type,
            accountId = x.AccountId,
            legalEntityId = x.LegalEntityId,
            payload = x.Payload,
            occurredAt = x.OccurredAt,
            dispatchedAt = x.DispatchedAt,
        }));
    }
}
