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

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-session");
builder.Services.AddNervesProxyHeaders(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.Configure<SessionLifetimeOptions>(builder.Configuration.GetSection(SessionLifetimeOptions.SectionName));
builder.Services.AddHostedService<SessionExpiryWorker>();
builder.Services.AddGrpc();

var auditAddress = builder.Configuration["Audit:GrpcAddress"] ?? "http://127.0.0.1:5205";
builder.Services.AddSingleton(_ => new AuditService.AuditServiceClient(GrpcChannel.ForAddress(auditAddress)));
builder.Services.AddSingleton<IAuditPublisher, AuditPublisher>();
builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddNervesBearerAuthentication(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<SessionDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<SessionDbContext>();
}

var app = builder.Build();

app.UseNervesProxyHeaders();

if (await app.MigrateNervesDatabaseAsync<SessionDbContext>())
{
    return;
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
