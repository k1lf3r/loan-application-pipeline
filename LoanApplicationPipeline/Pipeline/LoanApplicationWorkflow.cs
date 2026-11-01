using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Pipeline;

public static class LoanApplicationWorkflow
{
    public static Result<DecisionReceipt, DomainError> ProcessApplication(
        RawApplicationRecord rawRecord,
        ApplicantRegistry registry,
        ApprovalPolicy policy,
        string policyName)
    {
        ArgumentNullException.ThrowIfNull(rawRecord);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(policy);

        return ValidatedApplication.Create(rawRecord)
            .Bind(app => registry.FindById(app.ApplicantId)
                .ToResult<DomainError>(new DomainError.ApplicantNotFound(app.ApplicantId))
                .Map(profile => new LoanWorkflowState.PendingReview(app, profile)))
            .Bind(ApplicationStages.VerifyEligibility)
            .Bind(state => ApplicationStages.ApplyDecision(state, policy, policyName))
            .Map(ApplicationStages.ToReceipt);
    }
}