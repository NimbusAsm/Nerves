using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nerves.Client;

/// <summary>Internal HTTP transport shared by every builder.</summary>
internal sealed class ApiRequester
{
    private static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private readonly HttpClient _http;
    private readonly string _root;
    private readonly string? _token;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryBaseDelay;

    public ApiRequester(HttpClient http, NervesClientOptions options)
    {
        _http = http;
        _root = options.BaseUrl.AbsoluteUri.TrimEnd('/') + "/api/v1";
        _token = string.IsNullOrWhiteSpace(options.Token) ? null : options.Token;
        _maxRetries = options.MaxRetries;
        _retryBaseDelay = options.RetryBaseDelay;
    }

    public async Task<T> PostAsync<T>(string path, object body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(body, Json);
        var key = idempotencyKey ?? Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);

        return await SendAndReadAsync<T>(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(path))
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                ApplyHeaders(request, key, null);
                return request;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> GetAsync<T>(string path, string? query, CancellationToken cancellationToken)
    {
        var uri = BuildUri(path) + query;

        return await SendAndReadAsync<T>(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                ApplyHeaders(request, null, null);
                return request;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<NervesPage<T>> GetPageAsync<T>(string path, string? query, CancellationToken cancellationToken)
    {
        var envelope = await GetAsync<PageEnvelope<T>>(path, query, cancellationToken).ConfigureAwait(false);
        return new NervesPage<T>(envelope.Items ?? [], envelope.NextCursor);
    }

    public async Task<T> PatchAsync<T>(string path, object body, string? ifMatch, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(body, Json);

        return await SendAndReadAsync<T>(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, BuildUri(path))
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                ApplyHeaders(request, null, ifMatch);
                return request;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string path, string? ifMatch, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, BuildUri(path));
                ApplyHeaders(request, null, ifMatch);
                return request;
            },
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SendAndReadAsync<T>(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(requestFactory, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new NervesApiException(
                response.StatusCode,
                new NervesProblemDetails
                {
                    Status = (int)response.StatusCode,
                    Title = "The API returned an empty body where a document was expected.",
                });
        }

        var value = JsonSerializer.Deserialize<T>(body, Json);

        if (value is null)
        {
            throw new NervesApiException(
                response.StatusCode,
                new NervesProblemDetails
                {
                    Status = (int)response.StatusCode,
                    Title = "The API returned a JSON null where a document was expected.",
                },
                body);
        }

        return value;
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage response;
            try
            {
                response = await _http
                    .SendAsync(requestFactory(), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                await Task.Delay(GetDelay(attempt, null), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (attempt < _maxRetries && IsRetryable(response.StatusCode))
            {
                var delay = GetDelay(attempt, response.Headers.RetryAfter?.Delta);
                response.Dispose();
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return response;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw await CreateExceptionAsync(response, body).ConfigureAwait(false);
    }

    private static Task<NervesApiException> CreateExceptionAsync(HttpResponseMessage response, string body)
    {
        NervesProblemDetails? problem = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                problem = JsonSerializer.Deserialize<NervesProblemDetails>(body, Json);
            }
            catch (JsonException)
            {
                // Non-JSON error bodies (a proxy, a load balancer) still become NervesApiException.
            }
        }

        var traceId = problem?.TraceId;
        if (string.IsNullOrWhiteSpace(traceId)
            && response.Headers.TryGetValues("X-Trace-Id", out var traceValues))
        {
            traceId = traceValues.FirstOrDefault();
        }

        problem ??= new NervesProblemDetails
        {
            Status = (int)response.StatusCode,
            Title = response.ReasonPhrase,
        };

        if (string.IsNullOrWhiteSpace(problem.TraceId) && !string.IsNullOrWhiteSpace(traceId))
        {
            problem = problem with { TraceId = traceId };
        }

        return Task.FromResult(new NervesApiException(response.StatusCode, problem, body));
    }

    private static bool IsRetryable(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private TimeSpan GetDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { } requested && requested > TimeSpan.Zero)
        {
            return requested;
        }

        var exponential = _retryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt);
        var jitter = Random.Shared.NextDouble() * _retryBaseDelay.TotalMilliseconds;
        return TimeSpan.FromMilliseconds(exponential + jitter);
    }

    private string BuildUri(string path) => _root + "/" + path.TrimStart('/');

    private void ApplyHeaders(HttpRequestMessage request, string? idempotencyKey, string? ifMatch)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
    }

    private sealed record PageEnvelope<T>(IReadOnlyList<T>? Items, string? NextCursor);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

/// <summary>Shared query-string encoding.</summary>
internal static class QueryString
{
    public static string Build(IEnumerable<(string Key, string? Value)> parameters)
    {
        var builder = new StringBuilder();

        foreach (var (key, value) in parameters)
        {
            if (value is null)
            {
                continue;
            }

            builder.Append(builder.Length == 0 ? '?' : '&');
            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
        }

        return builder.ToString();
    }
}
