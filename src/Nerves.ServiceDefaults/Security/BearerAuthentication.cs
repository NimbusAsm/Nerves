using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Nerves.ServiceDefaults.Security;

/// <summary>
/// Bearer-token authentication for management APIs: tokens are issued by the
/// IdP and validated locally against its JWKS. No shared static secret.
/// </summary>
public static class BearerAuthentication
{
    /// <summary>Policy that requires the admin permission in the token's permission snapshot.</summary>
    public const string AdminPolicy = "nerves-admin";

    /// <summary>Permission code that grants access to management APIs.</summary>
    public const string AdminPermission = "nerves:admin";

    public static IServiceCollection AddNervesBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var authority = configuration["Authentication:Authority"] ?? "http://127.0.0.1:5101";
        var audience = configuration["Authentication:Audience"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = false;
                options.Audience = audience;
                options.RefreshOnIssuerKeyNotFound = true;
                options.RefreshInterval = TimeSpan.FromMinutes(1);
                options.AutomaticRefreshInterval = TimeSpan.FromMinutes(5);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    NameClaimType = "sub",
                };
            });

        services.AddAuthorization(options => options.AddPolicy(AdminPolicy, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
            {
                var permissions = context.User.FindFirst("permissions")?.Value;
                return !string.IsNullOrEmpty(permissions)
                       && permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                           .Contains(AdminPermission, StringComparer.Ordinal);
            })));

        return services;
    }

    /// <summary>Adds conservative security headers to every response.</summary>
    public static IApplicationBuilder UseNervesSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            headers["Cross-Origin-Resource-Policy"] = "same-site";
            return next(context);
        });
    }
}
