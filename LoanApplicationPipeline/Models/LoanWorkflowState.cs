namespace LoanApplicationPipeline.Models;

public abstract record LoanWorkflowState
{
    private LoanWorkflowState() { }

    public sealed record PendingReview(ValidatedApplication Application, ApplicantProfile Profile) : LoanWorkflowState;

    public sealed record Priced(PricedApplication Application) : LoanWorkflowState;

    public sealed record Approved(PricedApplication Application, long ApprovedAmount, string PolicyName) : LoanWorkflowState;

    public sealed record Rejected(PricedApplication Application, string Reason, string PolicyName) : LoanWorkflowState;
}
