using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Pipeline;

namespace LoanApplicationPipeline.Native;

public static class NativeErrorCodes
{
    public const string InvalidInput = "native.invalid_input";
    public const string Rejected = "native.rejected_input";
    public const string LoadFailed = "native.load_failed";
    public const string EntryPointMissing = "native.entry_point_missing";
    public const string WrongArchitecture = "native.wrong_architecture";

    // Fatal load/binding failures affect every record and must abort batch workflows.
    public static bool IsFatal(string code) =>
        code is LoadFailed or EntryPointMissing or WrongArchitecture;
}

public sealed class NativePaymentCalculator : IPaymentCalculator
{
    private readonly Func<long, int, int, long> _nativeCall;

    public NativePaymentCalculator() : this(NativeMethods.MonthlyPayment) { }

    public NativePaymentCalculator(Func<long, int, int, long> nativeCall)
    {
        _nativeCall = nativeCall ?? throw new ArgumentNullException(nameof(nativeCall));
    }

    public Result<long, DomainError> MonthlyPayment(long principalTenge, int annualRateBp, int termMonths)
    {
        if (principalTenge < 1 || principalTenge > 1_000_000_000_000L)
            return Fail(NativeErrorCodes.InvalidInput, "Principal must be 1..1 000 000 000 000 tenge.");
        if (annualRateBp < 0 || annualRateBp > 10_000)
            return Fail(NativeErrorCodes.InvalidInput, "Rate must be 0..10000 basis points.");
        if (termMonths < 1 || termMonths > 600)
            return Fail(NativeErrorCodes.InvalidInput, "Term must be 1..600 months.");

        try
        {
            var value = _nativeCall(principalTenge, annualRateBp, termMonths);

            if (value < 0)
                return Fail(NativeErrorCodes.Rejected, $"Native library rejected the input (code {value}).");

            return Res.Ok(value);
        }
        catch (DllNotFoundException ex)
        {
            return Fail(NativeErrorCodes.LoadFailed, $"Native library 'loanrisk' could not be loaded: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            return Fail(NativeErrorCodes.EntryPointMissing, $"Function 'lp_monthly_payment' not found: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            return Fail(NativeErrorCodes.WrongArchitecture, $"Native library has the wrong architecture: {ex.Message}");
        }
    }

    private static Result<long, DomainError> Fail(string code, string details) =>
        Res.Fail<long>(new DomainError.NativeCallFailed(code, details));
}
