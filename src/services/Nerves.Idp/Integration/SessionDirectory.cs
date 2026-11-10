using Microsoft.Extensions.Options;
using Nerves.Contracts.Session.V1;
using Nerves.Idp.Options;

namespace Nerves.Idp.Integration;

/// <summary>Creates and revokes interactive sessions through the session service.</summary>
public sealed class SessionDirectory(
    SessionService.SessionServiceClient client,
    IOptions<SessionLifetimeOptions> lifetime)
{
    public async Task<string?> CreateAsync(
        string accountId,
        string legalEntityId,
        string realm,
        string deviceId,
        string ipHash,
        string userAgentHash,
        IEnumerable<string> amr,
        CancellationToken cancellationToken)
    {
        var reply = await client.CreateSessionAsync(
            new CreateSessionRequest
            {
                AccountId = accountId,
                LegalEntityId = legalEntityId,
                Realm = realm,
                DeviceId = deviceId,
                IpHash = ipHash,
                UserAgentHash = userAgentHash,
                Amr = { amr },
                IdleMinutes = lifetime.Value.IdleMinutes,
                AbsoluteHours = lifetime.Value.AbsoluteHours,
            },
            cancellationToken: cancellationToken);

        return reply.SessionId;
    }

    public async Task RevokeAllAsync(string accountId, string reason, CancellationToken cancellationToken)
    {
        await client.RevokeAccountSessionsAsync(
            new RevokeAccountSessionsRequest { AccountId = accountId, Reason = reason },
            cancellationToken: cancellationToken);
    }
}
