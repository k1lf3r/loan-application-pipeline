using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

public static class ApprovalPolicies
{
    public const string ConservativeName = "Conservative";
    public const string FlexibleName = "Flexible";

    public static (bool Approved, decimal ApprovedAmount) Conservative(ApplicationRequest app)
    {
        const int minScore = 650;
        const decimal incomeMultiplier = 3m;

        if (app.CreditScore < minScore)
            return (false, 0m);

        decimal limit = app.MonthlyIncome * incomeMultiplier;
        if (app.HasExistingDebt) limit *= 0.5m;

        return (true, Math.Min(app.RequestedAmount, limit));
    }

    public static (bool Approved, decimal ApprovedAmount) Flexible(ApplicationRequest app)
    {
        const int minScore = 550;
        const decimal incomeMultiplier = 5m;

        if (app.CreditScore < minScore)
            return (false, 0m);

        decimal limit = app.MonthlyIncome * incomeMultiplier;
        if (app.HasExistingDebt) limit *= 0.5m;

        return (true, Math.Min(app.RequestedAmount, limit));
    }
}