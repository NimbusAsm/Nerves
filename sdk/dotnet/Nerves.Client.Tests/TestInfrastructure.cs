using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Nerves.Client.Tests;

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    string? Body,
    IReadOnlyDictionary<string, string> Headers)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    public JsonDocument Json() => JsonDocument.Parse(Body ?? "null");
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public RecordingHandler Respond(HttpStatusCode status, string? json = null)
    {
        _responses.Enqueue(_ => json is null
            ? new HttpResponseMessage(status)
            : new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        return this;
    }

    public RecordingHandler RespondWith(Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        _responses.Enqueue(factory);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body, headers));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No response was queued for " + request.Method + " " + request.RequestUri);
        }

        return _responses.Dequeue()(request);
    }
}

internal static class TestClient
{
    public const string BaseUrl = "https://nerves.test";
    public const string Token = "pat-test-token";

    public static (NervesClient Client, RecordingHandler Handler) Create(int maxRetries = 0)
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NervesClient(new NervesClientOptions
        {
            BaseUrl = new Uri(BaseUrl),
            Token = Token,
            HttpClient = http,
            MaxRetries = maxRetries,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        });
        return (client, handler);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage Problem(HttpStatusCode status, string body, string? traceHeader = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/problem+json"),
        };

        if (traceHeader is not null)
        {
            response.Headers.TryAddWithoutValidation("X-Trace-Id", traceHeader);
        }

        return response;
    }
}

internal static class Json
{
    public static JsonDocument Parse(string? body) => JsonDocument.Parse(body ?? "null");

    public static string String(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? throw new InvalidOperationException(property + " was null");

    public static IEnumerable<string> Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(item => item.GetString()!);
}
