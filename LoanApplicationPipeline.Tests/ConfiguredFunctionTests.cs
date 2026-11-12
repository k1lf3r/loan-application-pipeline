using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Scoring;

namespace LoanApplicationPipeline.Tests;

public class ConfiguredFunctionTests
{
    private static PricedApplication Subject() =>
        Samples.Priced("S1", LoanPurpose.Consumer, 1_000_000, payment: 120_000, credit: 700, income: 400_000);

    [Fact]
    public void PartiallyApplied_Scorers_GiveDifferentResultsOnTheSameInput()
    {
        var app = Subject();

        Assert.Equal(400, ScoringFunctions.Conservative(app));
        Assert.Equal(580, ScoringFunctions.Flexible(app));
        Assert.Equal(ScoringFunctions.Conservative(app), ScoringFunctions.CreateScore(1)(10)(app));
    }

    [Fact]
    public void CurriedFunction_CanBePartiallyApplied_AtAnyStep()
    {
        var withCreditWeight2 = ScoringFunctions.CreateScore(2);
        var ignoreBurden = withCreditWeight2(0);
        var heavyBurden = withCreditWeight2(20);

        Assert.Equal(1400, ignoreBurden(Subject()));
        Assert.Equal(800, heavyBurden(Subject()));
    }

    [Fact]
    public void Policy_BuiltFromDifferentScoreFunctions_DecidesDifferently_WithoutGlobalState()
    {
        var app = Subject();

        var strictFirst = ApprovalPolicies.FromScore(ScoringFunctions.Conservative, 500)(app);
        var tolerant = ApprovalPolicies.FromScore(ScoringFunctions.Flexible, 500)(app);
        var strictAgain = ApprovalPolicies.FromScore(ScoringFunctions.Conservative, 500)(app);

        Assert.IsType<PolicyDecision.Reject>(strictFirst);
        Assert.Equal(1_000_000, Assert.IsType<PolicyDecision.Approve>(tolerant).Amount);
        Assert.Equal(strictFirst, strictAgain);
    }

    [Fact]
    public void Policy_ThresholdBoundary_ExactScoreIsApproved()
    {
        var app = Subject();
        var atBoundary = ApprovalPolicies.FromScore(ScoringFunctions.Flexible, 580)(app);
        var justAbove = ApprovalPolicies.FromScore(ScoringFunctions.Flexible, 581)(app);

        Assert.IsType<PolicyDecision.Approve>(atBoundary);
        Assert.IsType<PolicyDecision.Reject>(justAbove);
    }

    [Fact]
    public void PaymentBurden_SaturatesInsteadOfOverflowing()
    {
        // (1 + 10^12) * 10 000 / 1 = 10^16 overflows int; must saturate instead of wrapping negative.
        var extreme = Samples.Priced("E1", LoanPurpose.Consumer, 1_000, payment: 1, credit: 700, income: 1, debt: 1_000_000_000_000);

        Assert.Equal(int.MaxValue, extreme.PaymentToIncomeBp);
        Assert.True(extreme.BurdenPercent > 0);
    }
}
