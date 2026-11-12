using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class PipelineWorkflowTests
{
    private static readonly ApplicantRegistry Registry = Samples.Registry(
        Samples.Profile("P001", debt: 0),
        Samples.Profile("P002", debt: 40_000),
        Samples.Profile("P011", active: false));

    [Fact]
    public void CompleteSuccess_IntegratedResultChain_ProducesApprovedReceipt()
    {
        var calculator = new StubCalculator(88_849);

        var receipt = Check.Ok(LoanApplicationWorkflow.ProcessApplication(
            Samples.Raw(), Registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

        Assert.Equal("A1", receipt.ApplicationId);
        Assert.Equal("P001", receipt.ApplicantId);
        Assert.Equal("Approved", receipt.Status);
        Assert.Equal(1_000_000, receipt.ApprovedAmount);
        Assert.Equal(88_849, receipt.MonthlyPayment);
        Assert.Equal(1, calculator.Calls);
    }

    [Fact]
    public void SameApplication_DifferentPolicy_GivesDifferentBusinessOutcome()
    {
        var conservative = Check.Ok(LoanApplicationWorkflow.ProcessApplication(
            Samples.Raw(), Registry, new StubCalculator(88_849), ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName));
        var flexible = Check.Ok(LoanApplicationWorkflow.ProcessApplication(
            Samples.Raw(), Registry, new StubCalculator(88_849), ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

        Assert.Equal("Rejected", conservative.Status);
        Assert.Equal(0, conservative.ApprovedAmount);
        Assert.Contains("480", conservative.Details);
        Assert.Equal("Approved", flexible.Status);
    }

    [Fact]
    public void DomainFailure1_ScoreBelowEligibility_StopsBeforePricing()
    {
        var calculator = new StubCalculator(88_849);

        var result = LoanApplicationWorkflow.ProcessApplication(
            Samples.Raw(credit: "499"), Registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        var error = Assert.IsType<DomainError.IneligibleCreditScore>(Check.Err(result));
        Assert.Equal(499, error.Score);
        Assert.Equal(500, error.Threshold);
        Assert.Equal(0, calculator.Calls);
    }

    [Fact]
    public void BoundaryCreditScore_ExactlyAtEligibilityThreshold_Passes()
    {
        var pending = new LoanWorkflowState.PendingReview(Samples.Validated(credit: 500), Samples.Profile());
        var below = new LoanWorkflowState.PendingReview(Samples.Validated(credit: 499), Samples.Profile());

        Assert.True(ApplicationStages.VerifyEligibility(pending).IsSuccess);
        Assert.True(ApplicationStages.VerifyEligibility(below).IsFailure);
    }

    [Fact]
    public void DomainFailure2_ApplicantNotFound_StopsTheWorkflow()
    {
        var calculator = new StubCalculator(1);

        var result = LoanApplicationWorkflow.ProcessApplication(
            Samples.Raw(applicantId: "UNKNOWN_USER"), Registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        Assert.Equal("UNKNOWN_USER", Assert.IsType<DomainError.ApplicantNotFound>(Check.Err(result)).ApplicantId);
        Assert.Equal(0, calculator.Calls);
    }

    [Fact]
    public void DomainFailure3_InactiveApplicant_IsRejected()
    {
        var result = LoanApplicationWorkflow.Prepare(Samples.Raw(applicantId: "P011"), Registry, new StubCalculator(1));

        Assert.Equal("P011", Assert.IsType<DomainError.ApplicantInactive>(Check.Err(result)).ApplicantId);
    }

    [Fact]
    public void InvalidInput_IsReportedAsValidationError_AndNothingElseRuns()
    {
        var calculator = new StubCalculator(1);

        var result = LoanApplicationWorkflow.Prepare(Samples.Raw(amount: "-5"), Registry, calculator);

        Assert.Equal("RequestedAmount", Assert.IsType<DomainError.ValidationError>(Check.Err(result)).Field);
        Assert.Equal(0, calculator.Calls);
    }

    [Fact]
    public void CalculatorFailure_Propagates_AndThePolicyIsNeverCalled()
    {
        var failure = new DomainError.NativeCallFailed("native.load_failed", "boom");
        var calculator = new StubCalculator((_, _, _) => Res.Fail<long>(failure));
        var policyCalls = 0;
        ApprovalPolicy counting = _ => { policyCalls++; return new PolicyDecision.Approve(1); };

        var result = LoanApplicationWorkflow.ProcessApplication(Samples.Raw(), Registry, calculator, counting, "Counting");

        Assert.Equal(failure, Check.Err(result));
        Assert.Equal(0, policyCalls);
    }

    [Fact]
    public void Prepare_ReturnsPricedState_WithProfileAndPayment()
    {
        var priced = Check.Ok(LoanApplicationWorkflow.Prepare(
            Samples.Raw(applicantId: "P002"), Registry, new StubCalculator(88_849)));

        Assert.Equal(88_849, priced.Application.MonthlyPayment);
        Assert.Equal(40_000, priced.Application.Profile.ExistingDebtMonthly);
        Assert.Equal(3221, priced.Application.PaymentToIncomeBp);
        Assert.Equal(32, priced.Application.BurdenPercent);
    }
}
