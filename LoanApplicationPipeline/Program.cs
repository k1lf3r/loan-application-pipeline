using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Validation;

var rawRecords = new List<RawApplicationRecord>
{
    new("A1", "50000", "100000", "700", "false"),
    new("A2", "30000", "20000",  "500", "false"),
    new("A3", "40000", "30000",  "600", "false"),
    new("A4", "10000", "20000",  "700", "true"),
    new("A5", "20000", "5000",   "499", "false"),
    new("A6", null,    "5000",   "600", "false"),
    new("A7", "10000", "5000",   "abc", "false"),
    new("A8", "10000", "5000",   "900", "false"),
};

Console.WriteLine("== Парсинг и валидация ==");
var validRequests = new List<ApplicationRequest>();
foreach (var raw in rawRecords)
{
    if (ApplicationParser.TryParse(raw, out var request, out var error))
    {
        validRequests.Add(request!);
        Console.WriteLine($"OK   {request}");
    }
    else
    {
        Console.WriteLine($"SKIP {error}");
    }
}

Console.WriteLine();
Console.WriteLine("== Политика Conservative ==");
var conservative = ApplicationWorkflow.Run(validRequests, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName);
PrintSummary(conservative);

Console.WriteLine();
Console.WriteLine("== Политика Flexible ==");
var flexible = ApplicationWorkflow.Run(validRequests, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);
PrintSummary(flexible);

static void PrintSummary(ApplicationSummary summary)
{
    Console.WriteLine($"Policy: {summary.PolicyName}");
    Console.WriteLine($"Submitted: {summary.TotalSubmitted}, EligibleForReview: {summary.EligibleForReview}, Approved: {summary.Approved}, TotalApprovedAmount: {summary.TotalApprovedAmount}");
    foreach (var d in summary.Decisions)
    {
        Console.WriteLine($"  {d.ApplicantId}: score={d.CreditScore}, requested={d.RequestedAmount} -> approved={d.Approved}, amount={d.ApprovedAmount}");
    }
}