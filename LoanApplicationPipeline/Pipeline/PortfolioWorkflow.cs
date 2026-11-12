using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Native;
using LoanApplicationPipeline.Queries;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Pipeline;

public sealed record PreparedPortfolio(IReadOnlyList<PricedApplication> Priced, IReadOnlyList<DomainError> Rejected);

public sealed record WorkflowReport(
    int PricedCount,
    int RejectedCount,
    long TotalRequested,
    IReadOnlyList<ReviewRow> ReviewQueue,
    IReadOnlyList<PurposeTotalRow> ApprovedByPurpose);

public static class PortfolioWorkflow
{
    public static Result<PreparedPortfolio, DomainError> Prepare(
        Func<TextReader> open, ApplicantRegistry registry, IPaymentCalculator calculator, string source = "(csv)")
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(calculator);

        return ApplicationFile.Load(open, source)
            .Bind(loaded => Price(loaded, registry, calculator));
    }

    public static Result<PreparedPortfolio, DomainError> PrepareFile(
        string path, ApplicantRegistry registry, IPaymentCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(calculator);

        return ApplicationFile.LoadFile(path)
            .Bind(loaded => Price(loaded, registry, calculator));
    }

    public static IEnumerable<Result<PricedApplication, DomainError>> PriceLazily(
        IEnumerable<CsvRecord> records, ApplicantRegistry registry, IPaymentCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(calculator);
        return PriceLazilyCore(records, registry, calculator);
    }

    private static IEnumerable<Result<PricedApplication, DomainError>> PriceLazilyCore(
        IEnumerable<CsvRecord> records, ApplicantRegistry registry, IPaymentCalculator calculator)
    {
        foreach (var record in records)
        {
            yield return LoanApplicationWorkflow.Prepare(record.Raw, registry, calculator)
                .Map(priced => priced.Application)
                .MapError(error => (DomainError)new DomainError.RowRejected(record.Line, error));
        }
    }

    private static Result<PreparedPortfolio, DomainError> Price(
        LoadedRecords loaded, ApplicantRegistry registry, IPaymentCalculator calculator)
    {
        var (priced, failed) = PriceLazily(loaded.Records, registry, calculator).Partition();

        var fatal = failed.Select(FatalNativeCause).FirstOrDefault(cause => cause is not null);
        if (fatal is not null)
            return Res.Fail<PreparedPortfolio>(fatal);

        var rejected = loaded.Rejected.Concat(failed).ToArray();
        return Res.Ok(new PreparedPortfolio(priced, rejected));
    }

    private static DomainError? FatalNativeCause(DomainError error) => error switch
    {
        DomainError.RowRejected { Cause: DomainError.NativeCallFailed native } when NativeErrorCodes.IsFatal(native.NativeCode) => native,
        _ => null
    };

    public static WorkflowReport BuildReport(PreparedPortfolio portfolio, Func<PricedApplication, int> score, int threshold)
    {
        ArgumentNullException.ThrowIfNull(portfolio);

        return new WorkflowReport(
            portfolio.Priced.Count,
            portfolio.Rejected.Count,
            portfolio.Priced.TotalRequested(),
            ApplicationReports.ReviewQueue(portfolio.Priced, score, threshold),
            ApplicationReports.ApprovedByPurpose(portfolio.Priced, score, threshold));
    }

    public static Result<WorkflowReport, DomainError> Run(
        Func<TextReader> open, ApplicantRegistry registry, IPaymentCalculator calculator,
        Func<PricedApplication, int> score, int threshold, string source = "(csv)") =>
        Prepare(open, registry, calculator, source)
            .Map(portfolio => BuildReport(portfolio, score, threshold));

    public static Result<IReadOnlyList<DecisionReceipt>, DomainError> DecideAll(
        PreparedPortfolio portfolio, ApprovalPolicy policy, string policyName)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(policy);

        var (receipts, errors) = portfolio.Priced
            .Select(priced => ApplicationStages
                .ApplyDecision(new LoanWorkflowState.Priced(priced), policy, policyName)
                .Map(state => ApplicationStages.ToReceipt(state)))
            .Partition();

        if (errors.Count > 0)
            return Res.Fail<IReadOnlyList<DecisionReceipt>>(errors[0]);

        return Res.Ok(receipts);
    }
}
