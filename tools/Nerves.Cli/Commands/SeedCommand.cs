using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nerves.Authorization.Persistence;
using Nerves.Cli.CommandLine;
using Nerves.Cli.Infrastructure;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;
using Nerves.Identity.Sharding;
using AuthorizationSeedOptions = Nerves.Authorization.Seeding.DevSeedOptions;
using IdentitySeedOptions = Nerves.Identity.Seeding.DevSeedOptions;

namespace Nerves.Cli.Commands;

/// <summary>
/// Runs the same development seeders the services run on startup, but on demand
/// and against an explicit environment. Refuses to touch a database with pending
/// migrations, and keeps the production guard of the seeders intact.
/// </summary>
public static class SeedCommand
{
    /// <summary>The identifier key `appsettings.Development.json` of the identity service ships with.</summary>
    private const string DevelopmentIdentifierHashKey = "ZGV2LW9ubHktaWRlbnRpZmllci1rZXktMzJieXRlcy0=";

    private const string DevelopmentEnvironment = "Development";

    public static async Task<int> RunAsync(
        CliArguments args,
        CliOutput output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        var settings = CliSettings.From(args);
        var environmentName = args.ValueOr(
            "environment",
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? DevelopmentEnvironment) ?? DevelopmentEnvironment;

        var allowProduction = args.Has("allow-production");
        var isDevelopment = string.Equals(environmentName, DevelopmentEnvironment, StringComparison.OrdinalIgnoreCase);

        var identifierHashKey = args.ValueOr(
            "identifier-hash-key",
            Environment.GetEnvironmentVariable("Security__IdentifierHashKey") ?? DevelopmentIdentifierHashKey)
            ?? DevelopmentIdentifierHashKey;

        if (!isDevelopment
            && string.Equals(identifierHashKey, DevelopmentIdentifierHashKey, StringComparison.Ordinal)
            && !allowProduction)
        {
            output.Line("error: refusing to seed outside Development with the development identifier hash key");
            output.Line("       set Security__IdentifierHashKey (or --identifier-hash-key) to the deployment key");
            return ExitCodes.Usage;
        }

        var identitySeed = new IdentitySeedOptions
        {
            Enabled = true,
            AllowInProduction = allowProduction,
            Realm = args.ValueOr("realm", "nerves") ?? "nerves",
            Username = args.ValueOr("username", "admin") ?? "admin",
            Password = args.ValueOr("password", "ChangeMe!123") ?? "ChangeMe!123",
            Email = args.ValueOr("email", "admin@nerves.local") ?? "admin@nerves.local",
            DisplayName = args.ValueOr("display-name", "Nerves Administrator") ?? "Nerves Administrator",
        };

        var authorizationSeed = new AuthorizationSeedOptions
        {
            Enabled = true,
            AllowInProduction = allowProduction,
            Realm = identitySeed.Realm,
            BootstrapUsername = identitySeed.Username,
        };

        var shardCount = int.TryParse(
            args.Value("shard-count"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedShardCount) && parsedShardCount > 0
                ? parsedShardCount
                : 1;

        var environment = new CliHostEnvironment(environmentName);

        try
        {
            await EnsureMigratedAsync(settings.ConnectionString, cancellationToken).ConfigureAwait(false);

            await using var provider = BuildProvider(settings, environment, identifierHashKey, shardCount);

            var identitySeeder = new Nerves.Identity.Seeding.DevSeeder(
                provider,
                Options.Create(identitySeed),
                environment);

            await identitySeeder.StartAsync(cancellationToken).ConfigureAwait(false);

            var authorizationSeeder = new Nerves.Authorization.Seeding.AuthorizationDevSeeder(
                provider,
                Options.Create(authorizationSeed),
                environment);

            await authorizationSeeder.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            output.Line("error: " + exception.Message);
            return ExitCodes.Failure;
        }

        if (output.Json)
        {
            output.Emit(new
            {
                generatedAt = CliOutput.Timestamp(),
                environment = environmentName,
                realm = identitySeed.Realm,
                username = identitySeed.Username,
                email = identitySeed.Email,
                seeded = true,
            });

            return ExitCodes.Success;
        }

        output.Line(string.Create(
            CultureInfo.InvariantCulture,
            $"seeded administrator '{identitySeed.Username}' in realm '{identitySeed.Realm}' (environment: {environmentName})"));
        output.Line("the authorization seeder granted the management role");

        if (string.Equals(identitySeed.Password, "ChangeMe!123", StringComparison.Ordinal))
        {
            output.Line("warning: the default development password is still in use");
        }

        return ExitCodes.Success;
    }

    private static ServiceProvider BuildProvider(
        CliSettings settings,
        CliHostEnvironment environment,
        string identifierHashKey,
        int shardCount)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton(Options.Create(new ShardingOptions { ShardCount = shardCount }));
        services.AddSingleton(_ => IdentityDataPlane.SingleDatabase(settings.ConnectionString));
        services.AddSingleton<IdentityStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IIdentifierHasher>(_ => new HmacIdentifierHasher(
            Options.Create(new IdentitySecurityOptions { IdentifierHashKey = identifierHashKey })));

        services.AddScoped(_ => new IdentityDbContext(
            DatabaseTargets.Options<IdentityDbContext>(settings.ConnectionString)));
        services.AddScoped(_ => new AuthorizationDbContext(
            DatabaseTargets.Options<AuthorizationDbContext>(settings.ConnectionString)));

        return services.BuildServiceProvider();
    }

    private static async Task EnsureMigratedAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var identity = new IdentityDbContext(
            DatabaseTargets.Options<IdentityDbContext>(connectionString));
        await using var authorization = new AuthorizationDbContext(
            DatabaseTargets.Options<AuthorizationDbContext>(connectionString));

        var pending = (await identity.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
        var pendingAuthorization = (await authorization.Database
            .GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToArray();

        if (pending.Length > 0 || pendingAuthorization.Length > 0)
        {
            throw new InvalidOperationException(
                "pending migrations detected; run 'nerves db migrate' first");
        }
    }
}
