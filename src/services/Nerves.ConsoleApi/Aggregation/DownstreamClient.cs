using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Nerves.ConsoleApi.Diagnostics;
using Nerves.ConsoleApi.Options;

namespace Nerves.ConsoleApi.Aggregation;

/// <summary>
/// Calls a downstream service with the caller's bearer token and turns every
/// failure mode (timeout, connection error, non-2xx, malformed JSON) into a
/// warning instead of an exception, so one broken dependency never fails the
/// whole console response.
/// </summary>
public sealed class DownstreamClient(
    IHttpClientFactory factory,
    IHttpContextAccessor accessor,
    ConsoleOptions options) : IDownstreamClient
{
    public async Task<DownstreamResult> GetAsync(
        string service,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var started = Stopwatch.GetTimestamp();

        try
        {
            var client = factory.CreateClient(service);

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authorization)
                && AuthenticationHeaderValue.TryParse(authorization, out var header))
            {
                request.Headers.Authorization = header;
            }

            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var elapsed = Elapsed(started);
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                RecordFailure(service, status, elapsed);
                return DownstreamResult.Failed(
                    status,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{service} {path} answered HTTP {status}"),
                    elapsed);
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));

            return new DownstreamResult(document.RootElement.Clone(), status, null, elapsed);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = Elapsed(started);
            RecordFailure(service, 0, elapsed);
            return DownstreamResult.Failed(
                0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{service} {path} timed out after {options.TimeoutSeconds}s"),
                elapsed);
        }
        catch (HttpRequestException exception)
        {
            var elapsed = Elapsed(started);
            RecordFailure(service, 0, elapsed);
            return DownstreamResult.Failed(
                0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{service} {path} is unreachable: {exception.Message}"),
                elapsed);
        }
        catch (JsonException)
        {
            var elapsed = Elapsed(started);
            RecordFailure(service, 0, elapsed);
            return DownstreamResult.Failed(
                0,
                string.Create(CultureInfo.InvariantCulture, $"{service} {path} returned malformed JSON"),
                elapsed);
        }
    }

    private static long Elapsed(long started) =>
        (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static void RecordFailure(string service, int status, long elapsed)
    {
        ConsoleMetrics.DownstreamFailures.Add(
            1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("status", status));
        ConsoleMetrics.DownstreamDuration.Record(
            elapsed,
            new KeyValuePair<string, object?>("service", service));
    }
}
