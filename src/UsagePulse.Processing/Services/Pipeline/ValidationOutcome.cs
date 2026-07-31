using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public sealed record ValidationOutcome(bool IsValid, ProcessingFailure? Failure = null)
{
    public static ValidationOutcome Valid() => new(true);

    public static ValidationOutcome Invalid(ValidationErrorCode code, string message)
        => new(false, new ProcessingFailure(DeadLetterReasonCode.ValidationFailed, message, code));
}
