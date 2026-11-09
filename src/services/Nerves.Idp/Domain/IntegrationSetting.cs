namespace Nerves.Idp.Domain;

/// <summary>
/// One persisted integration section (currently LDAP). The JSON payload is
/// written by the admin API so runtime changes survive a restart; the
/// configuration file remains the fallback when no row exists.
/// </summary>
public sealed class IntegrationSetting
{
    private IntegrationSetting() => Key = PayloadJson = string.Empty;

    private IntegrationSetting(Guid id, string key, string payloadJson, DateTimeOffset now)
    {
        Id = id;
        Key = key;
        PayloadJson = payloadJson;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; }

    public string PayloadJson { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static IntegrationSetting Create(string key, string payloadJson, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), key, payloadJson, now);

    public void Replace(string payloadJson, DateTimeOffset now)
    {
        PayloadJson = payloadJson;
        UpdatedAt = now;
    }
}
