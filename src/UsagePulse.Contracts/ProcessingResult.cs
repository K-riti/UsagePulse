namespace UsagePulse.Contracts;

public sealed record ProcessingResult(
    bool IsSuccess,
    bool IsDuplicate,
    int Attempts,
    ProcessingFailure? Failure = null)
{
    public static ProcessingResult Success(int attempts) => new(true, false, attempts);

    public static ProcessingResult Duplicate() => new(true, true, 0);

    public static ProcessingResult Failure(int attempts, DeadLetterReasonCode reasonCode, string message, ValidationErrorCode? validationCode = null)
        => new(false, false, attempts, new ProcessingFailure(reasonCode, message, validationCode));
}
