namespace Nerves.Identity.Domain;

/// <summary>
/// A single-use WebAuthn challenge. Stored server-side so registration and
/// assertion cannot be replayed.
/// </summary>
public sealed class PasskeyChallenge
{
    private PasskeyChallenge() => Challenge = Purpose = OptionsJson = string.Empty;

    private PasskeyChallenge(Guid id, Guid accountId, string challenge, string purpose, string optionsJson, DateTimeOffset expiresAt)
    {
        Id = id;
        AccountId = accountId;
        Challenge = challenge;
        Purpose = purpose;
        OptionsJson = optionsJson;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public string Challenge { get; private set; }

    public string Purpose { get; private set; }

    public string OptionsJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static PasskeyChallenge Create(
        Guid accountId, string challenge, string purpose, string optionsJson, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), accountId, challenge, purpose, optionsJson, DateTimeOffset.UtcNow.Add(lifetime));

    public void Consume() => ConsumedAt = DateTimeOffset.UtcNow;
}
