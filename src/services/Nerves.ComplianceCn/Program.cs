using Microsoft.EntityFrameworkCore;
using Nerves.ComplianceCn.Api;
using Nerves.ComplianceCn.Integration;
using Nerves.ComplianceCn.Persistence;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-compliance-cn");

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IdentityClient>();
builder.Services.AddNervesBearerAuthentication(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<ComplianceDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<ComplianceDbContext>();
}

var app = builder.Build();

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
