using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Pipeline;

public static class ApplicationStages
{
    public const int EligibilityThreshold = 500;

    public static Result<LoanWorkflowState.PendingReview, DomainError> AttachProfile(
        ValidatedApplication application, ApplicantRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(registry);

        return registry.FindById(application.ApplicantId)
            .ToResult<DomainError>(new DomainError.ApplicantNotFound(application.ApplicantId))
            .Bind(profile => profile.IsActive
                ? Res.Ok(new LoanWorkflowState.PendingReview(application, profile))
                : Res.Fail<LoanWorkflowState.PendingReview>(new DomainError.ApplicantInactive(application.ApplicantId)));
    }

    public static Result<LoanWorkflowState.PendingReview, DomainError> VerifyEligibility(
        LoanWorkflowState.PendingReview pending)
    {
        ArgumentNullException.ThrowIfNull(pending);

        if (pending.Application.CreditScore < EligibilityThreshold)
        {
            return Res.Fail<LoanWorkflowState.PendingReview>(
                new DomainError.IneligibleCreditScore(pending.Application.CreditScore, EligibilityThreshold));
        }

        return Res.Ok(pending);
    }

    public static Result<LoanWorkflowState.Priced, DomainError> Price(
        LoanWorkflowState.PendingReview pending, IPaymentCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(calculator);

        var application = pending.Application;

        return calculator
            .MonthlyPayment(application.RequestedAmount, application.AnnualRateBp, application.TermMonths)
            .Map(payment => new LoanWorkflowState.Priced(
                new PricedApplication(application, pending.Profile, payment)));
    }

    public static Result<LoanWorkflowState, DomainError> ApplyDecision(
        LoanWorkflowState currentState, ApprovalPolicy policy, string policyName)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(policy);

        return currentState switch
        {
            LoanWorkflowState.Priced priced => Evaluate(priced.Application, policy, policyName),
            LoanWorkflowState.PendingReview => Res.Fail<LoanWorkflowState>(
                new DomainError.InvalidStateTransition("PendingReview", "ApplyDecision (the application must be priced first)")),
            LoanWorkflowState.Approved => Res.Fail<LoanWorkflowState>(
                new DomainError.InvalidStateTransition("Approved", "ApplyDecision")),
            LoanWorkflowState.Rejected => Res.Fail<LoanWorkflowState>(
                new DomainError.InvalidStateTransition("Rejected", "ApplyDecision")),
            _ => throw new InvalidOperationException("Unknown workflow state.")
        };
    }

    private static Result<LoanWorkflowState, DomainError> Evaluate(
        PricedApplication application, ApprovalPolicy policy, string policyName)
    {
        var decision = policy(application);

        LoanWorkflowState next = decision switch
        {
            PolicyDecision.Approve approve => new LoanWorkflowState.Approved(application, approve.Amount, policyName),
            PolicyDecision.Reject reject => new LoanWorkflowState.Rejected(application, reject.Reason, policyName),
            _ => throw new InvalidOperationException("Unknown policy decision.")
        };

        return Res.Ok(next);
    }

    public static DecisionReceipt ToReceipt(LoanWorkflowState state) => state switch
    {
        LoanWorkflowState.Approved a => new DecisionReceipt(
            a.Application.Loan.Id, a.Application.Loan.ApplicantId, "Approved",
            a.ApprovedAmount, a.Application.MonthlyPayment, $"Policy: {a.PolicyName}"),
        LoanWorkflowState.Rejected r => new DecisionReceipt(
            r.Application.Loan.Id, r.Application.Loan.ApplicantId, "Rejected",
            0, r.Application.MonthlyPayment, $"Policy: {r.PolicyName}. Reason: {r.Reason}"),
        LoanWorkflowState.Priced p => new DecisionReceipt(
            p.Application.Loan.Id, p.Application.Loan.ApplicantId, "Priced",
            0, p.Application.MonthlyPayment, "Awaiting decision"),
        LoanWorkflowState.PendingReview p => new DecisionReceipt(
            p.Application.Id, p.Application.ApplicantId, "Pending",
            0, 0, "Awaiting pricing"),
        _ => throw new InvalidOperationException("Unknown workflow state.")
    };
}
