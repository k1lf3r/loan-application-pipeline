#include "loanrisk.h"
#include <math.h>

LOANRISK_API int64_t lp_monthly_payment(int64_t principal_tenge,
                                        int32_t annual_rate_bp,
                                        int32_t term_months)
{
    if (principal_tenge < 1 || principal_tenge > 1000000000000LL) return LP_ERR_PRINCIPAL;
    if (annual_rate_bp < 0 || annual_rate_bp > 10000)             return LP_ERR_RATE;
    if (term_months < 1 || term_months > 600)                     return LP_ERR_TERM;

    if (annual_rate_bp == 0) {
        /* No interest: equal split, rounded to the nearest tenge. */
        return (int64_t)llround((double)principal_tenge / (double)term_months);
    }

    /* r = monthly rate; payment = P * r / (1 - (1 + r)^-n) */
    double r = (double)annual_rate_bp / 10000.0 / 12.0;
    double payment = (double)principal_tenge * r / (1.0 - pow(1.0 + r, -(double)term_months));
    return (int64_t)llround(payment);
}
