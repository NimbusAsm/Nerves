using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nerves.Notification.Options;

namespace Nerves.Notification.Consumption;

/// <summary>
/// HTTP implementation of <see cref="IIdentityEventClient"/>. Transport failures
/// and non-success status codes throw, so the consumer keeps its cursor and
/// retries the same batch on the next poll.
/// </summary>
public sealed class IdentityEventClient(
    IHttpClientFactory clients,
    IOptions<EventConsumerOptions> options,
    AccessTokenProvider tokens) : IIdentityEventClient
{
    public async Task<IReadOnlyList<EventEnvelope>> ListEventsAsync(
        DateTimeOffset? after,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"?limit={Math.Clamp(limit, 1, 500)}");

        if (after is not null)
        {
            query += "&after=" + Uri.EscapeDataString(after.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }

        var baseUri = options.Value.IdentityBaseUrl.TrimEnd('/');
        using var document = await GetJsonAsync(baseUri + "/api/v1/events" + query, cancellationToken);

        var events = new List<EventEnvelope>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            events.Add(ParseEvent(item));
        }

        return events;
    }

    public async Task<AccountSnapshot?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var baseUri = options.Value.IdentityBaseUrl.TrimEnd('/');
        using var client = clients.CreateClient(AccessTokenProvider.HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(baseUri + "/api/v1/accounts/" + accountId.ToString("D", CultureInfo.InvariantCulture), UriKind.Absolute));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(cancellationToken));

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"identity account lookup returned {(int)response.StatusCode}"),
                inner: null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        return new AccountSnapshot(
            accountId,
            ReadString(root, "email"),
            ReadString(root, "displayName"),
            ReadString(root, "username") ?? string.Empty);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient(AccessTokenProvider.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Absolute));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(cancellationToken));

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"identity event stream returned {(int)response.StatusCode}"),
                inner: null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static EventEnvelope ParseEvent(JsonElement item) => new(
        item.GetProperty("id").GetGuid(),
        item.GetProperty("type").GetString() ?? string.Empty,
        ReadGuid(item, "accountId"),
        ReadGuid(item, "legalEntityId"),
        ReadString(item, "payload") ?? string.Empty,
        item.GetProperty("occurredAt").GetDateTimeOffset());

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Guid? ReadGuid(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.TryGetGuid(out var parsed)
            ? parsed
            : null;
}
