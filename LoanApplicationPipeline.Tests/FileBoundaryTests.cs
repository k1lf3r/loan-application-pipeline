using System.Text;
using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;
using LoanApplicationPipeline.Scoring;
using LoanApplicationPipeline.Services;

namespace LoanApplicationPipeline.Tests;

public class FileBoundaryTests
{
    [Fact]
    public void LoadFile_MissingFile_IsSafeFailure()
    {
        using var temp = new TempDir();

        var missing = Check.Err(ApplicationFile.LoadFile(temp.File("missing.csv")));
        var missingFolder = Check.Err(ApplicationFile.LoadFile(Path.Combine(temp.Root, "nope", "x.csv")));

        Assert.IsType<DomainError.FileNotFound>(missing);
        Assert.IsType<DomainError.FileNotFound>(missingFolder);
        Assert.Equal("FILE_NOT_FOUND", Check.Code(ApplicationFile.LoadFile("")));
    }

    [Fact]
    public void Load_BadHeader_FailsTheWholeFile()
    {
        Assert.Equal("MALFORMED_FILE", Check.Code(ApplicationFile.Load(CsvSamples.Source("wrong,header\nA,B"))));
        Assert.Equal("MALFORMED_FILE", Check.Code(ApplicationFile.Load(CsvSamples.Source(""))));
    }

    [Fact]
    public void LoadFile_MalformedRows_AreRejectedIndividually_BlankLinesSkipped()
    {
        using var temp = new TempDir();
        var path = temp.File("mixed.csv");
        File.WriteAllLines(path, new[]
        {
            ApplicationCsv.Header,
            "G001,P001,Housing,1000000,12,1200,500000,700",
            "M001,P001,Housing,1000000",
            "M002,P001,Housing,1000000,12,1200,500000,700,EXTRA",
            "",
            "G002,P002,Consumer,2000000,24,1500,600000,650"
        });

        var loaded = Check.Ok(ApplicationFile.LoadFile(path));

        Assert.Equal(new[] { 2, 6 }, loaded.Records.Select(r => r.Line));
        Assert.Equal(new[] { 3, 4 }, loaded.Rejected.Cast<DomainError.RowRejected>().Select(r => r.Row));
    }

    [Fact]
    public void LoadFile_ReadsUtf8FileWithByteOrderMark()
    {
        using var temp = new TempDir();
        var path = temp.File("bom.csv");
        File.WriteAllText(path, CsvSamples.Text(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal(10, Check.Ok(ApplicationFile.LoadFile(path)).Records.Count);
    }

    [Fact]
    public void SampleFiles_LoadTogether_AndMatchTheDocumentedCounts()
    {
        var registry = Check.Ok(ApplicantFile.Load(RepoPaths.Data("applicants.json")));

        var clean = Check.Ok(ApplicationFile.LoadFile(RepoPaths.Data("applications.csv")));
        Assert.Equal(10, clean.Records.Count);
        Assert.Empty(clean.Rejected);

        var withErrors = Check.Ok(PortfolioWorkflow.PrepareFile(
            RepoPaths.Data("applications_with_errors.csv"), registry, new StubCalculator(50_000)));
        Assert.Equal(10, withErrors.Priced.Count);
        Assert.Equal(6, withErrors.Rejected.Count);
    }

    [Fact]
    public void Export_WritesReport_CreatesMissingFolder_AndLeavesNoTempFile()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Root, "out", "report.csv");
        var portfolio = Check.Ok(PortfolioWorkflow.Prepare(
            CsvSamples.Source(CsvSamples.Text()), CsvSamples.Registry(), new StubCalculator(50_000)));
        var report = PortfolioWorkflow.BuildReport(portfolio, ScoringFunctions.Flexible, ApprovalPolicies.DefaultThreshold);

        Check.Ok(ReportExporter.Export(path, ReportFormatter.ToCsvLines(report)));

        var lines = File.ReadAllLines(path);
        Assert.Equal("# Summary", lines[0]);
        Assert.Equal("10,0,54600000", lines[2]);
        Assert.Contains("# Review queue", lines);
        Assert.Contains("# Approved by purpose", lines);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Export_WhenWriteFails_ReturnsFailure_AndKeepsPreviousFile()
    {
        using var temp = new TempDir();
        var path = temp.File("report.csv");
        File.WriteAllText(path, "old content");
        Directory.CreateDirectory(path + ".tmp");

        var result = ReportExporter.Export(path, ["new content"]);

        Assert.Equal("EXPORT_FAILED", Check.Code(result));
        Assert.Equal("old content", File.ReadAllText(path));
    }

    [Fact]
    public void Export_BlankPath_IsAFailureValue()
    {
        Assert.Equal("EXPORT_FAILED", Check.Code(ReportExporter.Export("  ", ["x"])));
    }

    [Fact]
    public void DecisionLines_QuoteValuesThatContainCommas()
    {
        var receipt = new DecisionReceipt("A1", "P1", "Rejected", 0, 10, "Policy: X, Reason: \"low\"");

        var lines = ReportFormatter.ToDecisionLines([receipt]);

        Assert.Equal("applicationId,applicantId,status,approvedAmount,monthlyPayment,details", lines[0]);
        Assert.Equal("A1,P1,Rejected,0,10,\"Policy: X, Reason: \"\"low\"\"\"", lines[1]);
    }
}
