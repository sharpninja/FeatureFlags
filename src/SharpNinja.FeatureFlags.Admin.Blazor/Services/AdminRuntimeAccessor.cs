using Microsoft.AspNetCore.Components.Authorization;

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
    private static readonly AdminRbacMetadata UntrustedInputMetadata = new("", "", [], []);
    private readonly IAdminRuntimeService runtime;
    private readonly IAdminRuntimeStore store;
    private readonly AuthenticationStateProvider authenticationStateProvider;
    private readonly IAdminActorResolver actorResolver;

    /// <summary>Initializes a new accessor with DI-resolved dependencies.</summary>
    /// <param name="runtime">Admin runtime service for mutating drafts.</param>
    /// <param name="store">Durable store used for direct snapshot queries.</param>
    /// <param name="authenticationStateProvider">Current authenticated component identity.</param>
    /// <param name="actorResolver">Resolves the actor and grants from identity claims.</param>
    public AdminRuntimeAccessor(
        IAdminRuntimeService runtime,
        IAdminRuntimeStore store,
        AuthenticationStateProvider authenticationStateProvider,
        IAdminActorResolver actorResolver)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authenticationStateProvider);
        ArgumentNullException.ThrowIfNull(actorResolver);
        this.runtime = runtime;
        this.store = store;
        this.authenticationStateProvider = authenticationStateProvider;
        this.actorResolver = actorResolver;
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
    public async ValueTask<FeatureFlagDraft> CreateDraftAsync(FeatureFlagDraftMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        AdminRbacMetadata actor = await ResolveActorMetadataAsync(cancellationToken);
        return await runtime.CreateDraftAsync(mutation with { RbacMetadata = actor }, cancellationToken);
    }

    /// <summary>Creates a draft from page fields with actor identity resolved only by this accessor.</summary>
    /// <param name="flagKey">Flag key.</param>
    /// <param name="environmentName">Target environment.</param>
    /// <param name="productScope">Requested product scope.</param>
    /// <param name="valueType">Flag value type.</param>
    /// <param name="defaultValue">Default flag value.</param>
    /// <param name="ruleDescriptions">Rule descriptions.</param>
    /// <param name="reason">Audit reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created draft.</returns>
    public ValueTask<FeatureFlagDraft> CreateDraftAsync(
        string flagKey,
        string environmentName,
        IReadOnlyCollection<string> productScope,
        string valueType,
        string defaultValue,
        IReadOnlyCollection<string> ruleDescriptions,
        string reason,
        CancellationToken cancellationToken = default) =>
        CreateDraftAsync(new FeatureFlagDraftMutation(
            flagKey, environmentName, productScope, valueType, defaultValue,
            ruleDescriptions, reason, UntrustedInputMetadata), cancellationToken);

    /// <summary>Updates an existing draft via the runtime service.</summary>
    /// <param name="mutation">Mutation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated draft snapshot.</returns>
    public async ValueTask<FeatureFlagDraft> UpdateDraftAsync(FeatureFlagDraftMutation mutation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        AdminRbacMetadata actor = await ResolveActorMetadataAsync(cancellationToken);
        return await runtime.UpdateDraftAsync(mutation with { RbacMetadata = actor }, cancellationToken);
    }

    /// <summary>Updates a draft from page fields with actor identity resolved only by this accessor.</summary>
    /// <param name="flagKey">Flag key.</param>
    /// <param name="environmentName">Target environment.</param>
    /// <param name="productScope">Requested product scope.</param>
    /// <param name="valueType">Flag value type.</param>
    /// <param name="defaultValue">Default flag value.</param>
    /// <param name="ruleDescriptions">Rule descriptions.</param>
    /// <param name="reason">Audit reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated draft.</returns>
    public ValueTask<FeatureFlagDraft> UpdateDraftAsync(
        string flagKey,
        string environmentName,
        IReadOnlyCollection<string> productScope,
        string valueType,
        string defaultValue,
        IReadOnlyCollection<string> ruleDescriptions,
        string reason,
        CancellationToken cancellationToken = default) =>
        UpdateDraftAsync(new FeatureFlagDraftMutation(
            flagKey, environmentName, productScope, valueType, defaultValue,
            ruleDescriptions, reason, UntrustedInputMetadata), cancellationToken);

    /// <summary>Publishes a draft via the runtime service.</summary>
    /// <param name="action">Publish action to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish audit entry.</returns>
    public async ValueTask<AdminAuditEntry> PublishAsync(FeatureFlagPublishAction action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        AdminRbacMetadata actor = await ResolveActorMetadataAsync(cancellationToken);
        return await runtime.PublishAsync(action with { RbacMetadata = actor }, cancellationToken);
    }

    /// <summary>Publishes a draft from page fields with actor identity resolved only by this accessor.</summary>
    /// <param name="flagKey">Flag key.</param>
    /// <param name="environmentName">Target environment.</param>
    /// <param name="reason">Audit reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish audit entry.</returns>
    public ValueTask<AdminAuditEntry> PublishAsync(
        string flagKey,
        string environmentName,
        string reason,
        CancellationToken cancellationToken = default) =>
        PublishAsync(new FeatureFlagPublishAction(
            flagKey, environmentName, reason, UntrustedInputMetadata), cancellationToken);

    /// <summary>Promotes a draft via the runtime service.</summary>
    /// <param name="action">Promotion action to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The promoted draft snapshot.</returns>
    public async ValueTask<FeatureFlagDraft> PromoteAsync(FeatureFlagPromotionAction action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        AdminRbacMetadata actor = await ResolveActorMetadataAsync(cancellationToken);
        return await runtime.PromoteAsync(action with { RbacMetadata = actor }, cancellationToken);
    }

    /// <summary>Promotes a draft from page fields with actor identity resolved only by this accessor.</summary>
    /// <param name="flagKey">Flag key.</param>
    /// <param name="sourceEnvironmentName">Source environment.</param>
    /// <param name="targetEnvironmentName">Target environment.</param>
    /// <param name="reason">Audit reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The promoted draft.</returns>
    public ValueTask<FeatureFlagDraft> PromoteAsync(
        string flagKey,
        string sourceEnvironmentName,
        string targetEnvironmentName,
        string reason,
        CancellationToken cancellationToken = default) =>
        PromoteAsync(new FeatureFlagPromotionAction(
            flagKey, sourceEnvironmentName, targetEnvironmentName, reason,
            UntrustedInputMetadata), cancellationToken);

    private async ValueTask<AdminRbacMetadata> ResolveActorMetadataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuthenticationState state = await authenticationStateProvider.GetAuthenticationStateAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return actorResolver.Resolve(state.User).ToRbacMetadata();
    }
}
