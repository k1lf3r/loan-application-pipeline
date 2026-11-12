namespace LoanApplicationPipeline.Models;

public sealed record RawApplicantRecord(
    string? ApplicantId,
    string? FullName,
    long? ExistingDebtMonthly,
    bool? IsActive);
