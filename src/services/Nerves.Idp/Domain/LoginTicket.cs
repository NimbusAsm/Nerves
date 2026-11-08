namespace Nerves.Idp.Domain;

/// <summary>
/// A single-use, short-lived ticket that proves a successful API login to the
/// authorization endpoint. Only the SHA-256 hash of the ticket is stored.
/// </summary>
public sealed class LoginTicket
{
    private LoginTicket()
    {
        TokenHash = string.Empty;
        Realm = string.Empty;
        DisplayName = string.Empty;
    }

    private LoginTicket(
        Guid id,
        string tokenHash,
        string realm,
        Guid accountId,
        Guid? legalEntityId,
        string displayName,
        string? email,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TokenHash = tokenHash;
        Realm = realm;
        AccountId = accountId;
        LegalEntityId = legalEntityId;
        DisplayName = displayName;
        Email = email;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string TokenHash { get; private set; }

    public string Realm { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid? LegalEntityId { get; private set; }

    public string DisplayName { get; private set; }

    public string? Email { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static LoginTicket Issue(
        string tokenHash,
        string realm,
        Guid accountId,
        Guid? legalEntityId,
        string displayName,
        string? email,
        TimeSpan lifetime)
    {
        var now = DateTimeOffset.UtcNow;
        return new LoginTicket(
            Guid.CreateVersion7(now),
            tokenHash,
            realm,
            accountId,
            legalEntityId,
            displayName,
            email,
            now.Add(lifetime));
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public void Consume() => ConsumedAt = DateTimeOffset.UtcNow;
}
