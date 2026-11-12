using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Pipeline;

public static class LoanApplicationWorkflow
{
    public static Result<LoanWorkflowState.Priced, DomainError> Prepare(
        RawApplicationRecord raw, ApplicantRegistry registry, IPaymentCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(calculator);

        return ValidatedApplication.Create(raw)
            .Bind(application => ApplicationStages.AttachProfile(application, registry))
            .Bind(pending => ApplicationStages.VerifyEligibility(pending))
            .Bind(pending => ApplicationStages.Price(pending, calculator));
    }

    public static Result<DecisionReceipt, DomainError> ProcessApplication(
        RawApplicationRecord raw,
        ApplicantRegistry registry,
        IPaymentCalculator calculator,
        ApprovalPolicy policy,
        string policyName)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return Prepare(raw, registry, calculator)
            .Bind(priced => ApplicationStages.ApplyDecision(priced, policy, policyName))
            .Map(state => ApplicationStages.ToReceipt(state));
    }
}
