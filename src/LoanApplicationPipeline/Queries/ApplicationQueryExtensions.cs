using LoanApplicationPipeline.Domain;

namespace LoanApplicationPipeline.Queries;

public static class ApplicationQueryExtensions
{
    /// <summary>
    /// EXTENSION 2 - reusable query operation.
    /// Receiver: IEnumerable&lt;PricedApplication&gt; (the sequence that "owns" scoring in this domain).
    /// Returns: IEnumerable&lt;ScoredApplication&gt;. Lazy: the score function runs only during iteration.
    /// Takes the configured scoring function as a parameter, so no global setting is involved.
    /// </summary>
    public static IEnumerable<ScoredApplication> Scored(
        this IEnumerable<PricedApplication> source, Func<PricedApplication, int> score)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(score);
        return source.Select(p => new ScoredApplication(p, score(p)));
    }

    /// <summary>
    /// EXTENSION 3 - aggregate.
    /// Receiver: IEnumerable&lt;PricedApplication&gt;. Returns: total requested amount in tenge (0 for empty).
    /// Uses Aggregate; reads only, changes nothing.
    /// </summary>
    public static long TotalRequested(this IEnumerable<PricedApplication> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Aggregate(0L, (sum, p) => sum + p.Application.RequestedAmount);
    }
}
