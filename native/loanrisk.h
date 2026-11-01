#ifndef LOANRISK_H
#define LOANRISK_H

#include <stdint.h>

#if defined(_WIN32)
#  define LOANRISK_API __declspec(dllexport)
#else
#  define LOANRISK_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Error codes returned instead of a payment (always negative). */
#define LP_ERR_PRINCIPAL (-1)
#define LP_ERR_RATE      (-2)
#define LP_ERR_TERM      (-3)

/*
 * Fixed-rate annuity payment.
 *   principal_tenge : loan amount in whole tenge, valid 1 .. 1_000_000_000_000
 *   annual_rate_bp  : annual interest rate in basis points (1 bp = 0.01 %), valid 0 .. 10000
 *   term_months     : number of monthly payments, valid 1 .. 600
 * Returns the monthly payment in whole tenge, rounded half away from zero,
 * or a negative LP_ERR_* code when an argument is outside its valid range.
 */
LOANRISK_API int64_t lp_monthly_payment(int64_t principal_tenge,
                                        int32_t annual_rate_bp,
                                        int32_t term_months);

#ifdef __cplusplus
}
#endif

#endif /* LOANRISK_H */
