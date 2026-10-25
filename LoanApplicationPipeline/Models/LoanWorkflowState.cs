namespace LoanApplicationPipeline.Models;

public abstract record LoanWorkflowState
{
    private LoanWorkflowState() { }

    public sealed record PendingReview(
        ValidatedApplication Application,
        ApplicantProfile Profile) : LoanWorkflowState;

    public sealed record Approved(
        ValidatedApplication Application,
        decimal ApprovedAmount,
        string PolicyName) : LoanWorkflowState;

    public sealed record Rejected(
        ValidatedApplication Application,
        string Reason,
        string PolicyName) : LoanWorkflowState;
}