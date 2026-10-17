using System.Globalization;

namespace LoanApplicationPipeline.Models;

/// <summary>
/// A loan application that has passed every domain rule.
/// The constructor is private and all properties are get-only, so the only way
/// to obtain an instance is <see cref="Create"/>, which either returns a
/// <see cref="ValidationOutcome.Valid"/> or explains why it refused.
/// </summary>
public sealed record ValidatedApplication
{
    public const int MinCreditScore = 300;
    public const int MaxCreditScore = 850;

    public string ApplicantId { get; }
    public decimal MonthlyIncome { get; }
    public decimal RequestedAmount { get; }
    public int CreditScore { get; }
    public bool HasExistingDebt { get; }

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

    public static ValidationOutcome Create(RawApplicationRecord raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.IsNullOrWhiteSpace(raw.ApplicantId))
        {
            return new ValidationOutcome.Invalid(new[] { "ApplicantId is missing." });
        }

        var id = raw.ApplicantId.Trim();
        var reasons = new List<string>();

        decimal income = 0m;
        if (raw.MonthlyIncomeText is null)
        {
            reasons.Add($"[{id}] MonthlyIncome is missing.");
        }
        else if (!decimal.TryParse(raw.MonthlyIncomeText, NumberStyles.Number, CultureInfo.InvariantCulture, out income))
        {
            reasons.Add($"[{id}] MonthlyIncome '{raw.MonthlyIncomeText}' is not a number.");
        }
        else if (income < 0m)
        {
            reasons.Add($"[{id}] MonthlyIncome cannot be negative.");
        }
        else
        {
            income = Math.Round(income, 2);
        }

        decimal requested = 0m;
        if (raw.RequestedAmountText is null)
        {
            reasons.Add($"[{id}] RequestedAmount is missing.");
        }
        else if (!decimal.TryParse(raw.RequestedAmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out requested))
        {
            reasons.Add($"[{id}] RequestedAmount '{raw.RequestedAmountText}' is not a number.");
        }
        else
        {
            requested = Math.Round(requested, 2);
            if (requested <= 0m)
            {
                reasons.Add($"[{id}] RequestedAmount must be greater than zero.");
            }
        }

        int score = 0;
        if (raw.CreditScoreText is null)
        {
            reasons.Add($"[{id}] CreditScore is missing.");
        }
        else if (!int.TryParse(raw.CreditScoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out score))
        {
            reasons.Add($"[{id}] CreditScore '{raw.CreditScoreText}' is not an integer.");
        }
        else if (score < MinCreditScore || score > MaxCreditScore)
        {
            reasons.Add($"[{id}] CreditScore {score} is outside the range [{MinCreditScore}, {MaxCreditScore}].");
        }

        bool hasDebt = false;
        if (raw.HasExistingDebtText is null)
        {
            reasons.Add($"[{id}] HasExistingDebt is missing.");
        }
        else if (!bool.TryParse(raw.HasExistingDebtText, out hasDebt))
        {
            reasons.Add($"[{id}] HasExistingDebt '{raw.HasExistingDebtText}' must be 'true' or 'false'.");
        }

        if (reasons.Count > 0)
        {
            return new ValidationOutcome.Invalid(reasons.ToArray());
        }

        return new ValidationOutcome.Valid(new ValidatedApplication(id, income, requested, score, hasDebt));
    }
}
