namespace Nerves.Client;

/// <summary>Builds <c>POST /api/v1/roles/{id}/assign</c>.</summary>
public sealed record RoleAssignBuilder : NervesRequest
{
    internal RoleAssignBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal string? SubjectType { get; init; }

    internal Guid? SubjectId { get; init; }

    /// <summary>Sets the subject kind: <c>account</c>, <c>legalEntity</c> or <c>group</c>.</summary>
    public RoleAssignBuilder WithSubject(string subjectType, Guid subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        return this with { SubjectType = subjectType, SubjectId = subjectId };
    }

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">The subject is missing.</exception>
    public Task SendAsync(CancellationToken cancellationToken = default)
    {
        var subjectType = SubjectType ?? throw new InvalidOperationException("WithSubject() is required before SendAsync().");
        var subjectId = SubjectId ?? throw new InvalidOperationException("WithSubject() is required before SendAsync().");
        return Api.PostVoidAsync($"roles/{Id:D}/assign", new { subjectType, subjectId }, null, cancellationToken);
    }
}

/// <summary>Builds <c>POST /api/v1/roles/{id}/revoke</c>.</summary>
public sealed record RoleRevokeBuilder : NervesRequest
{
    internal RoleRevokeBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal string? SubjectType { get; init; }

    internal Guid? SubjectId { get; init; }

    /// <summary>Sets the subject kind: <c>account</c>, <c>legalEntity</c> or <c>group</c>.</summary>
    public RoleRevokeBuilder WithSubject(string subjectType, Guid subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        return this with { SubjectType = subjectType, SubjectId = subjectId };
    }

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">The subject is missing.</exception>
    public Task SendAsync(CancellationToken cancellationToken = default)
    {
        var subjectType = SubjectType ?? throw new InvalidOperationException("WithSubject() is required before SendAsync().");
        var subjectId = SubjectId ?? throw new InvalidOperationException("WithSubject() is required before SendAsync().");
        return Api.PostVoidAsync($"roles/{Id:D}/revoke", new { subjectType, subjectId }, null, cancellationToken);
    }
}
