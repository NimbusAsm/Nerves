using Nerves.Contracts.Identity.V1;

namespace Nerves.Idp.Integration;

/// <summary>Thin gRPC client for the identity service's reset contract.</summary>
public sealed class PasswordResetDirectory(IdentityService.IdentityServiceClient client)
{
    public async Task<bool> RequestAsync(
        string identifier,
        string? realm,
        string resetUrlBase,
        CancellationToken cancellationToken)
    {
        var reply = await client.RequestPasswordResetAsync(
            new RequestPasswordResetRequest
            {
                Identifier = identifier,
                Realm = realm ?? string.Empty,
                ResetUrlBase = resetUrlBase,
            },
            cancellationToken: cancellationToken);

        return reply.Accepted;
    }

    public async Task<CompletePasswordResetResponse> CompleteAsync(
        string token,
        string? newPassword,
        CancellationToken cancellationToken) =>
        await client.CompletePasswordResetAsync(
            new CompletePasswordResetRequest { Token = token, NewPassword = newPassword ?? string.Empty },
            cancellationToken: cancellationToken);
}
