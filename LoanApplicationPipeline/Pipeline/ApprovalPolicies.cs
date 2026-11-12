using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Scoring;

namespace LoanApplicationPipeline.Pipeline;

public delegate PolicyDecision ApprovalPolicy(PricedApplication application);

public static class ApprovalPolicies
{
    public const string ConservativeName = "Conservative";
    public const string FlexibleName = "Flexible";

    public const int DefaultThreshold = 500;

    public static ApprovalPolicy FromScore(Func<PricedApplication, int> score, int threshold)
    {
        ArgumentNullException.ThrowIfNull(score);

        return application =>
        {
            var value = score(application);

            if (value >= threshold)
                return new PolicyDecision.Approve(application.Loan.RequestedAmount);

            return new PolicyDecision.Reject($"Score {value} is below the approval threshold {threshold}.");
        };
    }

    public static ApprovalPolicy Conservative { get; } = FromScore(ScoringFunctions.Conservative, DefaultThreshold);

    public static ApprovalPolicy Flexible { get; } = FromScore(ScoringFunctions.Flexible, DefaultThreshold);
}
