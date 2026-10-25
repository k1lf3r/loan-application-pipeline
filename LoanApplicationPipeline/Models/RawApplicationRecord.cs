namespace LoanApplicationPipeline.Models;

public sealed record RawApplicationRecord(
    string? ApplicantId,
    string? MonthlyIncomeText,
    string? RequestedAmountText,
    string? CreditScoreText,
    string? HasExistingDebtText);