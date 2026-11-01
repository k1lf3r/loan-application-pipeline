using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Domain;
using LoanApplicationPipeline.Queries;
using LoanApplicationPipeline.Scoring;
using Xunit;
using static LoanApplicationPipeline.Tests.Samples;

namespace LoanApplicationPipeline.Tests;

public class ReportAndExtensionTests
{
    // ---------- REPORT 1: review queue ----------
    [Fact]
    public void ReviewQueue_ReturnsOnlyLowScores_OrderedByScoreThenId()
    {
        // Input order is deliberately reversed. X1 and X3 tie on score 600.
        var rows = ApplicationReports.ReviewQueue(Portfolio(), ByCredit, threshold: 650);

        Assert.Equal(new[] { "X1", "X3", "X4" }, rows.Select(r => r.Id));
        Assert.Equal(new[] { 600, 600, 640 }, rows.Select(r => r.Score));
        Assert.Equal(18, rows[0].BurdenPercent); // (90 000 + 0) / 500 000 = 18 %
    }

    [Fact]
    public void ReviewQueue_EmptyInput_AndNoMatches_ReturnEmpty()
    {
        Assert.Empty(ApplicationReports.ReviewQueue([], ByCredit, 650));
        Assert.Empty(ApplicationReports.ReviewQueue(Portfolio(), ByCredit, threshold: 0));
    }

    // ---------- REPORT 2: approved by purpose ----------
    [Fact]
    public void ApprovedByPurpose_GroupsAndTotals_TiedTotalsOrderedByPurpose()
    {
        var rows = ApplicationReports.ApprovedByPurpose(Portfolio(), ByCredit, threshold: 600);

        Assert.Equal(new[] { LoanPurpose.Education, LoanPurpose.Housing, LoanPurpose.Consumer },
                     rows.Select(r => r.Purpose));
        Assert.Equal(new PurposeTotalRow(LoanPurpose.Education, 2, 2_000_000, 175_000), rows[0]);
        Assert.Equal(new PurposeTotalRow(LoanPurpose.Housing, 1, 2_000_000, 40_000), rows[1]);
        Assert.Equal(new PurposeTotalRow(LoanPurpose.Consumer, 1, 500_000, 20_000), rows[2]);
    }

    [Fact]
    public void ApprovedByPurpose_FiltersByThreshold_EmptyAndNoMatches()
    {
        var onlyHousing = ApplicationReports.ApprovedByPurpose(Portfolio(), ByCredit, threshold: 650);
        Assert.Single(onlyHousing);
        Assert.Equal(LoanPurpose.Housing, onlyHousing[0].Purpose);

        Assert.Empty(ApplicationReports.ApprovedByPurpose([], ByCredit, 600));
        Assert.Empty(ApplicationReports.ApprovedByPurpose(Portfolio(), ByCredit, threshold: 1000));
    }

    // ---------- EXTENSIONS ----------
    [Fact]
    public void Scored_IsLazy_AndProjectsScores()
    {
        var calls = 0;
        var query = Portfolio().Scored(p => { calls++; return p.Application.CreditScore; });

        Assert.Equal(0, calls);                      // building the query ran nothing
        var list = query.ToList();
        Assert.Equal(4, calls);                      // iteration ran the score function once per item
        Assert.Equal(new[] { 640, 600, 700, 600 }, list.Select(s => s.Score));
    }

    [Fact]
    public void TotalRequested_SumsAmounts_EmptyIsZero_AndDoesNotChangeSource()
    {
        var portfolio = Portfolio();
        Assert.Equal(4_500_000, portfolio.TotalRequested());
        Assert.Equal(4_500_000, portfolio.TotalRequested()); // repeatable: no state changed
        Assert.Equal(0, Array.Empty<PricedApplication>().TotalRequested());
    }

    [Fact]
    public void Bind_ChainsSuccess_AndSkipsNextOnFailure()
    {
        var chained = Result<int>.Success(2).Bind(x => Result<string>.Success($"v{x}"));
        Assert.Equal("v2", Check.Ok(chained));

        var called = false;
        var failed = Result<int>.Failure("test.code", "boom").Bind(x => { called = true; return Result<string>.Success("never"); });
        Assert.Equal("test.code", Check.FailCode(failed));
        Assert.False(called);
    }

    // ---------- CONFIGURED (CURRIED) FUNCTIONS ----------
    [Fact]
    public void PartiallyApplied_Scorers_GiveDifferentResultsOnSameInput()
    {
        // credit 700, payment 120 000 / income 400 000 = 30 % burden
        var app = Priced("S1", LoanPurpose.Consumer, 1_000_000, payment: 120_000, credit: 700, income: 400_000);

        Assert.Equal(400, ScoringFunctions.Conservative(app));   // 700 - 30 * 10
        Assert.Equal(580, ScoringFunctions.Flexible(app));       // 700 - 30 * 4
        Assert.Equal(ScoringFunctions.Conservative(app), ScoringFunctions.CreateScore(1)(10)(app)); // same as full currying
    }

    [Fact]
    public void ConfiguredFunction_ChangesReportOutcome_WithoutGlobalState()
    {
        var app = Priced("S1", LoanPurpose.Consumer, 1_000_000, payment: 120_000, credit: 700, income: 400_000);

        var strict = ApplicationReports.ReviewQueue([app], ScoringFunctions.Conservative, 500);
        var tolerant = ApplicationReports.ReviewQueue([app], ScoringFunctions.Flexible, 500);

        Assert.Single(strict);
        Assert.Empty(tolerant);
    }

    // ---------- DOMAIN VALIDATION ----------
    [Theory]
    [InlineData("", "N", 1000, 12, 1000, 500000, 700, 0, "domain.invalid_id")]
    [InlineData("A", " ", 1000, 12, 1000, 500000, 700, 0, "domain.invalid_name")]
    [InlineData("A", "N", 0, 12, 1000, 500000, 700, 0, "domain.invalid_amount")]
    [InlineData("A", "N", 1000, 601, 1000, 500000, 700, 0, "domain.invalid_term")]
    [InlineData("A", "N", 1000, 12, 10001, 500000, 700, 0, "domain.invalid_rate")]
    [InlineData("A", "N", 1000, 12, 1000, 0, 700, 0, "domain.invalid_income")]
    [InlineData("A", "N", 1000, 12, 1000, 500000, 299, 0, "domain.invalid_credit_score")]
    [InlineData("A", "N", 1000, 12, 1000, 500000, 700, -1, "domain.invalid_debt")]
    public void Create_RejectsInvalidValues_WithSpecificCode(
        string id, string name, long amount, int term, int rate, long income, int credit, long debt, string code)
    {
        var result = LoanApplication.Create(id, name, LoanPurpose.Housing, amount, term, rate, income, credit, debt);
        Assert.Equal(code, Check.FailCode(result));
    }

    [Fact]
    public void Create_RejectsUnknownPurpose_AndAcceptsValidInput()
    {
        Assert.Equal("domain.invalid_purpose",
            Check.FailCode(LoanApplication.Create("A", "N", (LoanPurpose)99, 1000, 12, 1000, 500000, 700, 0)));
        Assert.Equal("A", Check.Ok(LoanApplication.Create(" A ", "N", LoanPurpose.Housing, 1000, 12, 1000, 500000, 700, 0)).Id);
    }
}
