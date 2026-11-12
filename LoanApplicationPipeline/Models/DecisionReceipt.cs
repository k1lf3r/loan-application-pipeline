namespace LoanApplicationPipeline.Models;

public sealed record DecisionReceipt(
    string ApplicationId,
    string ApplicantId,
    string Status,
    long ApprovedAmount,
    long MonthlyPayment,
    string Details);
