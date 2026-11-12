/* Stand-alone check of the native library, no .NET needed.
 * Build + run (Linux/macOS):  sh native/build.sh && gcc -std=c99 -Inative native/selftest.c -Lnative/bin -lloanrisk -lm -o native/bin/selftest && LD_LIBRARY_PATH=native/bin native/bin/selftest
 * Expected output: "native selftest OK" and exit code 0. */
#include <stdio.h>
#include "loanrisk.h"

static int failures = 0;

static void expect(const char *label, int64_t actual, int64_t expected)
{
    if (actual != expected) {
        printf("FAIL %s: expected %lld, got %lld\n", label, (long long)expected, (long long)actual);
        failures++;
    }
}

int main(void)
{
    /* manual example: 1 000 000 tenge, 12.00 % a year, 12 months -> 88 848.79 -> 88 849 */
    expect("annuity 12 %/12m", lp_monthly_payment(1000000, 1200, 12), 88849);
    /* 0 % interest: 1 200 000 / 12 = 100 000 exactly */
    expect("zero rate", lp_monthly_payment(1200000, 0, 12), 100000);
    /* invalid input is reported with a negative code */
    expect("bad principal", lp_monthly_payment(0, 1200, 12), LP_ERR_PRINCIPAL);
    expect("bad rate", lp_monthly_payment(1000, 10001, 12), LP_ERR_RATE);
    expect("bad term", lp_monthly_payment(1000, 1200, 601), LP_ERR_TERM);

    if (failures == 0) printf("native selftest OK\n");
    return failures == 0 ? 0 : 1;
}
