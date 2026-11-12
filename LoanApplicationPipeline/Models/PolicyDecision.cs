namespace LoanApplicationPipeline.Models;

public abstract record PolicyDecision
{
    private PolicyDecision() { }

    public sealed record Approve(long Amount) : PolicyDecision;
    public sealed record Reject(string Reason) : PolicyDecision;
}
