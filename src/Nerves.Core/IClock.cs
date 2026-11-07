namespace Nerves.Core;

/// <summary>Abstracts the current time so behaviour stays testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Default clock backed by the system clock.</summary>
public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
