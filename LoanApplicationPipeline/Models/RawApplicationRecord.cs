namespace LoanApplicationPipeline.Models;

public sealed record RawApplicationRecord(
    string? Id,
    string? ApplicantId,
    string? PurposeText,
    string? AmountText,
    string? TermMonthsText,
    string? AnnualRateBpText,
    string? MonthlyIncomeText,
    string? CreditScoreText);
