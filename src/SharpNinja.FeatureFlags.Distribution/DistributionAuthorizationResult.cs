namespace SharpNinja.FeatureFlags.Distribution;

internal sealed record DistributionAuthorizationResult(
    bool Succeeded,
    int StatusCode,
    string? FailureCode)
{
    public static DistributionAuthorizationResult Success { get; } = new(true, StatusCodes.Status200OK, null);

    public static DistributionAuthorizationResult Unauthorized(string code) =>
        new(false, StatusCodes.Status401Unauthorized, code);

    public static DistributionAuthorizationResult Forbidden(string code) =>
        new(false, StatusCodes.Status403Forbidden, code);
}
