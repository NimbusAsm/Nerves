namespace Nerves.Notification.Domain;

/// <summary>
/// Exponential backoff used after a failed delivery attempt. The first retry
/// waits <paramref name="baseDelay"/>, every further retry doubles it until
/// <paramref name="maxDelay"/> caps the wait.
/// </summary>
public sealed record RetryPolicy
{
    public RetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "maxAttempts must be at least 1");
        }

        if (baseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay), baseDelay, "baseDelay must be positive");
        }

        if (maxDelay < baseDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "maxDelay must not be shorter than baseDelay");
        }

        MaxAttempts = maxAttempts;
        BaseDelay = baseDelay;
        MaxDelay = maxDelay;
    }

    public int MaxAttempts { get; }

    public TimeSpan BaseDelay { get; }

    public TimeSpan MaxDelay { get; }

    /// <summary>
    /// Delay before the next attempt. <paramref name="attempts"/> is the number
    /// of attempts already made, so 1 yields the base delay.
    /// </summary>
    public TimeSpan DelayAfterFailure(int attempts)
    {
        if (attempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "attempts must be at least 1");
        }

        var exponent = Math.Min(attempts - 1, 62);
        var ticks = BaseDelay.Ticks * Math.Pow(2, exponent);

        return ticks >= MaxDelay.Ticks ? MaxDelay : TimeSpan.FromTicks((long)ticks);
    }
}
