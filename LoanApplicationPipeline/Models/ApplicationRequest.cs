namespace LoanApplicationPipeline.Models;

public sealed record ApplicationRequest(
    string ApplicantId,
    decimal MonthlyIncome,
    decimal RequestedAmount,
    int CreditScore,
    bool HasExistingDebt);