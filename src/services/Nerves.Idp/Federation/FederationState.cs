namespace Nerves.Idp.Federation;

/// <summary>Short-lived state that binds a federation callback to its request.</summary>
public sealed class FederationState
{
    private FederationState() => Provider = string.Empty;

    private FederationState(Guid id, string provider, string returnUrl, DateTimeOffset expiresAt)
    {
        Id = id;
        Provider = provider;
        ReturnUrl = returnUrl;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public string Provider { get; private set; }

    public string? ReturnUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static FederationState Create(string provider, string? returnUrl, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), provider, returnUrl ?? string.Empty, DateTimeOffset.UtcNow.Add(lifetime));

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public void Consume() => ConsumedAt = DateTimeOffset.UtcNow;
}
