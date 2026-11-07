using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nerves.ServiceDefaults;

/// <summary>
/// Shared wiring for every Nerves service: health endpoints, problem details,
/// and OpenTelemetry. Keeping it in one place means a new service is ~10 lines.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Registers the common service behaviour.</summary>
    public static IHostApplicationBuilder AddNervesDefaults(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks();

        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        var otel = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        otel.WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());

        otel.WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otel.WithTracing(tracing => tracing.AddOtlpExporter());
            otel.WithMetrics(metrics => metrics.AddOtlpExporter());
        }

        return builder;
    }

    /// <summary>Maps the endpoints every service exposes.</summary>
    public static WebApplication MapNervesDefaults(this WebApplication app, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var version = typeof(ServiceDefaultsExtensions).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/readyz", () => Results.Ok(new { status = "ready" }));

        app.MapGet("/api/v1/meta", () => Results.Ok(new
        {
            service = serviceName,
            version,
            contract = "v1",
        }));

        return app;
    }
}
