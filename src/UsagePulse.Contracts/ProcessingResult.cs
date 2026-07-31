namespace UsagePulse.Contracts;

public sealed record ProcessingResult(
    bool IsSuccess,
    bool IsDuplicate,
    int Attempts,
    string? Error = null)
{
    public static ProcessingResult Success(int attempts) => new(true, false, attempts);

    public static ProcessingResult Duplicate() => new(true, true, 0);

    public static ProcessingResult Failure(int attempts, string error) => new(false, false, attempts, error);
}
