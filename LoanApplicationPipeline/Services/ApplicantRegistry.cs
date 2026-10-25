using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Services;

public sealed class ApplicantRegistry
{
    private readonly Dictionary<string, ApplicantProfile> _profiles = new()
    {
        ["A1"] = new ApplicantProfile("A1", "John Doe", 0m, true),      // 0m is a valid amount, not absent
        ["A2"] = new ApplicantProfile("A2", "Jane Smith", 5000m, true),
        ["A3"] = new ApplicantProfile("A3", "Bob Wilson", 0m, true),
        ["A4"] = new ApplicantProfile("A4", "Alice Brown", 25000m, true),
        ["A5"] = new ApplicantProfile("A5", "Charlie Green", 0m, true),
    };

    public Option<ApplicantProfile> FindById(string applicantId)
    {
        if (string.IsNullOrWhiteSpace(applicantId))
            return Option.None<ApplicantProfile>();

        return _profiles.TryGetValue(applicantId, out var profile)
            ? Option.Some(profile)
            : Option.None<ApplicantProfile>();
    }
}