using LoanApplicationPipeline.Core;

namespace LoanApplicationPipeline.Pipeline;

public interface IPaymentCalculator
{
    Result<long, DomainError> MonthlyPayment(long principalTenge, int annualRateBp, int termMonths);
}
