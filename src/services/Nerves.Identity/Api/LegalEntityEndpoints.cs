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
        group.MapPost("/{id:guid}/verify", VerifyAsync);
        group.MapPost("/{id:guid}/reject", RejectAsync);
        group.MapPost("/{id:guid}/expire", ExpireAsync);
        group.MapPost("/{id:guid}/disable", DisableAsync);
        group.MapPost("/{id:guid}/enable", EnableAsync);

        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateLegalEntityRequest request,
        IdentityDbContext db,
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
        IdentityDbContext db,
        int limit = 50,
        string? cursor = null,
        string? state = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

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
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.LegalEntities.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? Results.NotFound() : Results.Ok(ToResponse(entity));
    }

    private static Task<IResult> VerifyAsync(
        Guid id,
        VerificationRequest? request,
        IdentityDbContext db,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionVerificationAsync(
            id,
            db,
            audit,
            http,
            "legal-entity.verified",
            (entity, now) => entity.MarkVerified(request?.ProviderReference, now),
            cancellationToken);

    private static Task<IResult> RejectAsync(
        Guid id,
        VerificationRequest? request,
        IdentityDbContext db,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionVerificationAsync(
            id,
            db,
            audit,
            http,
            "legal-entity.rejected",
            (entity, now) => entity.MarkRejected(request?.ProviderReference, now),
            cancellationToken);

    private static Task<IResult> ExpireAsync(
        Guid id,
        IdentityDbContext db,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken) =>
        TransitionVerificationAsync(
            id,
            db,
            audit,
            http,
            "legal-entity.expired",
            static (entity, now) => entity.Expire(now),
            cancellationToken);

    /// <summary>
    /// Applies a verification transition and fans out one event per owned
    /// account inside the same transaction (invariant I7).
    /// </summary>
    private static async Task<IResult> TransitionVerificationAsync(
        Guid id,
        IdentityDbContext db,
        AuditClient audit,
        HttpContext http,
        string action,
        Func<LegalEntity, DateTimeOffset, Nerves.Core.Result> transition,
        CancellationToken cancellationToken)
    {
        var entity = await db.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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

        var accounts = await db.NetworkAccounts
            .Where(x => x.LegalEntityId == id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            legalEntityId = entity.Id,
            state = entity.VerificationState.ToString(),
        });

        foreach (var accountId in accounts)
        {
            db.OutboxEvents.Add(OutboxEvent.Create("realname.changed", accountId, entity.Id, payload, now));
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            action, "admin", null, "legal-entity", entity.Id.ToString(), "success",
            http.TraceIdentifier, null, cancellationToken);

        return Results.Ok(ToResponse(entity));
    }

    private static async Task<IResult> DisableAsync(
        Guid id,
        IdentityDbContext db,
        SessionClient sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var entity = await db.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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

        var accounts = await db.NetworkAccounts
            .Where(x => x.LegalEntityId == id)
            .ToListAsync(cancellationToken);

        var disabled = 0;
        foreach (var account in accounts)
        {
            if (account.Disable(now).IsSuccess)
            {
                disabled++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var revoked = 0;
        foreach (var account in accounts)
        {
            revoked += await sessions.RevokeAllAsync(account.Id.ToString(), "legal-entity.disabled", cancellationToken);
        }

        await audit.AppendAsync(
            "legal-entity.disabled", "admin", null, "legal-entity", entity.Id.ToString(), "success",
            http.TraceIdentifier, null, cancellationToken);

        return Results.Ok(new LegalEntityTransitionResponse(
            entity.Id.ToString(), entity.Status.ToString(), disabled, revoked));
    }

    private static async Task<IResult> EnableAsync(
        Guid id,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var result = entity.Enable(DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            return Results.Problem(result.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(cancellationToken);
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
