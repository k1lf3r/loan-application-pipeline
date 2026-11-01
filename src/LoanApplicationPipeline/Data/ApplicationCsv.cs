using System.Globalization;
using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Domain;

namespace LoanApplicationPipeline.Data;

/// <summary>Pure CSV line parsing. No file or console access here.</summary>
public static class ApplicationCsv
{
    public const string Header = "id,name,purpose,amount,termMonths,annualRateBp,monthlyIncome,creditScore,existingDebt";
    private const int ColumnCount = 9;

    public static Result<LoanApplication> ParseLine(string line, int lineNumber)
    {
        var parts = line.Split(',');
        if (parts.Length != ColumnCount)
            return Malformed(lineNumber, $"expected {ColumnCount} columns but found {parts.Length}.");

        var purposeName = Enum.GetNames<LoanPurpose>()
            .FirstOrDefault(n => string.Equals(n, parts[2].Trim(), StringComparison.OrdinalIgnoreCase));
        if (purposeName is null) return Malformed(lineNumber, $"unknown purpose '{parts[2].Trim()}'.");

        if (!TryLong(parts[3], out var amount)) return Malformed(lineNumber, "amount is not a number.");
        if (!TryInt(parts[4], out var term)) return Malformed(lineNumber, "termMonths is not a number.");
        if (!TryInt(parts[5], out var rate)) return Malformed(lineNumber, "annualRateBp is not a number.");
        if (!TryLong(parts[6], out var income)) return Malformed(lineNumber, "monthlyIncome is not a number.");
        if (!TryInt(parts[7], out var credit)) return Malformed(lineNumber, "creditScore is not a number.");
        if (!TryLong(parts[8], out var debt)) return Malformed(lineNumber, "existingDebt is not a number.");

        var created = LoanApplication.Create(parts[0].Trim(), parts[1].Trim(),
            Enum.Parse<LoanPurpose>(purposeName), amount, term, rate, income, credit, debt);

        return created is Result<LoanApplication>.Fail fail
            ? Result<LoanApplication>.Failure(fail.Error.Code, $"Line {lineNumber}: {fail.Error.Message}")
            : created;
    }

    private static Result<LoanApplication> Malformed(int lineNumber, string reason) =>
        Result<LoanApplication>.Failure("csv.malformed", $"Line {lineNumber}: {reason}");

    private static bool TryLong(string text, out long value) =>
        long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryInt(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
