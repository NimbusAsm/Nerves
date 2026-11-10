using Microsoft.Extensions.Options;

namespace Nerves.Identity.Verification;

/// <summary>
/// Resolves the configured provider. Two providers ship in the box: <c>manual</c>
/// (an administrator decides) and <c>http</c> (an external service decides).
/// </summary>
public sealed class VerificationProviderFactory
{
    private readonly IReadOnlyDictionary<string, IVerificationProvider> _providers;

    public VerificationProviderFactory(IEnumerable<IVerificationProvider> providers, IOptions<VerificationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        _providers = providers.ToDictionary(x => x.Name, StringComparer.Ordinal);
        Configured = options.Value.Provider;
    }

    /// <summary>Provider name from <c>Verification:Provider</c>.</summary>
    public string Configured { get; }

    /// <summary>Resolves an explicit provider name, falling back to the configured default.</summary>
    public IVerificationProvider? Resolve(string? name)
    {
        var key = string.IsNullOrWhiteSpace(name) ? Configured : name.Trim().ToLowerInvariant();
        return _providers.GetValueOrDefault(key);
    }
}
