using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Queries;
using static LoanApplicationPipeline.Tests.Samples;

namespace LoanApplicationPipeline.Tests;

public class ReportAndExtensionTests
{
    [Fact]
    public void ReviewQueue_ReturnsOnlyLowScores_OrderedByScoreThenId()
    {
        var rows = ApplicationReports.ReviewQueue(Portfolio(), ByCredit, threshold: 650);

        Assert.Equal(new[] { "X1", "X3", "X4" }, rows.Select(r => r.Id));
        Assert.Equal(new[] { 600, 600, 640 }, rows.Select(r => r.Score));
        Assert.Equal(18, rows[0].BurdenPercent);
        Assert.Equal("Name P-X1", rows[0].Name);
    }

    [Fact]
    public void ReviewQueue_TiedScores_AreOrderedDeterministically_RegardlessOfInputOrder()
    {
        var forward = ApplicationReports.ReviewQueue(Portfolio(), ByCredit, 650).Select(r => r.Id).ToArray();
        var backward = ApplicationReports.ReviewQueue(Enumerable.Reverse(Portfolio()), ByCredit, 650).Select(r => r.Id).ToArray();

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void ReviewQueue_EmptyInput_AndNoMatches_ReturnEmpty()
    {
        Assert.Empty(ApplicationReports.ReviewQueue([], ByCredit, 650));
        Assert.Empty(ApplicationReports.ReviewQueue(Portfolio(), ByCredit, threshold: 0));
    }

    [Fact]
    public void ApprovedByPurpose_GroupsAndTotals_TiedTotalsOrderedByPurpose()
    {
        var rows = ApplicationReports.ApprovedByPurpose(Portfolio(), ByCredit, threshold: 600);

        Assert.Equal(new[] { LoanPurpose.Education, LoanPurpose.Housing, LoanPurpose.Consumer }, rows.Select(r => r.Purpose));
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

    [Fact]
    public void Scored_IsLazy_AndProjectsScores()
    {
        var calls = 0;
        var query = Portfolio().Scored(p => { calls++; return p.Loan.CreditScore; });

        Assert.Equal(0, calls);
        var list = query.ToList();
        Assert.Equal(4, calls);
        Assert.Equal(new[] { 640, 600, 700, 600 }, list.Select(s => s.Score));
    }

    [Fact]
    public void TotalRequested_SumsAmounts_EmptyIsZero_AndDoesNotChangeSource()
    {
        var portfolio = Portfolio();

        Assert.Equal(4_500_000, portfolio.TotalRequested());
        Assert.Equal(4_500_000, portfolio.TotalRequested());
        Assert.Equal(0, Array.Empty<PricedApplication>().TotalRequested());
    }

    [Fact]
    public void Partition_SplitsValuesFromErrors_InSourceOrder()
    {
        var results = new[]
        {
            Res.Ok(1),
            Res.Fail<int>(new DomainError.ValidationError("a", "x")),
            Res.Ok(3),
            Res.Fail<int>(new DomainError.ValidationError("b", "y"))
        };

        var (values, errors) = results.Partition();

        Assert.Equal(new[] { 1, 3 }, values);
        Assert.Equal(new[] { "a", "b" }, errors.Cast<DomainError.ValidationError>().Select(e => e.Field));
    }

    [Fact]
    public void Partition_EmptySequence_GivesTwoEmptyLists_AndEnumeratesSourceOnce()
    {
        var (values, errors) = Array.Empty<Result<int, DomainError>>().Partition();
        Assert.Empty(values);
        Assert.Empty(errors);

        var enumerations = 0;
        IEnumerable<Result<int, DomainError>> Counting()
        {
            enumerations++;
            yield return Res.Ok(1);
        }

        _ = Counting().Partition();
        Assert.Equal(1, enumerations);
    }
}
