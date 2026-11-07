using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nerves.Idp.Controllers;

/// <summary>Handles the interactive OpenID Connect endpoints.</summary>
[ApiController]
public sealed class AuthorizationController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager) : Controller
{
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString,
                },
                CookieAuthenticationDefaults.AuthenticationScheme);
        }

        if (await applicationManager.FindByClientIdAsync(request.ClientId!) is null)
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        var subject = result.Principal.GetClaim(Claims.Subject);
        if (string.IsNullOrEmpty(subject))
        {
            return Challenge(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        identity.AddClaim(Claims.Subject, subject);
        identity.AddClaim(Claims.Name, result.Principal.GetClaim(Claims.Name) ?? "user");

        var email = result.Principal.GetClaim(Claims.Email);
        if (!string.IsNullOrEmpty(email))
        {
            identity.AddClaim(Claims.Email, email);
        }

        var legalEntityId = result.Principal.GetClaim("leid");
        if (!string.IsNullOrEmpty(legalEntityId))
        {
            identity.AddClaim("leid", legalEntityId);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(await scopeManager.ListResourcesAsync(principal.GetScopes()).ToListAsync());

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

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim) =>
        claim.Type switch
        {
            Claims.Name or Claims.Email or Claims.Role or "leid" =>
                [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Subject =>
                [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken],
        };
}
