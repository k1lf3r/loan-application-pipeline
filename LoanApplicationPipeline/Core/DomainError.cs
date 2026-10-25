namespace LoanApplicationPipeline.Core;

public abstract record DomainError(string Code, string Message)
{
    public sealed record ValidationError(string Field, string Reason)
        : DomainError("VALIDATION_ERROR", $"Field '{Field}': {Reason}");

    public sealed record ApplicantNotFound(string ApplicantId)
        : DomainError("APPLICANT_NOT_FOUND", $"Applicant '{ApplicantId}' not found in registry.");

    public sealed record IneligibleCreditScore(int Score, int Threshold)
        : DomainError("SCORE_BELOW_THRESHOLD", $"Credit score {Score} is below required threshold ({Threshold}).");

    public sealed record InvalidStateTransition(string CurrentState, string AttemptedAction)
        : DomainError("INVALID_TRANSITION", $"Cannot execute '{AttemptedAction}' from terminal state '{CurrentState}'.");

    public sealed record FileNotFound(string FilePath)
        : DomainError("FILE_NOT_FOUND", $"Data file not found at path: '{FilePath}'.");

    public sealed record MalformedFile(string FilePath, string Details)
        : DomainError("MALFORMED_FILE", $"File '{FilePath}' is malformed: {Details}");
}