using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Data;
using LoanApplicationPipeline.Domain;
using LoanApplicationPipeline.Native;
using LoanApplicationPipeline.Queries;

namespace LoanApplicationPipeline.Workflow;

/// <summary>Snapshot: applications priced by the native library, plus every rejected record.</summary>
public sealed record PreparedPortfolio(IReadOnlyList<PricedApplication> Priced, IReadOnlyList<Error> Rejected);

public sealed record WorkflowReport(
    int PricedCount,
    int RejectedCount,
    long TotalRequested,
    IReadOnlyList<ReviewRow> ReviewQueue,
    IReadOnlyList<PurposeTotalRow> ApprovedByPurpose);

public static class LoanReportWorkflow
{
    /// <summary>
    /// Boundary step (I/O + native call): load file -> snapshot -> price every record natively.
    /// A bad record is rejected and reported; a native LOAD failure stops the whole workflow with a Fail,
    /// so no half-finished report exists.
    /// </summary>
    public static Result<PreparedPortfolio> Prepare(Func<TextReader> open, IPaymentCalculator calculator) =>
        ApplicationFile.Load(open).Bind(loaded => Price(loaded, calculator));

    /// <summary>Pure step: builds both reports from the snapshot with the given configured score function.</summary>
    public static WorkflowReport BuildReport(PreparedPortfolio portfolio, Func<PricedApplication, int> score, int threshold) =>
        new(portfolio.Priced.Count,
            portfolio.Rejected.Count,
            portfolio.Priced.TotalRequested(),
            ApplicationReports.ReviewQueue(portfolio.Priced, score, threshold),
            ApplicationReports.ApprovedByPurpose(portfolio.Priced, score, threshold));

    /// <summary>Whole workflow in one call: Prepare, then BuildReport.</summary>
    public static Result<WorkflowReport> Run(
        Func<TextReader> open, IPaymentCalculator calculator, Func<PricedApplication, int> score, int threshold) =>
        Prepare(open, calculator).Bind(p => Result<WorkflowReport>.Success(BuildReport(p, score, threshold)));

    private static Result<PreparedPortfolio> Price(LoadedApplications loaded, IPaymentCalculator calculator)
    {
        var priced = new List<PricedApplication>();
        var rejected = new List<Error>(loaded.Rejected);

        foreach (var app in loaded.Valid)
        {
            switch (calculator.MonthlyPayment(app.RequestedAmount, app.AnnualRateBp, app.TermMonths))
            {
                case Result<long>.Ok ok:
                    priced.Add(new PricedApplication(app, ok.Value));
                    break;
                case Result<long>.Fail fail when NativeErrorCodes.IsFatal(fail.Error.Code):
                    return Result<PreparedPortfolio>.Failure(fail.Error);
                case Result<long>.Fail fail:
                    rejected.Add(new Error(fail.Error.Code, $"{app.Id}: {fail.Error.Message}"));
                    break;
            }
        }

        return Result<PreparedPortfolio>.Success(new PreparedPortfolio(priced, rejected));
    }
}
