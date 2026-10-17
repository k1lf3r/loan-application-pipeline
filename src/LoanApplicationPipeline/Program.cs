using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;

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
    new("A9", "-5000", "100",    "700", "false"),
};

Console.WriteLine("== Intake: validated factory ==");
var intake = ApplicationIntake.Process(rawRecords);
foreach (var app in intake.Accepted)
{
    Console.WriteLine($"OK   {app}");
}
foreach (var reason in intake.Rejected)
{
    Console.WriteLine($"SKIP {reason}");
}

Console.WriteLine();
Console.WriteLine("== Policy Conservative ==");
PrintSummary(ApplicationWorkflow.Run(intake.Accepted, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName));

Console.WriteLine();
Console.WriteLine("== Policy Flexible ==");
PrintSummary(ApplicationWorkflow.Run(intake.Accepted, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

Console.WriteLine();
Console.WriteLine("== Safe transitions (A1) ==");
var a1 = intake.Accepted.First(a => a.ApplicantId == "A1");
var pending = ApplicationTransitions.Submit(a1);

Console.WriteLine("Invalid sequence: Decide before StartReview");
Console.WriteLine($"  {Describe(ApplicationTransitions.Decide(pending, ApprovalPolicies.Conservative))}");

Console.WriteLine("Valid sequence: StartReview, then Decide");
var reviewOutcome = ApplicationTransitions.StartReview(pending);
Console.WriteLine($"  {Describe(reviewOutcome)}");
if (reviewOutcome is TransitionOutcome.Moved reviewed)
{
    var decisionOutcome = ApplicationTransitions.Decide(reviewed.Next, ApprovalPolicies.Conservative);
    Console.WriteLine($"  {Describe(decisionOutcome)}");

    if (decisionOutcome is TransitionOutcome.Moved decided)
    {
        Console.WriteLine("Repeated operation: Decide again on the final state");
        var again = ApplicationTransitions.Decide(decided.Next, ApprovalPolicies.Conservative);
        Console.WriteLine($"  {Describe(again)}");
    }
}

static void PrintSummary(ApplicationSummary summary)
{
    Console.WriteLine($"Policy: {summary.PolicyName}");
    Console.WriteLine($"Submitted: {summary.TotalSubmitted}, EligibleForReview: {summary.EligibleForReview}, Approved: {summary.Approved}, TotalApprovedAmount: {summary.TotalApprovedAmount}");
    foreach (var d in summary.Decisions)
    {
        Console.WriteLine($"  {d.ApplicantId}: score={d.CreditScore}, requested={d.RequestedAmount} -> approved={d.Approved}, amount={d.ApprovedAmount} ({d.Note})");
    }
}

static string Describe(TransitionOutcome outcome) => outcome switch
{
    TransitionOutcome.Moved moved => $"Moved -> {DescribeState(moved.Next)}",
    TransitionOutcome.NotAllowed refused => $"Rejected: {refused.Reason}",
    _ => throw new InvalidOperationException("Unknown transition outcome.")
};

static string DescribeState(ApplicationState state) => state switch
{
    ApplicationState.Pending => "Pending",
    ApplicationState.UnderReview => "UnderReview",
    ApplicationState.Approved a => $"Approved (amount {a.Amount})",
    ApplicationState.Declined d => $"Declined ({d.Reason})",
    ApplicationState.Ineligible i => $"Ineligible (score below {i.Threshold})",
    _ => throw new InvalidOperationException("Unknown state.")
};
