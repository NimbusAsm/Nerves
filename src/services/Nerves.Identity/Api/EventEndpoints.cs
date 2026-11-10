using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>Incremental read of the identity domain event stream.</summary>
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
        IdentityDbContext db,
        Guid? accountId = null,
        DateTimeOffset? after = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        var query = db.OutboxEvents.AsNoTracking();

        if (accountId is not null)
        {
            query = query.Where(x => x.AccountId == accountId);
        }

        if (after is not null)
        {
            query = query.Where(x => x.OccurredAt > after);
        }

        var items = await query
            .OrderBy(x => x.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

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
