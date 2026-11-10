namespace Nerves.Notification.Consumption;

/// <summary>Read access to the identity event stream and account directory.</summary>
public interface IIdentityEventClient
{
    /// <summary>Lists events that occurred after <paramref name="after"/>, oldest first.</summary>
    Task<IReadOnlyList<EventEnvelope>> ListEventsAsync(
        DateTimeOffset? after,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Returns the account, or <see langword="null"/> when it no longer exists.</summary>
    Task<AccountSnapshot?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);
}
