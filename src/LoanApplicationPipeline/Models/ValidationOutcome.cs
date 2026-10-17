namespace LoanApplicationPipeline.Models;

/// <summary>
/// Result of the factory. Exactly two mutually exclusive cases:
/// Valid carries the validated value, Invalid carries the reasons.
/// The private base constructor closes the hierarchy: no other cases can exist.
/// </summary>
public abstract record ValidationOutcome
{
    private ValidationOutcome() { }

    public sealed record Valid(ValidatedApplication Application) : ValidationOutcome;

    public sealed record Invalid(IReadOnlyList<string> Reasons) : ValidationOutcome;
}
