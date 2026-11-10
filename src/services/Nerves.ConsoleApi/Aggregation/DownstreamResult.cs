using System.Text.Json;

namespace Nerves.ConsoleApi.Aggregation;

/// <summary>
/// Outcome of one downstream call. Failures are values, not exceptions: the
/// console answers 200 with <c>warnings</c> when a dependency is unavailable.
/// </summary>
public sealed record DownstreamResult(JsonElement? Body, int StatusCode, string? Warning, long ElapsedMilliseconds)
{
    /// <summary>True when the call returned 2xx and produced a JSON body.</summary>
    public bool IsSuccess => Warning is null && StatusCode is >= 200 and < 300;

    /// <summary>True when the downstream explicitly said the resource does not exist.</summary>
    public bool IsNotFound => StatusCode == 404;

    public static DownstreamResult Failed(int statusCode, string warning, long elapsedMilliseconds) =>
        new(null, statusCode, warning, elapsedMilliseconds);
}

/// <summary>Read-only HTTP access to the services behind the console.</summary>
public interface IDownstreamClient
{
    /// <summary>Calls <c>GET {path}</c> on <paramref name="service"/> and never throws for a bad answer.</summary>
    Task<DownstreamResult> GetAsync(string service, string path, CancellationToken cancellationToken);
}
