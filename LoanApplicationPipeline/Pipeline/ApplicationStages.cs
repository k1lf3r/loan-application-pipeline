using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

public static class ApplicationStages
{
    private const int EligibilityCreditScoreThreshold = 500;

    public static IReadOnlyList<ApplicationRequest> SelectEligibleForReview(IReadOnlyList<ApplicationRequest> requests)
    {
        return requests
            .Where(r => r.CreditScore >= EligibilityCreditScoreThreshold)
            .Select(r => r with
            {
                MonthlyIncome = Math.Round(r.MonthlyIncome, 2),
                RequestedAmount = Math.Round(r.RequestedAmount, 2)
            })
            .ToArray();
    }

    public static IReadOnlyList<ApplicationDecision> ComputeDecisions(
        IReadOnlyList<ApplicationRequest> eligibleRequests,
        Func<ApplicationRequest, (bool Approved, decimal ApprovedAmount)> approvalPolicy,
        string policyName)
    {
        ArgumentNullException.ThrowIfNull(approvalPolicy);

        return eligibleRequests
            .Select(r =>
            {
                var (approved, approvedAmount) = approvalPolicy(r);
                return new ApplicationDecision(
                    r.ApplicantId,
                    r.CreditScore,
                    r.RequestedAmount,
                    approved,
                    approvedAmount,
                    policyName);
            })
            .ToArray();
    }

    public static ApplicationSummary BuildSummary(
        IReadOnlyList<ApplicationDecision> decisions,
        string policyName,
        int totalSubmitted)
    {
        var ordered = decisions
            .OrderByDescending(d => d.ApprovedAmount)
            .ToArray();

        var approvedCount = ordered.Count(d => d.Approved);
        var totalApprovedAmount = ordered.Where(d => d.Approved).Sum(d => d.ApprovedAmount);

        return new ApplicationSummary(
            policyName,
            totalSubmitted,
            ordered.Length,
            approvedCount,
            totalApprovedAmount,
            ordered);
    }
}