using Fido2NetLib;
using Grpc.Net.Client;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Contracts.Audit.V1;
using Nerves.Identity.Api;
using Nerves.Identity.Grpc;
using Nerves.Identity.Integration;
using Nerves.Identity.Options;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;
using Nerves.Identity.Seeding;
using Nerves.Identity.Sharding;
using Nerves.Identity.Verification;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-identity");
builder.Services.AddNervesProxyHeaders(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.Configure<IdentitySecurityOptions>(
    builder.Configuration.GetSection(IdentitySecurityOptions.SectionName));

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IIdentifierHasher, HmacIdentifierHasher>();
builder.Services.AddSingleton<TotpService>();
builder.Services.AddSingleton<TotpSecretProtector>();
builder.Services.AddSingleton<RecoveryCodes>();
builder.Services.AddScoped<PasswordResetTokens>();
builder.Services.AddScoped<Nerves.Identity.PasswordReset.PasswordResetService>();
builder.Services.Configure<Nerves.Identity.PasswordReset.PasswordResetOptions>(
    builder.Configuration.GetSection(Nerves.Identity.PasswordReset.PasswordResetOptions.SectionName));
builder.Services.Configure<NotificationClientOptions>(builder.Configuration.GetSection(NotificationClientOptions.SectionName));
builder.Services.AddHttpClient(NotificationClient.HttpClientName, (services, client) =>
{
    var options = services.GetRequiredService<IOptions<NotificationClientOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30));
});
builder.Services.AddSingleton<NotificationClient>();
builder.Services.AddSingleton<INotificationSender>(services => services.GetRequiredService<NotificationClient>());
builder.Services.AddSingleton<ISessionRevoker>(services => services.GetRequiredService<SessionClient>());
builder.Services.Configure<VerificationOptions>(builder.Configuration.GetSection(VerificationOptions.SectionName));
builder.Services.Configure<HttpVerificationOptions>(builder.Configuration.GetSection(HttpVerificationOptions.SectionName));
builder.Services.AddHttpClient(HttpVerificationProvider.HttpClientName, (services, client) =>
{
    var options = services.GetRequiredService<IOptions<HttpVerificationOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 60));
});
builder.Services.AddSingleton<IVerificationProvider, ManualVerificationProvider>();
builder.Services.AddSingleton<IVerificationProvider, HttpVerificationProvider>();
builder.Services.AddSingleton<VerificationProviderFactory>();
builder.Services.Configure<ShardingOptions>(builder.Configuration.GetSection(ShardingOptions.SectionName));
builder.Services.AddSingleton<ShardResolver>();
builder.Services.AddScoped<PasskeyService>();
builder.Services.AddSingleton<Nerves.Identity.Integration.SessionClient>();
builder.Services.AddSingleton<Nerves.Identity.Integration.IAuditPublisher, Nerves.Identity.Integration.AuditPublisher>();
builder.Services.AddHostedService<Nerves.Identity.Integration.OutboxDispatcher>();
builder.Services.AddScoped<Nerves.Identity.Sharding.RoutingIndexReconciler>();
builder.Services.AddSingleton<IFido2>(_ => new Fido2(new Fido2Configuration
{
    RPID = "nerves.local",
    RPName = "Nerves",
    Origins = new HashSet<string> { "http://127.0.0.1:5300", "http://localhost:5300" },
}));
builder.Services.AddNervesDataProtection(builder.Configuration, builder.Environment);

// Provider callbacks are anonymous; a fixed window keeps a broken or hostile
// provider from turning them into a database write amplifier.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("verification-callback", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddNervesBearerAuthentication(builder.Configuration);

builder.Services.Configure<DevSeedOptions>(builder.Configuration.GetSection(DevSeedOptions.SectionName));
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection(AuditOptions.SectionName));
var auditAddress = builder.Configuration.GetSection(AuditOptions.SectionName)["GrpcAddress"] ?? "http://127.0.0.1:5205";
builder.Services.AddSingleton(_ => new AuditService.AuditServiceClient(GrpcChannel.ForAddress(auditAddress)));
builder.Services.AddSingleton<AuditClient>();

builder.Services.AddHostedService<DevSeeder>();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>();
}

var app = builder.Build();

app.UseNervesProxyHeaders();

if (await app.MigrateNervesDatabaseAsync<IdentityDbContext>())
{
    return;
}

if (app.Configuration.GetValue("Sharding:Recompute", false))
{
    await using var reconcileScope = app.Services.CreateAsyncScope();
    var reconciler = reconcileScope.ServiceProvider.GetRequiredService<RoutingIndexReconciler>();
    var dryRun = app.Configuration.GetValue("Sharding:RecomputeDryRun", false);
    var report = await reconciler.RunAsync(dryRun, CancellationToken.None);
    Console.WriteLine(
        "shard reconciliation: scanned={0} changed={1} dryRun={2} distribution={3}",
        report.Scanned,
        report.Changed,
        dryRun,
        string.Join(",", report.Distribution.OrderBy(x => x.Key).Select(x => x.Key + ":" + x.Value)));
    return;
}

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-identity");
app.MapGrpcService<IdentityGrpcService>();
app.MapAccountEndpoints();
app.MapLegalEntityEndpoints();
app.MapEventEndpoints();
app.MapStatsEndpoints();
app.MapMfaEndpoints();
app.MapScimUserEndpoints();
app.MapVerificationEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
