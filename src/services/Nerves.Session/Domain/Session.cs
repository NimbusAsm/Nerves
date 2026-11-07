using Nerves.Core;

namespace Nerves.Session.Domain;

/// <summary>An authenticated interactive session.</summary>
public sealed class Session
{
    private Session()
    {
        Realm = string.Empty;
        DeviceId = string.Empty;
        IpHash = string.Empty;
        UserAgentHash = string.Empty;
        Amr = string.Empty;
    }

    private Session(
        Guid id,
        Guid accountId,
        Guid? legalEntityId,
        string realm,
        string deviceId,
        string ipHash,
        string userAgentHash,
        string amr,
        DateTimeOffset now,
        DateTimeOffset idleExpiresAt,
        DateTimeOffset absoluteExpiresAt)
    {
        Id = id;
        AccountId = accountId;
        LegalEntityId = legalEntityId;
        Realm = realm;
        DeviceId = deviceId;
        IpHash = ipHash;
        UserAgentHash = userAgentHash;
        Amr = amr;
        CreatedAt = now;
        LastSeenAt = now;
        IdleExpiresAt = idleExpiresAt;
        AbsoluteExpiresAt = absoluteExpiresAt;
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid? LegalEntityId { get; private set; }

    public string Realm { get; private set; }

    public string DeviceId { get; private set; }

    public string IpHash { get; private set; }

    public string UserAgentHash { get; private set; }

    /// <summary>Authentication methods, space separated (for example "pwd totp").</summary>
    public string Amr { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset IdleExpiresAt { get; private set; }

    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    public static Session Create(
        Guid accountId,
        Guid? legalEntityId,
        string realm,
        string deviceId,
        string ipHash,
        string userAgentHash,
        IEnumerable<string> amr,
        DateTimeOffset now,
        TimeSpan idleTimeout,
        TimeSpan absoluteTimeout) =>
        new(
            IdGenerator.NewId(now),
            accountId,
            legalEntityId,
            realm.Trim().ToLowerInvariant(),
            deviceId,
            ipHash,
            userAgentHash,
            string.Join(' ', amr),
            now,
            now.Add(idleTimeout),
            now.Add(absoluteTimeout));

    public void Touch(DateTimeOffset now)
    {
        LastSeenAt = now;
        var extended = now.Add(IdleExpiresAt - CreatedAt - TimeSpan.FromMinutes(30));
        if (extended > IdleExpiresAt && extended < AbsoluteExpiresAt)
        {
            IdleExpiresAt = extended;
        }
    }

    public void Revoke(string? reason, DateTimeOffset now)
    {
        RevokedAt = now;
        RevokedReason = reason;
    }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && now < IdleExpiresAt && now < AbsoluteExpiresAt;
}
