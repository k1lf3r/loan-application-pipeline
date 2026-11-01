namespace LoanApplicationPipeline.Domain;

/// <summary>An application together with the monthly payment calculated by the native library.</summary>
public sealed record PricedApplication(LoanApplication Application, long MonthlyPayment)
{
    /// <summary>(new payment + existing debt) / income, in basis points, integer arithmetic.</summary>
    public int PaymentToIncomeBp =>
        (int)((MonthlyPayment + Application.ExistingDebtMonthly) * 10_000 / Application.MonthlyIncome);

    /// <summary>Burden in whole percent (truncated).</summary>
    public int BurdenPercent => PaymentToIncomeBp / 100;
}

/// <summary>A priced application with the score given by a configured scoring function.</summary>
public sealed record ScoredApplication(PricedApplication Priced, int Score);
