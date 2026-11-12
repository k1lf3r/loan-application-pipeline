namespace LoanApplicationPipeline.Core;

public abstract record DomainError(string Code, string Message)
{
    public sealed record ValidationError(string Field, string Reason)
        : DomainError("VALIDATION_ERROR", $"Field '{Field}': {Reason}");

    public sealed record ApplicantNotFound(string ApplicantId)
        : DomainError("APPLICANT_NOT_FOUND", $"Applicant '{ApplicantId}' not found in registry.");

    public sealed record ApplicantInactive(string ApplicantId)
        : DomainError("APPLICANT_INACTIVE", $"Applicant '{ApplicantId}' is not active.");

    public sealed record IneligibleCreditScore(int Score, int Threshold)
        : DomainError("SCORE_BELOW_THRESHOLD", $"Credit score {Score} is below required threshold ({Threshold}).");

    public sealed record InvalidStateTransition(string CurrentState, string AttemptedAction)
        : DomainError("INVALID_TRANSITION", $"Cannot execute '{AttemptedAction}' from state '{CurrentState}'.");

    public sealed record FileNotFound(string FilePath)
        : DomainError("FILE_NOT_FOUND", $"Data file not found at path: '{FilePath}'.");

    public sealed record MalformedFile(string FilePath, string Details)
        : DomainError("MALFORMED_FILE", $"File '{FilePath}' is malformed: {Details}");

    public sealed record FileUnreadable(string FilePath, string Details)
        : DomainError("FILE_UNREADABLE", $"File '{FilePath}' could not be read: {Details}");

    public sealed record RowRejected(int Row, DomainError Cause)
        : DomainError("ROW_REJECTED", $"Row {Row}: {Cause.Message}");

    public sealed record ExportFailed(string FilePath, string Details)
        : DomainError("EXPORT_FAILED", $"Report could not be written to '{FilePath}': {Details}");

    public sealed record NativeCallFailed(string NativeCode, string Details)
        : DomainError("NATIVE_CALL_FAILED", $"[{NativeCode}] {Details}");
}
