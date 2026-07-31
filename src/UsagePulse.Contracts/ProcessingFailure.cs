namespace UsagePulse.Contracts;

public sealed record ProcessingFailure(
    DeadLetterReasonCode ReasonCode,
    string Message,
    ValidationErrorCode? ValidationCode = null);
