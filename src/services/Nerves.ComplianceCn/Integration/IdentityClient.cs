using Grpc.Net.Client;
using Nerves.Contracts.Identity.V1;

namespace Nerves.ComplianceCn.Integration;

/// <summary>Resolves and disables accounts for data subject requests.</summary>
public sealed class IdentityClient
{
    private readonly IdentityService.IdentityServiceClient _client;

    public IdentityClient(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var address = configuration["Identity:GrpcAddress"] ?? "http://127.0.0.1:5202";
        _client = new IdentityService.IdentityServiceClient(GrpcChannel.ForAddress(address));
    }

    public async Task<GetAccountResponse?> GetAccountAsync(string accountId, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetAccountAsync(
                new GetAccountRequest { AccountId = accountId },
                cancellationToken: cancellationToken);
        }
        catch (global::Grpc.Core.RpcException)
        {
            return null;
        }
    }

    public async Task<bool> DisableAsync(string accountId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await _client.DisableAccountAsync(
                new DisableAccountRequest { AccountId = accountId, Reason = reason },
                cancellationToken: cancellationToken);

            return reply.Disabled;
        }
        catch (global::Grpc.Core.RpcException)
        {
            return false;
        }
    }
}
