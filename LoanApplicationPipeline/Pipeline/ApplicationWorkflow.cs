using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

public static class ApplicationWorkflow
{
    public static ApplicationSummary Run(
        IReadOnlyList<ApplicationRequest> validRequests,
        Func<ApplicationRequest, (bool Approved, decimal ApprovedAmount)> approvalPolicy,
        string policyName)
    {
        var eligible = ApplicationStages.SelectEligibleForReview(validRequests);
        var decisions = ApplicationStages.ComputeDecisions(eligible, approvalPolicy, policyName);
        var summary = ApplicationStages.BuildSummary(decisions, policyName, validRequests.Count);
        return summary;
    }
}