using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Audit.V1;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;
using Nerves.Session.Api;
using Nerves.Session.Grpc;
using Nerves.Session.Integration;
using Nerves.Session.Options;
using Nerves.Session.Persistence;
using Nerves.Sharding;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-session");
builder.Services.AddNervesProxyHeaders(builder.Configuration);
builder.Services.AddNervesRateLimiting(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.Configure<SessionLifetimeOptions>(builder.Configuration.GetSection(SessionLifetimeOptions.SectionName));
builder.Services.AddHostedService<SessionExpiryWorker>();
builder.Services.AddGrpc();

var auditAddress = builder.Configuration["Audit:GrpcAddress"] ?? "http://127.0.0.1:5205";
builder.Services.AddSingleton(_ => new AuditService.AuditServiceClient(GrpcChannel.ForAddress(auditAddress)));
builder.Services.AddSingleton<IAuditPublisher, AuditPublisher>();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddNervesBearerAuthentication(builder.Configuration);

builder.Services.Configure<SessionStoreOptions>(builder.Configuration.GetSection(SessionStoreOptions.SectionName));
builder.Services.AddSingleton<ShardConnectionResolver>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<SessionMigrator>();
builder.Services.AddSingleton<Nerves.Session.Sharding.SessionShardRebalancer>();
builder.Services.AddHealthChecks().AddCheck<SessionStoreHealthCheck>("session-store");

// Kept for design-time migrations and tests that resolve the context directly.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<SessionDbContext>(options => options.UseNpgsql(connectionString));
}

var app = builder.Build();

app.UseNervesProxyHeaders();
app.UseNervesRateLimiting();

var migrateOnly = app.Configuration.GetValue("Database:MigrateOnly", false);
var migrateOnStart = app.Configuration.GetValue("Database:MigrateOnStart", false);
if (migrateOnly || migrateOnStart)
{
    var migrator = app.Services.GetRequiredService<SessionMigrator>();
    await migrator.MigrateAsync(CancellationToken.None);

    if (migrateOnly)
    {
        return;
    }
}

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-session");
app.MapGrpcService<SessionGrpcService>();
app.MapSessionEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
