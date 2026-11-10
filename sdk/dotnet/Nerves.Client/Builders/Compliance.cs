namespace Nerves.Client;

/// <summary>Builds <c>GET /api/v1/compliance/consents</c>; the endpoint returns a plain array.</summary>
public sealed record ConsentListBuilder : NervesRequest
{
    internal ConsentListBuilder(ApiRequester api) => Api = api;

    internal string? SubjectId { get; init; }

    internal string? Purpose { get; init; }

    /// <summary>Restricts the result to one data subject.</summary>
    public ConsentListBuilder ForSubject(string subjectId) => this with { SubjectId = subjectId };

    /// <summary>Restricts the result to one purpose.</summary>
    public ConsentListBuilder WithPurpose(string purpose) => this with { Purpose = purpose };

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<ConsentRecord>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<ConsentRecord>>(
            "compliance/consents",
            QueryString.Build([("subjectId", SubjectId), ("purpose", Purpose)]),
            cancellationToken);
}

/// <summary>Builds <c>POST /api/v1/compliance/consents</c>.</summary>
public sealed record ConsentGrantBuilder : NervesRequest
{
    internal ConsentGrantBuilder(ApiRequester api) => Api = api;

    internal string? SubjectId { get; init; }

    internal string? Purpose { get; init; }

    internal string? Version { get; init; }

    internal DateTimeOffset? ExpiresAt { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the data subject.</summary>
    public ConsentGrantBuilder ForSubject(string subjectId) => this with { SubjectId = subjectId };

    /// <summary>Sets the processing purpose.</summary>
    public ConsentGrantBuilder WithPurpose(string purpose) => this with { Purpose = purpose };

    /// <summary>Sets the accepted consent-text version.</summary>
    public ConsentGrantBuilder WithVersion(string version) => this with { Version = version };

    /// <summary>Sets the expiry timestamp.</summary>
    public ConsentGrantBuilder Until(DateTimeOffset expiresAt) => this with { ExpiresAt = expiresAt };

    /// <summary>Overrides the generated idempotency key.</summary>
    public ConsentGrantBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">A required field is missing.</exception>
    public Task<ConsentRecord> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SubjectId))
        {
            throw new InvalidOperationException("ForSubject() is required before SendAsync().");
        }

        if (string.IsNullOrWhiteSpace(Purpose))
        {
            throw new InvalidOperationException("WithPurpose() is required before SendAsync().");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            throw new InvalidOperationException("WithVersion() is required before SendAsync().");
        }

        var body = new { subjectId = SubjectId, purpose = Purpose, version = Version, expiresAt = ExpiresAt };
        return Api.PostAsync<ConsentRecord>("compliance/consents", body, IdempotencyKey, cancellationToken);
    }
}

/// <summary>Builds <c>POST /api/v1/compliance/consents/{id}/revoke</c>.</summary>
public sealed record ConsentRevokeBuilder : NervesRequest
{
    internal ConsentRevokeBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal string? Reason { get; init; }

    /// <summary>Sets the revocation reason.</summary>
    public ConsentRevokeBuilder WithReason(string? reason) => this with { Reason = reason };

    /// <summary>Sends the request.</summary>
    public Task<ConsentRecord> SendAsync(CancellationToken cancellationToken = default) =>
        Api.PostAsync<ConsentRecord>($"compliance/consents/{Id:D}/revoke", new { reason = Reason }, null, cancellationToken);
}

/// <summary>Builds <c>GET /api/v1/compliance/retention-policies</c>.</summary>
public sealed record RetentionPolicyListBuilder : NervesRequest
{
    internal RetentionPolicyListBuilder(ApiRequester api) => Api = api;

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<RetentionPolicy>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<RetentionPolicy>>("compliance/retention-policies", null, cancellationToken);
}

/// <summary>Builds <c>PUT /api/v1/compliance/retention-policies/{category}</c>.</summary>
public sealed record RetentionPolicyUpsertBuilder : NervesRequest
{
    internal RetentionPolicyUpsertBuilder(ApiRequester api, string category) => (Api, Category) = (api, category);

    internal string Category { get; init; }

    internal int? RetentionDays { get; init; }

    internal string? LegalBasis { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the retention period in days.</summary>
    public RetentionPolicyUpsertBuilder WithRetention(int days) => this with { RetentionDays = days };

    /// <summary>Sets the recorded legal basis.</summary>
    public RetentionPolicyUpsertBuilder WithLegalBasis(string? legalBasis) => this with { LegalBasis = legalBasis };

    /// <summary>Overrides the generated idempotency key.</summary>
    public RetentionPolicyUpsertBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">The retention period is missing.</exception>
    public Task<RetentionPolicy> SendAsync(CancellationToken cancellationToken = default)
    {
        var days = RetentionDays ?? throw new InvalidOperationException("WithRetention() is required before SendAsync().");
        return Api.PutAsync<RetentionPolicy>(
            $"compliance/retention-policies/{Uri.EscapeDataString(Category)}",
            new { retentionDays = days, legalBasis = LegalBasis },
            IdempotencyKey,
            cancellationToken);
    }
}

/// <summary>Builds <c>GET /api/v1/compliance/summary</c>.</summary>
public sealed record ComplianceSummaryBuilder : NervesRequest
{
    internal ComplianceSummaryBuilder(ApiRequester api) => Api = api;

    /// <summary>Sends the request.</summary>
    public Task<ComplianceSummary> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<ComplianceSummary>("compliance/summary", null, cancellationToken);
}

/// <summary>Entry point for compliance records.</summary>
public sealed class ComplianceResource
{
    private readonly ApiRequester _api;

    internal ComplianceResource(ApiRequester api) => _api = api;

    /// <summary>Starts a consent list request.</summary>
    public ConsentListBuilder Consents() => new(_api);

    /// <summary>Starts a consent grant request.</summary>
    public ConsentGrantBuilder GrantConsent() => new(_api);

    /// <summary>Starts a consent revocation request.</summary>
    public ConsentRevokeBuilder RevokeConsent(Guid consentId) => new(_api, consentId);

    /// <summary>Starts a retention-policy list request.</summary>
    public RetentionPolicyListBuilder RetentionPolicies() => new(_api);

    /// <summary>Starts a retention-policy upsert for one category.</summary>
    public RetentionPolicyUpsertBuilder UpsertRetentionPolicy(string category) => new(_api, category);

    /// <summary>Starts a summary request.</summary>
    public ComplianceSummaryBuilder Summary() => new(_api);
}

/// <summary>Builds <c>GET /api/v1/compliance/subjects/{subjectId}/export</c>.</summary>
public sealed record SubjectExportBuilder : NervesRequest
{
    internal SubjectExportBuilder(ApiRequester api, string subjectId) => (Api, SubjectId) = (api, subjectId);

    internal string SubjectId { get; init; }

    /// <summary>Sends the request and returns the exported JSON document.</summary>
    public Task<SubjectExport> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<SubjectExport>($"compliance/subjects/{Uri.EscapeDataString(SubjectId)}/export", null, cancellationToken);
}

/// <summary>Builds <c>POST /api/v1/compliance/subjects/{subjectId}/erase</c>.</summary>
public sealed record SubjectEraseBuilder : NervesRequest
{
    internal SubjectEraseBuilder(ApiRequester api, string subjectId) => (Api, SubjectId) = (api, subjectId);

    internal string SubjectId { get; init; }

    internal string? Reason { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the erasure reason.</summary>
    public SubjectEraseBuilder WithReason(string? reason) => this with { Reason = reason };

    /// <summary>Overrides the generated idempotency key.</summary>
    public SubjectEraseBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    public Task<EraseResult> SendAsync(CancellationToken cancellationToken = default) =>
        Api.PostAsync<EraseResult>(
            $"compliance/subjects/{Uri.EscapeDataString(SubjectId)}/erase",
            new { reason = Reason },
            IdempotencyKey,
            cancellationToken);
}

/// <summary>Builds <c>GET /api/v1/compliance/subjects/{subjectId}/erasures</c>.</summary>
public sealed record SubjectErasureListBuilder : NervesRequest
{
    internal SubjectErasureListBuilder(ApiRequester api, string subjectId) => (Api, SubjectId) = (api, subjectId);

    internal string SubjectId { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<IReadOnlyList<ErasureRecord>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<IReadOnlyList<ErasureRecord>>(
            $"compliance/subjects/{Uri.EscapeDataString(SubjectId)}/erasures",
            null,
            cancellationToken);
}

/// <summary>Entry point for data-subject rights.</summary>
public sealed class SubjectsResource
{
    private readonly ApiRequester _api;

    internal SubjectsResource(ApiRequester api) => _api = api;

    /// <summary>Starts a subject export request.</summary>
    public SubjectExportBuilder Export(string subjectId) => new(_api, subjectId);

    /// <summary>Starts a subject erasure request.</summary>
    public SubjectEraseBuilder Erase(string subjectId) => new(_api, subjectId);

    /// <summary>Starts a request for the subject's erasure history.</summary>
    public SubjectErasureListBuilder Erasures(string subjectId) => new(_api, subjectId);
}
