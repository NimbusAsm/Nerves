using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Session.V1;
using Nerves.Session.Domain;
using Nerves.Session.Persistence;

namespace Nerves.Session.Grpc;

/// <summary>Serves the internal session contract.</summary>
public sealed class SessionGrpcService(SessionDbContext db) : SessionService.SessionServiceBase
{
    public override Task<GetServiceInfoResponse> GetServiceInfo(
        GetServiceInfoRequest request,
        ServerCallContext context) =>
        Task.FromResult(new GetServiceInfoResponse
        {
            Name = "nerves-session",
            Version = typeof(SessionGrpcService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            ContractVersion = "v1",
        });

    public override async Task<CreateSessionResponse> CreateSession(CreateSessionRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "account_id must be a UUID"));
        }

        Guid? legalEntityId = Guid.TryParse(request.LegalEntityId, out var entityId) ? entityId : null;

        var now = DateTimeOffset.UtcNow;
        var idle = TimeSpan.FromMinutes(request.IdleMinutes > 0 ? request.IdleMinutes : 30);
        var absolute = TimeSpan.FromHours(request.AbsoluteHours > 0 ? request.AbsoluteHours : 12);

        var session = Domain.Session.Create(
            accountId,
            legalEntityId,
            request.Realm,
            request.DeviceId,
            request.IpHash,
            request.UserAgentHash,
            request.Amr,
            now,
            idle,
            absolute);

        db.Sessions.Add(session);
        await db.SaveChangesAsync(context.CancellationToken);

        return new CreateSessionResponse
        {
            SessionId = session.Id.ToString(),
            AccountId = session.AccountId.ToString(),
            ExpiresAt = session.AbsoluteExpiresAt.ToString("O"),
        };
    }

    public override async Task<RevokeSessionResponse> RevokeSession(
        RevokeSessionRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.SessionId, out var sessionId))
        {
            return new RevokeSessionResponse { Revoked = false };
        }

        var session = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId, context.CancellationToken);
        if (session is null || session.RevokedAt is not null)
        {
            return new RevokeSessionResponse { Revoked = false };
        }

        session.Revoke(request.Reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(context.CancellationToken);
        return new RevokeSessionResponse { Revoked = true };
    }

    public override async Task<RevokeAccountSessionsResponse> RevokeAccountSessions(
        RevokeAccountSessionsRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new RevokeAccountSessionsResponse();
        }

        var sessions = await db.Sessions
            .Where(x => x.AccountId == accountId && x.RevokedAt == null)
            .ToListAsync(context.CancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions)
        {
            session.Revoke(request.Reason, now);
        }

        await db.SaveChangesAsync(context.CancellationToken);
        return new RevokeAccountSessionsResponse { Revoked = sessions.Count };
    }

    public override async Task<ListSessionsResponse> ListSessions(
        ListSessionsRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            return new ListSessionsResponse();
        }

        var sessions = await db.Sessions.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var response = new ListSessionsResponse();
        response.Sessions.AddRange(sessions.Select(s => new SessionInfo
        {
            SessionId = s.Id.ToString(),
            DeviceId = s.DeviceId,
            CreatedAt = s.CreatedAt.ToString("O"),
            LastSeenAt = s.LastSeenAt.ToString("O"),
            Revoked = s.RevokedAt is not null,
        }));

        return response;
    }
}
