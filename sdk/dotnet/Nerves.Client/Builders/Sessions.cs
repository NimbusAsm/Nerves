namespace Nerves.Client;

/// <summary>Builds <c>GET /api/v1/accounts/{accountId}/sessions</c>.</summary>
public sealed record SessionListBuilder : NervesRequest
{
    internal SessionListBuilder(ApiRequester api, Guid accountId) => (Api, AccountId) = (api, accountId);

    internal Guid AccountId { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<SessionInfo>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<SessionInfo>>($"accounts/{AccountId:D}/sessions", null, cancellationToken);
}

/// <summary>Builds <c>DELETE /api/v1/sessions/{sessionId}</c>.</summary>
public sealed record SessionRevokeBuilder : NervesRequest
{
    internal SessionRevokeBuilder(ApiRequester api, Guid sessionId) => (Api, SessionId) = (api, sessionId);

    internal Guid SessionId { get; init; }

    /// <summary>Revokes the session.</summary>
    public Task SendAsync(CancellationToken cancellationToken = default) =>
        Api.DeleteAsync($"sessions/{SessionId:D}", null, cancellationToken);
}

/// <summary>Entry point for session inspection and revocation.</summary>
public sealed class SessionsResource
{
    private readonly ApiRequester _api;

    internal SessionsResource(ApiRequester api) => _api = api;

    /// <summary>Starts a list request for one account.</summary>
    public SessionListBuilder List(Guid accountId) => new(_api, accountId);

    /// <summary>Starts a revoke request for one session.</summary>
    public SessionRevokeBuilder Revoke(Guid sessionId) => new(_api, sessionId);
}
