using Microsoft.EntityFrameworkCore;
using Nerves.Directory.Api;
using Nerves.Directory.Grpc;
using Nerves.Directory.Persistence;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-directory");

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.Configure<AdminTokenOptions>(builder.Configuration.GetSection(AdminTokenOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<DirectoryDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddHealthChecks().AddDbContextCheck<DirectoryDbContext>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapNervesDefaults("nerves-directory");
app.MapGrpcService<DirectoryGrpcService>();
app.MapOrganizationEndpoints();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
