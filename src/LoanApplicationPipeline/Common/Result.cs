namespace LoanApplicationPipeline.Common;

/// <summary>Explicit, typed failure: a stable machine-readable code plus a human message.</summary>
public readonly record struct Error(string Code, string Message);

/// <summary>
/// Success-or-failure value (same idea as Assignment 7). If your project already has a Result type,
/// keep yours and only adjust the type names used in the new files.
/// </summary>
public abstract record Result<T>
{
    private Result() { }

    public sealed record Ok(T Value) : Result<T>;

    public sealed record Fail(Error Error) : Result<T>;

    public static Result<T> Success(T value) => new Ok(value);

    public static Result<T> Failure(Error error) => new Fail(error);

    public static Result<T> Failure(string code, string message) => new Fail(new Error(code, message));
}
