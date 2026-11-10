namespace Nerves.Client;

/// <summary>
/// Builds <c>GET /api/v1/events</c>. The endpoint returns a plain array ordered by
/// <c>occurredAt</c>; callers advance with the last event's timestamp.
/// </summary>
public sealed record EventListBuilder : NervesRequest
{
    internal EventListBuilder(ApiRequester api) => Api = api;

    internal Guid? AccountId { get; init; }

    internal DateTimeOffset? AfterInstant { get; init; }

    internal int? EventLimit { get; init; }

    /// <summary>Restricts the stream to one account.</summary>
    public EventListBuilder ForAccount(Guid accountId) => this with { AccountId = accountId };

    /// <summary>Returns events strictly newer than the given instant.</summary>
    public EventListBuilder Since(DateTimeOffset after) => this with { AfterInstant = after };

    /// <summary>Sets the batch size (server default 100, max 500).</summary>
    public EventListBuilder Limit(int limit) => this with { EventLimit = limit };

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<IdentityEvent>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<IdentityEvent>>(
            "events",
            QueryString.Build(
            [
                ("accountId", AccountId?.ToString("D")),
                ("after", AfterInstant?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                ("limit", EventLimit?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ]),
            cancellationToken);
}

/// <summary>Entry point for the identity domain event stream.</summary>
public sealed class EventsResource
{
    private readonly ApiRequester _api;

    internal EventsResource(ApiRequester api) => _api = api;

    /// <summary>Starts an incremental read.</summary>
    public EventListBuilder List() => new(_api);
}
