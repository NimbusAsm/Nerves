using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nerves.Idp.Controllers;

/// <summary>Handles the interactive OpenID Connect endpoints.</summary>
[ApiController]
public sealed class AuthorizationController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    Nerves.Idp.Integration.AuthorizationDirectory authorization,
    Nerves.Idp.Integration.SessionDirectory sessions,
    Nerves.Idp.Security.LoginTickets tickets) : Controller
{
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (await applicationManager.FindByClientIdAsync(request.ClientId!) is null)
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var userPrincipal = result.Succeeded ? result.Principal : null;
        var realm = result.Principal?.GetClaim("realm") ?? "nerves";

        if (userPrincipal is null)
        {
            // Headless flow: the client's own login UI obtained a single-use
            // ticket from POST /api/v1/auth/login and passed it here.
            var rawTicket = Request.Query["login_ticket"].ToString();
            var ticket = await tickets.RedeemAsync(rawTicket, "login", HttpContext.RequestAborted);
            if (ticket is null)
            {
                return Unauthorized(new
                {
                    error = "login_required",
                    message = "Sign in through /api/v1/auth/login and retry the authorization request.",
                });
            }

            realm = ticket.Realm;

            var cookieIdentity = new ClaimsIdentity(
                CookieAuthenticationDefaults.AuthenticationScheme,
                Claims.Name,
                Claims.Role);

            cookieIdentity.AddClaim(new Claim(Claims.Subject, ticket.AccountId.ToString()));
            cookieIdentity.AddClaim(new Claim(Claims.Name, ticket.DisplayName));
            if (!string.IsNullOrEmpty(ticket.Email))
            {
                cookieIdentity.AddClaim(new Claim(Claims.Email, ticket.Email!));
            }

            if (ticket.LegalEntityId is not null)
            {
                cookieIdentity.AddClaim(new Claim("leid", ticket.LegalEntityId.Value.ToString()));
            }

            cookieIdentity.AddClaim(new Claim("realm", ticket.Realm));

            userPrincipal = new ClaimsPrincipal(cookieIdentity);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                userPrincipal,
                new AuthenticationProperties { IsPersistent = false });
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        var subject = userPrincipal.GetClaim(Claims.Subject);
        if (string.IsNullOrEmpty(subject))
        {
            return Unauthorized(new { error = "login_required" });
        }

        identity.AddClaim(Claims.Subject, subject);
        identity.AddClaim(Claims.Name, userPrincipal.GetClaim(Claims.Name) ?? "user");

        var email = userPrincipal.GetClaim(Claims.Email);
        if (!string.IsNullOrEmpty(email))
        {
            identity.AddClaim(Claims.Email, email);
        }

        var legalEntityId = userPrincipal.GetClaim("leid");
        if (!string.IsNullOrEmpty(legalEntityId))
        {
            identity.AddClaim("leid", legalEntityId);
        }

        var permissions = await authorization.GetPermissionsAsync(
            subject,
            legalEntityId ?? string.Empty,
            realm,
            HttpContext.RequestAborted);

        if (permissions.Permissions.Length > 0)
        {
            identity.AddClaim(new Claim("permissions", string.Join(' ', permissions.Permissions)));
        }

        identity.AddClaim(new Claim("authz_ver", permissions.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        var resources = await scopeManager.ListResourcesAsync(principal.GetScopes()).ToListAsync();
        resources.Add("nerves.api");
        principal.SetResources(resources);

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return BadRequest(new OpenIddictResponse
            {
                Error = Errors.UnsupportedGrantType,
                ErrorDescription = "Only authorization code and refresh token grants are supported.",
            });
        }

        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var principal = result.Principal;

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    [Produces("application/json")]
    public async Task<IActionResult> Userinfo()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Challenge(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        }

        var principal = result.Principal;
        return Ok(new
        {
            sub = principal.GetClaim(Claims.Subject),
            name = principal.GetClaim(Claims.Name),
            email = principal.GetClaim(Claims.Email),
            leid = principal.GetClaim("leid"),
            permissions = principal.GetClaim("permissions"),
        });
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        var authenticated = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var subject = authenticated.Principal?.GetClaim("sub");
        if (!string.IsNullOrEmpty(subject))
        {
            await sessions.RevokeAllAsync(subject, "user signed out", HttpContext.RequestAborted);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim) =>
        claim.Type switch
        {
            Claims.Name or Claims.Email or Claims.Role or "leid" =>
                [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Subject or "permissions" or "authz_ver" =>
                [Destinations.AccessToken],
            _ => [Destinations.AccessToken],
        };
}
