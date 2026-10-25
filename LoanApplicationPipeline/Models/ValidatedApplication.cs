using System.Globalization;
using LoanApplicationPipeline.Core;

namespace LoanApplicationPipeline.Models;

public sealed record ValidatedApplication
{
    public const int MinCreditScore = 300;
    public const int MaxCreditScore = 850;

    public string ApplicantId { get; }
    public decimal MonthlyIncome { get; }
    public decimal RequestedAmount { get; }
    public int CreditScore { get; }
    public bool HasExistingDebt { get; }

    // Enforce validated factory creation: external callers cannot bypass rules
    private ValidatedApplication(
        string applicantId,
        decimal monthlyIncome,
        decimal requestedAmount,
        int creditScore,
        bool hasExistingDebt)
    {
        ApplicantId = applicantId;
        MonthlyIncome = monthlyIncome;
        RequestedAmount = requestedAmount;
        CreditScore = creditScore;
        HasExistingDebt = hasExistingDebt;
    }

    public static Result<ValidatedApplication, DomainError> Create(RawApplicationRecord raw)
    {
        if (string.IsNullOrWhiteSpace(raw.ApplicantId))
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("ApplicantId", "ApplicantId is required."));

        if (!decimal.TryParse(raw.MonthlyIncomeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var income))
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("MonthlyIncome", $"'{raw.MonthlyIncomeText}' is not a valid decimal number."));

        if (income < 0)
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("MonthlyIncome", "Income cannot be negative."));

        if (!decimal.TryParse(raw.RequestedAmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("RequestedAmount", $"'{raw.RequestedAmountText}' is not a valid decimal number."));

        if (amount <= 0)
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("RequestedAmount", "Requested amount must be positive."));

        if (!int.TryParse(raw.CreditScoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("CreditScore", $"'{raw.CreditScoreText}' is not an integer."));

        if (score < MinCreditScore || score > MaxCreditScore)
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("CreditScore", $"Score {score} out of range [{MinCreditScore}, {MaxCreditScore}]."));

        if (!bool.TryParse(raw.HasExistingDebtText, out var hasDebt))
            return Result<ValidatedApplication, DomainError>.Failure(
                new DomainError.ValidationError("HasExistingDebt", $"'{raw.HasExistingDebtText}' must be 'true' or 'false'."));

        return Result<ValidatedApplication, DomainError>.Success(
            new ValidatedApplication(raw.ApplicantId.Trim(), Math.Round(income, 2), Math.Round(amount, 2), score, hasDebt));
    }
}