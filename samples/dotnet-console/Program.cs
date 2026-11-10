using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nerves.Client;

namespace Nerves.Samples.DotnetConsole;

/// <summary>
/// Walks the whole Nerves login chain the way a headless client or a CI job would:
///
/// <list type="number">
/// <item>POST /api/v1/auth/login — credentials in, single-use login ticket out.</item>
/// <item>GET /connect/authorize — authorization code + PKCE, no browser needed.</item>
/// <item>POST /connect/token — ID / access / refresh tokens.</item>
/// <item>GET /connect/userinfo — profile claims for the signed-in account.</item>
/// <item>GET /api/v1/* — management API through the gateway, using the .NET SDK.</item>
/// </list>
///
/// Everything is configured through environment variables; see <c>.env.example</c>.
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<int> Main()
    {
        var settings = Settings.FromEnvironment();

        if (string.IsNullOrEmpty(settings.Password))
        {
            Console.Error.WriteLine("NERVES_PASSWORD is not set. Copy .env.example to .env and source it first.");
            return 2;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var noRedirect = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };

        Console.WriteLine("Nerves .NET sample");
        Console.WriteLine($"  IdP      {settings.Idp}");
        Console.WriteLine($"  gateway  {settings.Gateway}");
        Console.WriteLine($"  client   {settings.ClientId}");
        Console.WriteLine($"  redirect {settings.RedirectUri}");
        Console.WriteLine();

        var loginTicket = await LoginAsync(http, settings).ConfigureAwait(false);
        if (loginTicket is null)
        {
            return 1;
        }

        var verifier = NewCodeVerifier();
        var code = await AuthorizeAsync(noRedirect, settings, verifier, loginTicket).ConfigureAwait(false);
        if (code is null)
        {
            return 1;
        }

        var tokens = await ExchangeAsync(http, settings, verifier, code).ConfigureAwait(false);
        if (tokens is null)
        {
            return 1;
        }

        await PrintUserInfoAsync(http, settings, tokens).ConfigureAwait(false);
        await CallManagementApiAsync(settings, tokens).ConfigureAwait(false);

        return 0;
    }

    /// <summary>Step 1: turn credentials into a single-use login ticket.</summary>
    private static async Task<string?> LoginAsync(HttpClient http, Settings settings)
    {
        var payload = new Dictionary<string, string>
        {
            ["username"] = settings.Username,
            ["password"] = settings.Password,
        };

        if (!string.IsNullOrEmpty(settings.Realm))
        {
            payload["realm"] = settings.Realm;
        }

        using var body = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await http.PostAsync(new Uri(new Uri(settings.Idp), "/api/v1/auth/login"), body)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"  login failed: {(int)response.StatusCode} {text}");
            return null;
        }

        var json = JsonNode.Parse(text)!.AsObject();
        if (json["mfaRequired"]?.GetValue<bool>() == true)
        {
            Console.Error.WriteLine("  this account requires MFA; the console sample does not implement the second factor.");
            return null;
        }

        var ticket = json["loginTicket"]?.GetValue<string>();
        Console.WriteLine($"  1. login api        ok (ticket {ticket?.Length ?? 0} chars, single use)");
        return ticket;
    }

    /// <summary>Step 2: run the authorization request with PKCE and read the code off the redirect.</summary>
    private static async Task<string?> AuthorizeAsync(
        HttpClient noRedirect,
        Settings settings,
        string verifier,
        string loginTicket)
    {
        var query = new StringBuilder();
        Append(query, "client_id", settings.ClientId);
        Append(query, "response_type", "code");
        Append(query, "scope", settings.Scopes);
        Append(query, "redirect_uri", settings.RedirectUri);
        Append(query, "state", "dotnet-sample");
        Append(query, "code_challenge", Challenge(verifier));
        Append(query, "code_challenge_method", "S256");
        Append(query, "login_ticket", loginTicket);

        var url = new Uri(new Uri(settings.Idp), "/connect/authorize?" + query.ToString(0, query.Length));
        using var response = await noRedirect.GetAsync(url).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Redirect && response.StatusCode != HttpStatusCode.Found)
        {
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Console.Error.WriteLine($"  authorize failed: {(int)response.StatusCode} {text}");
            return null;
        }

        var location = response.Headers.Location;
        if (location is null)
        {
            Console.Error.WriteLine("  authorize returned no Location header.");
            return null;
        }

        var code = QueryValue(location, "code");
        if (code is null)
        {
            Console.Error.WriteLine($"  authorize redirected without a code: {location}");
            return null;
        }

        Console.WriteLine("  2. oidc authorize   ok (code issued, PKCE S256)");
        return code;
    }

    /// <summary>Step 3: swap the code for tokens.</summary>
    private static async Task<TokenSet?> ExchangeAsync(
        HttpClient http,
        Settings settings,
        string verifier,
        string code)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = settings.RedirectUri,
            ["client_id"] = settings.ClientId,
            ["code_verifier"] = verifier,
        });

        using var response = await http.PostAsync(new Uri(new Uri(settings.Idp), "/connect/token"), form)
            .ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"  token exchange failed: {(int)response.StatusCode} {text}");
            return null;
        }

        var json = JsonNode.Parse(text)!.AsObject();
        var access = json["access_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(access))
        {
            Console.Error.WriteLine("  token response carried no access_token.");
            return null;
        }

        var set = new TokenSet(
            access,
            json["id_token"]?.GetValue<string>(),
            json["refresh_token"]?.GetValue<string>(),
            json["expires_in"]?.GetValue<int>() ?? 0);

        Console.WriteLine(
            $"  3. token endpoint   ok (access {set.Access.Length} chars, refresh {set.Refresh?.Length ?? 0} chars, expires in {set.ExpiresIn}s)");
        return set;
    }

    /// <summary>Step 4: read the profile from userinfo.</summary>
    private static async Task PrintUserInfoAsync(HttpClient http, Settings settings, TokenSet tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(settings.Idp), "/connect/userinfo"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Access);

        using var response = await http.SendAsync(request).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"  userinfo failed: {(int)response.StatusCode} {text}");
            return;
        }

        var json = JsonNode.Parse(text)!.AsObject();
        var sub = json["sub"]?.GetValue<string>();
        var name = json["name"]?.GetValue<string>();
        var leid = json["leid"]?.GetValue<string>();

        Console.WriteLine($"  4. userinfo         ok (sub {sub}, name {name}, leid {leid ?? "none"})");
    }

    /// <summary>Step 5: call the management API with the SDK, not hand-rolled HTTP.</summary>
    private static async Task CallManagementApiAsync(Settings settings, TokenSet tokens)
    {
        using var nerves = new NervesClient(new NervesClientOptions
        {
            BaseUrl = new Uri(settings.Gateway),
            Token = tokens.Access,
        });

        try
        {
            var page = await nerves.Accounts.List().Limit(5).SendAsync().ConfigureAwait(false);
            Console.WriteLine(
                $"  5. management api   ok ({page.Items.Count} account(s) in the first page, cursor {page.NextCursor ?? "none"})");

            foreach (var account in page.Items)
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"       - {account.Username} realm={account.Realm} status={account.Status}"));
            }
        }
        catch (NervesApiException error)
        {
            Console.Error.WriteLine($"  management api failed: {error.Status} {error.Message}");
        }
    }

    private static string NewCodeVerifier() =>
        Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Challenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void Append(StringBuilder query, string key, string value)
    {
        if (query.Length > 0)
        {
            query.Append('&');
        }

        query.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
    }

    private static string? QueryValue(Uri location, string name)
    {
        var query = location.Query;
        if (query.Length <= 1)
        {
            return null;
        }

        foreach (var pair in query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = separator < 0 ? pair : pair[..separator];

            if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.Ordinal))
            {
                continue;
            }

            return separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return null;
    }
}

/// <summary>Configuration for the sample, taken from the environment.</summary>
internal sealed record Settings(
    string Idp,
    string Gateway,
    string ClientId,
    string RedirectUri,
    string Scopes,
    string Username,
    string Password,
    string Realm)
{
    public static Settings FromEnvironment() => new(
        Idp: Env("NERVES_IDP", "http://127.0.0.1:5101"),
        Gateway: Env("NERVES_GATEWAY", "http://127.0.0.1:5100"),
        ClientId: Env("NERVES_CLIENT_ID", "nerves-admin"),
        RedirectUri: Env("NERVES_REDIRECT_URI", "http://127.0.0.1:5300/oidc-callback.html"),
        Scopes: Env("NERVES_SCOPES", "openid profile email nerves.api offline_access"),
        Username: Env("NERVES_USERNAME", "admin"),
        Password: Env("NERVES_PASSWORD", string.Empty),
        Realm: Env("NERVES_REALM", string.Empty));

    private static string Env(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}

/// <summary>The tokens the sample needs; nothing is written to disk.</summary>
internal sealed record TokenSet(string Access, string? Id, string? Refresh, int ExpiresIn);
