using System.Globalization;
using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Native;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Scoring;
using LoanApplicationPipeline.Services;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var mode = args.Length > 0 ? args[0] : "run";
var dataDir = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "Data");
var outputDir = args.Length > 2 ? args[2] : Path.Combine(Environment.CurrentDirectory, "output");

if (mode is not ("run" or "errors" or "fail-native" or "fail-file"))
{
    Console.Error.WriteLine($"Unknown mode '{mode}'. Use: run | errors | fail-native | fail-file");
    Environment.ExitCode = 2;
    return;
}

var applicantsPath = Path.Combine(dataDir, "applicants.json");
var csvPath = mode switch
{
    "errors" => Path.Combine(dataDir, "applications_with_errors.csv"),
    "fail-file" => Path.Combine(dataDir, "does-not-exist.csv"),
    _ => Path.Combine(dataDir, "applications.csv")
};

IPaymentCalculator calculator = mode == "fail-native"
    ? new NativePaymentCalculator((_, _, _) => throw new DllNotFoundException("simulated: libloanrisk not found"))
    : new NativePaymentCalculator();

Console.WriteLine("=== Loan Application Pipeline (final project) ===");
Console.WriteLine($"Mode:         {mode}");
Console.WriteLine($"Applicants:   {applicantsPath}");
Console.WriteLine($"Applications: {csvPath}");

var registryResult = ApplicantFile.Load(applicantsPath);
if (registryResult is Result<ApplicantRegistry, DomainError>.Err registryFailure)
{
    ReportFailure(registryFailure.Error);
    return;
}
var registry = ((Result<ApplicantRegistry, DomainError>.Ok)registryResult).Value;
Console.WriteLine($"Registry loaded: {registry.Count} applicants.");

if (mode is "run" or "errors")
{
    Console.WriteLine();
    Console.WriteLine("== [1] Single application: Validate > AttachProfile > VerifyEligibility > Price (native C) > ApplyDecision > Receipt ==");

    var demo = new RawApplicationRecord("DEMO-1", "P001", "Consumer", "1000000", "12", "1200", "400000", "700");
    PrintReceipt("  Conservative policy",
        LoanApplicationWorkflow.ProcessApplication(demo, registry, calculator, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName));
    PrintReceipt("  Flexible policy    ",
        LoanApplicationWorkflow.ProcessApplication(demo, registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

    Console.WriteLine("  Expected failures (each one stops the pipeline, later stages are skipped):");
    PrintReceipt("  Unknown applicant  ",
        LoanApplicationWorkflow.ProcessApplication(demo with { ApplicantId = "P999" }, registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));
    PrintReceipt("  Inactive applicant ",
        LoanApplicationWorkflow.ProcessApplication(demo with { ApplicantId = "P011" }, registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));
    PrintReceipt("  Credit score 480   ",
        LoanApplicationWorkflow.ProcessApplication(demo with { CreditScoreText = "480" }, registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));
    PrintReceipt("  Invalid amount     ",
        LoanApplicationWorkflow.ProcessApplication(demo with { AmountText = "-5" }, registry, calculator, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

    Console.WriteLine("  Safe operation sequence:");
    var decidedTwice = LoanApplicationWorkflow.Prepare(demo, registry, calculator)
        .Bind(priced => ApplicationStages.ApplyDecision(priced, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName))
        .Bind(approved => ApplicationStages.ApplyDecision(approved, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));
    Console.WriteLine("    Decide an already decided application -> " + DescribeFailure(decidedTwice));

    var decidedUnpriced = ValidatedApplication.Create(demo)
        .Bind(application => ApplicationStages.AttachProfile(application, registry))
        .Bind(pending => ApplicationStages.ApplyDecision(pending, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));
    Console.WriteLine("    Decide before pricing                 -> " + DescribeFailure(decidedUnpriced));

    Console.WriteLine();
    Console.WriteLine("== [2] Deferred execution (yield return) ==");
    var opens = 0;
    var stream = ApplicationFile.Stream(() => { opens++; return new StreamReader(csvPath); }, csvPath);
    Console.WriteLine($"  1. Enumerable created           -> file opened {opens} times (nothing ran yet)");
    var firstTwo = stream.Take(2).ToArray();
    Console.WriteLine($"  2. Take(2).ToArray()            -> {firstTwo.Length} records read, file opened {opens} time(s); early stop disposed the reader");
    var everything = stream.ToArray();
    Console.WriteLine($"  3. Enumerated again (no snapshot) -> {everything.Length} records, file opened {opens} times in total: every pass re-reads the file");
    Console.WriteLine("  4. ApplicationFile.Load(...) materialises ONE snapshot; everything below reuses it without touching the file again.");
}

Console.WriteLine();
Console.WriteLine("== [3] Portfolio: load CSV > validate > profile > eligibility > native pricing (one snapshot) ==");
var preparedResult = PortfolioWorkflow.PrepareFile(csvPath, registry, calculator);
if (preparedResult is Result<PreparedPortfolio, DomainError>.Err preparedFailure)
{
    ReportFailure(preparedFailure.Error);
    Console.Error.WriteLine("No report was written; previous output files were not touched.");
    return;
}
var portfolio = ((Result<PreparedPortfolio, DomainError>.Ok)preparedResult).Value;
Console.WriteLine($"Snapshot: {portfolio.Priced.Count} priced, {portfolio.Rejected.Count} rejected.");
foreach (var rejectedRow in portfolio.Rejected)
    Console.WriteLine($"  rejected [{rejectedRow.Code}] {rejectedRow.Message}");

var policies = new (string Name, Func<PricedApplication, int> Score, ApprovalPolicy Policy)[]
{
    (ApprovalPolicies.ConservativeName, ScoringFunctions.Conservative, ApprovalPolicies.Conservative),
    (ApprovalPolicies.FlexibleName, ScoringFunctions.Flexible, ApprovalPolicies.Flexible)
};

foreach (var (policyName, scoreFunction, policy) in policies)
{
    var report = PortfolioWorkflow.BuildReport(portfolio, scoreFunction, ApprovalPolicies.DefaultThreshold);

    Console.WriteLine();
    Console.WriteLine($"== [4] {policyName} policy (manual review if score < {ApprovalPolicies.DefaultThreshold}) ==");
    Console.WriteLine($"Total requested: {report.TotalRequested:N0} tenge");

    Console.WriteLine("Report 1 - Review queue (lowest score first, ties by id):");
    if (report.ReviewQueue.Count == 0) Console.WriteLine("  (none)");
    foreach (var row in report.ReviewQueue)
        Console.WriteLine($"  {row.Id} {row.Name,-18} burden {row.BurdenPercent,3}%  score {row.Score}");

    Console.WriteLine("Report 2 - Approved portfolio by purpose (largest total first, ties by purpose):");
    if (report.ApprovedByPurpose.Count == 0) Console.WriteLine("  (none)");
    foreach (var row in report.ApprovedByPurpose)
        Console.WriteLine($"  {row.Purpose,-10} count {row.Count}  amount {row.TotalAmount,12:N0}  payments/month {row.TotalMonthlyPayment,9:N0}");

    var reportPath = Path.Combine(outputDir, $"report_{policyName.ToLowerInvariant()}.csv");
    Console.WriteLine(ReportExporter.Export(reportPath, ReportFormatter.ToCsvLines(report))
        .Match(path => $"Report exported: {path}", error => $"Report export failed [{error.Code}] {error.Message}"));

    var decided = PortfolioWorkflow.DecideAll(portfolio, policy, policyName);
    switch (decided)
    {
        case Result<IReadOnlyList<DecisionReceipt>, DomainError>.Ok decidedOk:
        {
            var approvedCount = decidedOk.Value.Count(receipt => receipt.Status == "Approved");
            Console.WriteLine($"[5] Decisions by policy: {approvedCount} approved, {decidedOk.Value.Count - approvedCount} rejected (to manual review)");

            var decisionsPath = Path.Combine(outputDir, $"decisions_{policyName.ToLowerInvariant()}.csv");
            Console.WriteLine(ReportExporter.Export(decisionsPath, ReportFormatter.ToDecisionLines(decidedOk.Value))
                .Match(path => $"Decisions exported: {path}", error => $"Decisions export failed [{error.Code}] {error.Message}"));
            break;
        }
        case Result<IReadOnlyList<DecisionReceipt>, DomainError>.Err decidedFailure:
            ReportFailure(decidedFailure.Error);
            break;
    }
}

Console.WriteLine();
Console.WriteLine("Done. Exit code 0 means every requested step succeeded.");

static void ReportFailure(DomainError error)
{
    Console.Error.WriteLine($"FAILED [{error.Code}] {error.Message}");
    Environment.ExitCode = 1;
}

static void PrintReceipt(string label, Result<DecisionReceipt, DomainError> result) =>
    Console.WriteLine($"{label} -> " + result.Match(
        receipt => $"{receipt.Status} | approved {receipt.ApprovedAmount:N0} | payment/month {receipt.MonthlyPayment:N0} | {receipt.Details}",
        error => $"FAILED [{error.Code}] {error.Message}"));

static string DescribeFailure(Result<LoanWorkflowState, DomainError> result) =>
    result.Match(
        _ => "UNEXPECTED SUCCESS",
        error => $"REJECTED [{error.Code}] {error.Message}");
