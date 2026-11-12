using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Native;

namespace LoanApplicationPipeline.Tests;

public class NativeIntegrationTests
{
    [Fact]
    public void RealNativeCall_ReturnsManuallyCalculatedPayment()
    {
        var calculator = new NativePaymentCalculator();

        // 1 000 000 tenge, 12.00%/yr, 12 mo: r = 0.01; payment = 1 000 000 * 0.01 / (1 - 1.01^-12) = 88 848.79 -> 88 849
        Assert.Equal(88_849, Check.Ok(calculator.MonthlyPayment(1_000_000, 1200, 12)));

        Assert.Equal(100_000, Check.Ok(calculator.MonthlyPayment(1_200_000, 0, 12)));
    }

    [Fact]
    public void Adapter_ValidatesDomainInput_BeforeCallingNative()
    {
        var nativeCalls = 0;
        var calculator = new NativePaymentCalculator((_, _, _) => { nativeCalls++; return 1; });

        foreach (var result in new[]
        {
            calculator.MonthlyPayment(0, 1200, 12),
            calculator.MonthlyPayment(1000, 10_001, 12),
            calculator.MonthlyPayment(1000, 1200, 601)
        })
        {
            var error = Assert.IsType<DomainError.NativeCallFailed>(Check.Err(result));
            Assert.Equal(NativeErrorCodes.InvalidInput, error.NativeCode);
        }

        Assert.Equal(0, nativeCalls);
    }

    [Fact]
    public void Adapter_TurnsLoadAndBindingFailures_IntoResultFailures()
    {
        static string CodeFor(Exception ex) =>
            Assert.IsType<DomainError.NativeCallFailed>(
                Check.Err(new NativePaymentCalculator((_, _, _) => throw ex).MonthlyPayment(1_000_000, 1200, 12))).NativeCode;

        Assert.Equal(NativeErrorCodes.LoadFailed, CodeFor(new DllNotFoundException("missing")));
        Assert.Equal(NativeErrorCodes.EntryPointMissing, CodeFor(new EntryPointNotFoundException("no export")));
        Assert.Equal(NativeErrorCodes.WrongArchitecture, CodeFor(new BadImageFormatException("x86 vs x64")));
    }

    [Fact]
    public void Adapter_TurnsNegativeNativeCode_IntoRejectedFailure()
    {
        var calculator = new NativePaymentCalculator((_, _, _) => -2);

        var error = Assert.IsType<DomainError.NativeCallFailed>(Check.Err(calculator.MonthlyPayment(1_000_000, 1200, 12)));

        Assert.Equal(NativeErrorCodes.Rejected, error.NativeCode);
        Assert.False(NativeErrorCodes.IsFatal(error.NativeCode));
    }

    [Theory]
    [InlineData(NativeErrorCodes.LoadFailed, true)]
    [InlineData(NativeErrorCodes.EntryPointMissing, true)]
    [InlineData(NativeErrorCodes.WrongArchitecture, true)]
    [InlineData(NativeErrorCodes.Rejected, false)]
    [InlineData(NativeErrorCodes.InvalidInput, false)]
    public void FatalCodes_AreExactlyTheLoadingAndBindingProblems(string code, bool expectedFatal)
    {
        Assert.Equal(expectedFatal, NativeErrorCodes.IsFatal(code));
    }
}
