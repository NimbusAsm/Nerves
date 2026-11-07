using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Persistence;
using Nerves.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-identity");

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapNervesDefaults("nerves-identity");

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
