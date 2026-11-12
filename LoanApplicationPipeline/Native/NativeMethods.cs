using System.Runtime.InteropServices;

namespace LoanApplicationPipeline.Native;

// P/Invoke ABI contract for native/loanrisk.c (loanrisk.dll / libloanrisk.so / libloanrisk.dylib):
// C: int64_t lp_monthly_payment(int64_t principal_tenge, int32_t annual_rate_bp, int32_t term_months)
// C#: long (64-bit), int (32-bit), int (32-bit); Cdecl; pure value types (no native heap allocation).
internal static class NativeMethods
{
    private const string Library = "loanrisk";

    [DllImport(Library, EntryPoint = "lp_monthly_payment", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long MonthlyPayment(long principalTenge, int annualRateBp, int termMonths);
}
