namespace LoanApplicationPipeline.Core;

public abstract record Option<T>
{
    private Option() { }

    public sealed record Some(T Value) : Option<T>;
    public sealed record None : Option<T>;

    public bool IsSome => this is Some;
    public bool IsNone => this is None;

    public Result<T, E> ToResult<E>(E errorIfNone) => this switch
    {
        Some s => Result<T, E>.Success(s.Value),
        None => Result<T, E>.Failure(errorIfNone),
        _ => throw new InvalidOperationException()
    };

    public Option<U> Map<U>(Func<T, U> mapper) => this switch
    {
        Some s => Option<U>.From(mapper(s.Value)),
        None => new Option<U>.None(),
        _ => throw new InvalidOperationException()
    };

    public static Option<T> From(T? value) =>
        value is null ? new None() : new Some(value);
}

public static class Option
{
    public static Option<T> Some<T>(T value) => new Option<T>.Some(value);
    public static Option<T> None<T>() => new Option<T>.None();
}