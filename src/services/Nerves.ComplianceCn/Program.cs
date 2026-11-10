using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Nerves.ComplianceCn.Api;
using Nerves.ComplianceCn.Integration;
using Nerves.ComplianceCn.Options;
using Nerves.ComplianceCn.Persistence;
using Nerves.Contracts.Audit.V1;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-compliance-cn");
builder.Services.AddNervesProxyHeaders(builder.Configuration);
builder.Services.AddNervesRateLimiting(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IdentityClient>();
builder.Services.AddNervesBearerAuthentication(builder.Configuration);

builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddScoped<RetentionSweeper>();
builder.Services.AddHostedService<RetentionWorker>();
builder.Services.AddHostedService<OutboxDispatcher>();

var auditAddress = builder.Configuration["Audit:GrpcAddress"] ?? "http://127.0.0.1:5205";
builder.Services.AddSingleton(_ => new AuditService.AuditServiceClient(GrpcChannel.ForAddress(auditAddress)));
builder.Services.AddSingleton<IComplianceAuditPublisher, ComplianceAuditPublisher>();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<ComplianceDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<ComplianceDbContext>();
}

var app = builder.Build();

app.UseNervesProxyHeaders();
app.UseNervesRateLimiting();

if (await app.MigrateNervesDatabaseAsync<ComplianceDbContext>())
{
    return;
}

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-compliance-cn");
app.MapComplianceEndpoints();
app.MapSubjectEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
