namespace Nerves.Identity.Scim;

/// <summary>
/// Minimal SCIM filter support: <c>attribute eq "value"</c>. SCIM clients use it
/// for idempotent lookups (userName / externalId), which is all provisioners
/// need in practice; unsupported expressions are rejected explicitly.
/// </summary>
public sealed record ScimFilter(string Attribute, string Value)
{
    public static bool TryParse(string? raw, out ScimFilter filter, out string? error)
    {
        filter = new ScimFilter(string.Empty, string.Empty);
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            raw.Trim(),
            "^([A-Za-z0-9_.]+)\\s+eq\\s+(?:\"([^\"]*)\"|(\\S+))$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            error = "Only 'attribute eq \"value\"' filters are supported.";
            return false;
        }

        var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
        filter = new ScimFilter(match.Groups[1].Value, value);
        return true;
    }
}
