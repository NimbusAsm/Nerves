using Grpc.Net.Client;
using Nerves.Contracts.Identity.V1;
using Nerves.Gateway.Api;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-gateway");

builder.Services.AddOpenApi();
builder.Services.Configure<AdminTokenOptions>(builder.Configuration.GetSection(AdminTokenOptions.SectionName));

var identityAddress = builder.Configuration["Identity:GrpcAddress"] ?? "http://127.0.0.1:5202";
builder.Services.AddSingleton(_ => new IdentityService.IdentityServiceClient(GrpcChannel.ForAddress(identityAddress)));

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var adminOrigins = builder.Configuration.GetSection("Cors:AdminOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    policy.WithOrigins(adminOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();

app.MapOpenApi();
app.MapNervesDefaults("nerves-gateway");
app.MapLoginEndpoints();
app.MapReverseProxy();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
