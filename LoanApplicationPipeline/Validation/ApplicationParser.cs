using System.Globalization;
using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Validation;

public static class ApplicationParser
{
    private const int MinCreditScore = 300;
    private const int MaxCreditScore = 850;

    public static bool TryParse(RawApplicationRecord raw, out ApplicationRequest? request, out string? error)
    {
        request = null;

        if (string.IsNullOrWhiteSpace(raw.ApplicantId))
        {
            error = "ApplicantId отсутствует.";
            return false;
        }

        if (raw.MonthlyIncomeText is null)
        {
            error = $"[{raw.ApplicantId}] MonthlyIncome отсутствует.";
            return false;
        }

        if (!decimal.TryParse(raw.MonthlyIncomeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var income))
        {
            error = $"[{raw.ApplicantId}] MonthlyIncome '{raw.MonthlyIncomeText}' не является числом.";
            return false;
        }

        if (income < 0)
        {
            error = $"[{raw.ApplicantId}] MonthlyIncome не может быть отрицательным.";
            return false;
        }

        if (raw.RequestedAmountText is null)
        {
            error = $"[{raw.ApplicantId}] RequestedAmount отсутствует.";
            return false;
        }

        if (!decimal.TryParse(raw.RequestedAmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var requestedAmount))
        {
            error = $"[{raw.ApplicantId}] RequestedAmount '{raw.RequestedAmountText}' не является числом.";
            return false;
        }

        if (requestedAmount <= 0)
        {
            error = $"[{raw.ApplicantId}] RequestedAmount должен быть больше нуля.";
            return false;
        }

        if (raw.CreditScoreText is null)
        {
            error = $"[{raw.ApplicantId}] CreditScore отсутствует.";
            return false;
        }

        if (!int.TryParse(raw.CreditScoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var creditScore))
        {
            error = $"[{raw.ApplicantId}] CreditScore '{raw.CreditScoreText}' не является целым числом.";
            return false;
        }

        if (creditScore < MinCreditScore || creditScore > MaxCreditScore)
        {
            error = $"[{raw.ApplicantId}] CreditScore {creditScore} вне диапазона [{MinCreditScore}, {MaxCreditScore}].";
            return false;
        }

        if (raw.HasExistingDebtText is null)
        {
            error = $"[{raw.ApplicantId}] HasExistingDebt отсутствует.";
            return false;
        }

        if (!bool.TryParse(raw.HasExistingDebtText, out var hasDebt))
        {
            error = $"[{raw.ApplicantId}] HasExistingDebt '{raw.HasExistingDebtText}' должен быть 'true' или 'false'.";
            return false;
        }

        request = new ApplicationRequest(raw.ApplicantId, income, requestedAmount, creditScore, hasDebt);
        error = null;
        return true;
    }
}