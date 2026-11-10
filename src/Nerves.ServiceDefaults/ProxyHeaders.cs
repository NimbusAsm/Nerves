using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nerves.ServiceDefaults;

/// <summary>
/// Honors X-Forwarded-Proto/For from the TLS-terminating proxy. Only loopback
/// proxies are trusted by default; a deployment behind a private load balancer
/// can opt into trusting all proxies with Proxy:TrustAll=true.
/// </summary>
public static class ProxyHeaders
{
    public static IServiceCollection AddNervesProxyHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            var trusted = configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [];
            foreach (var value in trusted)
            {
                if (IPAddress.TryParse(value, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            if (configuration.GetValue("Proxy:TrustAll", false))
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            }
        });

        return services;
    }

    public static IApplicationBuilder UseNervesProxyHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseForwardedHeaders();
    }
}
