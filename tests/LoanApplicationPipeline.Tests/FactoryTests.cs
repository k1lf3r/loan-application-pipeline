using LoanApplicationPipeline.Models;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class FactoryTests
{
    // FACTORY SUCCESS
    [Fact]
    public void Create_ValidInput_ReturnsValidWithExpectedData()
    {
        var raw = new RawApplicationRecord("A1", "50000", "100000", "700", "false");

        var outcome = ValidatedApplication.Create(raw);

        var valid = Assert.IsType<ValidationOutcome.Valid>(outcome);
        Assert.Equal("A1", valid.Application.ApplicantId);
        Assert.Equal(50000m, valid.Application.MonthlyIncome);
        Assert.Equal(100000m, valid.Application.RequestedAmount);
        Assert.Equal(700, valid.Application.CreditScore);
        Assert.False(valid.Application.HasExistingDebt);
    }

    // FACTORY REJECTION: every rule rejects invalid input and reports the reason
    [Theory]
    [InlineData(null, "10000", "5000", "700", "false", "ApplicantId is missing")]
    [InlineData("A6", null, "5000", "600", "false", "MonthlyIncome is missing")]
    [InlineData("B1", "abc", "5000", "700", "false", "MonthlyIncome 'abc' is not a number")]
    [InlineData("A9", "-5000", "100", "700", "false", "MonthlyIncome cannot be negative")]
    [InlineData("B2", "10000", "0", "700", "false", "RequestedAmount must be greater than zero")]
    [InlineData("B3", "10000", "-5", "700", "false", "RequestedAmount must be greater than zero")]
    [InlineData("A7", "10000", "5000", "abc", "false", "CreditScore 'abc' is not an integer")]
    [InlineData("A8", "10000", "5000", "900", "false", "outside the range [300, 850]")]
    [InlineData("B4", "10000", "5000", "700", "maybe", "HasExistingDebt 'maybe' must be 'true' or 'false'")]
    public void Create_InvalidInput_ReturnsInvalidWithReason(
        string? id, string? income, string? amount, string? score, string? debt, string expectedReason)
    {
        var outcome = ValidatedApplication.Create(new RawApplicationRecord(id, income, amount, score, debt));

        var invalid = Assert.IsType<ValidationOutcome.Invalid>(outcome);
        Assert.Contains(invalid.Reasons, r => r.Contains(expectedReason));
    }

    // BOUNDARY VALUE: exact limits of the credit score range
    [Fact]
    public void Create_CreditScoreBoundaries_FollowRange()
    {
        Assert.IsType<ValidationOutcome.Valid>(Create("300"));
        Assert.IsType<ValidationOutcome.Valid>(Create("850"));
        Assert.IsType<ValidationOutcome.Invalid>(Create("299"));
        Assert.IsType<ValidationOutcome.Invalid>(Create("851"));

        static ValidationOutcome Create(string score)
            => ValidatedApplication.Create(new RawApplicationRecord("B", "1000", "500", score, "false"));
    }

    [Fact]
    public void Create_MultipleBrokenFields_ReportsAllReasons()
    {
        var raw = new RawApplicationRecord("X", "-1", "0", "900", "maybe");

        var invalid = Assert.IsType<ValidationOutcome.Invalid>(ValidatedApplication.Create(raw));

        Assert.Equal(4, invalid.Reasons.Count);
    }

    [Fact]
    public void Create_NormalisesAmountsToTwoDecimals()
    {
        var app = TestData.App("N", "1000.126", "500.004", "700");

        Assert.Equal(1000.13m, app.MonthlyIncome);
        Assert.Equal(500.00m, app.RequestedAmount);
    }

    // STATE PRESERVATION: a failed construction leaves the input unchanged
    [Fact]
    public void Create_FailedConstruction_LeavesRawRecordUnchanged()
    {
        var raw = new RawApplicationRecord("A9", "-5000", "100", "700", "false");
        var copy = raw with { };

        var outcome = ValidatedApplication.Create(raw);

        Assert.IsType<ValidationOutcome.Invalid>(outcome);
        Assert.Equal(copy, raw);
    }
}
