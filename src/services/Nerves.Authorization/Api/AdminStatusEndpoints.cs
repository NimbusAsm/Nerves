using Microsoft.Extensions.Options;
using Nerves.Authorization.Policy;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Authorization.Api;

/// <summary>Read-only deployment facts for the admin console.</summary>
public static class AdminStatusEndpoints
{
    public static RouteGroupBuilder MapAdminStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("/pdp", (IOptions<PdpOptions> options) =>
        {
            var pdp = options.Value;

            return Results.Ok(new
            {
                enabled = !string.IsNullOrWhiteSpace(pdp.Url),
                url = pdp.Url,
                timeoutSeconds = pdp.TimeoutSeconds,
                failOpen = pdp.FailOpen,
                tokenConfigured = !string.IsNullOrWhiteSpace(pdp.Token),
            });
        });

        return group;
    }
}
