using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nerves.Cli.CommandLine;
using Nerves.Cli.Infrastructure;
using Nerves.Identity.Persistence;

namespace Nerves.Cli.Commands;

/// <summary>
/// Answers "is this stack healthy and up to date?" without touching state:
/// database reachability, per-schema pending migrations, and the health plus
/// version of every configured service.
/// </summary>
public static class DoctorCommand
{
    private const string Ok = "ok";
    private const string Warn = "warn";
    private const string Fail = "fail";

    public static async Task<int> RunAsync(
        CliArguments args,
        CliOutput output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        var settings = CliSettings.From(args);
        var checks = new List<Check>();

        checks.Add(new Check(
            "cli",
            Ok,
            typeof(DoctorCommand).Assembly.GetName().Version?.ToString() ?? "0.0.0"));

        await CheckDatabaseAsync(settings, checks, cancellationToken).ConfigureAwait(false);
        await CheckServicesAsync(settings, checks, cancellationToken).ConfigureAwait(false);

        var failures = checks.Count(check => string.Equals(check.Status, Fail, StringComparison.Ordinal));

        if (output.Json)
        {
            output.Emit(new
            {
                generatedAt = CliOutput.Timestamp(),
                healthy = failures == 0,
                checks,
            });

            return failures == 0 ? ExitCodes.Success : ExitCodes.Failure;
        }

        output.Table(
            ["check", "status", "detail"],
            checks.Select(check => (IReadOnlyList<string>)[check.Name, check.Status, check.Detail]).ToArray());

        output.Line(failures == 0
            ? "no failures"
            : failures.ToString(CultureInfo.InvariantCulture) + " check(s) failed");

        return failures == 0 ? ExitCodes.Success : ExitCodes.Failure;
    }

    private static async Task CheckDatabaseAsync(
        CliSettings settings,
        List<Check> checks,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            checks.Add(new Check("database", Fail, "no connection string configured"));
            return;
        }

        try
        {
            await using var database = new IdentityDbContext(
                DatabaseTargets.Options<IdentityDbContext>(settings.ConnectionString));

            var connection = database.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            checks.Add(new Check("database", Ok, connection.ServerVersion));
            await connection.CloseAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            checks.Add(new Check("database", Fail, exception.Message));
            return;
        }

        foreach (var target in DatabaseTargets.All)
        {
            try
            {
                await using var database = target.CreateContext(settings.ConnectionString);
                var pending = (await database.Database
                    .GetPendingMigrationsAsync(cancellationToken)
                    .ConfigureAwait(false))
                    .ToArray();

                checks.Add(new Check(
                    "migrations:" + target.Name,
                    pending.Length == 0 ? Ok : Warn,
                    pending.Length == 0
                        ? target.Schema + " is up to date"
                        : pending.Length.ToString(CultureInfo.InvariantCulture) + " pending in " + target.Schema));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                checks.Add(new Check("migrations:" + target.Name, Fail, exception.Message));
            }
        }
    }

    private static async Task CheckServicesAsync(
        CliSettings settings,
        List<Check> checks,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        foreach (var (name, address) in settings.Services)
        {
            try
            {
                var baseUri = new Uri(address, UriKind.Absolute);

                using var health = await http
                    .GetAsync(new Uri(baseUri, "healthz"), cancellationToken)
                    .ConfigureAwait(false);

                if (!health.IsSuccessStatusCode)
                {
                    checks.Add(new Check(
                        "service:" + name,
                        Fail,
                        "healthz answered HTTP " + ((int)health.StatusCode).ToString(CultureInfo.InvariantCulture)));
                    continue;
                }

                using var meta = await http
                    .GetAsync(new Uri(baseUri, "api/v1/meta"), cancellationToken)
                    .ConfigureAwait(false);

                var version = "unknown";
                if (meta.IsSuccessStatusCode)
                {
                    var document = await meta.Content
                        .ReadFromJsonAsync<JsonElement>(cancellationToken)
                        .ConfigureAwait(false);

                    if (document.ValueKind == JsonValueKind.Object
                        && document.TryGetProperty("version", out var value)
                        && value.ValueKind == JsonValueKind.String)
                    {
                        version = value.GetString() ?? version;
                    }
                }

                checks.Add(new Check("service:" + name, Ok, "v" + version));
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                checks.Add(new Check("service:" + name, Fail, "timed out after 5s"));
            }
            catch (HttpRequestException exception)
            {
                checks.Add(new Check("service:" + name, Fail, exception.Message));
            }
            catch (JsonException exception)
            {
                checks.Add(new Check("service:" + name, Fail, exception.Message));
            }
        }
    }

    private sealed record Check(string Name, string Status, string Detail);
}
