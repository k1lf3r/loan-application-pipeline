using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

/// <summary>
/// Pure state transitions. Each method takes an explicit state (the prerequisite is
/// visible in the signature), returns a NEW outcome and never mutates anything.
/// Repeating an operation on a state that has already moved on is a documented no-op:
/// the existing state is returned unchanged.
/// </summary>
public static class ApplicationTransitions
{
    public const int EligibilityThreshold = 500;

    public static ApplicationState Submit(ValidatedApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return new ApplicationState.Pending(application);
    }

    public static TransitionOutcome StartReview(ApplicationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state switch
        {
            ApplicationState.Pending p when p.Application.CreditScore >= EligibilityThreshold
                => new TransitionOutcome.Moved(new ApplicationState.UnderReview(p.Application)),
            ApplicationState.Pending p
                => new TransitionOutcome.Moved(new ApplicationState.Ineligible(p.Application, EligibilityThreshold)),
            // Repeated operation: the review already happened, return the existing state.
            ApplicationState.UnderReview or ApplicationState.Approved
                or ApplicationState.Declined or ApplicationState.Ineligible
                => new TransitionOutcome.Moved(state),
            _ => throw new InvalidOperationException($"Unknown state: {state.GetType().Name}")
        };
    }

    public static TransitionOutcome Decide(
        ApplicationState state,
        Func<ValidatedApplication, (bool Approved, decimal ApprovedAmount)> policy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(policy);

        return state switch
        {
            ApplicationState.UnderReview review => ApplyPolicy(review.Application, policy),
            // Missing prerequisite: eligibility review has not happened yet.
            ApplicationState.Pending
                => new TransitionOutcome.NotAllowed("Cannot decide: eligibility review has not been started."),
            // Repeated operation: a decision (or ineligibility) already exists, return it.
            ApplicationState.Approved or ApplicationState.Declined or ApplicationState.Ineligible
                => new TransitionOutcome.Moved(state),
            _ => throw new InvalidOperationException($"Unknown state: {state.GetType().Name}")
        };
    }

    private static TransitionOutcome ApplyPolicy(
        ValidatedApplication application,
        Func<ValidatedApplication, (bool Approved, decimal ApprovedAmount)> policy)
    {
        var (approved, amount) = policy(application);

        // Validate everything before producing a new state.
        if (amount < 0m)
            return new TransitionOutcome.NotAllowed($"Policy returned a negative amount ({amount}).");
        if (!approved && amount != 0m)
            return new TransitionOutcome.NotAllowed("A rejected application must have a zero approved amount.");

        return approved
            ? new TransitionOutcome.Moved(new ApplicationState.Approved(application, amount))
            : new TransitionOutcome.Moved(new ApplicationState.Declined(application, "Rejected by approval policy."));
    }
}
