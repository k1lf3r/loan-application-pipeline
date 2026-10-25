using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class PipelineWorkflowTests
{
    private readonly ApplicantRegistry _registry = new();

    [Fact]
    public void CompleteSuccess_IntegratedResultChain_ProducesApprovedReceipt()
    {
        var raw = new RawApplicationRecord("A1", "50000", "100000", "700", "false");

        var result = LoanApplicationWorkflow.ProcessApplication(
            raw, _registry, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.True(result.IsSuccess);
        var receipt = ((Result<DecisionReceipt, DomainError>.Ok)result).Value;
        Assert.Equal("A1", receipt.ApplicantId);
        Assert.Equal("Approved", receipt.Status);
        Assert.Equal(100000m, receipt.ApprovedAmount);
    }

    [Fact]
    public void DomainFailure1_ScoreBelowThreshold_RejectsBeforeEvaluation()
    {
        var raw = new RawApplicationRecord("A5", "20000", "5000", "499", "false");

        var result = LoanApplicationWorkflow.ProcessApplication(
            raw, _registry, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.True(result.IsFailure);
        var err = ((Result<DecisionReceipt, DomainError>.Err)result).Error;
        var scoreErr = Assert.IsType<DomainError.IneligibleCreditScore>(err);
        Assert.Equal(499, scoreErr.Score);
        Assert.Equal(500, scoreErr.Threshold);
    }

    [Fact]
    public void DomainFailure2_ApplicantNotFound_RejectsWorkflow()
    {
        var raw = new RawApplicationRecord("UNKNOWN_USER", "50000", "10000", "700", "false");

        var result = LoanApplicationWorkflow.ProcessApplication(
            raw, _registry, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.True(result.IsFailure);
        var err = ((Result<DecisionReceipt, DomainError>.Err)result).Error;
        var notFound = Assert.IsType<DomainError.ApplicantNotFound>(err);
        Assert.Equal("UNKNOWN_USER", notFound.ApplicantId);
    }

    [Fact]
    public void BoundaryCreditScore_AtEligibilityThreshold_PassesEligibility()
    {
        var raw = new RawApplicationRecord("A2", "30000", "20000", "500", "false");
        var app = ((Result<ValidatedApplication, DomainError>.Ok)ValidatedApplication.Create(raw)).Value;
        var profile = ((Option<ApplicantProfile>.Some)_registry.FindById("A2")).Value;
        var pending = new LoanWorkflowState.PendingReview(app, profile);

        var result = ApplicationStages.VerifyEligibility(pending);

        Assert.True(result.IsSuccess);
    }
}