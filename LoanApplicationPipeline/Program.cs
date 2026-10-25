using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Services;

Console.WriteLine("=== Loan Application Scoring System (Assignments 6 & 7) ===");

var registry = new ApplicantRegistry();

// 1. Success path
Console.WriteLine("\n[1] Complete Success Path:");
var validRaw = new RawApplicationRecord("A1", "50000", "100000", "700", "false");
var successResult = LoanApplicationWorkflow.ProcessApplication(
    validRaw, registry, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName);
PrintOutcome(successResult);

// 2. Three domain failures
Console.WriteLine("\n[2] Domain Failures:");
// Failure 1: Score below threshold
var lowScoreRaw = new RawApplicationRecord("A5", "20000", "5000", "450", "false");
PrintOutcome(LoanApplicationWorkflow.ProcessApplication(
    lowScoreRaw, registry, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

// Failure 2: Factory invariant violation (negative income)
var invalidIncomeRaw = new RawApplicationRecord("A1", "-500", "5000", "700", "false");
PrintOutcome(LoanApplicationWorkflow.ProcessApplication(
    invalidIncomeRaw, registry, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

// Failure 3: Invalid repeated transition
var validatedApp = ((Result<ValidatedApplication, DomainError>.Ok)ValidatedApplication.Create(validRaw)).Value;
var approvedState = new LoanWorkflowState.Approved(validatedApp, 100000m, "Flexible");
var repeatResult = ApplicationStages.ApplyDecision(approvedState, ApprovalPolicies.Flexible, "Flexible");
Console.WriteLine(repeatResult switch
{
    Result<LoanWorkflowState, DomainError>.Err err => $"  REJECTED: [{err.Error.Code}] {err.Error.Message}",
    _ => "  SUCCESS"
});

// 3. Option lookup (Present, Absent, Legitimate 0)
Console.WriteLine("\n[3] Option Lookup (Present vs Absent vs 0):");
var foundProfile = registry.FindById("A1");
Console.WriteLine($"  Lookup A1: IsSome={foundProfile.IsSome}, Debt={((Option<ApplicantProfile>.Some)foundProfile).Value.ExistingDebtAmount:C2} (Valid 0, not absence)");

var missingRaw = new RawApplicationRecord("UNKNOWN", "50000", "10000", "700", "false");
PrintOutcome(LoanApplicationWorkflow.ProcessApplication(
    missingRaw, registry, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

// 4. File boundary exceptions
Console.WriteLine("\n[4] Boundary File Failures:");
string missingPath = Path.Combine(AppContext.BaseDirectory, "missing.json");
var missingFileResult = ApplicationDataLoader.LoadFromJsonFile(missingPath);
Console.WriteLine(missingFileResult switch
{
    Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Err err => $"  FILE ERROR: [{err.Error.Code}] {err.Error.Message}",
    _ => "  UNEXPECTED SUCCESS"
});

string malformedPath = Path.Combine(AppContext.BaseDirectory, "malformed_temp.json");
File.WriteAllText(malformedPath, "{ malformed json text");
try
{
    var malformedResult = ApplicationDataLoader.LoadFromJsonFile(malformedPath);
    Console.WriteLine(malformedResult switch
    {
        Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Err err => $"  JSON ERROR: [{err.Error.Code}] {err.Error.Message}",
        _ => "  UNEXPECTED SUCCESS"
    });
}
finally
{
    if (File.Exists(malformedPath)) File.Delete(malformedPath);
}

static void PrintOutcome(Result<DecisionReceipt, DomainError> result)
{
    result.Match(
        receipt =>
        {
            Console.WriteLine($"  SUCCESS: {receipt.ApplicantId} | {receipt.Status} | Amount: {receipt.ApprovedAmount:C2} | {receipt.Details}");
            return true;
        },
        error =>
        {
            Console.WriteLine($"  FAILED:  [{error.Code}] {error.Message}");
            return false;
        });
}