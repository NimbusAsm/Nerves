namespace Nerves.Core;

/// <summary>
/// Something that happened in the domain, published asynchronously.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Stable identifier used for consumer-side de-duplication.</summary>
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

/// <summary>Base record for domain events.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = IdGenerator.NewId();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
