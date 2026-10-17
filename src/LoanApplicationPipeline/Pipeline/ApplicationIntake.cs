using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Pipeline;

public sealed record IntakeResult(
    IReadOnlyList<ValidatedApplication> Accepted,
    IReadOnlyList<string> Rejected);

public static class ApplicationIntake
{
    /// <summary>
    /// Runs the factory on every raw record and matches both outcome cases.
    /// The input collection is never modified.
    /// </summary>
    public static IntakeResult Process(IEnumerable<RawApplicationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var accepted = new List<ValidatedApplication>();
        var rejected = new List<string>();

        foreach (var raw in records)
        {
            switch (ValidatedApplication.Create(raw))
            {
                case ValidationOutcome.Valid valid:
                    accepted.Add(valid.Application);
                    break;
                case ValidationOutcome.Invalid invalid:
                    rejected.AddRange(invalid.Reasons);
                    break;
                default:
                    throw new InvalidOperationException("Unknown validation outcome.");
            }
        }

        return new IntakeResult(accepted, rejected);
    }
}
