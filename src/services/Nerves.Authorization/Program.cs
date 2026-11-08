using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Api;
using Nerves.Authorization.Grpc;
using Nerves.Authorization.Persistence;
using Nerves.Authorization.Seeding;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-authorization");

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.AddNervesBearerAuthentication(builder.Configuration);
builder.Services.Configure<DevSeedOptions>(builder.Configuration.GetSection(DevSeedOptions.SectionName));
builder.Services.AddHostedService<AuthorizationDevSeeder>();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AuthorizationDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<AuthorizationDbContext>();
}

var app = builder.Build();

app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-authorization");
app.MapGrpcService<AuthorizationGrpcService>();
app.MapAuthorizationEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
