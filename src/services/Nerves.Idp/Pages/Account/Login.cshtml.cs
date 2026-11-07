using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Nerves.Idp.Integration;

namespace Nerves.Idp.Pages.Account;

/// <summary>Username and password sign-in for the identity provider.</summary>
public sealed class LoginModel(IdentityDirectory directory, SessionDirectory sessions) : PageModel
{
    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Error = "请输入用户名与口令。";
            return Page();
        }

        var check = await directory.VerifyCredentialAsync(Username, Password, "nerves", cancellationToken);
        if (!check.Verified)
        {
            Error = "用户名或口令不正确。";
            return Page();
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", check.AccountId),
                new Claim("name", check.DisplayName),
                new Claim("leid", check.LegalEntityId),
                new Claim("email", check.Email),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        var deviceFingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    (HttpContext.Request.Headers.UserAgent.ToString() + "|" + HttpContext.Connection.RemoteIpAddress?.ToString())
                    .ToLowerInvariant()))).ToLowerInvariant();

        await sessions.CreateAsync(
            check.AccountId,
            check.LegalEntityId,
            "nerves",
            deviceFingerprint[..32],
            deviceFingerprint[..16],
            deviceFingerprint[16..32],
            cancellationToken);

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return Redirect("/");
    }
}
