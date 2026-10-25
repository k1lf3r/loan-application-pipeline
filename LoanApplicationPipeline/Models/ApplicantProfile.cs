namespace LoanApplicationPipeline.Models;

public sealed record ApplicantProfile(
    string ApplicantId,
    string FullName,
    decimal ExistingDebtAmount,
    bool IsActive)
{
    // A legitimate 0m debt is valid data, not missing value
    public bool HasZeroDebt => ExistingDebtAmount == 0m;
}