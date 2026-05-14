using System.Collections.ObjectModel;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: DI-resident Admin authoring service backed by a durable store abstraction.</summary>
public sealed class InMemoryAdminRuntimeService : IAdminRuntimeService
{
    private static readonly Action<ILogger, string, string, long, Exception?> DraftCreated =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(9000, nameof(DraftCreated)),
            "Admin draft created for flag '{FeatureFlagKey}' in environment '{EnvironmentName}' at revision {Revision}.");

    private static readonly Action<ILogger, string, string, long, Exception?> DraftUpdated =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(9001, nameof(DraftUpdated)),
            "Admin draft updated for flag '{FeatureFlagKey}' in environment '{EnvironmentName}' at revision {Revision}.");

    private static readonly Action<ILogger, string, string, long, Exception?> DraftPublished =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(9002, nameof(DraftPublished)),
            "Admin draft published for flag '{FeatureFlagKey}' in environment '{EnvironmentName}' at revision {Revision}.");

    private static readonly Action<ILogger, string, string, string, long, Exception?> DraftPromoted =
        LoggerMessage.Define<string, string, string, long>(
            LogLevel.Information,
            new EventId(9003, nameof(DraftPromoted)),
            "Admin draft promoted for flag '{FeatureFlagKey}' from '{SourceEnvironmentName}' to '{TargetEnvironmentName}' at revision {Revision}.");

    private readonly IAdminRuntimeStore store;
    private readonly IAdminRbacAuthorizer authorizer;
    private readonly ILogger<InMemoryAdminRuntimeService> logger;

    /// <summary>Creates the Admin runtime with DI-supplied store, RBAC authorizer, and typed logger.</summary>
    /// <param name="store">Durable Admin runtime store.</param>
    /// <param name="authorizer">Admin RBAC authorizer.</param>
    /// <param name="logger">Typed runtime logger.</param>
    public InMemoryAdminRuntimeService(
        IAdminRuntimeStore store,
        IAdminRbacAuthorizer authorizer,
        ILogger<InMemoryAdminRuntimeService> logger)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async ValueTask<FeatureFlagDraft> CreateDraftAsync(
        FeatureFlagDraftMutation mutation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        NormalizedDraftMutation normalized = NormalizeMutation(mutation);
        EnsureAuthorized(
            normalized.RbacMetadata,
            AdminRight.Edit,
            normalized.EnvironmentName,
            normalized.ProductScope);

        FeatureFlagDraft? existing = await store.FindDraftAsync(
            normalized.FlagKey,
            normalized.EnvironmentName,
            cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Flag draft '{normalized.FlagKey}' already exists in environment '{normalized.EnvironmentName}'.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        FeatureFlagDraft draft = CreateDraft(normalized, revision: 1, now);
        await store.AddDraftAsync(draft, cancellationToken);
        _ = await AppendAuditEntryAsync(AdminAuditAction.Created, draft, targetEnvironmentName: null, now, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            DraftCreated(logger, draft.FlagKey, draft.EnvironmentName, draft.Revision, null);
        }

        return draft;
    }

    /// <inheritdoc />
    public async ValueTask<FeatureFlagDraft> UpdateDraftAsync(
        FeatureFlagDraftMutation mutation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        NormalizedDraftMutation normalized = NormalizeMutation(mutation);
        EnsureAuthorized(
            normalized.RbacMetadata,
            AdminRight.Edit,
            normalized.EnvironmentName,
            normalized.ProductScope);

        FeatureFlagDraft? existing = await store.FindDraftAsync(
            normalized.FlagKey,
            normalized.EnvironmentName,
            cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException(
                $"Flag draft '{normalized.FlagKey}' does not exist in environment '{normalized.EnvironmentName}'.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        FeatureFlagDraft draft = CreateDraft(normalized, existing.Revision + 1, now);
        await store.UpdateDraftAsync(draft, cancellationToken);
        _ = await AppendAuditEntryAsync(AdminAuditAction.Updated, draft, targetEnvironmentName: null, now, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            DraftUpdated(logger, draft.FlagKey, draft.EnvironmentName, draft.Revision, null);
        }

        return draft;
    }

    /// <inheritdoc />
    public async ValueTask<AdminAuditEntry> PublishAsync(
        FeatureFlagPublishAction action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(action);

        string flagKey = NormalizeRequired(action.FlagKey, nameof(action.FlagKey));
        string environmentName = NormalizeEnvironmentName(action.EnvironmentName);
        string reason = NormalizeRequired(action.Reason, nameof(action.Reason));
        AdminRbacMetadata rbacMetadata = NormalizeRbac(action.RbacMetadata);

        FeatureFlagDraft? draft = await store.FindDraftAsync(flagKey, environmentName, cancellationToken);
        if (draft is null)
        {
            throw new InvalidOperationException(
                $"Flag draft '{flagKey}' does not exist in environment '{environmentName}'.");
        }

        EnsureAuthorized(rbacMetadata, AdminRight.Publish, draft.EnvironmentName, draft.ProductScope);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        FeatureFlagDraft auditedDraft = draft with
        {
            LastReason = reason,
            LastRbacMetadata = rbacMetadata,
        };

        AdminAuditEntry entry = await AppendAuditEntryAsync(
            AdminAuditAction.Published,
            auditedDraft,
            targetEnvironmentName: null,
            now,
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            DraftPublished(logger, draft.FlagKey, draft.EnvironmentName, draft.Revision, null);
        }

        return entry;
    }

    /// <inheritdoc />
    public async ValueTask<FeatureFlagDraft> PromoteAsync(
        FeatureFlagPromotionAction action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(action);

        string flagKey = NormalizeRequired(action.FlagKey, nameof(action.FlagKey));
        string sourceEnvironmentName = NormalizeEnvironmentName(action.SourceEnvironmentName);
        string targetEnvironmentName = NormalizeEnvironmentName(action.TargetEnvironmentName);
        string reason = NormalizeRequired(action.Reason, nameof(action.Reason));
        AdminRbacMetadata rbacMetadata = NormalizeRbac(action.RbacMetadata);

        if (string.Equals(sourceEnvironmentName, targetEnvironmentName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Promotion source and target environments must be different.");
        }

        FeatureFlagDraft? sourceDraft = await store.FindDraftAsync(flagKey, sourceEnvironmentName, cancellationToken);
        if (sourceDraft is null)
        {
            throw new InvalidOperationException(
                $"Flag draft '{flagKey}' does not exist in environment '{sourceEnvironmentName}'.");
        }

        EnsureAuthorized(rbacMetadata, AdminRight.Promote, sourceDraft.EnvironmentName, sourceDraft.ProductScope);

        FeatureFlagDraft? targetDraft = await store.FindDraftAsync(flagKey, targetEnvironmentName, cancellationToken);
        long nextRevision = targetDraft is null ? 1 : targetDraft.Revision + 1;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        FeatureFlagDraft promotedDraft = sourceDraft with
        {
            EnvironmentName = targetEnvironmentName,
            LastReason = reason,
            LastRbacMetadata = rbacMetadata,
            LastModifiedAt = now,
            Revision = nextRevision,
        };

        if (targetDraft is null)
        {
            await store.AddDraftAsync(promotedDraft, cancellationToken);
        }
        else
        {
            await store.UpdateDraftAsync(promotedDraft, cancellationToken);
        }

        FeatureFlagDraft auditDraft = promotedDraft with
        {
            EnvironmentName = sourceEnvironmentName,
        };
        _ = await AppendAuditEntryAsync(
            AdminAuditAction.Promoted,
            auditDraft,
            targetEnvironmentName,
            now,
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            DraftPromoted(
                logger,
                promotedDraft.FlagKey,
                sourceEnvironmentName,
                promotedDraft.EnvironmentName,
                promotedDraft.Revision,
                null);
        }

        return promotedDraft;
    }

    /// <inheritdoc />
    public IReadOnlyList<FeatureFlagDraft> GetDrafts() =>
        store.ListDraftsAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlyList<AdminAuditEntry> GetAuditTrail() =>
        store.ListAuditTrailAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public AdminRuntimeMetrics GetMetrics() =>
        store.GetMetricsAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

    private static FeatureFlagDraft CreateDraft(
        NormalizedDraftMutation normalized,
        long revision,
        DateTimeOffset now) =>
        new(
            normalized.FlagKey,
            normalized.EnvironmentName,
            ToReadOnlyCollection(normalized.ProductScope),
            normalized.ValueType,
            normalized.DefaultValue,
            ToReadOnlyCollection(normalized.RuleDescriptions),
            normalized.Reason,
            normalized.RbacMetadata,
            revision,
            now);

    private async ValueTask<AdminAuditEntry> AppendAuditEntryAsync(
        AdminAuditAction action,
        FeatureFlagDraft draft,
        string? targetEnvironmentName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entry = new AdminAuditEntry(
            Sequence: 0,
            action,
            draft.FlagKey,
            draft.EnvironmentName,
            targetEnvironmentName,
            ToReadOnlyCollection(draft.ProductScope),
            draft.ValueType,
            draft.DefaultValue,
            ToReadOnlyCollection(draft.RuleDescriptions),
            draft.LastReason,
            draft.LastRbacMetadata,
            draft.Revision,
            now);

        return await store.AppendAuditEntryAsync(entry, cancellationToken);
    }

    private void EnsureAuthorized(
        AdminRbacMetadata rbacMetadata,
        AdminRight right,
        string environmentName,
        IReadOnlyCollection<string> productScope)
    {
        AdminAuthorizationResult result = authorizer.Authorize(
            rbacMetadata,
            right,
            new AdminResourceScope(rbacMetadata.TenantId, productScope, environmentName));

        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException(result.FailureReason);
        }
    }

    private static NormalizedDraftMutation NormalizeMutation(FeatureFlagDraftMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        string[] productScope = NormalizeProductScope(mutation.ProductScope, nameof(mutation.ProductScope));
        AdminRbacMetadata rbacMetadata = NormalizeRbac(mutation.RbacMetadata);

        return new NormalizedDraftMutation(
            NormalizeRequired(mutation.FlagKey, nameof(mutation.FlagKey)),
            NormalizeEnvironmentName(mutation.EnvironmentName),
            productScope,
            NormalizeRequired(mutation.ValueType, nameof(mutation.ValueType)),
            NormalizeRequired(mutation.DefaultValue, nameof(mutation.DefaultValue)),
            NormalizeTextCollection(mutation.RuleDescriptions, nameof(mutation.RuleDescriptions)),
            NormalizeRequired(mutation.Reason, nameof(mutation.Reason)),
            rbacMetadata);
    }

    private static AdminRbacMetadata NormalizeRbac(AdminRbacMetadata rbacMetadata)
    {
        ArgumentNullException.ThrowIfNull(rbacMetadata);

        string[] productIds = NormalizeRbacProductIds(rbacMetadata.ProductIds, nameof(rbacMetadata.ProductIds));
        string[] roleIds = NormalizeTextCollection(rbacMetadata.RoleIds, nameof(rbacMetadata.RoleIds));
        if (roleIds.Length == 0)
        {
            throw new InvalidOperationException("At least one RBAC role is required.");
        }

        return new AdminRbacMetadata(
            NormalizeRequired(rbacMetadata.TenantId, nameof(rbacMetadata.TenantId)),
            NormalizeRequired(rbacMetadata.PrincipalId, nameof(rbacMetadata.PrincipalId)),
            ToReadOnlyCollection(productIds),
            ToReadOnlyCollection(roleIds));
    }

    private static string NormalizeEnvironmentName(string environmentName)
    {
        string normalized = NormalizeRequired(environmentName, nameof(environmentName));
        if (string.Equals(normalized, "dev", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, SharpNinjaDeploymentEnvironment.Development.Name, StringComparison.OrdinalIgnoreCase))
        {
            return SharpNinjaDeploymentEnvironment.Development.Name;
        }

        if (string.Equals(normalized, SharpNinjaDeploymentEnvironment.Staging.Name, StringComparison.OrdinalIgnoreCase))
        {
            return SharpNinjaDeploymentEnvironment.Staging.Name;
        }

        if (string.Equals(normalized, SharpNinjaDeploymentEnvironment.Production.Name, StringComparison.OrdinalIgnoreCase))
        {
            return SharpNinjaDeploymentEnvironment.Production.Name;
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string[] NormalizeProductScope(IReadOnlyCollection<string> productIds, string parameterName)
    {
        string[] normalized = NormalizeTextCollection(productIds, parameterName);
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("At least one product scope is required.");
        }

        foreach (string candidate in normalized)
        {
            if (!SharpNinjaProductCatalog.IsV1Product(candidate))
            {
                throw new InvalidOperationException($"Product '{candidate}' is not registered in the v1 product catalog.");
            }
        }

        return normalized;
    }

    private static string[] NormalizeRbacProductIds(IReadOnlyCollection<string> productIds, string parameterName)
    {
        string[] normalized = NormalizeTextCollection(productIds, parameterName);
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("At least one RBAC product grant is required.");
        }

        foreach (string candidate in normalized)
        {
            if (!string.Equals(candidate, "*", StringComparison.Ordinal)
                && !SharpNinjaProductCatalog.IsV1Product(candidate))
            {
                throw new InvalidOperationException($"Product '{candidate}' is not registered in the v1 product catalog.");
            }
        }

        return normalized;
    }

    private static string[] NormalizeTextCollection(IReadOnlyCollection<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        List<string> normalized = [];
        foreach (string value in values)
        {
            string candidate = NormalizeRequired(value, parameterName);
            if (!ContainsOrdinal(normalized, candidate))
            {
                normalized.Add(candidate);
            }
        }

        return normalized.ToArray();
    }

    private static bool ContainsOrdinal(IEnumerable<string> values, string value)
    {
        foreach (string candidate in values)
        {
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlyCollection<string> ToReadOnlyCollection(IEnumerable<string> values) =>
        Array.AsReadOnly(values.ToArray());

    private sealed record NormalizedDraftMutation(
        string FlagKey,
        string EnvironmentName,
        string[] ProductScope,
        string ValueType,
        string DefaultValue,
        string[] RuleDescriptions,
        string Reason,
        AdminRbacMetadata RbacMetadata);
}
