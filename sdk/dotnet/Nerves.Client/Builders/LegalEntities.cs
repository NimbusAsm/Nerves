using System.Runtime.CompilerServices;

namespace Nerves.Client;

/// <summary>Builds <c>POST /api/v1/legal-entities</c>.</summary>
public sealed record LegalEntityCreateBuilder : NervesRequest
{
    internal LegalEntityCreateBuilder(ApiRequester api) => Api = api;

    internal LegalEntityType Type { get; init; } = LegalEntityType.NaturalPerson;

    internal string? DisplayName { get; init; }

    internal IdDocumentType? DocumentType { get; init; }

    internal string? DocumentNumber { get; init; }

    internal string? Country { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the entity type.</summary>
    public LegalEntityCreateBuilder WithType(LegalEntityType type) => this with { Type = type };

    /// <summary>Sets the display name.</summary>
    public LegalEntityCreateBuilder WithDisplayName(string displayName) => this with { DisplayName = displayName };

    /// <summary>
    /// Supplies the identity document. The document number is sent once and is never
    /// returned; the server persists a keyed hash only.
    /// </summary>
    public LegalEntityCreateBuilder WithDocument(IdDocumentType documentType, string number, string country) =>
        this with { DocumentType = documentType, DocumentNumber = number, Country = country };

    /// <summary>Overrides the generated idempotency key.</summary>
    public LegalEntityCreateBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">A required modifier is missing.</exception>
    public Task<LegalEntity> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new InvalidOperationException("WithDisplayName() is required before SendAsync().");
        }

        if (DocumentType is null || string.IsNullOrWhiteSpace(DocumentNumber) || string.IsNullOrWhiteSpace(Country))
        {
            throw new InvalidOperationException("WithDocument(...) is required before SendAsync().");
        }

        var body = new CreateLegalEntityRequest(
            Type,
            DisplayName,
            new RealNameInput(DocumentType.Value, DocumentNumber, Country));

        return Api.PostAsync<LegalEntity>("legal-entities", body, IdempotencyKey, cancellationToken);
    }

    private sealed record CreateLegalEntityRequest(LegalEntityType Type, string DisplayName, RealNameInput RealName);

    private sealed record RealNameInput(IdDocumentType DocumentType, string Number, string Country);
}

/// <summary>Builds <c>GET /api/v1/legal-entities/{id}</c>.</summary>
public sealed record LegalEntityGetBuilder : NervesRequest
{
    internal LegalEntityGetBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<LegalEntity> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<LegalEntity>($"legal-entities/{Id:D}", null, cancellationToken);
}

/// <summary>Builds <c>GET /api/v1/legal-entities</c> with cursor pagination.</summary>
public sealed record LegalEntityListBuilder : NervesListRequest<LegalEntityListBuilder>
{
    internal LegalEntityListBuilder(ApiRequester api) => Api = api;

    internal VerificationState? State { get; init; }

    internal LegalEntityType? EntityType { get; init; }

    /// <summary>Restricts the list to one verification state.</summary>
    public LegalEntityListBuilder WithVerificationState(VerificationState state) => this with { State = state };

    /// <summary>Restricts the list to one entity type.</summary>
    public LegalEntityListBuilder WithType(LegalEntityType type) => this with { EntityType = type };

    /// <summary>Fetches one page.</summary>
    public Task<NervesPage<LegalEntity>> SendAsync(CancellationToken cancellationToken = default)
    {
        var query = new List<(string Key, string? Value)>
        {
            ("filter[verificationState]", State is null ? null : WireFormat.FormatVerificationState(State.Value)),
            ("filter[type]", EntityType is null ? null : WireFormat.FormatLegalEntityType(EntityType.Value)),
        };
        query.AddRange(SharedQueryParameters);

        return Api.GetPageAsync<LegalEntity>("legal-entities", QueryString.Build(query), cancellationToken);
    }

    /// <summary>Iterates every legal entity, following <c>nextCursor</c> until the end.</summary>
    public async IAsyncEnumerable<LegalEntity> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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

/// <summary>Builds <c>PATCH /api/v1/legal-entities/{id}</c>.</summary>
public sealed record LegalEntityUpdateBuilder : NervesRequest
{
    internal LegalEntityUpdateBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal IReadOnlyDictionary<string, object?> Patch { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    internal string? IfMatchTag { get; init; }

    /// <summary>Sets the display name.</summary>
    public LegalEntityUpdateBuilder WithDisplayName(string displayName) =>
        this with { Patch = PatchDocument.Set(Patch, "displayName", displayName) };

    /// <summary>Sets the verification state.</summary>
    public LegalEntityUpdateBuilder WithVerificationState(VerificationState state) =>
        this with { Patch = PatchDocument.Set(Patch, "verificationState", WireFormat.FormatVerificationState(state)) };

    /// <summary>Sets the provider reference.</summary>
    public LegalEntityUpdateBuilder WithProviderReference(string? providerReference) =>
        this with { Patch = PatchDocument.Set(Patch, "providerReference", providerReference) };

    /// <summary>Sends <c>If-Match</c> for optimistic concurrency.</summary>
    public LegalEntityUpdateBuilder IfMatch(string etag) => this with { IfMatchTag = etag };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">No field was set.</exception>
    public Task<LegalEntity> SendAsync(CancellationToken cancellationToken = default)
    {
        if (Patch.Count == 0)
        {
            throw new InvalidOperationException("Set at least one field with With...() before SendAsync().");
        }

        return Api.PatchAsync<LegalEntity>($"legal-entities/{Id:D}", Patch, IfMatchTag, cancellationToken);
    }
}

/// <summary>Builds <c>DELETE /api/v1/legal-entities/{id}</c>.</summary>
public sealed record LegalEntityDeleteBuilder : NervesRequest
{
    internal LegalEntityDeleteBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal string? IfMatchTag { get; init; }

    /// <summary>Sends <c>If-Match</c> for optimistic concurrency.</summary>
    public LegalEntityDeleteBuilder IfMatch(string etag) => this with { IfMatchTag = etag };

    /// <summary>Sends the request.</summary>
    public Task SendAsync(CancellationToken cancellationToken = default) =>
        Api.DeleteAsync($"legal-entities/{Id:D}", IfMatchTag, cancellationToken);
}
