using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

public static class ApplicationStages
{
    /// <summary>
    /// Builds the summary from FINAL states. Every state case is matched explicitly:
    /// non-final states are rejected instead of being silently treated as results.
    /// </summary>
    public static ApplicationSummary BuildSummary(
        IReadOnlyList<ApplicationState> finalStates,
        string policyName,
        int totalSubmitted)
    {
        ArgumentNullException.ThrowIfNull(finalStates);
        ArgumentNullException.ThrowIfNull(policyName);

        var decisions = new List<ApplicationDecision>();

        foreach (var state in finalStates)
        {
            switch (state)
            {
                case ApplicationState.Approved approved:
                    decisions.Add(new ApplicationDecision(
                        approved.Application.ApplicantId,
                        approved.Application.CreditScore,
                        approved.Application.RequestedAmount,
                        true,
                        approved.Amount,
                        policyName,
                        "Approved"));
                    break;

                case ApplicationState.Declined declined:
                    decisions.Add(new ApplicationDecision(
                        declined.Application.ApplicantId,
                        declined.Application.CreditScore,
                        declined.Application.RequestedAmount,
                        false,
                        0m,
                        policyName,
                        declined.Reason));
                    break;

                case ApplicationState.Ineligible:
                    // Never scored, so it is counted in "submitted" but not in "eligible".
                    break;

                case ApplicationState.Pending or ApplicationState.UnderReview:
                    throw new InvalidOperationException(
                        $"State {state.GetType().Name} is not final and cannot be summarised.");

                default:
                    throw new InvalidOperationException($"Unknown state: {state?.GetType().Name ?? "null"}");
            }
        }

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
