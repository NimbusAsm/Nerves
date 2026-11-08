using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nerves.Idp.Api;
using Nerves.Idp.Integration;
using Nerves.Idp.Security;

namespace Nerves.Idp.Federation;

/// <summary>
/// Upstream OpenID Connect login. The client starts the flow, the IdP
/// redirects to the upstream provider, and the callback verifies the upstream
/// ID token before issuing a normal Nerves login ticket.
/// </summary>
public static class FederationEndpoints
{
    private static readonly HttpClient UpstreamHttp = new();

    private static readonly ConcurrentDictionary<string, (JsonWebKeySet Keys, string Issuer, DateTimeOffset FetchedAt)> JwksCache = new(StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapFederationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth/federation");
        group.MapPost("/start", StartAsync).RequireRateLimiting("login");
        group.MapGet("/callback", CallbackAsync);
        group.MapGet("/providers", Providers);
        group.MapPost("/ldap", LdapLoginAsync).RequireRateLimiting("login");
        return app;
    }

    private static async Task<IResult> LdapLoginAsync(
        [FromBody] LdapLoginRequest request,
        LdapDirectory ldap,
        IdentityDirectory directory,
        LoginTickets tickets,
        SessionDirectory sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem("username and password are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var identity = await ldap.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        if (identity is null)
        {
            await audit.AppendAsync(
                "auth.federation.ldap.failed",
                "anonymous",
                request.Username,
                string.Empty,
                "failure",
                "{\"reason\":\"ldap_rejected\"}",
                cancellationToken);

            return Results.Json(
                new { error = "invalid_credentials", message = "Invalid username or password." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var realm = string.IsNullOrWhiteSpace(request.Realm) ? "nerves" : request.Realm;
        var account = !string.IsNullOrWhiteSpace(identity.Mail)
            ? await directory.ResolveIdentifierAsync("email", realm, identity.Mail!, cancellationToken)
            : await directory.ResolveIdentifierAsync("username", realm, request.Username, cancellationToken);

        if (account is null)
        {
            await audit.AppendAsync(
                "auth.federation.ldap.unlinked",
                "anonymous",
                identity.DistinguishedName,
                string.Empty,
                "failure",
                "{\"reason\":\"no_linked_account\"}",
                cancellationToken);

            return Results.Problem(
                "No Nerves account is linked to this directory entry.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(
                (http.Request.Headers.UserAgent.ToString() + "|" + http.Connection.RemoteIpAddress).ToLowerInvariant())))
            .ToLowerInvariant();

        await sessions.CreateAsync(
            account.AccountId,
            account.LegalEntityId,
            account.Realm,
            fingerprint[..32],
            fingerprint[..16],
            fingerprint[16..32],
            ["ldap"],
            cancellationToken);

        await audit.AppendAsync(
            "auth.federation.ldap.succeeded",
            "account",
            account.AccountId,
            account.AccountId,
            "success",
            null,
            cancellationToken);

        var loginTicket = await tickets.IssueAsync(
            account.Realm,
            Guid.Parse(account.AccountId),
            Guid.TryParse(account.LegalEntityId, out var entityId) ? entityId : null,
            account.DisplayName,
            account.Email,
            "login",
            cancellationToken);

        return Results.Ok(new
        {
            loginTicket,
            expiresInSeconds = (int)LoginTickets.Lifetime.TotalSeconds,
            user = new
            {
                id = account.AccountId,
                name = account.DisplayName,
                email = account.Email,
                legalEntityId = account.LegalEntityId,
            },
        });
    }

    private static IResult Providers(IOptions<FederationOptions> options) =>
        Results.Ok(options.Value.Providers.Select(p => new { p.Id, p.DisplayName, p.AllowSignUp }));

    private static async Task<IResult> StartAsync(
        [FromBody] StartFederationRequest request,
        IOptions<FederationOptions> options,
        FederationStateStore states,
        CancellationToken cancellationToken)
    {
        var provider = options.Value.Providers.FirstOrDefault(p => p.Id == request.Provider);
        if (provider is null)
        {
            return Results.NotFound();
        }

        var state = await states.CreateAsync(provider.Id, request.ReturnUrl, cancellationToken);

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));

        var authorize = provider.Authority.TrimEnd('/') + "/connect/authorize?"
            + "response_type=code"
            + "&client_id=" + Uri.EscapeDataString(provider.ClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(provider.RedirectUri)
            + "&scope=" + Uri.EscapeDataString(provider.Scope)
            + "&state=" + state.Id
            + "&code_challenge=" + challenge
            + "&code_challenge_method=S256";

        return Results.Ok(new
        {
            state = state.Id,
            verifier,
            authorizeUrl = authorize,
        });
    }

    private static async Task<IResult> CallbackAsync(
        string? code,
        string? state,
        string? error,
        FederationStateStore states,
        IOptions<FederationOptions> options,
        IdentityDirectory directory,
        LoginTickets tickets,
        SessionDirectory sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return Results.Problem(
                "The upstream provider rejected the login: " + error,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var pending = await states.ConsumeAsync(state ?? string.Empty, cancellationToken);
        if (pending is null || string.IsNullOrEmpty(code))
        {
            return Results.Problem("Unknown or expired federation state.", statusCode: StatusCodes.Status400BadRequest);
        }

        var provider = options.Value.Providers.FirstOrDefault(p => p.Id == pending.Provider);
        if (provider is null)
        {
            return Results.NotFound();
        }

        var verifier = http.Request.Query["code_verifier"].ToString();
        if (string.IsNullOrEmpty(verifier))
        {
            return Results.Problem("code_verifier is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var tokens = await ExchangeAsync(provider, code, verifier, cancellationToken);
        if (tokens is null)
        {
            return Results.Problem("Upstream token exchange failed.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var claims = tokens!;
        var subject = claims.TryGetValue("sub", out var sub) ? sub?.ToString() : null;
        if (string.IsNullOrEmpty(subject))
        {
            return Results.Problem("The upstream token has no subject.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var email = claims.TryGetValue("email", out var emailValue) ? emailValue?.ToString() : null;
        var name = claims.TryGetValue("name", out var nameValue) ? nameValue?.ToString() : null;

        var account = !string.IsNullOrWhiteSpace(email)
            ? await directory.ResolveIdentifierAsync("email", provider.Id == "upstream" ? "nerves" : "nerves", email!, cancellationToken)
            : null;

        if (account is null)
        {
            await audit.AppendAsync(
                "auth.federation.failed",
                "anonymous",
                subject,
                string.Empty,
                "failure",
                "{\"reason\":\"no_linked_account\"}",
                cancellationToken);

            return Results.Problem(
                "No Nerves account is linked to this upstream identity.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(
                (http.Request.Headers.UserAgent.ToString() + "|" + http.Connection.RemoteIpAddress).ToLowerInvariant())))
            .ToLowerInvariant();

        await sessions.CreateAsync(
            account.AccountId,
            account.LegalEntityId,
            account.Realm,
            fingerprint[..32],
            fingerprint[..16],
            fingerprint[16..32],
            ["federated"],
            cancellationToken);

        await audit.AppendAsync(
            "auth.federation.succeeded",
            "account",
            account.AccountId,
            account.AccountId,
            "success",
            "{\"provider\":\"" + provider.Id + "\"}",
            cancellationToken);

        var loginTicket = await tickets.IssueAsync(
            account.Realm,
            Guid.Parse(account.AccountId),
            Guid.TryParse(account.LegalEntityId, out var entityId) ? entityId : null,
            string.IsNullOrWhiteSpace(account.DisplayName) ? (name ?? email ?? subject) : account.DisplayName,
            account.Email,
            "login",
            cancellationToken);

        return Results.Ok(new
        {
            loginTicket,
            expiresInSeconds = (int)LoginTickets.Lifetime.TotalSeconds,
            user = new
            {
                id = account.AccountId,
                name = account.DisplayName,
                email = account.Email,
                legalEntityId = account.LegalEntityId,
            },
        });
    }

    private static async Task<Dictionary<string, object>?> ExchangeAsync(
        FederationProvider provider,
        string code,
        string verifier,
        CancellationToken cancellationToken)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = provider.RedirectUri,
            ["client_id"] = provider.ClientId,
            ["code_verifier"] = verifier,
        });

        using var response = await UpstreamHttp.PostAsync(
            provider.Authority.TrimEnd('/') + "/connect/token",
            form,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        if (!payload.TryGetProperty("id_token", out var idToken))
        {
            return null;
        }

        var (keys, issuer) = await GetUpstreamKeysAsync(provider, cancellationToken);
        if (keys is null)
        {
            return null;
        }

        var validation = await new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler()
            .ValidateTokenAsync(idToken.GetString()!, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKeys = keys.GetSigningKeys(),
            });

        if (!validation.IsValid)
        {
            return null;
        }

        // The audience must address this client, never another relying party.
        if (!validation.Claims.TryGetValue("aud", out var audience)
            || audience?.ToString() != provider.ClientId)
        {
            return null;
        }

        return (Dictionary<string, object>?)validation.Claims;
    }

    private static async Task<(JsonWebKeySet? Keys, string Issuer)> GetUpstreamKeysAsync(
        FederationProvider provider,
        CancellationToken cancellationToken)
    {
        if (JwksCache.TryGetValue(provider.Authority, out var cached)
            && DateTimeOffset.UtcNow - cached.FetchedAt < TimeSpan.FromMinutes(10))
        {
            return (cached.Keys, cached.Issuer);
        }

        try
        {
            var discoveryJson = await UpstreamHttp.GetStringAsync(
                provider.Authority.TrimEnd('/') + "/.well-known/openid-configuration",
                cancellationToken);
            var discovery = JsonDocument.Parse(discoveryJson).RootElement;

            var issuer = discovery.GetProperty("issuer").GetString() ?? provider.Authority;
            var jwksUri = discovery.GetProperty("jwks_uri").GetString()
                ?? provider.Authority.TrimEnd('/') + "/.well-known/jwks";

            var keys = new JsonWebKeySet(await UpstreamHttp.GetStringAsync(jwksUri, cancellationToken));
            JwksCache[provider.Authority] = (keys, issuer, DateTimeOffset.UtcNow);
            return (keys, issuer);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException)
        {
            return (null, provider.Authority);
        }
    }

    private static string Base64Url(byte[] bytes) => System.Buffers.Text.Base64Url.EncodeToString(bytes);
}

/// <summary>LDAP login payload.</summary>
public sealed record LdapLoginRequest(string Username, string Password, string? Realm);

/// <summary>Federation start payload.</summary>
public sealed record StartFederationRequest(string Provider, string? ReturnUrl);
