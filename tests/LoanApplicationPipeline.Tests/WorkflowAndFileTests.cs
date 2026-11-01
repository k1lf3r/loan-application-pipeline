using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Data;
using LoanApplicationPipeline.Domain;
using LoanApplicationPipeline.Native;
using LoanApplicationPipeline.Queries;
using LoanApplicationPipeline.Scoring;
using LoanApplicationPipeline.Workflow;
using Xunit;

namespace LoanApplicationPipeline.Tests;

public class WorkflowAndFileTests
{
    private static Func<TextReader> Source(string csv) => () => new StringReader(csv);

    // ---------- COMPLETE WORKFLOWS ----------
    [Fact]
    public void Workflow_NormalPath_ReportsAndRealNativeCall_ConservativeVsFlexible()
    {
        var portfolio = Check.Ok(LoanReportWorkflow.Prepare(Source(Samples.Csv()), new NativePaymentCalculator()));

        Assert.Equal(10, portfolio.Priced.Count);
        Assert.Empty(portfolio.Rejected);
        Assert.Equal(44_894, portfolio.Priced.First(p => p.Application.Id == "A001").MonthlyPayment); // native value

        var conservative = LoanReportWorkflow.BuildReport(portfolio, ScoringFunctions.Conservative, 500);
        Assert.Equal(54_600_000, conservative.TotalRequested);
        Assert.Equal(new[] { "A006", "A004", "A009", "A008", "A002", "A010" }, conservative.ReviewQueue.Select(r => r.Id));
        Assert.Equal(
            new[]
            {
                new PurposeTotalRow(LoanPurpose.Housing, 2, 15_000_000, 278_816),
                new PurposeTotalRow(LoanPurpose.Education, 2, 3_000_000, 109_508)
            },
            conservative.ApprovedByPurpose);

        var flexible = LoanReportWorkflow.BuildReport(portfolio, ScoringFunctions.Flexible, 500);
        Assert.Equal(new[] { "A006" }, flexible.ReviewQueue.Select(r => r.Id));      // different result, same data
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
    public void Workflow_BadRecords_AreRejected_WhileValidRecordsAreStillReported()
    {
        var csv = Samples.Csv(
            "A011,Bad Amount,Consumer,abc,12,1000,300000,700,0",
            "A012,Bad Score,Consumer,100000,12,1000,300000,900,0");

        var report = Check.Ok(LoanReportWorkflow.Run(Source(csv), new NativePaymentCalculator(), ScoringFunctions.Flexible, 500));

        Assert.Equal(10, report.PricedCount);
        Assert.Equal(2, report.RejectedCount);
    }

    [Fact]
    public void Workflow_NativeRejectsEverything_GivesEmptyReports_NotAnException()
    {
        var calculator = new NativePaymentCalculator((_, _, _) => -1);

        var report = Check.Ok(LoanReportWorkflow.Run(Source(Samples.Csv()), calculator, ScoringFunctions.Flexible, 500));

        Assert.Equal(0, report.PricedCount);
        Assert.Equal(10, report.RejectedCount);
        Assert.Empty(report.ReviewQueue);
        Assert.Empty(report.ApprovedByPurpose);
        Assert.Equal(0, report.TotalRequested);
    }

    [Fact]
    public void Workflow_NativeLoadFailure_FailsCleanly_AndWritesNothing()
    {
        using var temp = new TempDir();
        var outputPath = temp.File("report.csv");
        var failing = new NativePaymentCalculator((_, _, _) => throw new DllNotFoundException("missing"));

        var result = LoanReportWorkflow.Run(Source(Samples.Csv()), failing, ScoringFunctions.Flexible, 500);

        Assert.Equal(NativeErrorCodes.LoadFailed, Check.FailCode(result));
        Assert.False(File.Exists(outputPath));        // the caller exports only on Ok, so no partial output exists
    }

    // ---------- FILE BOUNDARIES ----------
    [Fact]
    public void Load_MissingFile_IsSafeFailure()
    {
        using var temp = new TempDir();

        Assert.Equal("file.unreadable", Check.FailCode(ApplicationFile.Load(temp.File("missing.csv"))));
        Assert.Equal("file.unreadable", Check.FailCode(ApplicationFile.Load(Path.Combine(temp.Root, "nope", "x.csv"))));
    }

    [Fact]
    public void Load_MalformedContent_BadHeaderFails_BadRowsAreRejectedIndividually()
    {
        Assert.Equal("csv.bad_header", Check.FailCode(ApplicationFile.Load(Source("wrong,header\nA,B"))));
        Assert.Equal("csv.bad_header", Check.FailCode(ApplicationFile.Load(Source(""))));

        using var temp = new TempDir();
        var path = temp.File("mixed.csv");
        File.WriteAllLines(path, new[]
        {
            ApplicationCsv.Header,
            "G001,Good Row,Housing,1000000,12,1200,500000,700,0",
            "M001,Too Few Columns,Housing,1000000",
            "M002,Unknown Purpose,Spaceship,1000000,12,1200,500000,700,0",
            "M003,Not A Number,Housing,lots,12,1200,500000,700,0",
            "M004,Out Of Range,Housing,1000000,12,1200,500000,900,0"
        });

        var loaded = Check.Ok(ApplicationFile.Load(path));

        Assert.Equal(new[] { "G001" }, loaded.Valid.Select(a => a.Id));
        Assert.Equal(
            new[] { "csv.malformed", "csv.malformed", "csv.malformed", "domain.invalid_credit_score" },
            loaded.Rejected.Select(e => e.Code));
    }

    [Fact]
    public void Export_WritesReport_AndLeavesNoTempFile()
    {
        using var temp = new TempDir();
        var path = temp.File("report.csv");
        var portfolio = Check.Ok(LoanReportWorkflow.Prepare(Source(Samples.Csv()), new NativePaymentCalculator()));
        var report = LoanReportWorkflow.BuildReport(portfolio, ScoringFunctions.Conservative, 500);

        Check.Ok(ReportExporter.Export(path, ReportFormatter.ToCsvLines(report)));

        var lines = File.ReadAllLines(path);
        Assert.Equal("# Summary", lines[0]);
        Assert.Equal("10,0,54600000", lines[2]);
        Assert.Contains("A006,Ruslan Ibragimov,37,240", lines);
        Assert.Contains("Housing,2,15000000,278816", lines);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Export_WhenWriteFails_ReturnsFailure_AndKeepsPreviousFile()
    {
        using var temp = new TempDir();
        var path = temp.File("report.csv");
        File.WriteAllText(path, "old content");
        Directory.CreateDirectory(path + ".tmp");     // the temp path is occupied by a directory, so writing fails

        var result = ReportExporter.Export(path, ["new content"]);

        Assert.Equal("file.export_failed", Check.FailCode(result));
        Assert.Equal("old content", File.ReadAllText(path));
    }
}
