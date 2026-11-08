using Fido2NetLib;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Audit.V1;
using Nerves.Identity.Api;
using Nerves.Identity.Grpc;
using Nerves.Identity.Integration;
using Nerves.Identity.Options;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;
using Nerves.Identity.Seeding;
using Nerves.Identity.Sharding;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-identity");

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.Configure<IdentitySecurityOptions>(
    builder.Configuration.GetSection(IdentitySecurityOptions.SectionName));

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IIdentifierHasher, HmacIdentifierHasher>();
builder.Services.AddSingleton<TotpService>();
builder.Services.AddSingleton<TotpSecretProtector>();
builder.Services.AddSingleton<RecoveryCodes>();
builder.Services.Configure<ShardingOptions>(builder.Configuration.GetSection(ShardingOptions.SectionName));
builder.Services.AddSingleton<ShardResolver>();
builder.Services.AddScoped<PasskeyService>();
builder.Services.AddSingleton<IFido2>(_ => new Fido2(new Fido2Configuration
{
    RPID = "nerves.local",
    RPName = "Nerves",
    Origins = new HashSet<string> { "http://127.0.0.1:5300", "http://localhost:5300" },
}));
builder.Services.AddDataProtection();

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

await app.MigrateNervesDatabaseAsync<IdentityDbContext>();

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-identity");
app.MapGrpcService<IdentityGrpcService>();
app.MapAccountEndpoints();
app.MapLegalEntityEndpoints();
app.MapStatsEndpoints();
app.MapMfaEndpoints();
app.MapScimUserEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
