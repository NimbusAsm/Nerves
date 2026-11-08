using Nerves.Contracts.Session.V1;

namespace Nerves.Idp.Integration;

/// <summary>Creates and revokes interactive sessions through the session service.</summary>
public sealed class SessionDirectory(SessionService.SessionServiceClient client)
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
                IdleMinutes = 30,
                AbsoluteHours = 12,
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
