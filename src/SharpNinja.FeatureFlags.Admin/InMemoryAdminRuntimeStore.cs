namespace SharpNinja.FeatureFlags.Admin;

internal sealed class InMemoryAdminRuntimeStore : IAdminRuntimeStore
{
    private readonly Dictionary<FlagCoordinate, FeatureFlagDraft> drafts = [];
    private readonly List<AdminAuditEntry> auditEntries = [];
    private readonly object sync = new();
    private long nextAuditSequence;

    public ValueTask<FeatureFlagDraft?> FindDraftAsync(
        string flagKey,
        string environmentName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var coordinate = new FlagCoordinate(flagKey, environmentName);
        lock (sync)
        {
            return ValueTask.FromResult(drafts.TryGetValue(coordinate, out FeatureFlagDraft? draft) ? draft : null);
        }
    }

    public ValueTask AddDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(draft);
        var coordinate = new FlagCoordinate(draft.FlagKey, draft.EnvironmentName);

        lock (sync)
        {
            if (drafts.ContainsKey(coordinate))
            {
                throw new InvalidOperationException(
                    $"Flag draft '{draft.FlagKey}' already exists in environment '{draft.EnvironmentName}'.");
            }

            drafts.Add(coordinate, draft);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateDraftAsync(FeatureFlagDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(draft);
        var coordinate = new FlagCoordinate(draft.FlagKey, draft.EnvironmentName);

        lock (sync)
        {
            if (!drafts.ContainsKey(coordinate))
            {
                throw new InvalidOperationException(
                    $"Flag draft '{draft.FlagKey}' does not exist in environment '{draft.EnvironmentName}'.");
            }

            drafts[coordinate] = draft;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<AdminAuditEntry> AppendAuditEntryAsync(AdminAuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Sequence != 0)
        {
            throw new InvalidOperationException("Audit entries must be appended with sequence zero so the store can assign order.");
        }

        lock (sync)
        {
            AdminAuditEntry persisted = entry with
            {
                Sequence = ++nextAuditSequence,
            };

            auditEntries.Add(persisted);
            return ValueTask.FromResult(persisted);
        }
    }

    public ValueTask<IReadOnlyList<FeatureFlagDraft>> ListDraftsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            IReadOnlyList<FeatureFlagDraft> snapshot = drafts.Values
                .OrderBy(draft => draft.FlagKey, StringComparer.Ordinal)
                .ThenBy(draft => draft.EnvironmentName, StringComparer.Ordinal)
                .ToArray();

            return ValueTask.FromResult(snapshot);
        }
    }

    public ValueTask<IReadOnlyList<AdminAuditEntry>> ListAuditTrailAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            return ValueTask.FromResult((IReadOnlyList<AdminAuditEntry>)auditEntries.ToArray());
        }
    }

    public ValueTask<AdminRuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            int publishCount = 0;
            int promotionCount = 0;
            foreach (AdminAuditEntry entry in auditEntries)
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

            return ValueTask.FromResult(new AdminRuntimeMetrics(
                drafts.Count,
                auditEntries.Count,
                publishCount,
                promotionCount));
        }
    }

    private readonly record struct FlagCoordinate(string FlagKey, string EnvironmentName);
}
