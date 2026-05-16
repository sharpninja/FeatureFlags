namespace SharpNinja.FeatureFlags.Admin.Blazor.Services;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: Component-facing wrapper around <see cref="IAdminRuntimeService"/> and <see cref="IAdminRuntimeStore"/>.</summary>
/// <remarks>
/// Scoped accessor; not safe for concurrent use across requests. Resolves the current admin runtime
/// from the active DI scope.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public sealed class AdminRuntimeAccessor
{
    private readonly IAdminRuntimeService runtime;
    private readonly IAdminRuntimeStore store;

    /// <summary>Initializes a new accessor with DI-resolved dependencies.</summary>
    /// <param name="runtime">Admin runtime service for mutating drafts.</param>
    /// <param name="store">Durable store used for direct snapshot queries.</param>
    public AdminRuntimeAccessor(IAdminRuntimeService runtime, IAdminRuntimeStore store)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(store);
        this.runtime = runtime;
        this.store = store;
    }

    /// <summary>Lists current flag drafts.</summary>
    /// <returns>Current drafts.</returns>
    public IReadOnlyList<FeatureFlagDraft> GetDrafts() => runtime.GetDrafts();

    /// <summary>Lists current audit trail entries.</summary>
    /// <returns>Audit entries in sequence order.</returns>
    public IReadOnlyList<AdminAuditEntry> GetAuditTrail() => runtime.GetAuditTrail();

    /// <summary>Gets the current admin runtime metrics snapshot.</summary>
    /// <returns>Metrics snapshot.</returns>
    public AdminRuntimeMetrics GetMetrics() => runtime.GetMetrics();

    /// <summary>Lists drafts asynchronously from the durable store.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Persisted drafts.</returns>
    public ValueTask<IReadOnlyList<FeatureFlagDraft>> ListDraftsAsync(CancellationToken cancellationToken = default) =>
        store.ListDraftsAsync(cancellationToken);

    /// <summary>Creates a draft via the runtime service.</summary>
    /// <param name="mutation">Mutation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created draft snapshot.</returns>
    public ValueTask<FeatureFlagDraft> CreateDraftAsync(FeatureFlagDraftMutation mutation, CancellationToken cancellationToken = default) =>
        runtime.CreateDraftAsync(mutation, cancellationToken);

    /// <summary>Updates an existing draft via the runtime service.</summary>
    /// <param name="mutation">Mutation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated draft snapshot.</returns>
    public ValueTask<FeatureFlagDraft> UpdateDraftAsync(FeatureFlagDraftMutation mutation, CancellationToken cancellationToken = default) =>
        runtime.UpdateDraftAsync(mutation, cancellationToken);

    /// <summary>Publishes a draft via the runtime service.</summary>
    /// <param name="action">Publish action to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish audit entry.</returns>
    public ValueTask<AdminAuditEntry> PublishAsync(FeatureFlagPublishAction action, CancellationToken cancellationToken = default) =>
        runtime.PublishAsync(action, cancellationToken);

    /// <summary>Promotes a draft via the runtime service.</summary>
    /// <param name="action">Promotion action to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The promoted draft snapshot.</returns>
    public ValueTask<FeatureFlagDraft> PromoteAsync(FeatureFlagPromotionAction action, CancellationToken cancellationToken = default) =>
        runtime.PromoteAsync(action, cancellationToken);
}
