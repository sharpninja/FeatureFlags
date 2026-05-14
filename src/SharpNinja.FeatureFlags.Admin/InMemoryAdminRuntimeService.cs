using System.Collections.ObjectModel;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: DI-resident in-memory admin authoring and immutable audit runtime.</summary>
public sealed class InMemoryAdminRuntimeService : IAdminRuntimeService
{
    private static readonly Action<ILogger, string, string, long, Exception?> s_draftCreated =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(9000, nameof(s_draftCreated)),
            "Admin draft created for flag '{FeatureFlagKey}' in environment '{EnvironmentName}' at revision {Revision}.");

    private static readonly Action<ILogger, string, string, long, Exception?> s_draftUpdated =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(9001, nameof(s_draftUpdated)),
            "Admin draft updated for flag '{FeatureFlagKey}' in environment '{EnvironmentName}' at revision {Revision}.");

    private static readonly Action<ILogger, string, string, long, Exception?> s_draftPublished =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(9002, nameof(s_draftPublished)),
            "Admin draft published for flag '{FeatureFlagKey}' in environment '{EnvironmentName}' at revision {Revision}.");

    private static readonly Action<ILogger, string, string, string, long, Exception?> s_draftPromoted =
        LoggerMessage.Define<string, string, string, long>(
            LogLevel.Information,
            new EventId(9003, nameof(s_draftPromoted)),
            "Admin draft promoted for flag '{FeatureFlagKey}' from '{SourceEnvironmentName}' to '{TargetEnvironmentName}' at revision {Revision}.");

    private readonly Dictionary<FlagCoordinate, FeatureFlagDraft> _drafts = [];
    private readonly List<AdminAuditEntry> _auditEntries = [];
    private readonly ILogger<InMemoryAdminRuntimeService> _logger;
    private readonly object _sync = new();
    private long _nextAuditSequence;

    /// <summary>Creates the in-memory admin runtime with a typed logger supplied by DI.</summary>
    /// <param name="logger">Typed runtime logger.</param>
    public InMemoryAdminRuntimeService(ILogger<InMemoryAdminRuntimeService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ValueTask<FeatureFlagDraft> CreateDraftAsync(
        FeatureFlagDraftMutation mutation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        NormalizedDraftMutation normalized = NormalizeMutation(mutation);
        var coordinate = new FlagCoordinate(normalized.FlagKey, normalized.EnvironmentName);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (_drafts.ContainsKey(coordinate))
            {
                throw new InvalidOperationException(
                    $"Flag draft '{normalized.FlagKey}' already exists in environment '{normalized.EnvironmentName}'.");
            }

            FeatureFlagDraft draft = CreateDraft(normalized, revision: 1, now);
            _drafts.Add(coordinate, draft);
            _ = AppendAuditEntry(AdminAuditAction.Created, draft, targetEnvironmentName: null, now);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                s_draftCreated(_logger, draft.FlagKey, draft.EnvironmentName, draft.Revision, null);
            }

            return ValueTask.FromResult(draft);
        }
    }

    /// <inheritdoc />
    public ValueTask<FeatureFlagDraft> UpdateDraftAsync(
        FeatureFlagDraftMutation mutation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        NormalizedDraftMutation normalized = NormalizeMutation(mutation);
        var coordinate = new FlagCoordinate(normalized.FlagKey, normalized.EnvironmentName);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (!_drafts.TryGetValue(coordinate, out FeatureFlagDraft? existing))
            {
                throw new InvalidOperationException(
                    $"Flag draft '{normalized.FlagKey}' does not exist in environment '{normalized.EnvironmentName}'.");
            }

            FeatureFlagDraft draft = CreateDraft(normalized, existing.Revision + 1, now);
            _drafts[coordinate] = draft;
            _ = AppendAuditEntry(AdminAuditAction.Updated, draft, targetEnvironmentName: null, now);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                s_draftUpdated(_logger, draft.FlagKey, draft.EnvironmentName, draft.Revision, null);
            }

            return ValueTask.FromResult(draft);
        }
    }

    /// <inheritdoc />
    public ValueTask<AdminAuditEntry> PublishAsync(
        FeatureFlagPublishAction action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(action);

        string flagKey = NormalizeRequired(action.FlagKey, nameof(action.FlagKey));
        string environmentName = NormalizeEnvironmentName(action.EnvironmentName);
        string reason = NormalizeRequired(action.Reason, nameof(action.Reason));
        AdminRbacMetadata rbacMetadata = NormalizeRbac(action.RbacMetadata);
        var coordinate = new FlagCoordinate(flagKey, environmentName);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (!_drafts.TryGetValue(coordinate, out FeatureFlagDraft? draft))
            {
                throw new InvalidOperationException(
                    $"Flag draft '{flagKey}' does not exist in environment '{environmentName}'.");
            }

            ValidateRbacAllowsProducts(rbacMetadata, draft.ProductScope);
            FeatureFlagDraft auditedDraft = draft with
            {
                LastReason = reason,
                LastRbacMetadata = rbacMetadata,
            };
            AdminAuditEntry entry = AppendAuditEntry(
                AdminAuditAction.Published,
                auditedDraft,
                targetEnvironmentName: null,
                now);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                s_draftPublished(_logger, draft.FlagKey, draft.EnvironmentName, draft.Revision, null);
            }

            return ValueTask.FromResult(entry);
        }
    }

    /// <inheritdoc />
    public ValueTask<FeatureFlagDraft> PromoteAsync(
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

        var sourceCoordinate = new FlagCoordinate(flagKey, sourceEnvironmentName);
        var targetCoordinate = new FlagCoordinate(flagKey, targetEnvironmentName);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (!_drafts.TryGetValue(sourceCoordinate, out FeatureFlagDraft? sourceDraft))
            {
                throw new InvalidOperationException(
                    $"Flag draft '{flagKey}' does not exist in environment '{sourceEnvironmentName}'.");
            }

            ValidateRbacAllowsProducts(rbacMetadata, sourceDraft.ProductScope);
            long nextRevision = _drafts.TryGetValue(targetCoordinate, out FeatureFlagDraft? targetDraft)
                ? targetDraft.Revision + 1
                : 1;

            FeatureFlagDraft promotedDraft = sourceDraft with
            {
                EnvironmentName = targetEnvironmentName,
                LastReason = reason,
                LastRbacMetadata = rbacMetadata,
                LastModifiedAt = now,
                Revision = nextRevision,
            };

            _drafts[targetCoordinate] = promotedDraft;
            FeatureFlagDraft auditDraft = promotedDraft with
            {
                EnvironmentName = sourceEnvironmentName,
            };
            _ = AppendAuditEntry(AdminAuditAction.Promoted, auditDraft, targetEnvironmentName, now);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                s_draftPromoted(
                    _logger,
                    promotedDraft.FlagKey,
                    sourceEnvironmentName,
                    promotedDraft.EnvironmentName,
                    promotedDraft.Revision,
                    null);
            }

            return ValueTask.FromResult(promotedDraft);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FeatureFlagDraft> GetDrafts()
    {
        lock (_sync)
        {
            return _drafts.Values
                .OrderBy(draft => draft.FlagKey, StringComparer.Ordinal)
                .ThenBy(draft => draft.EnvironmentName, StringComparer.Ordinal)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AdminAuditEntry> GetAuditTrail()
    {
        lock (_sync)
        {
            return _auditEntries.ToArray();
        }
    }

    /// <inheritdoc />
    public AdminRuntimeMetrics GetMetrics()
    {
        lock (_sync)
        {
            int publishCount = 0;
            int promotionCount = 0;
            foreach (AdminAuditEntry entry in _auditEntries)
            {
                if (entry.Action == AdminAuditAction.Published)
                {
                    publishCount++;
                }

                if (entry.Action == AdminAuditAction.Promoted)
                {
                    promotionCount++;
                }
            }

            return new AdminRuntimeMetrics(
                _drafts.Count,
                _auditEntries.Count,
                publishCount,
                promotionCount);
        }
    }

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

    private AdminAuditEntry AppendAuditEntry(
        AdminAuditAction action,
        FeatureFlagDraft draft,
        string? targetEnvironmentName,
        DateTimeOffset now)
    {
        var entry = new AdminAuditEntry(
            ++_nextAuditSequence,
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

        _auditEntries.Add(entry);
        return entry;
    }

    private static NormalizedDraftMutation NormalizeMutation(FeatureFlagDraftMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        string[] productScope = NormalizeProductIds(mutation.ProductScope, nameof(mutation.ProductScope));
        AdminRbacMetadata rbacMetadata = NormalizeRbac(mutation.RbacMetadata);
        ValidateRbacAllowsProducts(rbacMetadata, productScope);

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

        string[] productIds = NormalizeProductIds(rbacMetadata.ProductIds, nameof(rbacMetadata.ProductIds));
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

    private static string[] NormalizeProductIds(IReadOnlyCollection<string> productIds, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(productIds, parameterName);

        List<string> normalized = [];
        foreach (string productId in productIds)
        {
            string candidate = NormalizeRequired(productId, parameterName);
            if (!SharpNinjaProductCatalog.IsV1Product(candidate))
            {
                throw new InvalidOperationException($"Product '{candidate}' is not registered in the v1 product catalog.");
            }

            if (!ContainsOrdinal(normalized, candidate))
            {
                normalized.Add(candidate);
            }
        }

        if (normalized.Count == 0)
        {
            throw new InvalidOperationException("At least one product scope is required.");
        }

        return normalized.ToArray();
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

    private static void ValidateRbacAllowsProducts(
        AdminRbacMetadata rbacMetadata,
        IReadOnlyCollection<string> productScope)
    {
        foreach (string productId in productScope)
        {
            if (!ContainsOrdinal(rbacMetadata.ProductIds, productId))
            {
                throw new UnauthorizedAccessException(
                    $"Principal '{rbacMetadata.PrincipalId}' is not authorized for product '{productId}'.");
            }
        }
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

    private readonly record struct FlagCoordinate(string FlagKey, string EnvironmentName);

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
