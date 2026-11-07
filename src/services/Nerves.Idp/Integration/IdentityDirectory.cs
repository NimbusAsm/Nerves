using Microsoft.Extensions.Options;
using Nerves.Contracts.Identity.V1;
using Nerves.Idp.Options;

namespace Nerves.Idp.Integration;

/// <summary>Result of a credential check performed by the identity service.</summary>
public sealed record CredentialCheck(
    bool Verified,
    string AccountId,
    string LegalEntityId,
    string DisplayName,
    string FailureCode,
    string Email = "");

/// <summary>Thin gRPC client over the identity service contract.</summary>
public sealed class IdentityDirectory(
    IdentityService.IdentityServiceClient client,
    IOptions<IdentityClientOptions> options)
{
    public IdentityClientOptions Options { get; } = options.Value;

    public async Task<CredentialCheck> VerifyCredentialAsync(
        string identifier,
        string password,
        string realm,
        CancellationToken cancellationToken)
    {
        var reply = await client.VerifyCredentialAsync(
            new VerifyCredentialRequest { Identifier = identifier, Password = password, Realm = realm },
            cancellationToken: cancellationToken);

        return new CredentialCheck(
            reply.Verified,
            reply.AccountId,
            reply.LegalEntityId,
            reply.DisplayName,
            reply.FailureCode,
            reply.Email);
    }

    public async Task<GetAccountResponse?> GetAccountAsync(string accountId, CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetAccountAsync(
                new GetAccountRequest { AccountId = accountId },
                cancellationToken: cancellationToken);
        }
        catch (Grpc.Core.RpcException e) when (e.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return null;
        }
    }
}
