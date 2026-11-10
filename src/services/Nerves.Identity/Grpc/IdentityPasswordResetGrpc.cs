using Grpc.Core;
using Microsoft.Extensions.Options;
using Nerves.Contracts.Identity.V1;
using Nerves.Identity.PasswordReset;

namespace Nerves.Identity.Grpc;

/// <summary>Password reset entry points used by the IdP, which owns the login API.</summary>
public sealed partial class IdentityGrpcService
{
    private IOptions<PasswordResetOptions> PasswordResetOptions => passwordResetOptions;

    public override async Task<RequestPasswordResetResponse> RequestPasswordReset(
        RequestPasswordResetRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accepted = await passwordReset.RequestAsync(
            request.Identifier,
            request.Realm,
            string.IsNullOrWhiteSpace(request.ResetUrlBase) ? null : request.ResetUrlBase,
            context.GetHttpContext()?.TraceIdentifier,
            context.CancellationToken);

        return new RequestPasswordResetResponse
        {
            Accepted = true,
            ExpiresInSeconds = PasswordResetOptions.Value.LifetimeMinutes * 60,
            FailureCode = accepted ? string.Empty : "verification_failed",
        };
    }

    public override async Task<CompletePasswordResetResponse> CompletePasswordReset(
        CompletePasswordResetRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await passwordReset.ConfirmAsync(
            request.Token,
            request.NewPassword,
            context.GetHttpContext()?.TraceIdentifier,
            context.CancellationToken);

        return result.IsSuccess
            ? new CompletePasswordResetResponse { Reset = true, AccountId = result.Value.ToString() }
            : new CompletePasswordResetResponse { Reset = false, FailureCode = "invalid_reset_token" };
    }
}
