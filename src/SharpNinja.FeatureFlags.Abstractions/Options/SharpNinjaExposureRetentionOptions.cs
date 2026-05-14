namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-8 v1 contract: user-definable exposure event retention policy.</summary>
/// <param name="RetentionPeriod">Exposure event retention period, or <see langword="null" /> for indefinite retention.</param>
public sealed record SharpNinjaExposureRetentionOptions(TimeSpan? RetentionPeriod)
{
    /// <summary>FR-8 v1 contract: default exposure event retention period.</summary>
    public static SharpNinjaExposureRetentionOptions Default { get; } = new(TimeSpan.FromDays(90));

    /// <summary>FR-8 v1 contract: policy for retaining exposure events indefinitely.</summary>
    public static SharpNinjaExposureRetentionOptions Indefinite { get; } = new((TimeSpan?)null);

    /// <summary>FR-8 v1 contract: validates exposure retention invariants.</summary>
    /// <returns>The current exposure retention options when validation succeeds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the retention period is zero or negative.</exception>
    public SharpNinjaExposureRetentionOptions Validate()
    {
        if (RetentionPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetentionPeriod),
                RetentionPeriod,
                "Exposure retention period must be greater than zero when specified.");
        }

        return this;
    }
}
