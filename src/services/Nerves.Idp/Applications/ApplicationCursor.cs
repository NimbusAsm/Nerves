using System.Text;

namespace Nerves.Idp.Applications;

/// <summary>
/// Opaque cursor for the application list. Applications are few (tens to
/// hundreds), so an offset inside an encoded token is accurate enough while
/// still hiding paging details from clients.
/// </summary>
public static class ApplicationCursor
{
    private const string Prefix = "a:";

    public static string? Encode(int offset)
    {
        if (offset <= 0)
        {
            return null;
        }

        var raw = Encoding.UTF8.GetBytes(Prefix + offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? cursor, out int offset, out string? error)
    {
        offset = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));

            if (!decoded.StartsWith(Prefix, StringComparison.Ordinal)
                || !int.TryParse(decoded[Prefix.Length..], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                || parsed < 0)
            {
                error = "cursor is not a valid paging cursor";
                return false;
            }

            offset = parsed;
            return true;
        }
        catch (FormatException)
        {
            error = "cursor is not a valid paging cursor";
            return false;
        }
    }
}
