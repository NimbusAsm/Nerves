using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Integration;
using Nerves.Identity.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>Management API for legal entities.</summary>
public static class LegalEntityEndpoints
{
    public static RouteGroupBuilder MapLegalEntityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/legal-entities").RequireAuthorization(Nerves.ServiceDefaults.Security.BearerAuthentication.AdminPolicy);

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/accounts", ListAccountsAsync);
        group.MapPost("/{id:guid}/verify", VerifyAsync);
        group.MapPost("/{id:guid}/reject", RejectAsync);
        group.MapPost("/{id:guid}/expire", ExpireAsync);
        group.MapPost("/{id:guid}/disable", DisableAsync);
        group.MapPost("/{id:guid}/enable", EnableAsync);

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateLegalEntityRequest request,
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<LegalEntityType>(request.Type, ignoreCase: true, out var type))
        {
            return Results.Problem("Unknown legal entity type.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<IdDocumentType>(request.DocumentType, ignoreCase: true, out var documentType))
        {
            return Results.Problem("Unknown document type.", statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var realNameResult = RealNameId.Create(documentType, request.DocumentHash, request.Country);
        if (realNameResult.IsFailure)
        {
            return Results.Problem(realNameResult.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        await using var db = store.Global();
        if (await db.LegalEntities.AnyAsync(x => x.RealName.Hash == realNameResult.Value.Hash, cancellationToken))
        {
            return Results.Problem("The identity document is already registered.", statusCode: StatusCodes.Status409Conflict);
        }

        var entityResult = LegalEntity.Create(type, request.DisplayName, realNameResult.Value, now);
        if (entityResult.IsFailure)
        {
            return Results.Problem(entityResult.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        db.LegalEntities.Add(entityResult.Value);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/v1/legal-entities/{entityResult.Value.Id}",
            ToResponse(entityResult.Value));
    }

    private static async Task<IResult> ListAsync(
        IdentityStore store,
        int limit = 50,
        string? cursor = null,
        string? state = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        await using var db = store.Global();
        var entities = db.LegalEntities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(state)
            && Enum.TryParse<VerificationState>(state, ignoreCase: true, out var parsedState))
        {
            entities = entities.Where(x => x.VerificationState == parsedState);
        }

        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
        {
            entities = entities.Where(x => x.Id.CompareTo(cursorId) > 0);
        }

        var items = await entities.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = items.Count > limit;
        if (hasMore)
        {
            items = items.Take(limit).ToList();
        }

        var page = new PageResponse<LegalEntityResponse>(
            items.Select(ToResponse).ToList(),
            hasMore ? items[^1].Id.ToString() : null);

        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        await using var db = store.Global();
        var entity = await db.LegalEntities.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? Results.NotFound() : Results.Ok(ToResponse(entity));
    }

    /// <summary>
    /// Accounts owned by the entity. Accounts are sharded, so this fans out and
    /// merges instead of joining against the global database.
    /// </summary>
    private static async Task<IResult> ListAccountsAsync(
        Guid id,
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        await using var global = store.Global();
        if (!await global.LegalEntities.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var accounts = await store.ListAccountsForEntityAsync(id, cancellationToken);

        return Results.Ok(new
        {
            legalEntityId = id,
            items = accounts
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    realm = x.Realm,
                    username = x.Username,
                    email = x.Email,
                    status = x.Status.ToString(),
                    shard = store.ShardFor(x.Id),
                })
                .ToList(),
        });
    }

    private static Task<IResult> VerifyAsync(
        Guid id,
        VerificationRequest? request,
        IdentityStore store,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionVerificationAsync(
            id,
            store,
            audit,
            http,
            "legal-entity.verified",
            (entity, now) => entity.MarkVerified(request?.ProviderReference, now),
            cancellationToken);

    private static Task<IResult> RejectAsync(
        Guid id,
        VerificationRequest? request,
        IdentityStore store,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionVerificationAsync(
            id,
            store,
            audit,
            http,
            "legal-entity.rejected",
            (entity, now) => entity.MarkRejected(request?.ProviderReference, now),
            cancellationToken);

    private static Task<IResult> ExpireAsync(
        Guid id,
        IdentityStore store,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionVerificationAsync(
            id,
            store,
            audit,
            http,
            "legal-entity.expired",
            static (entity, now) => entity.Expire(now),
            cancellationToken);

    /// <summary>
    /// Applies a verification transition and fans out one event per owned
    /// account (invariant I7). Accounts live on different shards, so each event
    /// is written to the shard that owns its account.
    /// </summary>
    private static async Task<IResult> TransitionVerificationAsync(
        Guid id,
        IdentityStore store,
        AuditClient audit,
        HttpContext http,
        string action,
        Func<LegalEntity, DateTimeOffset, Nerves.Core.Result> transition,
        CancellationToken cancellationToken)
    {
        await using var global = store.Global();
        var entity = await global.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var result = transition(entity, now);
        if (result.IsFailure)
        {
            return Results.Problem(result.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await global.SaveChangesAsync(cancellationToken);

        var accounts = await store.ListAccountsForEntityAsync(id, cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            legalEntityId = entity.Id,
            state = entity.VerificationState.ToString(),
        });

        foreach (var account in accounts)
        {
            await store.AddEventAsync(
                OutboxEvent.Create("realname.changed", account.Id, entity.Id, payload, now),
                cancellationToken);
        }

        await audit.AppendAsync(
            action, "admin", null, "legal-entity", entity.Id.ToString(), "success",
            http.TraceIdentifier, null, cancellationToken);

        return Results.Ok(ToResponse(entity));
    }

    private static async Task<IResult> DisableAsync(
        Guid id,
        IdentityStore store,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        await using var global = store.Global();
        var entity = await global.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var result = entity.Disable(now);
        if (result.IsFailure)
        {
            return Results.Problem(result.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await global.SaveChangesAsync(cancellationToken);

        var disabled = 0;
        var accountIds = new List<Guid>();
        await store.MutateShardsAsync(async db =>
        {
            var accounts = await db.NetworkAccounts
                .Where(x => x.LegalEntityId == id)
                .ToListAsync(cancellationToken);

            foreach (var account in accounts)
            {
                accountIds.Add(account.Id);
                if (account.Disable(now).IsSuccess)
                {
                    disabled++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return accounts.Count;
        }, cancellationToken);

        var revoked = 0;
        foreach (var accountId in accountIds)
        {
            revoked += await sessions.RevokeAllAsync(accountId.ToString(), "legal-entity.disabled", cancellationToken);
        }

        await audit.AppendAsync(
            "legal-entity.disabled", "admin", null, "legal-entity", entity.Id.ToString(), "success",
            http.TraceIdentifier, null, cancellationToken);

        return Results.Ok(new LegalEntityTransitionResponse(
            entity.Id.ToString(), entity.Status.ToString(), disabled, revoked));
    }

    private static async Task<IResult> EnableAsync(
        Guid id,
        IdentityStore store,
        CancellationToken cancellationToken)
    {
        await using var global = store.Global();
        var entity = await global.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var result = entity.Enable(DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return Results.Problem(result.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await global.SaveChangesAsync(cancellationToken);
        return Results.Ok(new LegalEntityTransitionResponse(entity.Id.ToString(), entity.Status.ToString(), 0, 0));
    }

    private static LegalEntityResponse ToResponse(LegalEntity entity) => new(
        entity.Id.ToString(),
        entity.Type.ToString(),
        entity.DisplayName,
        entity.VerificationState.ToString(),
        entity.CreatedAt,
        entity.Status.ToString());
}
