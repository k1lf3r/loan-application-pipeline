using System.Globalization;
using LoanApplicationPipeline.Core;

namespace LoanApplicationPipeline.Models;

// Money is in whole tenge, rates in basis points (1200 = 12.00%); numeric strings are rejected for Purpose.
public sealed record ValidatedApplication
{
    public const long MaxAmount = 1_000_000_000_000L;
    public const int MinTermMonths = 1;
    public const int MaxTermMonths = 600;
    public const int MaxRateBp = 10_000;
    public const int MinCreditScore = 300;
    public const int MaxCreditScore = 850;

    public string Id { get; }
    public string ApplicantId { get; }
    public LoanPurpose Purpose { get; }
    public long RequestedAmount { get; }
    public int TermMonths { get; }
    public int AnnualRateBp { get; }
    public long MonthlyIncome { get; }
    public int CreditScore { get; }

    private ValidatedApplication(string id, string applicantId, LoanPurpose purpose, long requestedAmount,
        int termMonths, int annualRateBp, long monthlyIncome, int creditScore)
    {
        Id = id;
        ApplicantId = applicantId;
        Purpose = purpose;
        RequestedAmount = requestedAmount;
        TermMonths = termMonths;
        AnnualRateBp = annualRateBp;
        MonthlyIncome = monthlyIncome;
        CreditScore = creditScore;
    }

    public static Result<ValidatedApplication, DomainError> Create(RawApplicationRecord raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.IsNullOrWhiteSpace(raw.Id))
            return Invalid("Id", "Id is required.");

        if (string.IsNullOrWhiteSpace(raw.ApplicantId))
            return Invalid("ApplicantId", "ApplicantId is required.");

        if (!TryPurpose(raw.PurposeText, out var purpose))
            return Invalid("Purpose", $"'{raw.PurposeText}' is not one of: {string.Join(", ", Enum.GetNames<LoanPurpose>())}.");

        if (!TryWhole(raw.AmountText, out var amount))
            return Invalid("RequestedAmount", $"'{raw.AmountText}' is not a whole number.");
        if (amount < 1 || amount > MaxAmount)
            return Invalid("RequestedAmount", $"Requested amount must be 1..{MaxAmount}.");

        if (!TryInt(raw.TermMonthsText, out var term))
            return Invalid("TermMonths", $"'{raw.TermMonthsText}' is not a whole number.");
        if (term < MinTermMonths || term > MaxTermMonths)
            return Invalid("TermMonths", $"Term must be {MinTermMonths}..{MaxTermMonths} months.");

        if (!TryInt(raw.AnnualRateBpText, out var rate))
            return Invalid("AnnualRateBp", $"'{raw.AnnualRateBpText}' is not a whole number.");
        if (rate < 0 || rate > MaxRateBp)
            return Invalid("AnnualRateBp", $"Rate must be 0..{MaxRateBp} basis points.");

        if (!TryWhole(raw.MonthlyIncomeText, out var income))
            return Invalid("MonthlyIncome", $"'{raw.MonthlyIncomeText}' is not a whole number.");
        if (income < 1 || income > MaxAmount)
            return Invalid("MonthlyIncome", "Monthly income must be positive.");

        if (!TryInt(raw.CreditScoreText, out var score))
            return Invalid("CreditScore", $"'{raw.CreditScoreText}' is not an integer.");
        if (score < MinCreditScore || score > MaxCreditScore)
            return Invalid("CreditScore", $"Score {score} out of range [{MinCreditScore}, {MaxCreditScore}].");

        return Res.Ok(new ValidatedApplication(
            raw.Id.Trim(), raw.ApplicantId.Trim(), purpose, amount, term, rate, income, score));
    }

    private static Result<ValidatedApplication, DomainError> Invalid(string field, string reason) =>
        Res.Fail<ValidatedApplication>(new DomainError.ValidationError(field, reason));

    private static bool TryWhole(string? text, out long value) =>
        long.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryInt(string? text, out int value) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryPurpose(string? text, out LoanPurpose purpose)
    {
        var name = Enum.GetNames<LoanPurpose>()
            .FirstOrDefault(n => string.Equals(n, text?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            purpose = default;
            return false;
        }

        purpose = Enum.Parse<LoanPurpose>(name);
        return true;
    }
}
