namespace Nerves.Core;

/// <summary>
/// Generates time-ordered identifiers (UUIDv7).
/// </summary>
/// <remarks>
/// UUIDv7 keeps index locality on PostgreSQL while staying generatable without a
/// central allocator, which is required for horizontal scaling (see PLAN §2.5).
/// </remarks>
public static class IdGenerator
{
    public static Guid NewId() => Guid.CreateVersion7();

    public static Guid NewId(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);
}
