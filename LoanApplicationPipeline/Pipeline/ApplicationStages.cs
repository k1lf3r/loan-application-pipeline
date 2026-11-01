using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

public static class ApplicationStages
{
    public const int EligibilityThreshold = 500;

    public static Result<LoanWorkflowState.PendingReview, DomainError> VerifyEligibility(
        LoanWorkflowState.PendingReview pending)
    {
        ArgumentNullException.ThrowIfNull(pending);

        if (pending.Application.CreditScore < EligibilityThreshold)
        {
            return Result<LoanWorkflowState.PendingReview, DomainError>.Failure(
                new DomainError.IneligibleCreditScore(pending.Application.CreditScore, EligibilityThreshold));
        }

        return Result<LoanWorkflowState.PendingReview, DomainError>.Success(pending);
    }

    public static Result<LoanWorkflowState, DomainError> ApplyDecision(
        LoanWorkflowState currentState,
        ApprovalPolicy policy,
        string policyName)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(policy);

        return currentState switch
        {
            LoanWorkflowState.PendingReview pending => Evaluate(pending.Application, policy, policyName),
            LoanWorkflowState.Approved => Result<LoanWorkflowState, DomainError>.Failure(
                new DomainError.InvalidStateTransition("Approved", "ApplyDecision")),
            LoanWorkflowState.Rejected => Result<LoanWorkflowState, DomainError>.Failure(
                new DomainError.InvalidStateTransition("Rejected", "ApplyDecision")),
            _ => throw new InvalidOperationException()
        };
    }

    private static Result<LoanWorkflowState, DomainError> Evaluate(
        ValidatedApplication app,
        ApprovalPolicy policy,
        string policyName)
    {
        var (approved, amount, reason) = policy(app);

        return approved
            ? Result<LoanWorkflowState, DomainError>.Success(new LoanWorkflowState.Approved(app, amount, policyName))
            : Result<LoanWorkflowState, DomainError>.Success(new LoanWorkflowState.Rejected(app, reason ?? "Rejected by policy.", policyName));
    }

    public static DecisionReceipt ToReceipt(LoanWorkflowState state) => state switch
    {
        LoanWorkflowState.Approved a => new DecisionReceipt(
            a.Application.ApplicantId, "Approved", a.ApprovedAmount, $"Policy: {a.PolicyName}", DateTime.UtcNow),
        LoanWorkflowState.Rejected r => new DecisionReceipt(
            r.Application.ApplicantId, "Rejected", 0m, $"Policy: {r.PolicyName}. Reason: {r.Reason}", DateTime.UtcNow),
        LoanWorkflowState.PendingReview p => new DecisionReceipt(
            p.Application.ApplicantId, "Pending", 0m, "Awaiting decision", DateTime.UtcNow),
        _ => throw new InvalidOperationException()
    };
}