using System.Text.Json;
using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Services;

public static class ApplicantFile
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static Result<ApplicantRegistry, DomainError> Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Res.Fail<ApplicantRegistry>(new DomainError.FileNotFound(path ?? string.Empty));

        try
        {
            return Parse(File.ReadAllText(path), path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Res.Fail<ApplicantRegistry>(new DomainError.FileUnreadable(path, ex.Message));
        }
    }

    public static Result<ApplicantRegistry, DomainError> Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);

        List<RawApplicantRecord?>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<RawApplicantRecord?>>(json, Options);
        }
        catch (JsonException ex)
        {
            return Res.Fail<ApplicantRegistry>(new DomainError.MalformedFile(source, ex.Message));
        }

        if (raw is null)
            return Res.Fail<ApplicantRegistry>(new DomainError.MalformedFile(source, "JSON root resolved to null."));

        var profiles = new List<ApplicantProfile>();
        for (var i = 0; i < raw.Count; i++)
        {
            var row = i + 1;
            var record = raw[i];

            if (record is null)
            {
                return Res.Fail<ApplicantRegistry>(new DomainError.RowRejected(
                    row, new DomainError.ValidationError("record", "Entry is null.")));
            }

            var created = ApplicantProfile
                .Create(record)
                .MapError(error => (DomainError)new DomainError.RowRejected(row, error));

            switch (created)
            {
                case Result<ApplicantProfile, DomainError>.Ok ok:
                    profiles.Add(ok.Value);
                    break;
                case Result<ApplicantProfile, DomainError>.Err err:
                    return Res.Fail<ApplicantRegistry>(err.Error);
            }
        }

        return ApplicantRegistry.FromProfiles(profiles);
    }
}
