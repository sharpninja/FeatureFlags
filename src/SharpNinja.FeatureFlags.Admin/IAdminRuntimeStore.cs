namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: durable Admin runtime store contract behind <see cref="IAdminRuntimeService" />.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface IAdminRuntimeStore
{
    /// <summary>FR-9 FR-10 FR-11: finds a draft by flag and environment.</summary>
    /// <param name="flagKey">Feature flag key.</param>
    /// <param name="environmentName">Environment name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The draft snapshot, or null when none exists.</returns>
    ValueTask<FeatureFlagDraft?> FindDraftAsync(
        string flagKey,
        string environmentName,
        CancellationToken cancellationToken);

    /// <summary>FR-9 FR-10 FR-11: inserts a new draft snapshot.</summary>
    /// <param name="draft">Draft to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask AddDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken);

    /// <summary>FR-9 FR-10 FR-11: updates an existing draft snapshot.</summary>
    /// <param name="draft">Draft to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completion task.</returns>
    ValueTask UpdateDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken);

    /// <summary>FR-9 TR-9: appends one audit entry and assigns the durable sequence.</summary>
    /// <param name="entry">Entry to append with sequence zero.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted append-only audit entry.</returns>
    ValueTask<AdminAuditEntry> AppendAuditEntryAsync(AdminAuditEntry entry, CancellationToken cancellationToken);

    /// <summary>FR-9 FR-10 FR-11: lists immutable draft snapshots.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current drafts.</returns>
    ValueTask<IReadOnlyList<FeatureFlagDraft>> ListDraftsAsync(CancellationToken cancellationToken);

    /// <summary>FR-9: lists append-only audit entries in sequence order.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audit entries.</returns>
    ValueTask<IReadOnlyList<AdminAuditEntry>> ListAuditTrailAsync(CancellationToken cancellationToken);

    /// <summary>TR-10: calculates Admin runtime metrics from persisted store state.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Admin runtime metrics.</returns>
    ValueTask<AdminRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken);
}
