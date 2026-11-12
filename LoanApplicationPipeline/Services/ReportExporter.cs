using System.Text;
using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;
using LoanApplicationPipeline.Pipeline;

namespace LoanApplicationPipeline.Services;

public static class ReportFormatter
{
    public static IReadOnlyList<string> ToCsvLines(WorkflowReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var lines = new List<string>
        {
            "# Summary",
            "pricedRecords,rejectedRecords,totalRequested",
            $"{report.PricedCount},{report.RejectedCount},{report.TotalRequested}",
            "# Review queue",
            "id,name,burdenPercent,score"
        };
        lines.AddRange(report.ReviewQueue.Select(r => $"{Cell(r.Id)},{Cell(r.Name)},{r.BurdenPercent},{r.Score}"));
        lines.Add("# Approved by purpose");
        lines.Add("purpose,count,totalAmount,totalMonthlyPayment");
        lines.AddRange(report.ApprovedByPurpose.Select(r => $"{r.Purpose},{r.Count},{r.TotalAmount},{r.TotalMonthlyPayment}"));
        return lines;
    }

    public static IReadOnlyList<string> ToDecisionLines(IEnumerable<DecisionReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);

        var lines = new List<string> { "applicationId,applicantId,status,approvedAmount,monthlyPayment,details" };
        lines.AddRange(receipts.Select(r =>
            $"{Cell(r.ApplicationId)},{Cell(r.ApplicantId)},{r.Status},{r.ApprovedAmount},{r.MonthlyPayment},{Cell(r.Details)}"));
        return lines;
    }

    private static string Cell(string value) =>
        value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}

public static class ReportExporter
{
    // Atomic write via .tmp; 'using' disposes StreamWriter before File.Move so Windows does not lock the file.
    public static Result<string, DomainError> Export(string path, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (string.IsNullOrWhiteSpace(path))
            return Res.Fail<string>(new DomainError.ExportFailed(path ?? string.Empty, "Output path is empty."));

        var temp = path + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using (var writer = new StreamWriter(temp, append: false, new UTF8Encoding(false)))
            {
                foreach (var line in lines) writer.WriteLine(line);
            }

            File.Move(temp, path, overwrite: true);
            return Res.Ok(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return Res.Fail<string>(new DomainError.ExportFailed(path, ex.Message));
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception) { /* best effort cleanup; the original error is what matters */ }
    }
}
