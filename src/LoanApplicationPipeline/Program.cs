using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Data;
using LoanApplicationPipeline.Native;
using LoanApplicationPipeline.Queries;
using LoanApplicationPipeline.Scoring;
using LoanApplicationPipeline.Workflow;

// Usage: dotnet run --project src/LoanApplicationPipeline -- [run|fail-native|fail-file] [csvPath] [outputDir]
const int ReviewThreshold = 500;

var mode = args.Length > 0 ? args[0] : "run";
var csvPath = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "Data", "applications.csv");
var outputDir = args.Length > 2 ? args[2] : Path.Combine(Environment.CurrentDirectory, "output");

if (mode == "fail-file") csvPath = Path.Combine(outputDir, "does-not-exist.csv");

var calculator = mode == "fail-native"
    ? new NativePaymentCalculator((_, _, _) => throw new DllNotFoundException("simulated: libloanrisk not found"))
    : new NativePaymentCalculator();

Console.WriteLine($"Mode: {mode}");
Console.WriteLine($"Input: {csvPath}");

// ---- Deferred execution demo (probe lives here, not in production code) ----
if (mode == "run")
{
    Console.WriteLine();
    Console.WriteLine("== Deferred execution demo ==");
    var stream = ApplicationFile.Stream(() =>
    {
        Console.WriteLine("   [probe] file opened");
        return new StreamReader(csvPath);
    });
    Console.WriteLine("1. Enumerable created -> nothing opened yet.");
    var firstTwo = stream.Take(2).ToArray();
    Console.WriteLine($"2. Take(2).ToArray() read {firstTwo.Length} records; stopping early disposed the reader.");
}

// ---- Boundary: load + native pricing (one materialised snapshot) ----
var prepared = LoanReportWorkflow.Prepare(() => new StreamReader(csvPath), calculator);
if (prepared is Result<PreparedPortfolio>.Fail failure)
{
    Console.Error.WriteLine($"FAILED [{failure.Error.Code}] {failure.Error.Message}");
    Console.Error.WriteLine("No report was written; previous output files were not touched.");
    Environment.ExitCode = 1;
    return;
}

var portfolio = ((Result<PreparedPortfolio>.Ok)prepared).Value;
Console.WriteLine();
Console.WriteLine($"Snapshot: {portfolio.Priced.Count} priced, {portfolio.Rejected.Count} rejected.");
foreach (var error in portfolio.Rejected) Console.WriteLine($"  rejected [{error.Code}] {error.Message}");

// ---- Same snapshot, two partially applied scoring functions ----
var policies = new (string Name, Func<LoanApplicationPipeline.Domain.PricedApplication, int> Score)[]
{
    ("Conservative", ScoringFunctions.Conservative),
    ("Flexible", ScoringFunctions.Flexible)
};

Directory.CreateDirectory(outputDir);

foreach (var (name, score) in policies)
{
    var report = LoanReportWorkflow.BuildReport(portfolio, score, ReviewThreshold);

    Console.WriteLine();
    Console.WriteLine($"== {name} policy (review if score < {ReviewThreshold}) ==");
    Console.WriteLine($"Total requested: {report.TotalRequested:N0} tenge");
    Console.WriteLine("Review queue:");
    if (report.ReviewQueue.Count == 0) Console.WriteLine("  (none)");
    foreach (var r in report.ReviewQueue)
        Console.WriteLine($"  {r.Id} {r.Name,-18} burden {r.BurdenPercent,3}%  score {r.Score}");
    Console.WriteLine("Approved by purpose:");
    if (report.ApprovedByPurpose.Count == 0) Console.WriteLine("  (none)");
    foreach (var r in report.ApprovedByPurpose)
        Console.WriteLine($"  {r.Purpose,-10} count {r.Count}  amount {r.TotalAmount,12:N0}  payments/month {r.TotalMonthlyPayment,9:N0}");

    var exportPath = Path.Combine(outputDir, $"report_{name.ToLowerInvariant()}.csv");
    var exported = ReportExporter.Export(exportPath, ReportFormatter.ToCsvLines(report));
    Console.WriteLine(exported switch
    {
        Result<string>.Ok ok => $"Exported: {ok.Value}",
        Result<string>.Fail fail => $"Export failed [{fail.Error.Code}] {fail.Error.Message}",
        _ => "Unknown export result"
    });
}
