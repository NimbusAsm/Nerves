namespace Nerves.Core;

/// <summary>
/// Why an operation failed. Business failures are values, not exceptions.
/// </summary>
/// <param name="Code">Stable machine-readable code, e.g. <c>account.not_found</c>.</param>
/// <param name="Message">Human-readable and safe to expose to clients.</param>
public sealed record Reason(string Code, string Message)
{
    public static Reason None { get; } = new(string.Empty, string.Empty);

    public static Reason Validation(string message) => new("validation.failed", message);

    public static Reason Conflict(string message) => new("conflict", message);

    public static Reason NotFound(string message) => new("not_found", message);

    public static Reason Forbidden(string message) => new("forbidden", message);
}
