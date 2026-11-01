using LoanApplicationPipeline.Domain;

namespace LoanApplicationPipeline.Queries;

public sealed record ReviewRow(string Id, string Name, int BurdenPercent, int Score);

public sealed record PurposeTotalRow(LoanPurpose Purpose, int Count, long TotalAmount, long TotalMonthlyPayment);

/// <summary>Pure reports: input in, rows out. No Console, no files, no native calls.</summary>
public static class ApplicationReports
{
    /// <summary>
    /// REPORT 1 - Manual review queue (user: credit underwriter).
    /// Input: priced applications + configured score function + threshold.
    /// Stages: Scored (score) -> Where (score &lt; threshold) -> OrderBy score -> ThenBy Id (deterministic
    /// tie-break, ordinal) -> Select (output fields: Id, Name, BurdenPercent, Score).
    /// </summary>
    public static IReadOnlyList<ReviewRow> ReviewQueue(
        IEnumerable<PricedApplication> source, Func<PricedApplication, int> score, int threshold) =>
        source.Scored(score)
            .Where(s => s.Score < threshold)
            .OrderBy(s => s.Score)
            .ThenBy(s => s.Priced.Application.Id, StringComparer.Ordinal)
            .Select(s => new ReviewRow(s.Priced.Application.Id, s.Priced.Application.ApplicantName,
                                       s.Priced.BurdenPercent, s.Score))
            .ToList();

    /// <summary>
    /// REPORT 2 - Approved portfolio by purpose (user: portfolio manager).
    /// Input: priced applications + configured score function + threshold.
    /// Stages: Scored -> Where (score &gt;= threshold) -> GroupBy purpose -> Select (Count, Sum of amount via
    /// TotalRequested/Aggregate, Sum of payments) -> OrderByDescending total -> ThenBy purpose.
    /// </summary>
    public static IReadOnlyList<PurposeTotalRow> ApprovedByPurpose(
        IEnumerable<PricedApplication> source, Func<PricedApplication, int> score, int threshold) =>
        source.Scored(score)
            .Where(s => s.Score >= threshold)
            .GroupBy(s => s.Priced.Application.Purpose)
            .Select(g => new PurposeTotalRow(
                g.Key,
                g.Count(),
                g.Select(s => s.Priced).TotalRequested(),
                g.Sum(s => s.Priced.MonthlyPayment)))
            .OrderByDescending(r => r.TotalAmount)
            .ThenBy(r => r.Purpose)
            .ToList();
}
