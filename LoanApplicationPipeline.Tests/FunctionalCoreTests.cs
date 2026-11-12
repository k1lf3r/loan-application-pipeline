using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class FunctionalCoreTests
{
    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var mapped = Res.Ok(42).Map(x => x * 2);

        Assert.Equal(84, Check.Ok(mapped));
    }

    [Fact]
    public void Bind_OnSuccess_ChainsDependentResult()
    {
        var bound = Res.Ok(50).Bind(x => Res.Ok($"Value: {x}"));

        Assert.Equal("Value: 50", Check.Ok(bound));
    }

    [Fact]
    public void Bind_CanTurnSuccessIntoFailure()
    {
        var bound = Res.Ok(-1).Bind(x => x < 0
            ? Res.Fail<int>(new DomainError.ValidationError("x", "negative"))
            : Res.Ok(x));

        Assert.Equal("VALIDATION_ERROR", Check.Code(bound));
    }

    [Fact]
    public void FailurePropagation_MapAndBind_DoNotInvokeSuppliedFunctions()
    {
        var mapCalls = 0;
        var bindCalls = 0;

        var failed = Res.Fail<int>(new DomainError.ValidationError("Field", "Error"));

        var afterMap = failed.Map(x => { mapCalls++; return x * 10; });
        var afterBind = afterMap.Bind(x => { bindCalls++; return Res.Ok(x.ToString()); });

        Assert.True(afterBind.IsFailure);
        Assert.Equal(0, mapCalls);
        Assert.Equal(0, bindCalls);
        Assert.Equal("VALIDATION_ERROR", Check.Code(afterBind));
    }

    [Fact]
    public void MapError_ChangesOnlyTheError_AndLeavesSuccessUntouched()
    {
        var wrapped = Res.Fail<int>(new DomainError.ValidationError("f", "r"))
            .MapError(e => (DomainError)new DomainError.RowRejected(7, e));
        var untouched = Res.Ok(5).MapError(e => (DomainError)new DomainError.RowRejected(7, e));

        var rejected = Assert.IsType<DomainError.RowRejected>(Check.Err(wrapped));
        Assert.Equal(7, rejected.Row);
        Assert.IsType<DomainError.ValidationError>(rejected.Cause);
        Assert.Equal(5, Check.Ok(untouched));
    }

    [Fact]
    public void Option_DistinguishesBetweenZeroAndAbsence()
    {
        var registry = Samples.Registry(Samples.Profile("P001", debt: 0));

        var present = registry.FindById("P001");
        var absent = registry.FindById("NON_EXISTING");
        var blank = registry.FindById("  ");

        Assert.True(present.IsSome);
        var profile = ((Option<ApplicantProfile>.Some)present).Value;
        Assert.Equal(0, profile.ExistingDebtMonthly);
        Assert.True(profile.HasZeroDebt);
        Assert.True(absent.IsNone);
        Assert.True(blank.IsNone);
    }

    [Fact]
    public void Option_ToResult_ConvertsNoneToTheGivenError()
    {
        var none = Option.None<int>().ToResult<DomainError>(new DomainError.ApplicantNotFound("X"));
        var some = Option.Some(3).ToResult<DomainError>(new DomainError.ApplicantNotFound("X"));

        Assert.Equal("APPLICANT_NOT_FOUND", Check.Code(none));
        Assert.Equal(3, Check.Ok(some));
    }
}
