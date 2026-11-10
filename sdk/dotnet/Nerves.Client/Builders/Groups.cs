namespace Nerves.Client;

/// <summary>Builds <c>GET /api/v1/groups</c>; the endpoint returns a plain array, not a cursor page.</summary>
public sealed record GroupListBuilder : NervesRequest
{
    internal GroupListBuilder(ApiRequester api) => Api = api;

    internal string? Realm { get; init; }

    /// <summary>Restricts the result to one realm.</summary>
    public GroupListBuilder InRealm(string realm) => this with { Realm = realm };

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<Group>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<Group>>(
            "groups",
            QueryString.Build([("realm", Realm)]),
            cancellationToken);
}

/// <summary>Builds <c>POST /api/v1/groups</c>.</summary>
public sealed record GroupCreateBuilder : NervesRequest
{
    internal GroupCreateBuilder(ApiRequester api) => Api = api;

    internal string? Realm { get; init; }

    internal string? Code { get; init; }

    internal string? Name { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the realm.</summary>
    public GroupCreateBuilder InRealm(string realm) => this with { Realm = realm };

    /// <summary>Sets the unique code.</summary>
    public GroupCreateBuilder WithCode(string code) => this with { Code = code };

    /// <summary>Sets the display name.</summary>
    public GroupCreateBuilder WithName(string name) => this with { Name = name };

    /// <summary>Overrides the generated idempotency key.</summary>
    public GroupCreateBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">A required field is missing.</exception>
    public Task<Group> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Realm))
        {
            throw new InvalidOperationException("InRealm() is required before SendAsync().");
        }

        if (string.IsNullOrWhiteSpace(Code))
        {
            throw new InvalidOperationException("WithCode() is required before SendAsync().");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("WithName() is required before SendAsync().");
        }

        return Api.PostAsync<Group>("groups", new { realm = Realm, code = Code, name = Name }, IdempotencyKey, cancellationToken);
    }
}

/// <summary>Builds <c>GET /api/v1/groups/{id}</c>.</summary>
public sealed record GroupGetBuilder : NervesRequest
{
    internal GroupGetBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<GroupDetail> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<GroupDetail>($"groups/{Id:D}", null, cancellationToken);
}

/// <summary>Builds <c>DELETE /api/v1/groups/{id}</c>.</summary>
public sealed record GroupDeleteBuilder : NervesRequest
{
    internal GroupDeleteBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    /// <summary>Sends the request.</summary>
    public Task SendAsync(CancellationToken cancellationToken = default) =>
        Api.DeleteAsync($"groups/{Id:D}", null, cancellationToken);
}

/// <summary>Builds <c>GET /api/v1/groups/{id}/members</c>.</summary>
public sealed record GroupMemberListBuilder : NervesRequest
{
    internal GroupMemberListBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<GroupMember>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<GroupMember>>($"groups/{Id:D}/members", null, cancellationToken);
}

/// <summary>Builds <c>POST /api/v1/groups/{id}/members</c>.</summary>
public sealed record GroupMemberAddBuilder : NervesRequest
{
    internal GroupMemberAddBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal Guid? AccountId { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the account to add.</summary>
    public GroupMemberAddBuilder WithAccount(Guid accountId) => this with { AccountId = accountId };

    /// <summary>Overrides the generated idempotency key.</summary>
    public GroupMemberAddBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">The account is missing.</exception>
    public Task SendAsync(CancellationToken cancellationToken = default)
    {
        var accountId = AccountId ?? throw new InvalidOperationException("WithAccount() is required before SendAsync().");
        return Api.PostVoidAsync($"groups/{Id:D}/members", new { accountId }, IdempotencyKey, cancellationToken);
    }
}

/// <summary>Builds <c>DELETE /api/v1/groups/{id}/members/{accountId}</c>.</summary>
public sealed record GroupMemberRemoveBuilder : NervesRequest
{
    internal GroupMemberRemoveBuilder(ApiRequester api, Guid id, Guid accountId) =>
        (Api, Id, AccountId) = (api, id, accountId);

    internal Guid Id { get; init; }

    internal Guid AccountId { get; init; }

    /// <summary>Sends the request.</summary>
    public Task SendAsync(CancellationToken cancellationToken = default) =>
        Api.DeleteAsync($"groups/{Id:D}/members/{AccountId:D}", null, cancellationToken);
}

/// <summary>Entry point for groups and their members.</summary>
public sealed class GroupsResource
{
    private readonly ApiRequester _api;

    internal GroupsResource(ApiRequester api) => _api = api;

    /// <summary>Starts a list request.</summary>
    public GroupListBuilder List() => new(_api);

    /// <summary>Starts a create request.</summary>
    public GroupCreateBuilder Create() => new(_api);

    /// <summary>Starts a read request for one group.</summary>
    public GroupGetBuilder Get(Guid id) => new(_api, id);

    /// <summary>Starts a delete request for one group.</summary>
    public GroupDeleteBuilder Delete(Guid id) => new(_api, id);

    /// <summary>Starts a member list request.</summary>
    public GroupMemberListBuilder Members(Guid id) => new(_api, id);

    /// <summary>Starts an add-member request.</summary>
    public GroupMemberAddBuilder AddMember(Guid id) => new(_api, id);

    /// <summary>Starts a remove-member request.</summary>
    public GroupMemberRemoveBuilder RemoveMember(Guid id, Guid accountId) => new(_api, id, accountId);
}
