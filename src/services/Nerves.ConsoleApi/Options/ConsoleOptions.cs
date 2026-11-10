using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Nerves.ConsoleApi.Options;

/// <summary>
/// Downstream addresses and budgets for the console aggregation layer. The
/// console owns no state: everything it answers comes from a service call.
/// </summary>
public sealed record ConsoleOptions
{
    public const string SectionName = "Console";

    /// <summary>Services the console always aggregates; a missing one is a config error.</summary>
    public static readonly IReadOnlyList<string> Required = ["identity", "session", "authorization", "audit", "compliance"];

    private static readonly IReadOnlyDictionary<string, string> Defaults =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["identity"] = "http://127.0.0.1:5102/",
            ["session"] = "http://127.0.0.1:5103/",
            ["authorization"] = "http://127.0.0.1:5104/",
            ["audit"] = "http://127.0.0.1:5105/",
            ["compliance"] = "http://127.0.0.1:5107/",
        };

    /// <summary>Per-call timeout. A slow dependency degrades the response, never blocks it.</summary>
    public int TimeoutSeconds { get; init; } = 3;

    /// <summary>How many audit events an account detail embeds.</summary>
    public int MaxAuditEvents { get; init; } = 20;

    /// <summary>
    /// Upper bound on the account scan used by the entity view. Identity has no
    /// legal-entity filter yet, so the console scans one page and reports when it
    /// stopped early (see the <c>accountsTruncated</c> flag).
    /// </summary>
    public int AccountScanLimit { get; init; } = 200;

    /// <summary>Base address per downstream service, keyed by service name.</summary>
    public IReadOnlyDictionary<string, string> Services { get; init; } = Defaults;

    /// <summary>Reads the configuration section and validates the address map.</summary>
    public static ConsoleOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var configured = section.GetSection("Services").Get<Dictionary<string, string>>()
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var addresses = new Dictionary<string, string>(Defaults, StringComparer.Ordinal);
        foreach (var (name, address) in configured)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                addresses[name] = address;
            }
        }

        foreach (var name in Required)
        {
            if (!addresses.TryGetValue(name, out var address) || string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Console:Services:{name} is not configured."));
            }

            if (!Uri.TryCreate(address, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Console:Services:{name} must be an absolute URI."));
            }
        }

        var timeout = section.GetValue("TimeoutSeconds", 3);
        var auditEvents = section.GetValue("MaxAuditEvents", 20);
        var scanLimit = section.GetValue("AccountScanLimit", 200);

        return new ConsoleOptions
        {
            TimeoutSeconds = Math.Clamp(timeout, 1, 30),
            MaxAuditEvents = Math.Clamp(auditEvents, 1, 200),
            AccountScanLimit = Math.Clamp(scanLimit, 1, 1000),
            Services = addresses,
        };
    }
}
