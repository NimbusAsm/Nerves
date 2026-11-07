using Microsoft.EntityFrameworkCore;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;
using Nerves.Session.Api;
using Nerves.Session.Grpc;
using Nerves.Session.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-session");

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.Configure<AdminTokenOptions>(builder.Configuration.GetSection(AdminTokenOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<SessionDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<SessionDbContext>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapNervesDefaults("nerves-session");
app.MapGrpcService<SessionGrpcService>();
app.MapSessionEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
