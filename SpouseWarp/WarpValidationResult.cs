namespace SpouseWarp;

internal readonly record struct WarpValidationResult(bool Success, string? Message)
{
    public static WarpValidationResult Pass()
    {
        return new WarpValidationResult(true, null);
    }

    public static WarpValidationResult Fail(string message)
    {
        return new WarpValidationResult(false, message);
    }
}
