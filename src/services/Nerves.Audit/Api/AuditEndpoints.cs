using Microsoft.EntityFrameworkCore;
using Nerves.Audit.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Audit.Api;

/// <summary>Read-only access to the audit trail plus chain verification.</summary>
public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").AddEndpointFilter<AdminTokenFilter>();

        group.MapGet("/audit-events", ListAsync);
        group.MapGet("/audit-events/verify", VerifyAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        AuditDbContext db,
        string? stream = null,
        string? actorId = null,
        string? action = null,
        string? result = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 50,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var query = db.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(stream)) query = query.Where(x => x.Stream == stream);
        if (!string.IsNullOrWhiteSpace(actorId)) query = query.Where(x => x.ActorId == actorId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action);
        if (!string.IsNullOrWhiteSpace(result)) query = query.Where(x => x.Result == result);
        if (from is not null) query = query.Where(x => x.OccurredAt >= from);
        if (to is not null) query = query.Where(x => x.OccurredAt <= to);
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
        {
            query = query.Where(x => x.Id.CompareTo(cursorId) > 0);
        }

        var items = await query
            .OrderBy(x => x.Stream).ThenBy(x => x.Sequence)
            .Take(limit + 1)
            .Select(x => new
            {
                id = x.Id,
                x.Stream,
                x.Sequence,
                x.OccurredAt,
                x.ActorType,
                x.ActorId,
                x.Action,
                x.TargetType,
                x.TargetId,
                x.Result,
                x.TraceId,
                x.Hash,
            })
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        if (hasMore) items = items.Take(limit).ToList();

        return Results.Ok(new
        {
            items,
            nextCursor = hasMore ? items[^1].id.ToString() : null,
        });
    }

    private static async Task<IResult> VerifyAsync(
        AuditDbContext db,
        string? stream = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(stream)) query = query.Where(x => x.Stream == stream);

        var events = await query.OrderBy(x => x.Stream).ThenBy(x => x.Sequence).ToListAsync(cancellationToken);

        string? expectedPrevious = null;
        foreach (var auditEvent in events)
        {
            if (auditEvent.RecomputeHash() != auditEvent.Hash)
            {
                return Results.Ok(new { valid = false, brokenAt = auditEvent.Id, reason = "hash_mismatch" });
            }

            if (expectedPrevious is not null && auditEvent.PreviousHash != expectedPrevious)
            {
                return Results.Ok(new { valid = false, brokenAt = auditEvent.Id, reason = "chain_mismatch" });
            }

            expectedPrevious = auditEvent.Hash;
        }

        return Results.Ok(new { valid = true, count = events.Count });
    }
}
