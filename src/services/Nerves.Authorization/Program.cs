using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Api;
using Nerves.Authorization.Caching;
using Nerves.Authorization.Grpc;
using Nerves.Authorization.Integration;
using Nerves.Authorization.Persistence;
using Nerves.Authorization.Policy;
using Nerves.Authorization.Seeding;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-authorization");
builder.Services.AddNervesProxyHeaders(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.AddNervesBearerAuthentication(builder.Configuration);
builder.Services.AddSingleton<PermissionCache>();
builder.Services.AddScoped<CachedPermissions>();
builder.Services.AddScoped<IntegrationSettingsStore>();
builder.Services.AddSingleton<PdpClient>();
builder.Services.Configure<PdpOptions>(builder.Configuration.GetSection(PdpOptions.SectionName));
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection(AuditOptions.SectionName));
var auditAddress = builder.Configuration["Audit:GrpcAddress"] ?? "http://127.0.0.1:5205";
builder.Services.AddSingleton(_ => new Nerves.Contracts.Audit.V1.AuditService.AuditServiceClient(
    Grpc.Net.Client.GrpcChannel.ForAddress(auditAddress)));
builder.Services.AddSingleton<IAuditPublisher, AuditPublisher>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHttpClient("pdp", client => client.Timeout = TimeSpan.FromSeconds(3));
builder.Services.Configure<DevSeedOptions>(builder.Configuration.GetSection(DevSeedOptions.SectionName));
builder.Services.AddHostedService<AuthorizationDevSeeder>();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AuthorizationDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<AuthorizationDbContext>();
}

var app = builder.Build();

app.UseNervesProxyHeaders();

if (await app.MigrateNervesDatabaseAsync<AuthorizationDbContext>())
{
    return;
}

await using (var startupScope = app.Services.CreateAsyncScope())
{
    var store = startupScope.ServiceProvider.GetRequiredService<IntegrationSettingsStore>();
    var pdp = startupScope.ServiceProvider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<PdpOptions>>().Value;
    await store.ApplyAsync(pdp, CancellationToken.None);
}

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-authorization");
app.MapGrpcService<AuthorizationGrpcService>();
app.MapAuthorizationEndpoints();
app.MapGroupEndpoints();
app.MapAdminStatusEndpoints();
app.MapDecisionEndpoints();
app.MapScimGroupEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
