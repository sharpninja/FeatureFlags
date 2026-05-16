using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharpNinja.FeatureFlags.Admin.Data.Entities;

namespace SharpNinja.FeatureFlags.Admin.Data;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10: EF Core implementation of <see cref="IAdminRuntimeStore"/> backed by any relational provider.</summary>
public sealed class EfCoreAdminRuntimeStore : IAdminRuntimeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminDbContext context;

    /// <summary>Initializes a new instance of <see cref="EfCoreAdminRuntimeStore"/>.</summary>
    /// <param name="context">The admin EF Core DbContext scoped to the current request.</param>
    public EfCoreAdminRuntimeStore(AdminDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
    }

    /// <inheritdoc />
    public async ValueTask<FeatureFlagDraft?> FindDraftAsync(
        string flagKey,
        string environmentName,
        CancellationToken cancellationToken)
    {
        FlagDraftEntity? entity = await context.FlagDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.FlagKey == flagKey && d.EnvironmentName == environmentName, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async ValueTask AddDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        FlagDraftEntity entity = ToEntity(draft);
        context.FlagDrafts.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UpdateDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        FlagDraftEntity? existing = await context.FlagDrafts
            .FirstOrDefaultAsync(d => d.FlagKey == draft.FlagKey && d.EnvironmentName == draft.EnvironmentName, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            throw new InvalidOperationException(
                $"Flag draft '{draft.FlagKey}' does not exist in environment '{draft.EnvironmentName}'.");
        }

        existing.ProductScopeJson = SerializeList(draft.ProductScope);
        existing.ValueType = draft.ValueType;
        existing.DefaultValue = draft.DefaultValue;
        existing.RuleDescriptionsJson = SerializeList(draft.RuleDescriptions);
        existing.LastReason = draft.LastReason;
        existing.LastRbacMetadata = ToRbacOwned(draft.LastRbacMetadata);
        existing.Revision = draft.Revision;
        existing.LastModifiedAt = draft.LastModifiedAt;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<AdminAuditEntry> AppendAuditEntryAsync(AdminAuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        AuditEntryEntity entity = ToAuditEntity(entry);
        context.AuditEntries.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entry with { Sequence = entity.Sequence };
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<FeatureFlagDraft>> ListDraftsAsync(CancellationToken cancellationToken)
    {
        List<FlagDraftEntity> entities = await context.FlagDrafts
            .AsNoTracking()
            .OrderBy(d => d.FlagKey)
            .ThenBy(d => d.EnvironmentName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AdminAuditEntry>> ListAuditTrailAsync(CancellationToken cancellationToken)
    {
        List<AuditEntryEntity> entities = await context.AuditEntries
            .AsNoTracking()
            .OrderBy(a => a.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToAuditDomain).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<AdminRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken)
    {
        int draftCount = await context.FlagDrafts.CountAsync(cancellationToken).ConfigureAwait(false);
        int auditCount = await context.AuditEntries.CountAsync(cancellationToken).ConfigureAwait(false);
        int publishCount = await context.AuditEntries
            .CountAsync(a => a.Action == AdminAuditAction.Published, cancellationToken)
            .ConfigureAwait(false);
        int promotionCount = await context.AuditEntries
            .CountAsync(a => a.Action == AdminAuditAction.Promoted, cancellationToken)
            .ConfigureAwait(false);

        return new AdminRuntimeMetrics(draftCount, auditCount, publishCount, promotionCount);
    }

    // --- mapping helpers ---

    private static FeatureFlagDraft ToDomain(FlagDraftEntity e) =>
        new(
            FlagKey: e.FlagKey,
            EnvironmentName: e.EnvironmentName,
            ProductScope: DeserializeList(e.ProductScopeJson),
            ValueType: e.ValueType,
            DefaultValue: e.DefaultValue,
            RuleDescriptions: DeserializeList(e.RuleDescriptionsJson),
            LastReason: e.LastReason,
            LastRbacMetadata: ToRbacDomain(e.LastRbacMetadata),
            Revision: e.Revision,
            LastModifiedAt: e.LastModifiedAt);

    private static FlagDraftEntity ToEntity(FeatureFlagDraft d) =>
        new()
        {
            FlagKey = d.FlagKey,
            EnvironmentName = d.EnvironmentName,
            ProductScopeJson = SerializeList(d.ProductScope),
            ValueType = d.ValueType,
            DefaultValue = d.DefaultValue,
            RuleDescriptionsJson = SerializeList(d.RuleDescriptions),
            LastReason = d.LastReason,
            LastRbacMetadata = ToRbacOwned(d.LastRbacMetadata),
            Revision = d.Revision,
            LastModifiedAt = d.LastModifiedAt,
        };

    private static AdminAuditEntry ToAuditDomain(AuditEntryEntity e) =>
        new(
            Sequence: e.Sequence,
            Action: e.Action,
            FlagKey: e.FlagKey,
            EnvironmentName: e.EnvironmentName,
            TargetEnvironmentName: e.TargetEnvironmentName,
            ProductScope: DeserializeList(e.ProductScopeJson),
            ValueType: e.ValueType,
            DefaultValue: e.DefaultValue,
            RuleDescriptions: DeserializeList(e.RuleDescriptionsJson),
            Reason: e.Reason,
            RbacMetadata: ToRbacDomain(e.RbacMetadata),
            Revision: e.Revision,
            OccurredAt: e.OccurredAt);

    private static AuditEntryEntity ToAuditEntity(AdminAuditEntry a) =>
        new()
        {
            Action = a.Action,
            FlagKey = a.FlagKey,
            EnvironmentName = a.EnvironmentName,
            TargetEnvironmentName = a.TargetEnvironmentName,
            ProductScopeJson = SerializeList(a.ProductScope),
            ValueType = a.ValueType,
            DefaultValue = a.DefaultValue,
            RuleDescriptionsJson = SerializeList(a.RuleDescriptions),
            Reason = a.Reason,
            RbacMetadata = ToRbacOwned(a.RbacMetadata),
            Revision = a.Revision,
            OccurredAt = a.OccurredAt,
        };

    private static AdminRbacMetadata ToRbacDomain(RbacMetadataOwned o) =>
        new(
            TenantId: o.TenantId,
            PrincipalId: o.PrincipalId,
            ProductIds: DeserializeList(o.ProductIdsJson),
            RoleIds: DeserializeList(o.RoleIdsJson));

    private static RbacMetadataOwned ToRbacOwned(AdminRbacMetadata r) =>
        new()
        {
            TenantId = r.TenantId,
            PrincipalId = r.PrincipalId,
            ProductIdsJson = SerializeList(r.ProductIds),
            RoleIdsJson = SerializeList(r.RoleIds),
        };

    private static string SerializeList(IReadOnlyCollection<string> list) =>
        JsonSerializer.Serialize(list, JsonOptions);

    private static List<string> DeserializeList(string json) =>
        JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}
