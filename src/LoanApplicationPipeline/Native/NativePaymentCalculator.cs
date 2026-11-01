using LoanApplicationPipeline.Common;

namespace LoanApplicationPipeline.Native;

public static class NativeErrorCodes
{
    public const string InvalidInput = "native.invalid_input";    // domain check before the call
    public const string Rejected = "native.rejected_input";       // C function returned a negative code
    public const string LoadFailed = "native.load_failed";        // library file not found / not loadable
    public const string EntryPointMissing = "native.entry_point_missing";
    public const string WrongArchitecture = "native.wrong_architecture";

    /// <summary>Loading/binding problems affect every record, so the workflow must stop.</summary>
    public static bool IsFatal(string code) =>
        code is LoadFailed or EntryPointMissing or WrongArchitecture;
}

public interface IPaymentCalculator
{
    Result<long> MonthlyPayment(long principalTenge, int annualRateBp, int termMonths);
}

/// <summary>
/// Adapter around the native call. Validates domain input first, then calls C, then turns loading
/// and binding failures into explicit Result failures at this boundary.
/// The native function uses only simple values, so no native memory is allocated and nothing needs freeing.
/// </summary>
public sealed class NativePaymentCalculator : IPaymentCalculator
{
    private readonly Func<long, int, int, long> _nativeCall;

    /// <summary>Production: calls the real C library.</summary>
    public NativePaymentCalculator() : this(NativeMethods.MonthlyPayment) { }

    /// <summary>Tests: inject a call that fails, to check the loading-failure path in-process.</summary>
    public NativePaymentCalculator(Func<long, int, int, long> nativeCall)
    {
        _nativeCall = nativeCall ?? throw new ArgumentNullException(nameof(nativeCall));
    }

    public Result<long> MonthlyPayment(long principalTenge, int annualRateBp, int termMonths)
    {
        if (principalTenge is < 1 or > 1_000_000_000_000L)
            return Result<long>.Failure(NativeErrorCodes.InvalidInput, "Principal must be 1..1 000 000 000 000 tenge.");
        if (annualRateBp is < 0 or > 10_000)
            return Result<long>.Failure(NativeErrorCodes.InvalidInput, "Rate must be 0..10000 basis points.");
        if (termMonths is < 1 or > 600)
            return Result<long>.Failure(NativeErrorCodes.InvalidInput, "Term must be 1..600 months.");

        try
        {
            var value = _nativeCall(principalTenge, annualRateBp, termMonths);
            return value >= 0
                ? Result<long>.Success(value)
                : Result<long>.Failure(NativeErrorCodes.Rejected, $"Native library rejected the input (code {value}).");
        }
        catch (DllNotFoundException ex)
        {
            return Result<long>.Failure(NativeErrorCodes.LoadFailed, $"Native library 'loanrisk' could not be loaded: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result<long>.Failure(NativeErrorCodes.EntryPointMissing, $"Function 'lp_monthly_payment' not found: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            return Result<long>.Failure(NativeErrorCodes.WrongArchitecture, $"Native library has the wrong architecture: {ex.Message}");
        }
    }
}
