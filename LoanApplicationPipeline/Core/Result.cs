namespace LoanApplicationPipeline.Core;

public abstract record Result<T, E>
{
    private Result() { }

    public sealed record Ok(T Value) : Result<T, E>;
    public sealed record Err(E Error) : Result<T, E>;

    public bool IsSuccess => this is Ok;
    public bool IsFailure => this is Err;

    public static Result<T, E> Success(T value) => new Ok(value);
    public static Result<T, E> Failure(E error) => new Err(error);

    public Result<U, E> Map<U>(Func<T, U> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return this switch
        {
            Ok ok => Result<U, E>.Success(mapper(ok.Value)),
            Err err => Result<U, E>.Failure(err.Error),
            _ => throw new InvalidOperationException()
        };
    }

    public Result<U, E> Bind<U>(Func<T, Result<U, E>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return this switch
        {
            Ok ok => binder(ok.Value),
            Err err => Result<U, E>.Failure(err.Error),
            _ => throw new InvalidOperationException()
        };
    }

    public Result<T, F> MapError<F>(Func<E, F> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return this switch
        {
            Ok ok => Result<T, F>.Success(ok.Value),
            Err err => Result<T, F>.Failure(mapper(err.Error)),
            _ => throw new InvalidOperationException()
        };
    }

    public R Match<R>(Func<T, R> onSuccess, Func<E, R> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return this switch
        {
            Ok ok => onSuccess(ok.Value),
            Err err => onFailure(err.Error),
            _ => throw new InvalidOperationException()
        };
    }
}

public static class Res
{
    public static Result<T, DomainError> Ok<T>(T value) => Result<T, DomainError>.Success(value);

    public static Result<T, DomainError> Fail<T>(DomainError error) => Result<T, DomainError>.Failure(error);
}
