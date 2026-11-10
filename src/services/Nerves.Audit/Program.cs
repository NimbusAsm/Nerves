using Microsoft.EntityFrameworkCore;
using Nerves.Audit.Api;
using Nerves.Audit.Archiving;
using Nerves.Audit.Grpc;
using Nerves.Audit.Persistence;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-audit");
builder.Services.AddNervesProxyHeaders(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.AddNervesBearerAuthentication(builder.Configuration);
builder.Services.Configure<ArchiveOptions>(builder.Configuration.GetSection(ArchiveOptions.SectionName));
builder.Services.AddScoped<AuditArchiveService>();
builder.Services.AddHostedService<AuditArchiveWorker>();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<AuditDbContext>();
}

var app = builder.Build();

app.UseNervesProxyHeaders();

if (await app.MigrateNervesDatabaseAsync<AuditDbContext>())
{
    return;
}

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-audit");
app.MapGrpcService<AuditGrpcService>();
app.MapAuditEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
