using Nerves.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-gateway");

builder.Services.AddOpenApi();


var (routes, clusters) = Nerves.Gateway.Routing.GatewayRoutes.Build(builder.Configuration);

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);

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
app.MapReverseProxy();

app.Run();

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
