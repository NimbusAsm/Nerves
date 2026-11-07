namespace Nerves.Core;

/// <summary>
/// A cursor-based page. Offset paging is deliberately not offered: it degrades
/// as the table grows and cannot be served by a single shard at scale.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
