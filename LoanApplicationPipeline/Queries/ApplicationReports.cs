using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Queries;

public sealed record ReviewRow(string Id, string Name, int BurdenPercent, int Score);

public sealed record PurposeTotalRow(LoanPurpose Purpose, int Count, long TotalAmount, long TotalMonthlyPayment);

public static class ApplicationReports
{
    public static IReadOnlyList<ReviewRow> ReviewQueue(
        IEnumerable<PricedApplication> source, Func<PricedApplication, int> score, int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Scored(score)
            .Where(s => s.Score < threshold)
            .OrderBy(s => s.Score)
            .ThenBy(s => s.Priced.Loan.Id, StringComparer.Ordinal)
            .Select(s => new ReviewRow(s.Priced.Loan.Id, s.Priced.Profile.FullName, s.Priced.BurdenPercent, s.Score))
            .ToList();
    }

    public static IReadOnlyList<PurposeTotalRow> ApprovedByPurpose(
        IEnumerable<PricedApplication> source, Func<PricedApplication, int> score, int threshold)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Scored(score)
            .Where(s => s.Score >= threshold)
            .GroupBy(s => s.Priced.Loan.Purpose)
            .Select(g => new PurposeTotalRow(
                g.Key,
                g.Count(),
                g.Select(s => s.Priced).TotalRequested(),
                g.Sum(s => s.Priced.MonthlyPayment)))
            .OrderByDescending(r => r.TotalAmount)
            .ThenBy(r => r.Purpose)
            .ToList();
    }
}
