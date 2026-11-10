namespace Nerves.Identity.Domain;

/// <summary>
/// Maps a login identifier to the account that owns it.
/// </summary>
/// <remarks>
/// This table is the only global uniqueness authority in the system: usernames
/// are realm scoped, while email addresses and phone numbers are deployment
/// wide. It stores hashes only, so a leak does not expose contact details
/// (PLAN §2.2, §8.1).
/// </remarks>
public sealed class RoutingIndexEntry
{
    private RoutingIndexEntry()
    {
        IdentifierHash = string.Empty;
        IdentifierType = string.Empty;
        Realm = string.Empty;
    }

    private RoutingIndexEntry(
        string identifierHash,
        string identifierType,
        string realm,
        Guid accountId,
        Guid? legalEntityId,
        int shard,
        DateTimeOffset now)
    {
        IdentifierHash = identifierHash;
        IdentifierType = identifierType;
        Realm = realm;
        AccountId = accountId;
        LegalEntityId = legalEntityId;
        Shard = shard;
        CreatedAt = now;
    }

    /// <summary>Primary key: HMAC of the normalized identifier.</summary>
    public string IdentifierHash { get; private set; }

    /// <summary>One of <c>username</c>, <c>email</c>, <c>phone</c>.</summary>
    public string IdentifierType { get; private set; }

    public string Realm { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid? LegalEntityId { get; private set; }

    /// <summary>Logical shard that owns the account (PLAN §2.5).</summary>
    public int Shard { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Moves the entry to another logical shard after a shard count change.</summary>
    public void Reshard(int shard)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shard);

        Shard = shard;
    }

    public static RoutingIndexEntry Create(
        string identifierHash,
        string identifierType,
        string realm,
        Guid accountId,
        Guid? legalEntityId,
        int shard,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifierHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifierType);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);

        return new RoutingIndexEntry(identifierHash, identifierType, realm, accountId, legalEntityId, shard, now);
    }
}
