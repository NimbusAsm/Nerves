namespace Nerves.Cli.CommandLine;

/// <summary>
/// Minimal `--name value` / `--name=value` / `--flag` parser. The CLI keeps its
/// dependency surface small on purpose: it ships as a dotnet tool.
/// </summary>
public sealed class CliArguments
{
    private static readonly char[] Separators = [','];

    private readonly Dictionary<string, string?> _options;

    private CliArguments(IReadOnlyList<string> positionals, Dictionary<string, string?> options)
    {
        Positionals = positionals;
        _options = options;
    }

    /// <summary>Arguments that are not options, in order (the command path).</summary>
    public IReadOnlyList<string> Positionals { get; }

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var positionals = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);

        for (var index = 0; index < args.Count; index++)
        {
            var current = args[index];

            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(current);
                continue;
            }

            var name = current[2..];
            var separator = name.IndexOf('=', StringComparison.Ordinal);

            if (separator >= 0)
            {
                options[name[..separator]] = name[(separator + 1)..];
                continue;
            }

            if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[name] = args[index + 1];
                index++;
                continue;
            }

            options[name] = null;
        }

        return new CliArguments(positionals, options);
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Value(string name) => _options.TryGetValue(name, out var value) ? value : null;

    public string? ValueOr(string name, string? fallback) => Value(name) ?? fallback;

    /// <summary>Comma-separated option expanded into trimmed values.</summary>
    public IReadOnlyList<string> ListValue(string name) =>
        Value(name)?.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
}
