using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class FunctionalCoreTests
{
    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var result = Result<int, DomainError>.Success(42);
        var mapped = result.Map(x => x * 2);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(84, ((Result<int, DomainError>.Ok)mapped).Value);
    }

    [Fact]
    public void Bind_OnSuccess_ChainsDependentResult()
    {
        var result = Result<int, DomainError>.Success(50);
        var bound = result.Bind(x => Result<string, DomainError>.Success($"Value: {x}"));

        Assert.True(bound.IsSuccess);
        Assert.Equal("Value: 50", ((Result<string, DomainError>.Ok)bound).Value);
    }

    [Fact]
    public void FailurePropagation_MapAndBind_DoNotInvokeSuppliedFunctions()
    {
        int mapCalls = 0;
        int bindCalls = 0;

        var failed = Result<int, DomainError>.Failure(new DomainError.ValidationError("Field", "Error"));

        var afterMap = failed.Map(x =>
        {
            mapCalls++;
            return x * 10;
        });

        var afterBind = afterMap.Bind(x =>
        {
            bindCalls++;
            return Result<string, DomainError>.Success(x.ToString());
        });

        Assert.True(afterBind.IsFailure);
        Assert.Equal(0, mapCalls);
        Assert.Equal(0, bindCalls);
    }

    [Fact]
    public void Option_DistinguishesBetweenZeroAndAbsence()
    {
        var registry = new ApplicantRegistry();

        var present = registry.FindById("A1");
        var absent = registry.FindById("NON_EXISTING");

        Assert.True(present.IsSome);
        var profile = ((Option<ApplicantProfile>.Some)present).Value;
        Assert.Equal(0m, profile.ExistingDebtAmount);
        Assert.True(profile.HasZeroDebt);

        Assert.True(absent.IsNone);
    }
}