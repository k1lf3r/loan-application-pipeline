using System.Runtime.InteropServices;

namespace LoanApplicationPipeline.Native;

/// <summary>
/// P/Invoke binding for native/loanrisk.c.
/// Library name "loanrisk" is resolved by .NET to loanrisk.dll (Windows), libloanrisk.so (Linux),
/// libloanrisk.dylib (macOS) in the application folder.
/// C:  int64_t lp_monthly_payment(int64_t principal_tenge, int32_t annual_rate_bp, int32_t term_months)
/// C#: long (64-bit) / int (32-bit) / int (32-bit); Cdecl calling convention.
/// </summary>
internal static class NativeMethods
{
    private const string Library = "loanrisk";

    [DllImport(Library, EntryPoint = "lp_monthly_payment", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long MonthlyPayment(long principalTenge, int annualRateBp, int termMonths);
}
