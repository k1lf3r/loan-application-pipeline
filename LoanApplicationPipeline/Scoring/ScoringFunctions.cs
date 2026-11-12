using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Scoring;

public static class ScoringFunctions
{
    // Curried scoring formula: Score = CreditScore * creditWeight - BurdenPercent * burdenWeight.
    public static Func<int, Func<int, Func<PricedApplication, int>>> CreateScore { get; } =
        creditWeight => burdenWeight => app =>
            app.Loan.CreditScore * creditWeight - app.BurdenPercent * burdenWeight;

    public static Func<PricedApplication, int> Conservative { get; } = CreateScore(1)(10);

    public static Func<PricedApplication, int> Flexible { get; } = CreateScore(1)(4);
}
