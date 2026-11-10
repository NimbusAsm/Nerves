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
        SessionStore store,
        CancellationToken cancellationToken)
    {
        var sessions = await store.ListAsync(accountId, cancellationToken);

        return Results.Ok(sessions.Select(x => new
        {
            x.Id,
            x.DeviceId,
            x.CreatedAt,
            x.LastSeenAt,
            x.AbsoluteExpiresAt,
            revoked = x.RevokedAt != null,
            x.RevokedReason,
        }));
    }

    private static async Task<IResult> RevokeAsync(
        Guid sessionId,
        SessionStore store,
        CancellationToken cancellationToken)
    {
        var revoked = await store.RevokeSessionAsync(sessionId, "revoked by administrator", cancellationToken);
        if (!revoked)
        {
            var existing = await store.FindAsync(sessionId, cancellationToken);
            return existing is null ? Results.NotFound() : Results.Ok();
        }

        return Results.Ok();
    }
}
