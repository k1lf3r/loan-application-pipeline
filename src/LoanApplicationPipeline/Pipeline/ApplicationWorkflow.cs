using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

public static class ApplicationWorkflow
{
    /// <summary>
    /// Runs every validated application through Pending -> review -> decision
    /// and summarises the final states. The input can only contain
    /// ValidatedApplication values, so unvalidated data cannot enter the workflow.
    /// </summary>
    public static ApplicationSummary Run(
        IReadOnlyList<ValidatedApplication> applications,
        Func<ValidatedApplication, (bool Approved, decimal ApprovedAmount)> approvalPolicy,
        string policyName)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(approvalPolicy);
        ArgumentNullException.ThrowIfNull(policyName);

        var finalStates = applications
            .Select(a => Process(a, approvalPolicy))
            .ToArray();

        return ApplicationStages.BuildSummary(finalStates, policyName, applications.Count);
    }

    public static ApplicationState Process(
        ValidatedApplication application,
        Func<ValidatedApplication, (bool Approved, decimal ApprovedAmount)> approvalPolicy)
    {
        var submitted = ApplicationTransitions.Submit(application);

        switch (ApplicationTransitions.StartReview(submitted))
        {
            case TransitionOutcome.Moved reviewed:
                switch (ApplicationTransitions.Decide(reviewed.Next, approvalPolicy))
                {
                    case TransitionOutcome.Moved decided:
                        return decided.Next;
                    case TransitionOutcome.NotAllowed refusedDecision:
                        return Failed(application, refusedDecision.Reason);
                    default:
                        throw new InvalidOperationException("Unknown transition outcome.");
                }

            case TransitionOutcome.NotAllowed refusedReview:
                return Failed(application, refusedReview.Reason);

            default:
                throw new InvalidOperationException("Unknown transition outcome.");
        }
    }

    // A failed transition never becomes an approval: the application is declined with the reason.
    private static ApplicationState Failed(ValidatedApplication application, string reason)
        => new ApplicationState.Declined(application, $"Processing failed: {reason}");
}
