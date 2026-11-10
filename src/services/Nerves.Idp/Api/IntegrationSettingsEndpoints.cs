using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nerves.Idp.Federation;
using Nerves.Idp.Integration;
using OpenIddict.Validation.AspNetCore;

namespace Nerves.Idp.Api;

/// <summary>
/// Runtime update of the LDAP integration. The change lives in memory until the
/// process restarts, so operators can validate a configuration without editing
/// files; governance is preserved by the audit event.
/// </summary>
public static class IntegrationSettingsEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/v1/admin/integrations/ldap", PatchLdapAsync);
        return app;
    }

    private static async Task<IResult> PatchLdapAsync(
        [FromBody] LdapSettingsPatch patch,
        IOptionsMonitor<LdapOptions> options,
        IntegrationSettingsStore store,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var authentication = await http.AuthenticateAsync(
            OpenIddictValidationAspCoreDefaults());

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Results.Unauthorized();
        }

        var permissions = authentication.Principal.FindFirst("permissions")?.Value ?? string.Empty;
        if (!permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("nerves:admin"))
        {
            return Results.Forbid();
        }

        var current = options.CurrentValue;

        if (patch.Host is not null) current.Host = patch.Host.Trim();
        if (patch.Port is > 0 and <= 65535) current.Port = patch.Port.Value;
        if (patch.Enabled is not null) current.Enabled = patch.Enabled.Value;
        if (patch.UseSsl is not null) current.UseSsl = patch.UseSsl.Value;
        if (patch.BaseDn is not null) current.BaseDn = patch.BaseDn.Trim();
        if (patch.UserFilter is not null) current.UserFilter = patch.UserFilter.Trim();
        if (patch.TimeoutSeconds is > 0 and <= 60) current.TimeoutSeconds = patch.TimeoutSeconds.Value;

        await store.SaveLdapAsync(current, cancellationToken);

        await audit.AppendAsync(
            "admin.integration.ldap.updated",
            "admin",
            "integration",
            "ldap",
            "success",
            "{\"enabled\":" + (current.Enabled ? "true" : "false") + "}",
            cancellationToken);

        return Results.Ok(new
        {
            enabled = current.Enabled,
            host = current.Host,
            port = current.Port,
            useSsl = current.UseSsl,
            baseDn = current.BaseDn,
            userFilter = current.UserFilter,
            timeoutSeconds = current.TimeoutSeconds,
            note = "The change is persisted and applies after a restart.",
        });
    }

    private static string OpenIddictValidationAspCoreDefaults() =>
        OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
}

/// <summary>Partial LDAP update; omitted fields stay unchanged.</summary>
public sealed record LdapSettingsPatch(
    bool? Enabled,
    string? Host,
    int? Port,
    bool? UseSsl,
    string? BaseDn,
    string? UserFilter,
    int? TimeoutSeconds);
