using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Nerves.Client;

/// <summary>Builds <c>POST /api/v1/accounts</c>. Immutable: every modifier returns a new builder.</summary>
public sealed record AccountCreateBuilder : NervesRequest
{
    internal AccountCreateBuilder(ApiRequester api) => Api = api;

    internal Guid? LegalEntityId { get; init; }

    internal string? Realm { get; init; }

    internal string? Username { get; init; }

    internal string? Email { get; init; }

    internal string? PhoneNumber { get; init; }

    internal string? DisplayName { get; init; }

    internal AccountType Type { get; init; } = AccountType.Personal;

    internal IReadOnlyList<string> Roles { get; init; } = [];

    internal bool? IsPrimary { get; init; }

    internal string? IdempotencyKey { get; init; }

    /// <summary>Anchors the account to a legal entity.</summary>
    public AccountCreateBuilder ForEntity(Guid legalEntityId) => this with { LegalEntityId = legalEntityId };

    /// <summary>Sets the realm (server default when omitted).</summary>
    public AccountCreateBuilder InRealm(string realm) => this with { Realm = realm };

    /// <summary>Sets the login name.</summary>
    public AccountCreateBuilder WithUsername(string username) => this with { Username = username };

    /// <summary>Sets the email address.</summary>
    public AccountCreateBuilder WithEmail(string? email) => this with { Email = email };

    /// <summary>Sets the phone number.</summary>
    public AccountCreateBuilder WithPhoneNumber(string? phoneNumber) => this with { PhoneNumber = phoneNumber };

    /// <summary>Sets the display name.</summary>
    public AccountCreateBuilder WithDisplayName(string? displayName) => this with { DisplayName = displayName };

    /// <summary>Sets the account type.</summary>
    public AccountCreateBuilder WithType(AccountType type) => this with { Type = type };

    /// <summary>Replaces the role set.</summary>
    public AccountCreateBuilder WithRoles(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        return this with { Roles = [.. roles] };
    }

    /// <summary>Marks the account as the entity's primary account.</summary>
    public AccountCreateBuilder WithPrimary(bool isPrimary = true) => this with { IsPrimary = isPrimary };

    /// <summary>Overrides the generated idempotency key.</summary>
    public AccountCreateBuilder WithIdempotencyKey(string idempotencyKey) => this with { IdempotencyKey = idempotencyKey };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">A required modifier is missing.</exception>
    /// <exception cref="NervesApiException">The API rejected the request.</exception>
    public Task<Account> SendAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("WithUsername() is required before SendAsync().");
        }

        if (Type == AccountType.Service && LegalEntityId is not null)
        {
            throw new InvalidOperationException("Service accounts cannot belong to a legal entity.");
        }

        if (Type != AccountType.Service && LegalEntityId is null)
        {
            throw new InvalidOperationException("Non-service accounts must call ForEntity(...) before SendAsync().");
        }

        var body = new CreateAccountRequest(
            LegalEntityId,
            Realm,
            Username,
            Email,
            PhoneNumber,
            DisplayName,
            Type,
            Roles.Count == 0 ? null : Roles,
            IsPrimary);

        return Api.PostAsync<Account>("accounts", body, IdempotencyKey, cancellationToken);
    }

    private sealed record CreateAccountRequest(
        Guid? LegalEntityId,
        string? Realm,
        string Username,
        string? Email,
        string? PhoneNumber,
        string? DisplayName,
        AccountType Type,
        IReadOnlyList<string>? Roles,
        bool? IsPrimary);
}

/// <summary>Builds <c>GET /api/v1/accounts/{id}</c>.</summary>
public sealed record AccountGetBuilder : NervesRequest
{
    internal AccountGetBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    /// <summary>Sends the request.</summary>
    public Task<Account> SendAsync(CancellationToken cancellationToken = default) =>
        Api.GetAsync<Account>($"accounts/{Id:D}", null, cancellationToken);
}

/// <summary>Builds <c>GET /api/v1/accounts</c> with cursor pagination.</summary>
public sealed record AccountListBuilder : NervesListRequest<AccountListBuilder>
{
    internal AccountListBuilder(ApiRequester api) => Api = api;

    internal Guid? LegalEntityId { get; init; }

    internal string? Realm { get; init; }

    internal AccountStatus? Status { get; init; }

    /// <summary>Restricts the list to accounts of one legal entity.</summary>
    public AccountListBuilder ForEntity(Guid legalEntityId) => this with { LegalEntityId = legalEntityId };

    /// <summary>Restricts the list to one realm.</summary>
    public AccountListBuilder InRealm(string realm) => this with { Realm = realm };

    /// <summary>Restricts the list to one status.</summary>
    public AccountListBuilder WithStatus(AccountStatus status) => this with { Status = status };

    /// <summary>Fetches one page.</summary>
    public Task<NervesPage<Account>> SendAsync(CancellationToken cancellationToken = default)
    {
        var query = new List<(string Key, string? Value)>
        {
            ("legalEntityId", LegalEntityId?.ToString("D")),
            ("realm", Realm),
            ("filter[status]", Status is null ? null : WireFormat.FormatAccountStatus(Status.Value)),
        };
        query.AddRange(SharedQueryParameters);

        return Api.GetPageAsync<Account>("accounts", QueryString.Build(query), cancellationToken);
    }

    /// <summary>Iterates every account, following <c>nextCursor</c> until the end.</summary>
    public async IAsyncEnumerable<Account> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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

/// <summary>Builds <c>PATCH /api/v1/accounts/{id}</c>. Only fields set with <c>With*</c> are sent.</summary>
public sealed record AccountUpdateBuilder : NervesRequest
{
    internal AccountUpdateBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal IReadOnlyDictionary<string, object?> Patch { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    internal string? IfMatchTag { get; init; }

    /// <summary>Sets the login name.</summary>
    public AccountUpdateBuilder WithUsername(string username) =>
        this with { Patch = PatchDocument.Set(Patch, "username", username) };

    /// <summary>Sets or clears the email address.</summary>
    public AccountUpdateBuilder WithEmail(string? email) =>
        this with { Patch = PatchDocument.Set(Patch, "email", email) };

    /// <summary>Sets or clears the phone number.</summary>
    public AccountUpdateBuilder WithPhoneNumber(string? phoneNumber) =>
        this with { Patch = PatchDocument.Set(Patch, "phoneNumber", phoneNumber) };

    /// <summary>Sets or clears the display name.</summary>
    public AccountUpdateBuilder WithDisplayName(string? displayName) =>
        this with { Patch = PatchDocument.Set(Patch, "displayName", displayName) };

    /// <summary>Sets the status.</summary>
    public AccountUpdateBuilder WithStatus(AccountStatus status) =>
        this with { Patch = PatchDocument.Set(Patch, "status", WireFormat.FormatAccountStatus(status)) };

    /// <summary>Replaces the role set; pass no arguments to clear it.</summary>
    public AccountUpdateBuilder WithRoles(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        return this with { Patch = PatchDocument.Set(Patch, "roles", roles.ToArray()) };
    }

    /// <summary>Sends <c>If-Match</c> for optimistic concurrency.</summary>
    public AccountUpdateBuilder IfMatch(string etag) => this with { IfMatchTag = etag };

    /// <summary>Sends the request.</summary>
    /// <exception cref="InvalidOperationException">No field was set.</exception>
    public Task<Account> SendAsync(CancellationToken cancellationToken = default)
    {
        if (Patch.Count == 0)
        {
            throw new InvalidOperationException("Set at least one field with With...() before SendAsync().");
        }

        return Api.PatchAsync<Account>($"accounts/{Id:D}", Patch, IfMatchTag, cancellationToken);
    }
}

/// <summary>Builds <c>DELETE /api/v1/accounts/{id}</c>.</summary>
public sealed record AccountDeleteBuilder : NervesRequest
{
    internal AccountDeleteBuilder(ApiRequester api, Guid id) => (Api, Id) = (api, id);

    internal Guid Id { get; init; }

    internal string? IfMatchTag { get; init; }

    /// <summary>Sends <c>If-Match</c> for optimistic concurrency.</summary>
    public AccountDeleteBuilder IfMatch(string etag) => this with { IfMatchTag = etag };

    /// <summary>Sends the request.</summary>
    public Task SendAsync(CancellationToken cancellationToken = default) =>
        Api.DeleteAsync($"accounts/{Id:D}", IfMatchTag, cancellationToken);
}

/// <summary>Wire strings shared by query filters and patch documents.</summary>
internal static class WireFormat
{
    public static string FormatAccountStatus(AccountStatus status) => status switch
    {
        AccountStatus.Active => "active",
        AccountStatus.Locked => "locked",
        AccountStatus.Disabled => "disabled",
        _ => throw new JsonException($"Unknown account status: {status}."),
    };

    public static string FormatLegalEntityType(LegalEntityType type) => type switch
    {
        LegalEntityType.NaturalPerson => "naturalPerson",
        LegalEntityType.LegalPerson => "legalPerson",
        LegalEntityType.Government => "government",
        LegalEntityType.Other => "other",
        _ => throw new JsonException($"Unknown legal entity type: {type}."),
    };

    public static string FormatVerificationState(VerificationState state) => state switch
    {
        VerificationState.Pending => "pending",
        VerificationState.Verified => "verified",
        VerificationState.Rejected => "rejected",
        VerificationState.Expired => "expired",
        _ => throw new JsonException($"Unknown verification state: {state}."),
    };
}
