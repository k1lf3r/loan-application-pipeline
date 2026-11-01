using System.Text;
using LoanApplicationPipeline.Common;

namespace LoanApplicationPipeline.Workflow;

/// <summary>Pure formatting: report -> CSV lines.</summary>
public static class ReportFormatter
{
    public static IReadOnlyList<string> ToCsvLines(WorkflowReport report)
    {
        var lines = new List<string>
        {
            "# Summary",
            "pricedRecords,rejectedRecords,totalRequested",
            $"{report.PricedCount},{report.RejectedCount},{report.TotalRequested}",
            "# Review queue",
            "id,name,burdenPercent,score"
        };
        lines.AddRange(report.ReviewQueue.Select(r => $"{r.Id},{r.Name},{r.BurdenPercent},{r.Score}"));
        lines.Add("# Approved by purpose");
        lines.Add("purpose,count,totalAmount,totalMonthlyPayment");
        lines.AddRange(report.ApprovedByPurpose.Select(r => $"{r.Purpose},{r.Count},{r.TotalAmount},{r.TotalMonthlyPayment}"));
        return lines;
    }
}

/// <summary>File boundary for output. The StreamWriter is owned and disposed inside Export.</summary>
public static class ReportExporter
{
    /// <summary>
    /// Writes to "path.tmp" first, then replaces the target. OWNER of the StreamWriter: this method;
    /// RELEASE POINT: the 'using' block, which ends BEFORE the move (an open handle would block it on Windows).
    /// If anything fails, the temp file is removed and an existing target file is left untouched.
    /// </summary>
    public static Result<string> Export(string path, IReadOnlyList<string> lines)
    {
        var temp = path + ".tmp";
        try
        {
            using (var writer = new StreamWriter(temp, append: false, new UTF8Encoding(false)))
            {
                foreach (var line in lines) writer.WriteLine(line);
            }

            File.Move(temp, path, overwrite: true);
            return Result<string>.Success(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return Result<string>.Failure("file.export_failed", ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception) { /* best effort cleanup; the original error is what matters */ }
    }
}
