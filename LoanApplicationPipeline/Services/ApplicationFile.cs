using System.Text;
using LoanApplicationPipeline.Core;

namespace LoanApplicationPipeline.Services;

public sealed record LoadedRecords(IReadOnlyList<CsvRecord> Records, IReadOnlyList<DomainError> Rejected);

public static class ApplicationFile
{
    // Deferred iterator owns the TextReader and disposes it via 'using' on completion, early break, or exception.
    public static IEnumerable<Result<CsvRecord, DomainError>> Stream(Func<TextReader> open, string source = "(csv)")
    {
        ArgumentNullException.ThrowIfNull(open);
        return StreamCore(open, source);
    }

    private static IEnumerable<Result<CsvRecord, DomainError>> StreamCore(Func<TextReader> open, string source)
    {
        using var reader = open();

        var header = reader.ReadLine();
        if (header is null || header.Trim() != ApplicationCsv.Header)
        {
            yield return Res.Fail<CsvRecord>(
                new DomainError.MalformedFile(source, $"Expected header '{ApplicationCsv.Header}'."));
            yield break;
        }

        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return ApplicationCsv.ParseLine(line, lineNumber);
        }
    }

    public static Result<LoadedRecords, DomainError> Load(Func<TextReader> open, string source = "(csv)")
    {
        try
        {
            var snapshot = Stream(open, source).ToArray();
            var (records, errors) = snapshot.Partition();

            var headerProblem = errors.OfType<DomainError.MalformedFile>().FirstOrDefault();
            if (headerProblem is not null)
                return Res.Fail<LoadedRecords>(headerProblem);

            return Res.Ok(new LoadedRecords(records, errors));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Res.Fail<LoadedRecords>(new DomainError.FileUnreadable(source, ex.Message));
        }
    }

    public static Result<LoadedRecords, DomainError> LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Res.Fail<LoadedRecords>(new DomainError.FileNotFound(path ?? string.Empty));

        return Load(() => new StreamReader(path, Encoding.UTF8), path);
    }
}
