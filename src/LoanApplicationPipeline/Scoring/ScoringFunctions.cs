using LoanApplicationPipeline.Domain;

namespace LoanApplicationPipeline.Scoring;

public static class ScoringFunctions
{
    /// <summary>
    /// CURRIED function: creditWeight -> burdenWeight -> (application -> score).
    /// Score = CreditScore * creditWeight - BurdenPercent * burdenWeight.
    /// Each argument arrives in its own call, so every step can be fixed (partially applied) separately.
    /// </summary>
    public static Func<int, Func<int, Func<PricedApplication, int>>> CreateScore { get; } =
        creditWeight => burdenWeight => app =>
            app.Application.CreditScore * creditWeight - app.BurdenPercent * burdenWeight;

    /// <summary>PARTIAL APPLICATION 1: strict about payment burden (weight 10 per percent).</summary>
    public static Func<PricedApplication, int> Conservative { get; } = CreateScore(1)(10);

    /// <summary>PARTIAL APPLICATION 2: tolerant about payment burden (weight 4 per percent).</summary>
    public static Func<PricedApplication, int> Flexible { get; } = CreateScore(1)(4);
}
