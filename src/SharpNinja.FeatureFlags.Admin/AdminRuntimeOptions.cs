namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: host-configured Admin runtime options.</summary>
public sealed record AdminRuntimeOptions
{
    /// <summary>TR-9: authentication and claims configuration for Admin endpoints.</summary>
    public AdminAuthenticationOptions Authentication { get; } = new();

    /// <summary>FR-9 TR-10: retention configuration for Admin evidence and non-audit runtime data.</summary>
    public AdminRetentionOptions Retention { get; } = new();

    /// <summary>FR-9 FR-10 FR-11 TR-9 TR-10: validates Admin runtime options before DI registration.</summary>
    /// <returns>The validated options.</returns>
    public AdminRuntimeOptions Validate()
    {
        Authentication.Validate();
        Retention.Validate();
        return this;
    }
}

/// <summary>FR-9 TR-10 TR-11: user-defined retention windows for Admin evidence data.</summary>
public sealed record AdminRetentionOptions
{
    /// <summary>FR-9: retention period for publish evidence packages; null keeps evidence indefinitely.</summary>
    public TimeSpan? PublishEvidenceRetentionPeriod { get; set; }

    /// <summary>TR-10: retention period for metric snapshots; null keeps snapshots indefinitely.</summary>
    public TimeSpan? MetricSnapshotRetentionPeriod { get; set; }

    /// <summary>FR-9: validates retention windows without allowing destructive audit pruning.</summary>
    /// <returns>The validated options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a configured finite window is not positive.</exception>
    public AdminRetentionOptions Validate()
    {
        ValidatePositive(PublishEvidenceRetentionPeriod, nameof(PublishEvidenceRetentionPeriod));
        ValidatePositive(MetricSnapshotRetentionPeriod, nameof(MetricSnapshotRetentionPeriod));
        return this;
    }

    private static void ValidatePositive(TimeSpan? value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Retention periods must be positive when specified.");
        }
    }
}
