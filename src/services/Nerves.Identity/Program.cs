using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Grpc;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;
using Nerves.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-identity");

builder.Services.AddOpenApi();
builder.Services.AddGrpc();

builder.Services.Configure<IdentitySecurityOptions>(
    builder.Configuration.GetSection(IdentitySecurityOptions.SectionName));

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IIdentifierHasher, HmacIdentifierHasher>();

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
app.MapGrpcService<IdentityGrpcService>();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
