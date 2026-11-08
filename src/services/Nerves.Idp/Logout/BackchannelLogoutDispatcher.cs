using Microsoft.Extensions.Options;

namespace Nerves.Idp.Logout;

/// <summary>Configuration of the relying parties to notify on logout.</summary>
public sealed class BackchannelLogoutOptions
{
    public const string SectionName = "BackchannelLogout";

    public List<BackchannelClient> Clients { get; set; } = [];
}

/// <summary>One relying party and its back-channel logout endpoint.</summary>
public sealed class BackchannelClient
{
    public string ClientId { get; set; } = string.Empty;

    public string LogoutUri { get; set; } = string.Empty;
}

/// <summary>Posts signed logout tokens to every configured relying party.</summary>
public sealed class BackchannelLogoutDispatcher(
    LogoutTokenIssuer issuer,
    IOptions<BackchannelLogoutOptions> options,
    IHttpClientFactory httpClientFactory)
{
    public async Task<int> DispatchAsync(string issuerUrl, string subject, CancellationToken cancellationToken)
    {
        var delivered = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var client in options.Value.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.LogoutUri))
            {
                continue;
            }

            var token = issuer.Issue(issuerUrl, client.ClientId, subject, now);
            var http = httpClientFactory.CreateClient("backchannel-logout");

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["logout_token"] = token,
            });

            try
            {
                var response = await http.PostAsync(client.LogoutUri, form, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    delivered++;
                }
            }
            catch (HttpRequestException)
            {
                // A single unresponsive RP must not block the logout.
            }
        }

        return delivered;
    }
}
