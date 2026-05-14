namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: DI-resolved admin authoring and audit runtime service.</summary>
public interface IAdminRuntimeService
{
    /// <summary>FR-9 FR-10 TR-9: Creates a product-scoped flag draft and appends an audit entry.</summary>
    /// <param name="mutation">Draft mutation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created draft snapshot.</returns>
    ValueTask<FeatureFlagDraft> CreateDraftAsync(
        FeatureFlagDraftMutation mutation,
        CancellationToken cancellationToken = default);

    /// <summary>FR-9 FR-10 TR-9: Updates a product-scoped flag draft and appends an audit entry.</summary>
    /// <param name="mutation">Draft mutation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated draft snapshot.</returns>
    ValueTask<FeatureFlagDraft> UpdateDraftAsync(
        FeatureFlagDraftMutation mutation,
        CancellationToken cancellationToken = default);

    /// <summary>FR-9 FR-11 TR-9: Publishes an existing draft and appends an audit entry.</summary>
    /// <param name="action">Publish action to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish audit entry.</returns>
    ValueTask<AdminAuditEntry> PublishAsync(
        FeatureFlagPublishAction action,
        CancellationToken cancellationToken = default);

    /// <summary>FR-9 FR-11 TR-9: Promotes an existing draft to another environment and appends an audit entry.</summary>
    /// <param name="action">Promotion action to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The promoted draft snapshot.</returns>
    ValueTask<FeatureFlagDraft> PromoteAsync(
        FeatureFlagPromotionAction action,
        CancellationToken cancellationToken = default);

    /// <summary>FR-9 FR-10 FR-11: Gets immutable draft snapshots currently held by the in-memory runtime.</summary>
    /// <returns>Current flag drafts.</returns>
    IReadOnlyList<FeatureFlagDraft> GetDrafts();

    /// <summary>FR-9: Gets immutable append-only audit snapshots in sequence order.</summary>
    /// <returns>Audit entries.</returns>
    IReadOnlyList<AdminAuditEntry> GetAuditTrail();

    /// <summary>TR-10: Gets lightweight metrics for the in-memory admin runtime.</summary>
    /// <returns>Admin runtime metrics.</returns>
    AdminRuntimeMetrics GetMetrics();
}
