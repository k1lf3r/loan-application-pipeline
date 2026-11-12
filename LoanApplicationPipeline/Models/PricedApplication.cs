namespace LoanApplicationPipeline.Models;

public sealed record PricedApplication(ValidatedApplication Loan, ApplicantProfile Profile, long MonthlyPayment)
{
    // (new payment + existing debt) / income in basis points; saturates at int.MaxValue to avoid overflow.
    public int PaymentToIncomeBp
    {
        get
        {
            long bp = (MonthlyPayment + Profile.ExistingDebtMonthly) * 10_000 / Loan.MonthlyIncome;
            return bp > int.MaxValue ? int.MaxValue : (int)bp;
        }
    }

    public int BurdenPercent => PaymentToIncomeBp / 100;
}

public sealed record ScoredApplication(PricedApplication Priced, int Score);
