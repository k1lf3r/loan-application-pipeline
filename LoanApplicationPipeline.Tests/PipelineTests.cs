using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class PipelineTests
{
    [Fact]
    public void NormalWorkflow_BothPolicies_ApproveFullRequestedAmount()
    {
        var requests = new[] { new ApplicationRequest("A1", 50000m, 100000m, 700, false) };

        var conservative = ApplicationWorkflow.Run(requests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
        var flexible = ApplicationWorkflow.Run(requests, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        Assert.True(conservative.Decisions[0].Approved);
        Assert.Equal(100000m, conservative.Decisions[0].ApprovedAmount);
        Assert.True(flexible.Decisions[0].Approved);
        Assert.Equal(100000m, flexible.Decisions[0].ApprovedAmount);
    }

    [Fact]
    public void EmptyDataset_ProducesEmptySummaryWithoutCrashing()
    {
        var requests = Array.Empty<ApplicationRequest>();

        var summary = ApplicationWorkflow.Run(requests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(0, summary.TotalSubmitted);
        Assert.Equal(0, summary.EligibleForReview);
        Assert.Equal(0, summary.Approved);
        Assert.Equal(0m, summary.TotalApprovedAmount);
        Assert.Empty(summary.Decisions);
    }

    [Fact]
    public void BoundaryCreditScore_AtEligibilityThreshold_IsIncludedInReview()
    {
        var requests = new[] { new ApplicationRequest("A2", 30000m, 20000m, 500, false) };

        var summary = ApplicationWorkflow.Run(requests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(1, summary.EligibleForReview);
        Assert.False(summary.Decisions[0].Approved);
    }

    [Fact]
    public void BoundaryCreditScore_JustBelowEligibilityThreshold_IsExcludedFromReview()
    {
        var requests = new[] { new ApplicationRequest("A5", 20000m, 5000m, 499, false) };

        var summary = ApplicationWorkflow.Run(requests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(1, summary.TotalSubmitted);
        Assert.Equal(0, summary.EligibleForReview);
    }

    [Fact]
    public void TwoPolicies_OnSameInput_ProduceDifferentApprovalDecision()
    {
        var requests = new[] { new ApplicationRequest("A3", 40000m, 30000m, 600, false) };

        var conservative = ApplicationWorkflow.Run(requests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
        var flexible = ApplicationWorkflow.Run(requests, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        Assert.False(conservative.Decisions[0].Approved);
        Assert.Equal(0m, conservative.Decisions[0].ApprovedAmount);

        Assert.True(flexible.Decisions[0].Approved);
        Assert.Equal(30000m, flexible.Decisions[0].ApprovedAmount);
    }

    [Fact]
    public void DebtSurcharge_CapsApprovedAmountDifferentlyPerPolicy()
    {
        var requests = new[] { new ApplicationRequest("A4", 10000m, 20000m, 700, true) };

        var conservative = ApplicationWorkflow.Run(requests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
        var flexible = ApplicationWorkflow.Run(requests, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        Assert.True(conservative.Decisions[0].Approved);
        Assert.Equal(15000m, conservative.Decisions[0].ApprovedAmount);

        Assert.True(flexible.Decisions[0].Approved);
        Assert.Equal(20000m, flexible.Decisions[0].ApprovedAmount);
    }

    [Fact]
    public void ComputeDecisions_DoesNotMutateCallerOwnedInput()
    {
        var requests = new List<ApplicationRequest>
        {
            new("A1", 50000m, 100000m, 700, false)
        };
        var snapshot = requests.ToArray();

        var decisions = ApplicationStages.ComputeDecisions(requests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(snapshot[0], requests[0]);
        Assert.NotEmpty(decisions);
    }
}