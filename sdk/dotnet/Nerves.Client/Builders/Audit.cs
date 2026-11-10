namespace Nerves.Client;

/// <summary>Builds <c>GET /api/v1/audit-events</c> with cursor pagination.</summary>
public sealed record AuditListBuilder : NervesRequest
{
    internal AuditListBuilder(ApiRequester api) => Api = api;

    internal string? Stream { get; init; }

    internal string? ActorId { get; init; }

    internal string? Action { get; init; }

    internal string? Result { get; init; }

    internal DateTimeOffset? FromInstant { get; init; }

    internal DateTimeOffset? To { get; init; }

    internal int? PageSize { get; init; }

    internal string? CursorToken { get; init; }

    /// <summary>Restricts the result to one hash-chained stream.</summary>
    public AuditListBuilder InStream(string stream) => this with { Stream = stream };

    /// <summary>Restricts the result to one actor.</summary>
    public AuditListBuilder ByActor(string actorId) => this with { ActorId = actorId };

    /// <summary>Restricts the result to one dotted action name.</summary>
    public AuditListBuilder WithAction(string action) => this with { Action = action };

    /// <summary>Restricts the result to one outcome.</summary>
    public AuditListBuilder WithResult(string result) => this with { Result = result };

    /// <summary>Sets the inclusive lower bound.</summary>
    public AuditListBuilder Since(DateTimeOffset from) => this with { FromInstant = from };

    /// <summary>Sets the inclusive upper bound.</summary>
    public AuditListBuilder Until(DateTimeOffset to) => this with { To = to };

    /// <summary>Sets the page size (server default 50, max 200).</summary>
    public AuditListBuilder Limit(int pageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");
        }

        return this with { PageSize = pageSize };
    }

    /// <summary>Continues from an opaque cursor returned by a previous page.</summary>
    public AuditListBuilder Cursor(string cursor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cursor);
        return this with { CursorToken = cursor };
    }

    /// <summary>Fetches one page.</summary>
    public Task<NervesPage<AuditEvent>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetPageAsync<AuditEvent>("audit-events", QueryString.Build(QueryParameters()), cancellationToken);

    /// <summary>Iterates every matching event, following <c>nextCursor</c>.</summary>
    public async IAsyncEnumerable<AuditEvent> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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

    private IEnumerable<(string Key, string? Value)> QueryParameters()
    {
        yield return ("stream", Stream);
        yield return ("actorId", ActorId);
        yield return ("action", Action);
        yield return ("result", Result);
        yield return ("from", FromInstant?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        yield return ("to", To?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        yield return ("limit", PageSize?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        yield return ("cursor", CursorToken);
    }
}

/// <summary>Builds <c>GET /api/v1/audit-events/verify</c>.</summary>
public sealed record AuditVerifyBuilder : NervesRequest
{
    internal AuditVerifyBuilder(ApiRequester api) => Api = api;

    internal string? Stream { get; init; }

    /// <summary>Restricts verification to one stream.</summary>
    public AuditVerifyBuilder InStream(string stream) => this with { Stream = stream };

    /// <summary>Sends the request.</summary>
    public Task<AuditVerifyResult> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<AuditVerifyResult>(
            "audit-events/verify",
            QueryString.Build([("stream", Stream)]),
            cancellationToken);
}

/// <summary>Builds <c>GET /api/v1/audit-events/export</c>.</summary>
public sealed record AuditExportBuilder : NervesRequest
{
    internal AuditExportBuilder(ApiRequester api) => Api = api;

    internal string? Format { get; init; }

    internal int? ExportLimit { get; init; }

    /// <summary>Selects <c>csv</c> (default) or <c>json</c>.</summary>
    public AuditExportBuilder As(string format) => this with { Format = format };

    /// <summary>Sets the row cap (server default 5000, max 20000).</summary>
    public AuditExportBuilder Limit(int rows) => this with { ExportLimit = rows };

    /// <summary>Sends the request and returns the downloaded document.</summary>
    public Task<NervesFile> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetFileAsync(
            "audit-events/export",
            QueryString.Build(
            [
                ("format", Format),
                ("limit", ExportLimit?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ]),
            cancellationToken);
}

/// <summary>Entry point for the audit trail.</summary>
public sealed class AuditResource
{
    private readonly ApiRequester _api;

    internal AuditResource(ApiRequester api) => _api = api;

    /// <summary>Starts a list request.</summary>
    public AuditListBuilder List() => new(_api);

    /// <summary>Starts a chain-verification request.</summary>
    public AuditVerifyBuilder Verify() => new(_api);

    /// <summary>Starts an export request.</summary>
    public AuditExportBuilder Export() => new(_api);
}
