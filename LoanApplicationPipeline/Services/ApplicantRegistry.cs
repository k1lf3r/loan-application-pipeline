using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Services;

public sealed class ApplicantRegistry
{
    private readonly IReadOnlyDictionary<string, ApplicantProfile> _profiles;

    private ApplicantRegistry(IReadOnlyDictionary<string, ApplicantProfile> profiles) => _profiles = profiles;

    public static ApplicantRegistry Empty { get; } = new(new Dictionary<string, ApplicantProfile>());

    public int Count => _profiles.Count;

    public static Result<ApplicantRegistry, DomainError> FromProfiles(IEnumerable<ApplicantProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var map = new Dictionary<string, ApplicantProfile>(StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            if (!map.TryAdd(profile.ApplicantId, profile))
            {
                return Res.Fail<ApplicantRegistry>(
                    new DomainError.ValidationError("ApplicantId", $"Duplicate applicant id '{profile.ApplicantId}'."));
            }
        }

        return Res.Ok(new ApplicantRegistry(map));
    }

    public Option<ApplicantProfile> FindById(string applicantId)
    {
        if (string.IsNullOrWhiteSpace(applicantId))
            return Option.None<ApplicantProfile>();

        return _profiles.TryGetValue(applicantId.Trim(), out var profile)
            ? Option.Some(profile)
            : Option.None<ApplicantProfile>();
    }
}
