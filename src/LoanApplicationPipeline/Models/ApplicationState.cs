namespace LoanApplicationPipeline.Models;

/// <summary>
/// Workflow state of one application. Every case carries only the data that
/// makes sense for that case, and every case needs a ValidatedApplication.
///   Pending     - submitted, eligibility review not started yet
///   UnderReview - passed the eligibility threshold, waiting for a policy decision
///   Approved    - policy approved; carries the approved amount
///   Declined    - policy rejected; carries the reason
///   Ineligible  - below the eligibility threshold, never scored; carries the threshold
/// </summary>
public abstract record ApplicationState
{
    private ApplicationState() { }

    public sealed record Pending(ValidatedApplication Application) : ApplicationState;

    public sealed record UnderReview(ValidatedApplication Application) : ApplicationState;

    public sealed record Approved(ValidatedApplication Application, decimal Amount) : ApplicationState;

    public sealed record Declined(ValidatedApplication Application, string Reason) : ApplicationState;

    public sealed record Ineligible(ValidatedApplication Application, int Threshold) : ApplicationState;
}
