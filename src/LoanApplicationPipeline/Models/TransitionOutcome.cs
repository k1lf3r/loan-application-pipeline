namespace LoanApplicationPipeline.Models;

/// <summary>
/// Result of a state transition: either the next state, or a clear rejection.
/// A rejected transition never produces a new state.
/// </summary>
public abstract record TransitionOutcome
{
    private TransitionOutcome() { }

    public sealed record Moved(ApplicationState Next) : TransitionOutcome;

    public sealed record NotAllowed(string Reason) : TransitionOutcome;
}
