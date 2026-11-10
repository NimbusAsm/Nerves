using Grpc.Net.Client;
using Nerves.Contracts.Session.V1;

namespace Nerves.Identity.Integration;

/// <summary>Revokes sessions when an account is disabled or locked.</summary>
public sealed class SessionClient
{
    private readonly SessionService.SessionServiceClient _client;

    public SessionClient(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var address = configuration["Session:GrpcAddress"] ?? "http://127.0.0.1:5203";
        _client = new SessionService.SessionServiceClient(GrpcChannel.ForAddress(address));
    }

    public async Task<int> RevokeAllAsync(string accountId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await _client.RevokeAccountSessionsAsync(
                new RevokeAccountSessionsRequest { AccountId = accountId, Reason = reason },
                cancellationToken: cancellationToken);

            return reply.Revoked;
        }
        catch (global::Grpc.Core.RpcException)
        {
            return 0;
        }
    }
}
