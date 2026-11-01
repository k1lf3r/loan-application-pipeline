namespace LoanApplicationPipeline.Common;

/// <summary>EXTENSION 1 - composes Result values.</summary>
public static class ResultExtensions
{
    /// <summary>
    /// Receiver: Result&lt;TIn&gt;. Returns: Result&lt;TOut&gt;.
    /// Success: runs <paramref name="next"/> with the value. Failure: returns the same error and
    /// never calls <paramref name="next"/>. Does not change any state.
    /// </summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);

        return result switch
        {
            Result<TIn>.Ok ok => next(ok.Value),
            Result<TIn>.Fail fail => Result<TOut>.Failure(fail.Error),
            _ => throw new InvalidOperationException("Unknown Result case.")
        };
    }
}
