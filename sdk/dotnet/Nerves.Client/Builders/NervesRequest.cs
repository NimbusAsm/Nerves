using System.Globalization;

namespace Nerves.Client;

/// <summary>Common state carried by every chainable request.</summary>
public abstract record NervesRequest
{
    /// <summary>Transport. Internal so callers cannot build a request without a client.</summary>
    internal ApiRequester Api { get; init; } = null!;
}

/// <summary>
/// Shared chainable modifiers for list requests. The self type keeps each
/// modifier returning the concrete builder, so chains stay typed.
/// </summary>
/// <typeparam name="TSelf">Concrete builder type.</typeparam>
public abstract record NervesListRequest<TSelf> : NervesRequest
    where TSelf : NervesListRequest<TSelf>
{
    internal IReadOnlyList<KeyValuePair<string, string>> Filters { get; init; } = [];

    internal string? SortExpression { get; init; }

    internal int? PageSize { get; init; }

    internal string? CursorToken { get; init; }

    /// <summary>Adds <c>filter[field]=value</c>.</summary>
    public TSelf Filter(string field, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(value);

        return (TSelf)(this with
        {
            Filters = [.. Filters, new KeyValuePair<string, string>(field, value)],
        });
    }

    /// <summary>Sets <c>sort</c>; prefix with <c>-</c> for descending.</summary>
    public TSelf Sort(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        return (TSelf)(this with { SortExpression = expression });
    }

    /// <summary>Sets the page size (<c>limit</c>).</summary>
    public TSelf Limit(int pageSize)
    {
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than zero.");
        }

        return (TSelf)(this with { PageSize = pageSize });
    }

    /// <summary>Continues from an opaque cursor returned by a previous page.</summary>
    public TSelf Cursor(string cursor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cursor);
        return (TSelf)(this with { CursorToken = cursor });
    }

    internal IEnumerable<(string Key, string? Value)> SharedQueryParameters
    {
        get
        {
            foreach (var filter in Filters)
            {
                yield return ($"filter[{filter.Key}]", filter.Value);
            }

            if (SortExpression is not null)
            {
                yield return ("sort", SortExpression);
            }

            if (PageSize is not null)
            {
                yield return ("limit", PageSize.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (CursorToken is not null)
            {
                yield return ("cursor", CursorToken);
            }
        }
    }
}

/// <summary>Helpers for immutable PATCH documents.</summary>
internal static class PatchDocument
{
    public static IReadOnlyDictionary<string, object?> Set(
        IReadOnlyDictionary<string, object?> source,
        string key,
        object? value)
    {
        var copy = new Dictionary<string, object?>(source, StringComparer.Ordinal)
        {
            [key] = value,
        };
        return copy;
    }
}
