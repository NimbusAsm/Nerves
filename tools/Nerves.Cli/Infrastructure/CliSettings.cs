using Nerves.Cli.CommandLine;

namespace Nerves.Cli.Infrastructure;

/// <summary>
/// Where the CLI talks to. Both the database and the service addresses come from
/// the same environment variables the services themselves read, so an operator
/// can point the CLI at a stack without repeating the configuration.
/// </summary>
public sealed record CliSettings(string ConnectionString, IReadOnlyDictionary<string, string> Services)
{
    /// <summary>The development compose stack (see deploy/docker/docker-compose.yml).</summary>
    public const string DevelopmentConnectionString =
        "Host=127.0.0.1;Port=55440;Database=nerves;Username=nerves;Password=nerves";

    private static readonly IReadOnlyDictionary<string, string> DefaultServices =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["identity"] = "http://127.0.0.1:5102/",
            ["session"] = "http://127.0.0.1:5103/",
            ["authorization"] = "http://127.0.0.1:5104/",
            ["audit"] = "http://127.0.0.1:5105/",
            ["directory"] = "http://127.0.0.1:5106/",
            ["compliance"] = "http://127.0.0.1:5107/",
            ["notification"] = "http://127.0.0.1:5108/",
        };

    public static CliSettings From(CliArguments args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var connectionString = args.ValueOr(
            "connection-string",
            Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? DevelopmentConnectionString);

        var services = new Dictionary<string, string>(DefaultServices, StringComparer.Ordinal);

        foreach (var name in DefaultServices.Keys)
        {
            var configured = Environment.GetEnvironmentVariable("Console__Services__" + name);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                services[name] = configured;
            }
        }

        return new CliSettings(connectionString ?? DevelopmentConnectionString, services);
    }
}
