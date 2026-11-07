using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Api;

/// <summary>Management API for legal entities.</summary>
public static class LegalEntityEndpoints
{
    public static RouteGroupBuilder MapLegalEntityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/legal-entities").AddEndpointFilter<AdminTokenFilter>();

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);

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

    private static LegalEntityResponse ToResponse(LegalEntity entity) => new(
        entity.Id.ToString(),
        entity.Type.ToString(),
        entity.DisplayName,
        entity.VerificationState.ToString(),
        entity.CreatedAt);
}
