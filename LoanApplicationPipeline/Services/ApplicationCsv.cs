using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Services;

public sealed record CsvRecord(int Line, RawApplicationRecord Raw);

public static class ApplicationCsv
{
    public const string Header = "id,applicantId,purpose,amount,termMonths,annualRateBp,monthlyIncome,creditScore";
    public const int ColumnCount = 8;

    public static Result<CsvRecord, DomainError> ParseLine(string line, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(line);

        var parts = line.Split(',');
        if (parts.Length != ColumnCount)
        {
            return Res.Fail<CsvRecord>(new DomainError.RowRejected(lineNumber,
                new DomainError.ValidationError("row", $"expected {ColumnCount} columns but found {parts.Length}.")));
        }

        var raw = new RawApplicationRecord(
            parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], parts[7]);

        return Res.Ok(new CsvRecord(lineNumber, raw));
    }
}
