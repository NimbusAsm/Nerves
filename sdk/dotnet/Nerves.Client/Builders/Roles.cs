using System.Runtime.CompilerServices;

namespace Nerves.Client;

/// <summary>Builds <c>POST /api/v1/roles</c>.</summary>
public sealed record RoleCreateBuilder : NervesRequest
{
    internal RoleCreateBuilder(ApiRequester api) => Api = api;

    internal string? Name { get; init; }

    internal string? Description { get; init; }

    internal IReadOnlyList<string> Permissions { get; init; } = [];

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the role name.</summary>
    public RoleCreateBuilder WithName(string name) => this with { Name = name };

    /// <summary>Sets the description.</summary>
    public RoleCreateBuilder WithDescription(string? description) => this with { Description = description };

    /// <summary>Replaces the permission set.</summary>
    public RoleCreateBuilder WithPermissions(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return this with { Permissions = [.. permissions] };
    }

    /// <summary>Overrides the generated idempotency key.</summary>
    public RoleCreateBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">The name is missing.</exception>
    public Task<Role> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("WithName() is required before SendAsync().");
        }

        var body = new CreateRoleRequest(Name, Description, Permissions.Count == 0 ? null : Permissions);
        return Api.PostAsync<Role>("roles", body, IdempotencyKey, cancellationToken);
    }

    private sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string>? Permissions);
}

/// <summary>Builds <c>GET /api/v1/roles/{id}</c>.</summary>
public sealed record RoleGetBuilder : NervesRequest
{
    internal RoleGetBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<Role> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<Role>($"roles/{Id:D}", null, cancellationToken);
}

/// <summary>Builds <c>GET /api/v1/roles</c> with cursor pagination.</summary>
public sealed record RoleListBuilder : NervesListRequest<RoleListBuilder>
{
    internal RoleListBuilder(ApiRequester api) => Api = api;

    /// <summary>Fetches one page.</summary>
    public Task<NervesPage<Role>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetPageAsync<Role>("roles", QueryString.Build(SharedQueryParameters), cancellationToken);

    /// <summary>Iterates every role, following <c>nextCursor</c> until the end.</summary>
    public async IAsyncEnumerable<Role> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = CursorToken;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            var page = await (this with { CursorToken = cursor }).SendAsync(cancellationToken).ConfigureAwait(false);

            foreach (var item in page.Items)
            {
                yield return item;
            }

            if (string.IsNullOrEmpty(page.NextCursor) || !seen.Add(page.NextCursor))
            {
                yield break;
            }

            cursor = page.NextCursor;
        }
    }
}

/// <summary>Builds <c>PATCH /api/v1/roles/{id}</c>.</summary>
public sealed record RoleUpdateBuilder : NervesRequest
{
    internal RoleUpdateBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal IReadOnlyDictionary<string, object?> Patch { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    internal string? IfMatchTag { get; init; }

    /// <summary>Sets the role name.</summary>
    public RoleUpdateBuilder WithName(string name) =>
        this with { Patch = PatchDocument.Set(Patch, "name", name) };

    /// <summary>Sets or clears the description.</summary>
    public RoleUpdateBuilder WithDescription(string? description) =>
        this with { Patch = PatchDocument.Set(Patch, "description", description) };

    /// <summary>Replaces the permission set; pass no arguments to clear it.</summary>
    public RoleUpdateBuilder WithPermissions(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return this with { Patch = PatchDocument.Set(Patch, "permissions", permissions.ToArray()) };
    }

    /// <summary>Sends <c>If-Match</c> for optimistic concurrency.</summary>
    public RoleUpdateBuilder IfMatch(string etag) => this with { IfMatchTag = etag };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">No field was set.</exception>
    public Task<Role> SendAsync(CancellationToken cancellationToken = default)
    {
        if (Patch.Count == 0)
        {
            throw new InvalidOperationException("Set at least one field with With...() before SendAsync().");
        }

        return Api.PatchAsync<Role>($"roles/{Id:D}", Patch, IfMatchTag, cancellationToken);
    }
}

/// <summary>Builds <c>DELETE /api/v1/roles/{id}</c>.</summary>
public sealed record RoleDeleteBuilder : NervesRequest
{
    internal RoleDeleteBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal string? IfMatchTag { get; init; }

    /// <summary>Sends <c>If-Match</c> for optimistic concurrency.</summary>
    public RoleDeleteBuilder IfMatch(string etag) => this with { IfMatchTag = etag };

    /// <summary>Sends the request.</summary>
    public Task SendAsync(CancellationToken cancellationToken = default) =>
        Api.DeleteAsync($"roles/{Id:D}", IfMatchTag, cancellationToken);
}
