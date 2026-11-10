using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nerves.ConsoleApi.Aggregation;
using Nerves.ConsoleApi.Api;
using Nerves.ConsoleApi.Diagnostics;
using Nerves.ConsoleApi.Options;
using Nerves.ServiceDefaults;
using Nerves.ServiceDefaults.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddNervesDefaults("nerves-console");
builder.Services.AddNervesProxyHeaders(builder.Configuration);
builder.Services.AddNervesRateLimiting(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

// Addresses come from configuration and are fixed for the process lifetime:
// the console is a router over the control plane, not a service registry.
var consoleOptions = ConsoleOptions.From(builder.Configuration);
builder.Services.AddSingleton(consoleOptions);

foreach (var (name, address) in consoleOptions.Services)
{
    builder.Services.AddHttpClient(name, client =>
    {
        client.BaseAddress = new Uri(address, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(consoleOptions.TimeoutSeconds);
    });
}

builder.Services.AddSingleton<IDownstreamClient, DownstreamClient>();
builder.Services.AddSingleton<ConsoleAggregator>();
builder.Services.AddHealthChecks().AddCheck<DownstreamHealthCheck>("downstream");

builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(ConsoleMetrics.MeterName));

builder.Services.AddNervesBearerAuthentication(builder.Configuration);

var app = builder.Build();

app.UseNervesProxyHeaders();
app.UseNervesRateLimiting();
app.UseNervesSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapNervesDefaults("nerves-console");
app.MapConsoleEndpoints();

// /healthz and /readyz stay process-local (see ServiceDefaults); the downstream
// view is a separate endpoint so a degraded dependency never restarts the pod.
app.MapHealthChecks("/healthz/downstream", new HealthCheckOptions
{
    Predicate = registration => string.Equals(registration.Name, "downstream", StringComparison.Ordinal),
    ResponseWriter = WriteHealthAsync,
});

app.Run();

static Task WriteHealthAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        services = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Data,
            StringComparer.Ordinal),
    });
}

/// <summary>Exposed so integration tests can host the service in-process.</summary>
public partial class Program;
