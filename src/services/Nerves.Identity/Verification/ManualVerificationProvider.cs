namespace Nerves.Identity.Verification;

/// <summary>
/// Default provider: the attempt stays pending until an administrator records a
/// decision through the existing verify/reject endpoints. It never calls out.
/// </summary>
public sealed class ManualVerificationProvider : IVerificationProvider
{
    public string Name => "manual";

    public Task<VerificationProviderResponse> StartAsync(
        VerificationProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new VerificationProviderResponse(
            VerificationProviderOutcome.AwaitingManualReview,
            null,
            "Awaiting manual review by an administrator."));
    }
}
