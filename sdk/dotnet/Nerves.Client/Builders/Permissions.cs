namespace Nerves.Client;

/// <summary>Builds <c>GET /api/v1/permissions</c>.</summary>
public sealed record PermissionListBuilder : NervesRequest
{
    internal PermissionListBuilder(ApiRequester api) => Api = api;

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<PermissionInfo>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<PermissionInfo>>("permissions", null, cancellationToken);
}

/// <summary>Builds <c>POST /api/v1/permissions</c>.</summary>
public sealed record PermissionCreateBuilder : NervesRequest
{
    internal PermissionCreateBuilder(ApiRequester api) => Api = api;

    internal string? Code { get; init; }

    internal string? Description { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the unique permission code.</summary>
    public PermissionCreateBuilder WithCode(string code) => this with { Code = code };

    /// <summary>Sets the human-readable description.</summary>
    public PermissionCreateBuilder WithDescription(string? description) => this with { Description = description };

    /// <summary>Overrides the generated idempotency key.</summary>
    public PermissionCreateBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">The code is missing.</exception>
    public Task<PermissionInfo> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            throw new InvalidOperationException("WithCode() is required before SendAsync().");
        }

        return Api.PostAsync<PermissionInfo>("permissions", new { code = Code, description = Description }, IdempotencyKey, cancellationToken);
    }
}

/// <summary>Entry point for permission codes.</summary>
public sealed class PermissionsResource
{
    private readonly ApiRequester _api;

    internal PermissionsResource(ApiRequester api) => _api = api;

    /// <summary>Starts a list request.</summary>
    public PermissionListBuilder List() => new(_api);

    /// <summary>Starts a create request.</summary>
    public PermissionCreateBuilder Create() => new(_api);
}
