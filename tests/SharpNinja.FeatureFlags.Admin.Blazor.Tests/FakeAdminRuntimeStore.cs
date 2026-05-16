using System.Collections.Concurrent;
using SharpNinja.FeatureFlags.Admin;

namespace SharpNinja.FeatureFlags.Admin.Blazor.Tests;

/// <summary>Test double for <see cref="IAdminRuntimeStore"/> used by bUnit component tests.</summary>
internal sealed class FakeAdminRuntimeStore : IAdminRuntimeStore
{
    private readonly List<FeatureFlagDraft> drafts = new();
    private readonly List<AdminAuditEntry> auditEntries = new();
    private long sequence;

    public ValueTask<FeatureFlagDraft?> FindDraftAsync(string flagKey, string environmentName, CancellationToken cancellationToken)
    {
        FeatureFlagDraft? match = drafts.FirstOrDefault(d =>
            string.Equals(d.FlagKey, flagKey, StringComparison.Ordinal)
            && string.Equals(d.EnvironmentName, environmentName, StringComparison.Ordinal));
        return ValueTask.FromResult(match);
    }

    public ValueTask AddDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken)
    {
        drafts.Add(draft);
        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken)
    {
        int index = drafts.FindIndex(d =>
            string.Equals(d.FlagKey, draft.FlagKey, StringComparison.Ordinal)
            && string.Equals(d.EnvironmentName, draft.EnvironmentName, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException("draft not found");
        }

        drafts[index] = draft;
        return ValueTask.CompletedTask;
    }

    public ValueTask<AdminAuditEntry> AppendAuditEntryAsync(AdminAuditEntry entry, CancellationToken cancellationToken)
    {
        AdminAuditEntry persisted = entry with { Sequence = ++sequence };
        auditEntries.Add(persisted);
        return ValueTask.FromResult(persisted);
    }

    public ValueTask<IReadOnlyList<FeatureFlagDraft>> ListDraftsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<FeatureFlagDraft>>(drafts.ToArray());

    public ValueTask<IReadOnlyList<AdminAuditEntry>> ListAuditTrailAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<AdminAuditEntry>>(auditEntries.ToArray());

    public ValueTask<AdminRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken)
    {
        int publishes = auditEntries.Count(e => e.Action == AdminAuditAction.Published);
        int promotions = auditEntries.Count(e => e.Action == AdminAuditAction.Promoted);
        return ValueTask.FromResult(new AdminRuntimeMetrics(drafts.Count, auditEntries.Count, publishes, promotions));
    }

    /// <summary>Seeds the fake store with synchronous data for tests.</summary>
    /// <param name="draft">Draft to insert.</param>
    public void SeedDraft(FeatureFlagDraft draft) => drafts.Add(draft);

    /// <summary>Seeds an audit entry, assigning a sequence number.</summary>
    /// <param name="entry">Entry to seed.</param>
    public void SeedAudit(AdminAuditEntry entry) => auditEntries.Add(entry with { Sequence = ++sequence });
}
