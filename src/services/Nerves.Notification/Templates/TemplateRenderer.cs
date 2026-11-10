using System.Text;

namespace Nerves.Notification.Templates;

/// <summary>
/// Minimal <c>{{name}}</c> renderer. Deliberately not a general-purpose template
/// engine: no conditionals, loops or code execution, so a template can never
/// become a scripting surface.
/// </summary>
public static class TemplateRenderer
{
    /// <summary>
    /// Replaces every known placeholder. Unknown names stay untouched and are
    /// reported through <paramref name="missing"/> so callers can reject them.
    /// </summary>
    public static string Render(
        string text,
        IReadOnlyDictionary<string, string>? values,
        ICollection<string>? missing = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            builder.Append(text, index, open - index);

            var close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                // An unclosed placeholder is literal text; do not swallow the rest.
                builder.Append(text, open, text.Length - open);
                break;
            }

            var name = text[(open + 2)..close].Trim();
            if (values is not null && values.TryGetValue(name, out var value))
            {
                builder.Append(value);
            }
            else
            {
                missing?.Add(name);
                builder.Append(text, open, close - open + 2);
            }

            index = close + 2;
        }

        return builder.ToString();
    }

    /// <summary>Placeholder names referenced by a template, in first-seen order.</summary>
    public static IReadOnlyList<string> Placeholders(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0)
            {
                break;
            }

            var close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }

            var name = text[(open + 2)..close].Trim();
            if (name.Length > 0 && seen.Add(name))
            {
                names.Add(name);
            }

            index = close + 2;
        }

        return names;
    }
}
