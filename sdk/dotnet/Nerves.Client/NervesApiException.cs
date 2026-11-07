using System.Net;

namespace Nerves.Client;

/// <summary>
/// RFC 9457 <c>application/problem+json</c> body returned by the management API.
/// </summary>
public sealed record NervesProblemDetails
{
    /// <summary>URI reference identifying the problem type.</summary>
    public string? Type { get; init; }

    /// <summary>Short human-readable summary.</summary>
    public string? Title { get; init; }

    /// <summary>HTTP status code repeated in the body.</summary>
    public int? Status { get; init; }

    /// <summary>Human-readable explanation specific to this occurrence.</summary>
    public string? Detail { get; init; }

    /// <summary>URI reference identifying this occurrence.</summary>
    public string? Instance { get; init; }

    /// <summary>Correlation id to quote in support requests.</summary>
    public string? TraceId { get; init; }

    /// <summary>Validation errors keyed by field name.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}

/// <summary>
/// The single exception type raised by the SDK for non-success responses.
/// It always carries the HTTP status, the problem type, and the trace id.
/// </summary>
public sealed class NervesApiException : Exception
{
    /// <summary>Creates an exception from a parsed problem document.</summary>
    public NervesApiException(HttpStatusCode status, NervesProblemDetails problem, string? rawBody = null)
        : base(BuildMessage(status, problem))
    {
        ArgumentNullException.ThrowIfNull(problem);

        Status = (int)status;
        Problem = problem;
        RawBody = rawBody;
    }

    /// <summary>HTTP status code.</summary>
    public int Status { get; }

    /// <summary>Problem type URI, when the server supplied one.</summary>
    public string? Type => Problem.Type;

    /// <summary>Correlation id, when the server supplied one.</summary>
    public string? TraceId => Problem.TraceId;

    /// <summary>Human-readable problem title.</summary>
    public string? Title => Problem.Title;

    /// <summary>Human-readable problem detail.</summary>
    public string? Detail => Problem.Detail;

    /// <summary>Validation errors keyed by field name.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors => Problem.Errors;

    /// <summary>Parsed RFC 9457 document. Never <see langword="null"/>.</summary>
    public NervesProblemDetails Problem { get; }

    /// <summary>Raw response body, useful when the body was not valid ProblemDetails.</summary>
    public string? RawBody { get; }

    private static string BuildMessage(HttpStatusCode status, NervesProblemDetails problem)
    {
        var detail = string.IsNullOrWhiteSpace(problem.Detail) ? problem.Title : problem.Detail;
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"Nerves API request failed with status {(int)status} ({status})."
            : $"Nerves API request failed with status {(int)status} ({status}): {detail}";

        return string.IsNullOrWhiteSpace(problem.TraceId)
            ? message
            : $"{message} (traceId: {problem.TraceId})";
    }
}
