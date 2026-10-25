using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;

namespace LoanApplicationPipeline.Tests;

public class FactoryAndTemporalTests
{
    [Fact]
    public void FactorySuccess_ValidInput_CreatesValidatedApplication()
    {
        var raw = new RawApplicationRecord("A1", "50000", "100000", "700", "false");

        var result = ValidatedApplication.Create(raw);

        Assert.True(result.IsSuccess);
        var app = ((Result<ValidatedApplication, DomainError>.Ok)result).Value;
        Assert.Equal("A1", app.ApplicantId);
        Assert.Equal(50000m, app.MonthlyIncome);
        Assert.Equal(100000m, app.RequestedAmount);
        Assert.Equal(700, app.CreditScore);
        Assert.False(app.HasExistingDebt);
    }

    [Theory]
    [InlineData("", "50000", "10000", "700", "false", "ApplicantId")]
    [InlineData("A1", "-100", "10000", "700", "false", "MonthlyIncome")]
    [InlineData("A1", "50000", "0", "700", "false", "RequestedAmount")]
    [InlineData("A1", "50000", "10000", "299", "false", "CreditScore")]
    [InlineData("A1", "50000", "10000", "851", "false", "CreditScore")]
    [InlineData("A1", "50000", "10000", "700", "not_a_bool", "HasExistingDebt")]
    public void FactoryRejection_InvalidRules_RejectWithSpecificReason(
        string id, string income, string amount, string score, string debt, string expectedField)
    {
        var raw = new RawApplicationRecord(id, income, amount, score, debt);

        var result = ValidatedApplication.Create(raw);

        Assert.True(result.IsFailure);
        var err = ((Result<ValidatedApplication, DomainError>.Err)result).Error;
        var validationErr = Assert.IsType<DomainError.ValidationError>(err);
        Assert.Equal(expectedField, validationErr.Field);
    }

    [Fact]
    public void RepeatedOperation_OnFinalizedState_IsSafelyRejected()
    {
        var raw = new RawApplicationRecord("A1", "50000", "10000", "700", "false");
        var app = ((Result<ValidatedApplication, DomainError>.Ok)ValidatedApplication.Create(raw)).Value;
        var approvedState = new LoanWorkflowState.Approved(app, 10000m, "Flexible");

        var repeatResult = ApplicationStages.ApplyDecision(approvedState, ApprovalPolicies.Flexible, "Flexible");

        Assert.True(repeatResult.IsFailure);
        var err = ((Result<LoanWorkflowState, DomainError>.Err)repeatResult).Error;
        var transitionErr = Assert.IsType<DomainError.InvalidStateTransition>(err);
        Assert.Equal("Approved", transitionErr.CurrentState);
    }
}