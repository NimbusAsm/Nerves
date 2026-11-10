using Microsoft.EntityFrameworkCore;
using Nerves.ServiceDefaults.Security;
using Nerves.Session.Persistence;

namespace Nerves.Session.Api;

/// <summary>Management API for session inspection and revocation.</summary>
public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization(Nerves.ServiceDefaults.Security.BearerAuthentication.AdminPolicy);

        group.MapGet("/accounts/{accountId:guid}/sessions", ListAsync);
        group.MapDelete("/sessions/{sessionId:guid}", RevokeAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        Guid accountId,
        SessionDbContext db,
        CancellationToken cancellationToken)
    {
        var sessions = await db.Sessions.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.DeviceId,
                x.CreatedAt,
                x.LastSeenAt,
                x.AbsoluteExpiresAt,
                revoked = x.RevokedAt != null,
                x.RevokedReason,
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(sessions);
    }

    private static async Task<IResult> RevokeAsync(
        Guid sessionId,
        SessionDbContext db,
        CancellationToken cancellationToken)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        session.Revoke("revoked by administrator", DateTimeOffset.UtcNow);
        db.OutboxEvents.Add(Domain.OutboxEvent.Create(
            "session.admin_revoked",
            "session",
            session.Id.ToString(),
            "{}",
            DateTimeOffset.UtcNow));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }
}
