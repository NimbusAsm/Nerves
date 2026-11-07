using Nerves.Contracts.Authorization.V1;

namespace Nerves.Idp.Integration;

/// <summary>Reads the permission snapshot that goes into tokens.</summary>
public sealed class AuthorizationDirectory(AuthorizationService.AuthorizationServiceClient client)
{
    public async Task<(string[] Permissions, long Version)> GetPermissionsAsync(
        string accountId,
        string legalEntityId,
        string realm,
        CancellationToken cancellationToken)
    {
        var reply = await client.ListPermissionsAsync(
            new ListPermissionsRequest
            {
                AccountId = accountId,
                LegalEntityId = legalEntityId,
                Realm = realm,
            },
            cancellationToken: cancellationToken);

        return (reply.Permissions.ToArray(), reply.AuthorizationVersion);
    }
}
