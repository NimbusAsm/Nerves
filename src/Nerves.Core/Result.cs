namespace Nerves.Core;

/// <summary>
/// Outcome of an operation that can fail for expected reasons.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Reason reason)
    {
        IsSuccess = isSuccess;
        Reason = reason;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Reason Reason { get; }

    public static Result Success() => new(true, Reason.None);

    public static Result Failure(Reason reason) => new(false, reason);

    public static Result<T> Success<T>(T value) => new(value, true, Reason.None);

    public static Result<T> Failure<T>(Reason reason) => new(default, false, reason);
}

/// <summary>
/// Outcome carrying a value on success.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Reason reason)
        : base(isSuccess, reason) => _value = value;

    /// <exception cref="InvalidOperationException">Thrown when the result is a failure.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");
}
