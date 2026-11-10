using System.Globalization;
using Nerves.Cli.CommandLine;
using Nerves.Cli.Commands;

var parsed = CliArguments.Parse(args);

if (args.Length == 0 || parsed.Has("help"))
{
    PrintUsage();
    return args.Length == 0 ? ExitCodes.Usage : ExitCodes.Success;
}

var output = new CliOutput(parsed.Has("json"));

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    return string.Join(' ', parsed.Positionals) switch
    {
        "db migrate" => await DbMigrateCommand.RunAsync(parsed, output, cancellation.Token),
        "seed" => await SeedCommand.RunAsync(parsed, output, cancellation.Token),
        "doctor" => await DoctorCommand.RunAsync(parsed, output, cancellation.Token),
        "shard reconcile" => await ShardReconcileCommand.RunAsync(parsed, output, cancellation.Token),
        "version" => Version(output),
        _ => Unknown(parsed),
    };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("cancelled");
    return ExitCodes.Failure;
}

static int Version(CliOutput output)
{
    var assembly = typeof(CliOutput).Assembly;
    var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
    var informational = assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
        .FirstOrDefault()
        ?.InformationalVersion ?? version;

    if (output.Json)
    {
        output.Emit(new { product = "Nerves.Cli", version, informationalVersion = informational });
        return ExitCodes.Success;
    }

    output.Line(string.Create(
        CultureInfo.InvariantCulture,
        $"nerves {informational}"));

    return ExitCodes.Success;
}

static int Unknown(CliArguments parsed)
{
    var path = string.Join(' ', parsed.Positionals);

    if (path.Length > 0)
    {
        Console.Error.WriteLine("unknown command: " + path);
    }

    PrintUsage();
    return ExitCodes.Usage;
}

static void PrintUsage()
{
    Console.WriteLine("""
        nerves - Nerves operations CLI

        Usage:
          nerves db migrate [--service <name[,name]>] [--dry-run] [--connection-string <cs>]
          nerves seed [--realm <realm>] [--username <name>] [--password <secret>] [--environment <name>] [--allow-production] [--shard-count <n>]
          nerves doctor [--connection-string <cs>]
          nerves shard reconcile --shard-count <n> [--dry-run] [--connection-string <cs>]
          nerves version

        Global options:
          --connection-string <cs>  overrides ConnectionStrings__Default
          --json                    machine-readable output (one JSON document on stdout)
          --help                    show this help

        Environment:
          ConnectionStrings__Default   database to migrate, inspect or seed
          Security__IdentifierHashKey  required by "seed" outside Development
          Console__Services__<name>    service base addresses probed by "doctor"
          DOTNET_ENVIRONMENT           environment name used by "seed" (default Development)

        Examples:
          nerves doctor
          nerves db migrate --dry-run
          nerves seed --username admin
          nerves shard reconcile --shard-count 8 --dry-run
        """);
}
