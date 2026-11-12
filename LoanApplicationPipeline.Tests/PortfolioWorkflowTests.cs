using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Native;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Queries;
using LoanApplicationPipeline.Scoring;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class PortfolioWorkflowTests
{
    private static readonly string[] BadRows =
    [
        "A011,P999,Consumer,100000,12,1000,300000,700",
        "A012,P011,Consumer,100000,12,1000,300000,700",
        "A013,P001,Consumer,100000,12,1000,300000,480",
        "A014,P002,Consumer,abc,12,1000,300000,700",
        "A015,P003,Spaceship,100000,12,1000,300000,700",
        "A016,P004,Consumer,100000"
    ];

    [Fact]
    public void Workflow_NormalPath_ReportsAndRealNativeCall_ConservativeVsFlexible()
    {
        var portfolio = Check.Ok(PortfolioWorkflow.Prepare(
            CsvSamples.Source(CsvSamples.Text()), CsvSamples.Registry(), new NativePaymentCalculator()));

        Assert.Equal(10, portfolio.Priced.Count);
        Assert.Empty(portfolio.Rejected);
        Assert.Equal(44_894, portfolio.Priced.First(p => p.Loan.Id == "A001").MonthlyPayment);

        var conservative = PortfolioWorkflow.BuildReport(portfolio, ScoringFunctions.Conservative, ApprovalPolicies.DefaultThreshold);
        Assert.Equal(54_600_000, conservative.TotalRequested);
        Assert.Equal(new[] { "A006", "A004", "A009", "A008", "A002", "A010" }, conservative.ReviewQueue.Select(r => r.Id));
        Assert.Equal(37, conservative.ReviewQueue[0].BurdenPercent);
        Assert.Equal(240, conservative.ReviewQueue[0].Score);
        Assert.Equal(
            new[]
            {
                new PurposeTotalRow(LoanPurpose.Housing, 2, 15_000_000, 278_816),
                new PurposeTotalRow(LoanPurpose.Education, 2, 3_000_000, 109_508)
            },
            conservative.ApprovedByPurpose);

        var flexible = PortfolioWorkflow.BuildReport(portfolio, ScoringFunctions.Flexible, ApprovalPolicies.DefaultThreshold);
        Assert.Equal(new[] { "A006" }, flexible.ReviewQueue.Select(r => r.Id));
        Assert.Equal(
            new[]
            {
                new PurposeTotalRow(LoanPurpose.Housing, 3, 35_000_000, 525_320),
                new PurposeTotalRow(LoanPurpose.Business, 2, 14_000_000, 302_759),
                new PurposeTotalRow(LoanPurpose.Education, 2, 3_000_000, 109_508),
                new PurposeTotalRow(LoanPurpose.Consumer, 2, 1_800_000, 126_402)
            },
            flexible.ApprovedByPurpose);
    }

    [Fact]
    public void Workflow_DecideAll_UsesPolicyDelegate_AndKeepsTheSnapshotUnchanged()
    {
        var portfolio = Check.Ok(PortfolioWorkflow.Prepare(
            CsvSamples.Source(CsvSamples.Text()), CsvSamples.Registry(), new NativePaymentCalculator()));

        var conservative = Check.Ok(PortfolioWorkflow.DecideAll(portfolio, ApprovalPolicies.Conservative, ApprovalPolicies.ConservativeName));
        var flexible = Check.Ok(PortfolioWorkflow.DecideAll(portfolio, ApprovalPolicies.Flexible, ApprovalPolicies.FlexibleName));

        Assert.Equal(new[] { "A001", "A003", "A005", "A007" }, conservative.Where(r => r.Status == "Approved").Select(r => r.ApplicationId));
        Assert.Equal(18_000_000, conservative.Sum(r => r.ApprovedAmount));
        Assert.Equal(9, flexible.Count(r => r.Status == "Approved"));
        var a006 = flexible.Single(r => r.ApplicationId == "A006");
        Assert.Equal("Rejected", a006.Status);
        Assert.Contains("462", a006.Details);
        Assert.Equal(10, portfolio.Priced.Count);
    }

    [Fact]
    public void Workflow_BadRows_AreRejectedWithLineAndReason_WhileValidRowsAreStillReported()
    {
        var calculator = new StubCalculator(50_000);

        var portfolio = Check.Ok(PortfolioWorkflow.Prepare(
            CsvSamples.Source(CsvSamples.Text(BadRows)), CsvSamples.Registry(), calculator));

        Assert.Equal(10, portfolio.Priced.Count);
        Assert.Equal(10, calculator.Calls);

        var rejected = portfolio.Rejected.Cast<DomainError.RowRejected>().OrderBy(r => r.Row).ToArray();
        Assert.Equal(new[] { 12, 13, 14, 15, 16, 17 }, rejected.Select(r => r.Row));
        Assert.IsType<DomainError.ApplicantNotFound>(rejected[0].Cause);
        Assert.IsType<DomainError.ApplicantInactive>(rejected[1].Cause);
        Assert.IsType<DomainError.IneligibleCreditScore>(rejected[2].Cause);
        Assert.Equal("RequestedAmount", Assert.IsType<DomainError.ValidationError>(rejected[3].Cause).Field);
        Assert.Equal("Purpose", Assert.IsType<DomainError.ValidationError>(rejected[4].Cause).Field);
        Assert.Equal("row", Assert.IsType<DomainError.ValidationError>(rejected[5].Cause).Field);

        var report = Check.Ok(PortfolioWorkflow.Run(
            CsvSamples.Source(CsvSamples.Text(BadRows)), CsvSamples.Registry(), new StubCalculator(50_000),
            Samples.ByCredit, 650));
        Assert.Equal(10, report.PricedCount);
        Assert.Equal(6, report.RejectedCount);
    }

    [Fact]
    public void Workflow_NativeRejectsEveryRecord_GivesEmptyReports_NotAnException()
    {
        var calculator = new NativePaymentCalculator((_, _, _) => -1);

        var report = Check.Ok(PortfolioWorkflow.Run(
            CsvSamples.Source(CsvSamples.Text()), CsvSamples.Registry(), calculator, ScoringFunctions.Flexible, 500));

        Assert.Equal(0, report.PricedCount);
        Assert.Equal(10, report.RejectedCount);
        Assert.Empty(report.ReviewQueue);
        Assert.Empty(report.ApprovedByPurpose);
        Assert.Equal(0, report.TotalRequested);
    }

    [Fact]
    public void Workflow_NativeLoadFailure_FailsCleanly_WithTheOriginalError_AndWritesNothing()
    {
        using var temp = new TempDir();
        var outputPath = temp.File("report.csv");
        var failing = new NativePaymentCalculator((_, _, _) => throw new DllNotFoundException("missing"));

        var result = PortfolioWorkflow.Run(
            CsvSamples.Source(CsvSamples.Text()), CsvSamples.Registry(), failing, ScoringFunctions.Flexible, 500);

        var error = Assert.IsType<DomainError.NativeCallFailed>(Check.Err(result));
        Assert.Equal(NativeErrorCodes.LoadFailed, error.NativeCode);

        result.Map(report => ReportExporter.Export(outputPath, ReportFormatter.ToCsvLines(report)));
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void Workflow_MissingInputFile_FailsBeforeAnyPricing()
    {
        using var temp = new TempDir();
        var calculator = new StubCalculator(1);

        var result = PortfolioWorkflow.PrepareFile(temp.File("nope.csv"), CsvSamples.Registry(), calculator);

        Assert.Equal("FILE_NOT_FOUND", Check.Code(result));
        Assert.Equal(0, calculator.Calls);
    }
}
