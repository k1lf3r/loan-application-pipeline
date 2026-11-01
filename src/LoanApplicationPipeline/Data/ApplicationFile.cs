using System.Text;
using LoanApplicationPipeline.Common;
using LoanApplicationPipeline.Domain;

namespace LoanApplicationPipeline.Data;

/// <summary>Valid applications plus the lines that were rejected (kept as explicit errors).</summary>
public sealed record LoadedApplications(IReadOnlyList<LoanApplication> Valid, IReadOnlyList<Error> Rejected);

/// <summary>
/// File boundary. OWNER of every reader it opens; the reader is released inside this class.
/// </summary>
public static class ApplicationFile
{
    /// <summary>
    /// DEFERRED (yield return). Calling this method runs none of the body: the reader is not opened
    /// until the first MoveNext. One parsed record is produced per iteration step.
    /// OWNER: this iterator owns the TextReader it gets from <paramref name="open"/>.
    /// RELEASE POINT: the 'using' below disposes it when iteration finishes, when the consumer stops
    /// early (break / Take), or when the consumer throws (foreach disposes the enumerator).
    /// </summary>
    public static IEnumerable<Result<LoanApplication>> Stream(Func<TextReader> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        using var reader = open();

        var header = reader.ReadLine();
        if (header is null || header.Trim() != ApplicationCsv.Header)
        {
            yield return Result<LoanApplication>.Failure("csv.bad_header",
                $"Expected header '{ApplicationCsv.Header}'.");
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

    /// <summary>Streams a real file. The StreamReader created here is the one disposed by <see cref="Stream"/>.</summary>
    public static IEnumerable<Result<LoanApplication>> StreamFile(string path) =>
        Stream(() => new StreamReader(path, Encoding.UTF8));

    /// <summary>
    /// MATERIALISATION POINT. Reads the whole source once inside a bounded resource scope and returns a
    /// snapshot (arrays). Later pure processing can enumerate the snapshot as often as it likes without
    /// touching the file again. I/O problems become a Fail, not an exception.
    /// </summary>
    public static Result<LoadedApplications> Load(Func<TextReader> open)
    {
        try
        {
            var snapshot = Stream(open).ToArray();

            var badHeader = snapshot.OfType<Result<LoanApplication>.Fail>()
                .FirstOrDefault(f => f.Error.Code == "csv.bad_header");
            if (badHeader is not null)
                return Result<LoadedApplications>.Failure(badHeader.Error);

            var valid = snapshot.OfType<Result<LoanApplication>.Ok>().Select(o => o.Value).ToArray();
            var rejected = snapshot.OfType<Result<LoanApplication>.Fail>().Select(f => f.Error).ToArray();
            return Result<LoadedApplications>.Success(new LoadedApplications(valid, rejected));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result<LoadedApplications>.Failure("file.unreadable", ex.Message);
        }
    }

    public static Result<LoadedApplications> Load(string path) =>
        Load(() => new StreamReader(path, Encoding.UTF8));
}
