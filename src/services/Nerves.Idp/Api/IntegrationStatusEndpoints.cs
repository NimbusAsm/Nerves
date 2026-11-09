using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Nerves.Idp.Federation;
using OpenIddict.Validation.AspNetCore;

namespace Nerves.Idp.Api;

/// <summary>
/// Read-only view of the configured integrations so operators can confirm what
/// the IdP actually loaded. Secrets are never returned.
/// </summary>
public static class IntegrationStatusEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/integrations", GetAsync);
        return app;
    }

    private static async Task<IResult> GetAsync(
        IOptions<FederationOptions> federation,
        IOptions<LdapOptions> ldap,
        HttpContext http)
    {
        var authentication = await http.AuthenticateAsync(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Results.Unauthorized();
        }

        var permissions = authentication.Principal.FindFirst("permissions")?.Value ?? string.Empty;
        if (!permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("nerves:admin"))
        {
            return Results.Forbid();
        }

        var settings = ldap.Value;

        return Results.Ok(new
        {
            ldap = new
            {
                enabled = settings.Enabled,
                host = settings.Host,
                port = settings.Port,
                useSsl = settings.UseSsl,
                baseDn = settings.BaseDn,
                userFilter = settings.UserFilter,
                mailAttribute = settings.MailAttribute,
                timeoutSeconds = settings.TimeoutSeconds,
                serviceAccount = !string.IsNullOrEmpty(settings.BindDn),
            },
            federation = federation.Value.Providers.Select(provider => new
            {
                provider.Id,
                provider.DisplayName,
                provider.Authority,
                provider.ClientId,
                provider.RedirectUri,
            }),
        });
    }
}
