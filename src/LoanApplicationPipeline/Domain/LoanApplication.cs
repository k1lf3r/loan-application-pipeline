using LoanApplicationPipeline.Common;

namespace LoanApplicationPipeline.Domain;

public enum LoanPurpose { Education, Housing, Business, Consumer }

/// <summary>Validated, immutable loan application. Can only be created through <see cref="Create"/>.</summary>
public sealed record LoanApplication
{
    public const long MaxAmount = 1_000_000_000_000L;

    public string Id { get; }
    public string ApplicantName { get; }
    public LoanPurpose Purpose { get; }
    public long RequestedAmount { get; }        // whole tenge
    public int TermMonths { get; }
    public int AnnualRateBp { get; }            // basis points: 1200 = 12.00 %
    public long MonthlyIncome { get; }          // whole tenge
    public int CreditScore { get; }             // 300..850
    public long ExistingDebtMonthly { get; }    // whole tenge

    private LoanApplication(string id, string name, LoanPurpose purpose, long amount, int term,
                            int rateBp, long income, int credit, long debt)
    {
        Id = id; ApplicantName = name; Purpose = purpose; RequestedAmount = amount; TermMonths = term;
        AnnualRateBp = rateBp; MonthlyIncome = income; CreditScore = credit; ExistingDebtMonthly = debt;
    }

    public static Result<LoanApplication> Create(string id, string name, LoanPurpose purpose, long requestedAmount,
        int termMonths, int annualRateBp, long monthlyIncome, int creditScore, long existingDebtMonthly)
    {
        if (string.IsNullOrWhiteSpace(id)) return Invalid("id", "Id must not be empty.");
        if (string.IsNullOrWhiteSpace(name)) return Invalid("name", "Applicant name must not be empty.");
        if (!Enum.IsDefined(purpose)) return Invalid("purpose", "Purpose is not a known loan purpose.");
        if (requestedAmount is < 1 or > MaxAmount) return Invalid("amount", "Amount must be 1..1 000 000 000 000 tenge.");
        if (termMonths is < 1 or > 600) return Invalid("term", "Term must be 1..600 months.");
        if (annualRateBp is < 0 or > 10_000) return Invalid("rate", "Rate must be 0..10000 basis points.");
        if (monthlyIncome < 1) return Invalid("income", "Monthly income must be positive.");
        if (creditScore is < 300 or > 850) return Invalid("credit_score", "Credit score must be 300..850.");
        if (existingDebtMonthly < 0) return Invalid("debt", "Existing debt must not be negative.");

        return Result<LoanApplication>.Success(new LoanApplication(
            id.Trim(), name.Trim(), purpose, requestedAmount, termMonths, annualRateBp,
            monthlyIncome, creditScore, existingDebtMonthly));
    }

    private static Result<LoanApplication> Invalid(string field, string message) =>
        Result<LoanApplication>.Failure($"domain.invalid_{field}", message);
}
