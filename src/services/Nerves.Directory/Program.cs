using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Nerves.Contracts.Audit.V1;
using Nerves.Directory.Api;
using Nerves.Directory.Grpc;
using Nerves.Directory.Integration;
using Nerves.Directory.Persistence;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-directory");
builder.Services.AddNervesProxyHeaders(builder.Configuration);
builder.Services.AddNervesRateLimiting(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.AddNervesBearerAuthentication(builder.Configuration);

var auditAddress = builder.Configuration["Audit:GrpcAddress"] ?? "http://127.0.0.1:5205";
builder.Services.AddSingleton(_ => new AuditService.AuditServiceClient(GrpcChannel.ForAddress(auditAddress)));
builder.Services.AddSingleton<IAuditPublisher, AuditPublisher>();
builder.Services.AddHostedService<OutboxDispatcher>();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<DirectoryDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<DirectoryDbContext>();
}

var app = builder.Build();

app.UseNervesProxyHeaders();
app.UseNervesRateLimiting();

if (await app.MigrateNervesDatabaseAsync<DirectoryDbContext>())
{
    return;
}

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-directory");
app.MapGrpcService<DirectoryGrpcService>();
app.MapOrganizationEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
