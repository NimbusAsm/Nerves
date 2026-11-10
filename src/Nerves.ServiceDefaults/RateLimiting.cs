using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nerves.ServiceDefaults;

/// <summary>
/// Per-client fixed-window limiting for management APIs. The IdP keeps its own
/// stricter policies for login and token endpoints.
/// </summary>
public static class RateLimiting
{
    public static IServiceCollection AddNervesRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 600);
        if (permitLimit < 1)
        {
            throw new InvalidOperationException("RateLimiting:PermitLimit must be at least 1.");
        }

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    public static IApplicationBuilder UseNervesRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseRateLimiter();
    }
}
