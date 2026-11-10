namespace Nerves.Client;

/// <summary>Builds <c>POST /api/v1/decisions/check</c>.</summary>
public sealed record DecisionCheckBuilder : NervesRequest
{
    internal DecisionCheckBuilder(ApiRequester api) => Api = api;

    internal string? Permission { get; init; }

    internal Guid? AccountId { get; init; }

    internal Guid? LegalEntityId { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the permission code to check.</summary>
    public DecisionCheckBuilder WithPermission(string permission) => this with { Permission = permission };

    /// <summary>Sets the account the decision is about.</summary>
    public DecisionCheckBuilder ForAccount(Guid accountId) => this with { AccountId = accountId };

    /// <summary>Sets the legal entity the decision is about.</summary>
    public DecisionCheckBuilder ForEntity(Guid legalEntityId) => this with { LegalEntityId = legalEntityId };

    /// <summary>Overrides the generated idempotency key.</summary>
    public DecisionCheckBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">The permission is missing.</exception>
    public Task<DecisionResult> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Permission))
        {
            throw new InvalidOperationException("WithPermission() is required before SendAsync().");
        }

        var body = new
        {
            accountId = AccountId?.ToString("D"),
            legalEntityId = LegalEntityId?.ToString("D"),
            permission = Permission,
        };

        return Api.PostAsync<DecisionResult>("decisions/check", body, IdempotencyKey, cancellationToken);
    }
}

/// <summary>Builds <c>GET /api/v1/decisions/stats</c>.</summary>
public sealed record DecisionStatsBuilder : NervesRequest
{
    internal DecisionStatsBuilder(ApiRequester api) => Api = api;

    /// <summary>Sends the request.</summary>
    public Task<DecisionStats> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<DecisionStats>("decisions/stats", null, cancellationToken);
}

/// <summary>Entry point for authorization decisions.</summary>
public sealed class DecisionsResource
{
    private readonly ApiRequester _api;

    internal DecisionsResource(ApiRequester api) => _api = api;

    /// <summary>Starts a single decision check.</summary>
    public DecisionCheckBuilder Check(string permission) => new(_api) { Permission = permission };

    /// <summary>Starts a request for permission-cache statistics.</summary>
    public DecisionStatsBuilder Stats() => new(_api);
}
