using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;

namespace LoanApplicationPipeline.Tests;

public class FactoryAndTemporalTests
{
    [Fact]
    public void FactorySuccess_ValidInput_CreatesValidatedApplication_WithTrimmedValues()
    {
        var raw = Samples.Raw(id: " A1 ", applicantId: " P001 ", purpose: "housing", amount: " 5000000 ");

        var app = Check.Ok(ValidatedApplication.Create(raw));

        Assert.Equal("A1", app.Id);
        Assert.Equal("P001", app.ApplicantId);
        Assert.Equal(LoanPurpose.Housing, app.Purpose);
        Assert.Equal(5_000_000, app.RequestedAmount);
        Assert.Equal(12, app.TermMonths);
        Assert.Equal(1200, app.AnnualRateBp);
        Assert.Equal(400_000, app.MonthlyIncome);
        Assert.Equal(700, app.CreditScore);
    }

    [Theory]
    [InlineData("", "P001", "Consumer", "1000", "12", "1200", "400000", "700", "Id")]
    [InlineData("A1", " ", "Consumer", "1000", "12", "1200", "400000", "700", "ApplicantId")]
    [InlineData("A1", "P001", "Spaceship", "1000", "12", "1200", "400000", "700", "Purpose")]
    [InlineData("A1", "P001", "1", "1000", "12", "1200", "400000", "700", "Purpose")]
    [InlineData("A1", "P001", "Consumer", "abc", "12", "1200", "400000", "700", "RequestedAmount")]
    [InlineData("A1", "P001", "Consumer", "0", "12", "1200", "400000", "700", "RequestedAmount")]
    [InlineData("A1", "P001", "Consumer", "1000", "0", "1200", "400000", "700", "TermMonths")]
    [InlineData("A1", "P001", "Consumer", "1000", "601", "1200", "400000", "700", "TermMonths")]
    [InlineData("A1", "P001", "Consumer", "1000", "12", "-1", "400000", "700", "AnnualRateBp")]
    [InlineData("A1", "P001", "Consumer", "1000", "12", "10001", "400000", "700", "AnnualRateBp")]
    [InlineData("A1", "P001", "Consumer", "1000", "12", "1200", "0", "700", "MonthlyIncome")]
    [InlineData("A1", "P001", "Consumer", "1000", "12", "1200", "400000", "299", "CreditScore")]
    [InlineData("A1", "P001", "Consumer", "1000", "12", "1200", "400000", "851", "CreditScore")]
    [InlineData("A1", "P001", "Consumer", "1000", "12", "1200", "400000", "7x0", "CreditScore")]
    public void FactoryRejection_InvalidRules_RejectWithSpecificField(
        string id, string applicantId, string purpose, string amount, string term, string rate,
        string income, string credit, string expectedField)
    {
        var result = ValidatedApplication.Create(
            new RawApplicationRecord(id, applicantId, purpose, amount, term, rate, income, credit));

        var error = Assert.IsType<DomainError.ValidationError>(Check.Err(result));
        Assert.Equal(expectedField, error.Field);
    }

    [Fact]
    public void FactoryRejection_MissingFields_AreValidationErrors_NotExceptions()
    {
        var allNull = new RawApplicationRecord(null, null, null, null, null, null, null, null);
        var missingAmount = Samples.Raw() with { AmountText = null };

        Assert.IsType<DomainError.ValidationError>(Check.Err(ValidatedApplication.Create(allNull)));
        Assert.Equal("RequestedAmount",
            Assert.IsType<DomainError.ValidationError>(Check.Err(ValidatedApplication.Create(missingAmount))).Field);
    }

    [Theory]
    [InlineData("1", "1", "0", "300", "1")]
    [InlineData("1000000000000", "600", "10000", "850", "1")]
    public void FactoryBoundaries_ExactLimits_AreAccepted(string amount, string term, string rate, string credit, string income)
    {
        var raw = Samples.Raw(amount: amount, term: term, rate: rate, credit: credit, income: income);

        Assert.True(ValidatedApplication.Create(raw).IsSuccess);
    }

    [Fact]
    public void RepeatedOperation_OnFinalizedState_IsSafelyRejected()
    {
        var priced = new PricedApplication(Samples.Validated(), Samples.Profile(), 88_849);
        var approved = new LoanWorkflowState.Approved(priced, 1_000_000, "Flexible");
        var rejected = new LoanWorkflowState.Rejected(priced, "no", "Flexible");

        var again = ApplicationStages.ApplyDecision(approved, ApprovalPolicies.Flexible, "Flexible");
        var againRejected = ApplicationStages.ApplyDecision(rejected, ApprovalPolicies.Flexible, "Flexible");

        var error = Assert.IsType<DomainError.InvalidStateTransition>(Check.Err(again));
        Assert.Equal("Approved", error.CurrentState);
        Assert.Equal("Rejected", Assert.IsType<DomainError.InvalidStateTransition>(Check.Err(againRejected)).CurrentState);
    }

    [Fact]
    public void DecisionBeforePricing_IsRejected_ButTheLegitimateOrderWorks()
    {
        var application = Samples.Validated();
        var pending = new LoanWorkflowState.PendingReview(application, Samples.Profile());
        var policyCalls = 0;
        ApprovalPolicy counting = _ => { policyCalls++; return new PolicyDecision.Approve(1); };

        var tooEarly = ApplicationStages.ApplyDecision(pending, counting, "Counting");
        Assert.Equal("PendingReview", Assert.IsType<DomainError.InvalidStateTransition>(Check.Err(tooEarly)).CurrentState);
        Assert.Equal(0, policyCalls);

        var priced = new LoanWorkflowState.Priced(new PricedApplication(application, Samples.Profile(), 88_849));
        var decided = ApplicationStages.ApplyDecision(priced, counting, "Counting");
        Assert.IsType<LoanWorkflowState.Approved>(Check.Ok(decided));
        Assert.Equal(1, policyCalls);
    }
}
