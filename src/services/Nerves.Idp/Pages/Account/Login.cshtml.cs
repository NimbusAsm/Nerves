using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Nerves.Idp.Integration;
using Nerves.Idp.Security;

namespace Nerves.Idp.Pages.Account;

/// <summary>Username and password sign-in for the identity provider.</summary>
public sealed class LoginModel(IdentityDirectory directory, SessionDirectory sessions, AuditClient audit, LoginThrottle throttle) : PageModel
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

        var now = DateTimeOffset.UtcNow;
        if (throttle.IsLocked(Username, now))
        {
            Error = "登录失败次数过多，请 15 分钟后再试。";
            return Page();
        }

        var check = await directory.VerifyCredentialAsync(Username, Password, "nerves", cancellationToken);
        if (!check.Verified)
        {
            await audit.AppendAsync(
                "auth.login.failed",
                "anonymous",
                Username,
                string.Empty,
                "failure",
                "{\"reason\":\"invalid_credentials\"}",
                cancellationToken);

            throttle.RecordFailure(Username, now);
            Error = "用户名或口令不正确。";
            return Page();
        }

        throttle.Reset(Username);

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

        await audit.AppendAsync(
            "auth.login.succeeded",
            "account",
            check.AccountId,
            check.AccountId,
            "success",
            null,
            cancellationToken);

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return Redirect("/");
    }
}
