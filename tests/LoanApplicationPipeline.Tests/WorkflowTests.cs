using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class WorkflowTests
{
    [Fact]
    public void NormalWorkflow_BothPolicies_ApproveFullRequestedAmount()
    {
        var apps = new[] { TestData.App("A1", "50000", "100000", "700") };

        var conservative = ApplicationWorkflow.Run(apps, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
        var flexible = ApplicationWorkflow.Run(apps, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        Assert.True(conservative.Decisions[0].Approved);
        Assert.Equal(100000m, conservative.Decisions[0].ApprovedAmount);
        Assert.True(flexible.Decisions[0].Approved);
        Assert.Equal(100000m, flexible.Decisions[0].ApprovedAmount);
    }

    [Fact]
    public void EmptyDataset_ProducesEmptySummaryWithoutCrashing()
    {
        var summary = ApplicationWorkflow.Run(
            Array.Empty<ValidatedApplication>(), ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(0, summary.TotalSubmitted);
        Assert.Equal(0, summary.EligibleForReview);
        Assert.Equal(0, summary.Approved);
        Assert.Equal(0m, summary.TotalApprovedAmount);
        Assert.Empty(summary.Decisions);
    }

    [Fact]
    public void BoundaryCreditScore_AtEligibilityThreshold_IsIncludedInReview()
    {
        var apps = new[] { TestData.App("A2", "30000", "20000", "500") };

        var summary = ApplicationWorkflow.Run(apps, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(1, summary.EligibleForReview);
        Assert.False(summary.Decisions[0].Approved);
    }

    [Fact]
    public void BoundaryCreditScore_JustBelowEligibilityThreshold_IsExcludedFromReview()
    {
        var apps = new[] { TestData.App("A5", "20000", "5000", "499") };

        var summary = ApplicationWorkflow.Run(apps, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(1, summary.TotalSubmitted);
        Assert.Equal(0, summary.EligibleForReview);
        Assert.Empty(summary.Decisions);
    }

    [Fact]
    public void TwoPolicies_OnSameInput_ProduceDifferentApprovalDecision()
    {
        var apps = new[] { TestData.App("A3", "40000", "30000", "600") };

        var conservative = ApplicationWorkflow.Run(apps, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
        var flexible = ApplicationWorkflow.Run(apps, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        Assert.False(conservative.Decisions[0].Approved);
        Assert.Equal(0m, conservative.Decisions[0].ApprovedAmount);
        Assert.True(flexible.Decisions[0].Approved);
        Assert.Equal(30000m, flexible.Decisions[0].ApprovedAmount);
    }

    [Fact]
    public void DebtSurcharge_CapsApprovedAmountDifferentlyPerPolicy()
    {
        var apps = new[] { TestData.App("A4", "10000", "20000", "700", "true") };

        var conservative = ApplicationWorkflow.Run(apps, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
        var flexible = ApplicationWorkflow.Run(apps, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);

        Assert.Equal(15000m, conservative.Decisions[0].ApprovedAmount);
        Assert.Equal(20000m, flexible.Decisions[0].ApprovedAmount);
    }

    // STATE PRESERVATION: running the workflow does not change its input and is repeatable
    [Fact]
    public void Run_DoesNotMutateInput_AndIsRepeatable()
    {
        var apps = new List<ValidatedApplication>
        {
            TestData.App("A1", "50000", "100000", "700"),
            TestData.App("A5", "20000", "5000", "499"),
        };
        var snapshot = apps.ToArray();

        var first = ApplicationWorkflow.Run(apps, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
        var second = ApplicationWorkflow.Run(apps, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);

        Assert.Equal(snapshot, apps);
        Assert.Equal(first.Decisions, second.Decisions);
    }

    // DISTINCT CASES: Approved, Declined, Ineligible are each matched explicitly
    [Fact]
    public void BuildSummary_HandlesEveryFinalCase()
    {
        var approved = new ApplicationState.Approved(TestData.App("A1", "50000", "100000", "700"), 100000m);
        var declined = new ApplicationState.Declined(TestData.App("A3", "40000", "30000", "600"), "Rejected by approval policy.");
        var ineligible = new ApplicationState.Ineligible(TestData.App("A5", "20000", "5000", "499"), 500);

        var summary = ApplicationStages.BuildSummary(new ApplicationState[] { declined, ineligible, approved }, "P", 3);

        Assert.Equal(3, summary.TotalSubmitted);
        Assert.Equal(2, summary.EligibleForReview);
        Assert.Equal(1, summary.Approved);
        Assert.Equal(100000m, summary.TotalApprovedAmount);
        Assert.Equal("A1", summary.Decisions[0].ApplicantId);
    }

    // MISSING PREREQUISITE: non-final states are rejected, never summarised as results
    [Fact]
    public void BuildSummary_RejectsNonFinalStates()
    {
        var app = TestData.App("A1", "50000", "100000", "700");

        Assert.Throws<InvalidOperationException>(() =>
            ApplicationStages.BuildSummary(new ApplicationState[] { new ApplicationState.Pending(app) }, "P", 1));
        Assert.Throws<InvalidOperationException>(() =>
            ApplicationStages.BuildSummary(new ApplicationState[] { new ApplicationState.UnderReview(app) }, "P", 1));
    }

    // DISTINCT CASES for ValidationOutcome (Valid / Invalid) in the real intake path
    [Fact]
    public void Intake_SplitsValidAndInvalidRecords_AndKeepsInputUnchanged()
    {
        var raws = new List<RawApplicationRecord>
        {
            new("A1", "50000", "100000", "700", "false"),
            new("A6", null, "5000", "600", "false"),
        };
        var snapshot = raws.ToArray();

        var result = ApplicationIntake.Process(raws);

        Assert.Single(result.Accepted);
        Assert.Equal("A1", result.Accepted[0].ApplicantId);
        Assert.Single(result.Rejected);
        Assert.Contains("MonthlyIncome is missing", result.Rejected[0]);
        Assert.Equal(snapshot, raws);
    }
}
