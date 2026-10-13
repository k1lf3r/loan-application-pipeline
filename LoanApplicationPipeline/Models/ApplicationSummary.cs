namespace LoanApplicationPipeline.Models;

public sealed record ApplicationSummary(
    string PolicyName,
    int TotalSubmitted,
    int EligibleForReview,
    int Approved,
    decimal TotalApprovedAmount,
    IReadOnlyList<ApplicationDecision> Decisions);