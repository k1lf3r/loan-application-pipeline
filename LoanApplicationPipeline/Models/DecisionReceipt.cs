namespace LoanApplicationPipeline.Models;

public sealed record DecisionReceipt(
    string ApplicantId,
    string Status,
    decimal ApprovedAmount,
    string Details,
    DateTime TimestampUtc);