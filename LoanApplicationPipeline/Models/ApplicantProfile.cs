using LoanApplicationPipeline.Core;

namespace LoanApplicationPipeline.Models;

public sealed record ApplicantProfile(
    string ApplicantId,
    string FullName,
    long ExistingDebtMonthly,   // whole tenge per month; 0 is valid, not "missing"
    bool IsActive)
{
    public const long MaxDebt = 1_000_000_000_000L;

    public bool HasZeroDebt => ExistingDebtMonthly == 0;

    public static Result<ApplicantProfile, DomainError> Create(RawApplicantRecord raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.IsNullOrWhiteSpace(raw.ApplicantId))
            return Invalid("ApplicantId", "ApplicantId is required.");
        if (string.IsNullOrWhiteSpace(raw.FullName))
            return Invalid("FullName", "FullName is required.");
        if (raw.ExistingDebtMonthly is null)
            return Invalid("ExistingDebtMonthly", "ExistingDebtMonthly is required.");
        if (raw.ExistingDebtMonthly.Value < 0 || raw.ExistingDebtMonthly.Value > MaxDebt)
            return Invalid("ExistingDebtMonthly", $"Existing debt must be 0..{MaxDebt}.");
        if (raw.IsActive is null)
            return Invalid("IsActive", "IsActive is required (true or false).");

        return Res.Ok(new ApplicantProfile(
            raw.ApplicantId.Trim(), raw.FullName.Trim(), raw.ExistingDebtMonthly.Value, raw.IsActive.Value));
    }

    private static Result<ApplicantProfile, DomainError> Invalid(string field, string reason) =>
        Res.Fail<ApplicantProfile>(new DomainError.ValidationError(field, reason));
}
