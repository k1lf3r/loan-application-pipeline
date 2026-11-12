using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Queries;

public static class ApplicationQueryExtensions
{
    public static IEnumerable<ScoredApplication> Scored(
        this IEnumerable<PricedApplication> source, Func<PricedApplication, int> score)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(score);
        return source.Select(priced => new ScoredApplication(priced, score(priced)));
    }

    public static long TotalRequested(this IEnumerable<PricedApplication> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Aggregate(0L, (sum, priced) => sum + priced.Loan.RequestedAmount);
    }
}
