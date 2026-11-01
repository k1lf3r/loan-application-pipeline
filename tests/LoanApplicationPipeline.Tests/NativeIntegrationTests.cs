using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Native;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class NativeIntegrationTests
{
    [Fact]
    public void RealNativeCall_ReturnsManuallyCalculatedPayment()
    {
        var calculator = new NativePaymentCalculator();   // real C library, no test double

        // 1 000 000 tenge, 12.00 % a year, 12 months:
        // r = 0.01; payment = 1 000 000 * 0.01 / (1 - 1.01^-12) = 88 848.79 -> 88 849
        Assert.Equal(88_849, Check.Ok(calculator.MonthlyPayment(1_000_000, 1200, 12)));

        // 0 % interest: 1 200 000 / 12 = 100 000 exactly
        Assert.Equal(100_000, Check.Ok(calculator.MonthlyPayment(1_200_000, 0, 12)));
    }

    [Fact]
    public void Adapter_ValidatesDomainInput_BeforeCallingNative()
    {
        var nativeCalls = 0;
        var calculator = new NativePaymentCalculator((_, _, _) => { nativeCalls++; return 1; });

        Assert.Equal(NativeErrorCodes.InvalidInput, Check.FailCode(calculator.MonthlyPayment(0, 1200, 12)));
        Assert.Equal(NativeErrorCodes.InvalidInput, Check.FailCode(calculator.MonthlyPayment(1000, 10_001, 12)));
        Assert.Equal(NativeErrorCodes.InvalidInput, Check.FailCode(calculator.MonthlyPayment(1000, 1200, 601)));
        Assert.Equal(0, nativeCalls);
    }

    [Fact]
    public void Adapter_TurnsLoadAndBindingFailures_IntoResultFailures()
    {
        static Result<long> With(Exception ex) =>
            new NativePaymentCalculator((_, _, _) => throw ex).MonthlyPayment(1_000_000, 1200, 12);

        Assert.Equal(NativeErrorCodes.LoadFailed, Check.FailCode(With(new DllNotFoundException("missing"))));
        Assert.Equal(NativeErrorCodes.EntryPointMissing, Check.FailCode(With(new EntryPointNotFoundException("no export"))));
        Assert.Equal(NativeErrorCodes.WrongArchitecture, Check.FailCode(With(new BadImageFormatException("x86 vs x64"))));
    }

    [Fact]
    public void Adapter_TurnsNegativeNativeCode_IntoRejectedFailure()
    {
        var calculator = new NativePaymentCalculator((_, _, _) => -2);
        Assert.Equal(NativeErrorCodes.Rejected, Check.FailCode(calculator.MonthlyPayment(1_000_000, 1200, 12)));
    }
}
