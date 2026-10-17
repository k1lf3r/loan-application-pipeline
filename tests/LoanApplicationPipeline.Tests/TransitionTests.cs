using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class TransitionTests
{
    private static ApplicationState Next(TransitionOutcome outcome)
        => Assert.IsType<TransitionOutcome.Moved>(outcome).Next;

    private static ApplicationState Review(ValidatedApplication app)
        => Next(ApplicationTransitions.StartReview(ApplicationTransitions.Submit(app)));

    // BOUNDARY VALUE: exactly at the eligibility threshold
    [Fact]
    public void StartReview_ScoreAtThreshold_MovesToUnderReview()
    {
        var app = TestData.App("A2", "30000", "20000", "500");

        var state = Review(app);

        var review = Assert.IsType<ApplicationState.UnderReview>(state);
        Assert.Equal(app, review.Application);
    }

    // BOUNDARY VALUE: just below the threshold
    [Fact]
    public void StartReview_ScoreBelowThreshold_MovesToIneligible()
    {
        var app = TestData.App("A5", "20000", "5000", "499");

        var state = Review(app);

        var ineligible = Assert.IsType<ApplicationState.Ineligible>(state);
        Assert.Equal(ApplicationTransitions.EligibilityThreshold, ineligible.Threshold);
    }

    // NORMAL OPERATION: validated value -> Pending -> UnderReview -> Approved
    [Fact]
    public void FullFlow_ValidApplication_EndsApprovedWithExpectedAmount()
    {
        var app = TestData.App("A1", "50000", "100000", "700");

        var pending = ApplicationTransitions.Submit(app);
        Assert.IsType<ApplicationState.Pending>(pending);

        var review = Review(app);
        var decided = Next(ApplicationTransitions.Decide(review, ApprovalPolicies.Conservative));

        var approved = Assert.IsType<ApplicationState.Approved>(decided);
        Assert.Equal(100000m, approved.Amount);
    }

    // DISTINCT CASES: the Declined case carries a reason, not an amount
    [Fact]
    public void Decide_PolicyRejection_MovesToDeclined()
    {
        var app = TestData.App("A3", "40000", "30000", "600");

        var decided = Next(ApplicationTransitions.Decide(Review(app), ApprovalPolicies.Conservative));

        var declined = Assert.IsType<ApplicationState.Declined>(decided);
        Assert.Contains("policy", declined.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // MISSING PREREQUISITE + STATE PRESERVATION
    [Fact]
    public void Decide_OnPending_IsNotAllowed_AndKeepsStateUnchanged()
    {
        var app = TestData.App("A1", "50000", "100000", "700");
        var pending = ApplicationTransitions.Submit(app);
        var expectedCopy = new ApplicationState.Pending(app);

        var outcome = ApplicationTransitions.Decide(pending, ApprovalPolicies.Conservative);

        var refused = Assert.IsType<TransitionOutcome.NotAllowed>(outcome);
        Assert.Contains("eligibility", refused.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedCopy, pending);
        Assert.Equal(700, app.CreditScore);
    }

    // Validation of everything before committing: an invalid policy result produces no new state
    [Fact]
    public void Decide_PolicyReturnsNegativeAmount_IsNotAllowed()
    {
        var app = TestData.App("A1", "50000", "100000", "700");
        var review = Review(app);

        var outcome = ApplicationTransitions.Decide(review, _ => (true, -1m));

        Assert.IsType<TransitionOutcome.NotAllowed>(outcome);
        Assert.IsType<ApplicationState.UnderReview>(review);
    }

    // REPEATED OPERATION: documented rule = return the existing state unchanged
    [Fact]
    public void RepeatedOperations_ReturnExistingState_WithoutChange()
    {
        var approved = Next(ApplicationTransitions.Decide(
            Review(TestData.App("A1", "50000", "100000", "700")), ApprovalPolicies.Conservative));
        var declined = Next(ApplicationTransitions.Decide(
            Review(TestData.App("A3", "40000", "30000", "600")), ApprovalPolicies.Conservative));
        var ineligible = Review(TestData.App("A5", "20000", "5000", "499"));
        var underReview = Review(TestData.App("A2", "30000", "20000", "500"));

        Assert.Same(approved, Next(ApplicationTransitions.Decide(approved, ApprovalPolicies.Conservative)));
        Assert.Same(declined, Next(ApplicationTransitions.Decide(declined, ApprovalPolicies.Flexible)));
        Assert.Same(ineligible, Next(ApplicationTransitions.Decide(ineligible, ApprovalPolicies.Conservative)));
        Assert.Same(underReview, Next(ApplicationTransitions.StartReview(underReview)));
    }
}
