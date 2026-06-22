namespace SalesCom.Domain.Common;

/// <summary>
/// Result of an operation that either succeeds or fails with an <see cref="ErrorBase"/>.
/// Exceptions are reserved for unrecoverable infrastructure faults; business-rule violations
/// surface here. Use <see cref="Result{TValue}"/> when a value is produced on success.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, ErrorBase error)
    {
        if (isSuccess && error != ErrorBase.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == ErrorBase.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public ErrorBase Error { get; }

    public static Result Success() => new(true, ErrorBase.None);

    public static Result Failure(ErrorBase error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, ErrorBase.None);

    public static Result<TValue> Failure<TValue>(ErrorBase error) => new(default, false, error);

    public static implicit operator Result(ErrorBase error) => Failure(error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, ErrorBase error) : base(isSuccess, error) => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(ErrorBase error) => Failure<TValue>(error);
}
