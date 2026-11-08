using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Nerves.Authorization.Policy;

/// <summary>Calls the external policy decision point when one is configured.</summary>
public sealed class PdpClient(IOptions<PdpOptions> options, IHttpClientFactory httpClientFactory)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(options.Value.Url);

    public sealed record Decision(bool Allowed, string? Reason, bool FromPdp);

    public async Task<Decision?> DecideAsync(
        string accountId,
        string legalEntityId,
        string permission,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!Enabled)
        {
            return null;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, settings.Url)
        {
            Content = JsonContent.Create(new
            {
                subject = accountId,
                legalEntityId,
                permission,
            }),
        };

        if (!string.IsNullOrWhiteSpace(settings.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        }

        try
        {
            var client = httpClientFactory.CreateClient("pdp");
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return settings.FailOpen ? new Decision(true, "pdp_error_fail_open", true) : null;
            }

            var body = await response.Content.ReadFromJsonAsync<PdpResponse>(cancellationToken);
            return new Decision(body?.Allowed ?? false, body?.Reason, true);
        }
        catch (HttpRequestException) when (settings.FailOpen)
        {
            return new Decision(true, "pdp_unreachable_fail_open", true);
        }
        catch (TaskCanceledException) when (settings.FailOpen)
        {
            return new Decision(true, "pdp_timeout_fail_open", true);
        }
    }

    private sealed record PdpResponse(bool Allowed, string? Reason);
}
