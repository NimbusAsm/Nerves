namespace Nerves.Client;

/// <summary>
/// One cursor page. The cursor is opaque: pass <see cref="NextCursor"/> back to
/// <c>Cursor(...)</c>; never derive offsets from it.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Items in this page.</param>
/// <param name="NextCursor">Cursor of the next page, or <see langword="null"/> at the end.</param>
public sealed record NervesPage<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    /// <summary>Whether another page exists.</summary>
    public bool HasMore => !string.IsNullOrEmpty(NextCursor);

}
