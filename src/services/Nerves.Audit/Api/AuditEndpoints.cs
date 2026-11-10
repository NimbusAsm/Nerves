using Microsoft.EntityFrameworkCore;
using Nerves.Audit.Archiving;
using Nerves.Audit.Persistence;
using Nerves.ServiceDefaults.Security;
namespace Nerves.Audit.Api;

/// <summary>Read-only access to the audit trail plus chain verification.</summary>
public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization(Nerves.ServiceDefaults.Security.BearerAuthentication.AdminPolicy);

        group.MapGet("/audit-events", ListAsync);
        group.MapGet("/audit-events/verify", VerifyAsync);
        group.MapGet("/audit-events/export", ExportAsync);
        group.MapPost("/audit-events/archive", ArchiveAsync);
        group.MapGet("/audit-events/archive/status", ArchiveStatusAsync);

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

    private static async Task<IResult> ExportAsync(
        AuditDbContext db,
        string? format = "csv",
        int limit = 5000,
        CancellationToken cancellationToken = default)
    {
        var normalized = (format ?? "csv").Trim().ToLowerInvariant();
        if (normalized is not ("csv" or "json"))
        {
            return Results.Problem("format must be csv or json.", statusCode: StatusCodes.Status400BadRequest);
        }

        limit = Math.Clamp(limit, 1, 20000);

        var events = await db.AuditEvents
            .AsNoTracking()
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.Sequence)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var fileName = AuditExport.FileName(normalized, now);

        if (normalized == "csv")
        {
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(AuditExport.ToCsv(events)),
                "text/csv; charset=utf-8",
                fileName);
        }

        var payload = System.Text.Json.JsonSerializer.Serialize(events);
        return Results.File(
            System.Text.Encoding.UTF8.GetBytes(payload),
            "application/json; charset=utf-8",
            fileName);
    }

    private static async Task<IResult> ArchiveAsync(
        AuditArchiveService archive,
        CancellationToken cancellationToken)
    {
        var result = await archive.RunAsync(cancellationToken);
        return Results.Ok(new { archived = result.Events, files = result.Files });
    }

    private static async Task<IResult> ArchiveStatusAsync(
        AuditDbContext db,
        Microsoft.Extensions.Options.IOptions<ArchiveOptions> options,
        CancellationToken cancellationToken)
    {
        var pending = await db.AuditEvents.CountAsync(x => x.ArchivedAt == null, cancellationToken);
        var archived = await db.AuditEvents.CountAsync(x => x.ArchivedAt != null, cancellationToken);
        var last = await db.AuditEvents
            .Where(x => x.ArchivedAt != null)
            .MaxAsync(x => x.ArchivedAt, cancellationToken);

        return Results.Ok(new
        {
            pending,
            archived,
            lastArchivedAt = last,
            directory = Path.GetFullPath(options.Value.Directory),
        });
    }
}
