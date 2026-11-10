namespace Nerves.Client;

/// <summary>Builds <c>POST /api/v1/notifications/email</c>.</summary>
public sealed record NotificationSendBuilder : NervesRequest
{
    internal NotificationSendBuilder(ApiRequester api) => Api = api;

    internal string? Recipient { get; init; }

    internal string? Template { get; init; }

    internal IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    internal string? IdempotencyKey { get; init; }

    /// <summary>Sets the recipient address.</summary>
    public NotificationSendBuilder To(string to) => this with { Recipient = to };

    /// <summary>Sets the template code.</summary>
    public NotificationSendBuilder WithTemplate(string template) => this with { Template = template };

    /// <summary>Sets one template value.</summary>
    public NotificationSendBuilder WithValue(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return this with
        {
            Values = new Dictionary<string, string>(Values, StringComparer.Ordinal) { [key] = value },
        };
    }

    /// <summary>Replaces every template value.</summary>
    public NotificationSendBuilder WithValues(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return this with { Values = new Dictionary<string, string>(values, StringComparer.Ordinal) };
    }

    /// <summary>Overrides the generated idempotency key.</summary>
    public NotificationSendBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">A required field is missing.</exception>
    public Task<NotificationMessage> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Recipient))
        {
            throw new InvalidOperationException("To() is required before SendAsync().");
        }

        if (string.IsNullOrWhiteSpace(Template))
        {
            throw new InvalidOperationException("WithTemplate() is required before SendAsync().");
        }

        var body = new
        {
            to = Recipient,
            template = Template,
            values = Values.Count == 0 ? null : Values,
        };

        return Api.PostAsync<NotificationMessage>("notifications/email", body, IdempotencyKey, cancellationToken);
    }
}

/// <summary>Builds <c>GET /api/v1/notifications</c> with cursor pagination.</summary>
public sealed record NotificationListBuilder : NervesRequest
{
    internal NotificationListBuilder(ApiRequester api) => Api = api;

    internal string? Status { get; init; }

    internal string? Recipient { get; init; }

    internal int? PageSize { get; init; }

    internal string? CursorToken { get; init; }

    /// <summary>Restricts the result to <c>pending</c>, <c>sent</c> or <c>failed</c>.</summary>
    public NotificationListBuilder WithStatus(string status) => this with { Status = status };

    /// <summary>Restricts the result to one recipient.</summary>
    public NotificationListBuilder To(string to) => this with { Recipient = to };

    /// <summary>Sets the page size (server default 50, max 200).</summary>
    public NotificationListBuilder Limit(int pageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");
        }

        return this with { PageSize = pageSize };
    }

    /// <summary>Continues from an opaque cursor returned by a previous page.</summary>
    public NotificationListBuilder Cursor(string cursor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cursor);
        return this with { CursorToken = cursor };
    }

    /// <summary>Fetches one page.</summary>
    public Task<NervesPage<NotificationMessage>> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetPageAsync<NotificationMessage>(
            "notifications",
            QueryString.Build(QueryParameters()),
            cancellationToken);

    /// <summary>Iterates every matching notification, following <c>nextCursor</c>.</summary>
    public async IAsyncEnumerable<NotificationMessage> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
        yield return ("status", Status);
        yield return ("to", Recipient);
        yield return ("limit", PageSize?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        yield return ("cursor", CursorToken);
    }
}

/// <summary>Builds <c>GET /api/v1/notifications/{id}</c>.</summary>
public sealed record NotificationGetBuilder : NervesRequest
{
    internal NotificationGetBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<NotificationMessage> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<NotificationMessage>($"notifications/{Id:D}", null, cancellationToken);
}

/// <summary>Entry point for the notification delivery log.</summary>
public sealed class NotificationsResource
{
    private readonly ApiRequester _api;

    internal NotificationsResource(ApiRequester api) => _api = api;

    /// <summary>Starts an email send request.</summary>
    public NotificationSendBuilder SendEmail() => new(_api);

    /// <summary>Starts a list request.</summary>
    public NotificationListBuilder List() => new(_api);

    /// <summary>Starts a read request for one notification.</summary>
    public NotificationGetBuilder Get(Guid id) => new(_api, id);
}
