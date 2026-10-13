namespace LoanApplicationPipeline.Models;

public sealed record ApplicationDecision(
    string ApplicantId,
    int CreditScore,
    decimal RequestedAmount,
    bool Approved,
    decimal ApprovedAmount,
    string PolicyName);