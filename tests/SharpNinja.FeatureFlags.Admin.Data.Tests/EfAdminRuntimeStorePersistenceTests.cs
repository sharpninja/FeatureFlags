using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Data;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Data.Tests;

/// <summary>Production Admin draft and audit state must outlive a fresh service provider.</summary>
public sealed class EfAdminRuntimeStorePersistenceTests
{
    /// <summary>A draft and its assigned audit sequence survive disposal and recreation.</summary>
    [Fact]
    public async Task DraftAndAuditSurviveFreshProvider()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"featureflags-admin-{Guid.NewGuid():N}.db");
        try
        {
            await using (ServiceProvider first = BuildProvider(databasePath))
            {
                await using (var context = await first.GetRequiredService<IDbContextFactory<TestAdminDbContext>>()
                    .CreateDbContextAsync())
                {
                    await context.Database.EnsureCreatedAsync();
                }

                await using TestAdminDbContext storeContext = await first.GetRequiredService<IDbContextFactory<TestAdminDbContext>>().CreateDbContextAsync();
                EfCoreAdminRuntimeStore store = new(storeContext);
                var actor = new AdminRbacMetadata(
                    "tenant-a", "operator-a", ["gigdriving"], ["Publisher"]);
                var draft = new FeatureFlagDraft(
                    "navigation.mapfirst", "Production", ["gigdriving"],
                    "boolean", "false", ["Release gate"], "Initial release",
                    actor, 1, DateTimeOffset.UtcNow);

                await store.AddDraftAsync(draft, CancellationToken.None);
                AdminAuditEntry saved = await store.AppendAuditEntryAsync(
                    new AdminAuditEntry(
                        0, AdminAuditAction.Created, draft.FlagKey, draft.EnvironmentName,
                        null, draft.ProductScope, draft.ValueType, draft.DefaultValue,
                        draft.RuleDescriptions, draft.LastReason, actor, draft.Revision,
                        draft.LastModifiedAt),
                    CancellationToken.None);
                Assert.Equal(1, saved.Sequence);
            }

            await using (ServiceProvider second = BuildProvider(databasePath))
            {
                await using TestAdminDbContext storeContext = await second.GetRequiredService<IDbContextFactory<TestAdminDbContext>>().CreateDbContextAsync();
                EfCoreAdminRuntimeStore store = new(storeContext);
                FeatureFlagDraft? draft = await store.FindDraftAsync(
                    "navigation.mapfirst", "Production", CancellationToken.None);
                Assert.NotNull(draft);
                Assert.Equal("operator-a", draft.LastRbacMetadata.PrincipalId);
                Assert.Equal("false", draft.DefaultValue);
                Assert.Equal(["gigdriving"], draft.ProductScope);

                IReadOnlyList<AdminAuditEntry> audit = await store.ListAuditTrailAsync(CancellationToken.None);
                Assert.Single(audit);
                Assert.Equal(1, audit[0].Sequence);
                Assert.Equal(AdminAuditAction.Created, audit[0].Action);
                Assert.Equal("operator-a", audit[0].RbacMetadata.PrincipalId);
                Assert.Equal(1, (await store.GetMetricsAsync(CancellationToken.None)).AuditEntryCount);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static ServiceProvider BuildProvider(string databasePath) =>
        new ServiceCollection()
            .AddDbContextFactory<TestAdminDbContext>(options => options.UseSqlite($"Data Source={databasePath}"))
            .BuildServiceProvider();

    private sealed class TestAdminDbContext(DbContextOptions<TestAdminDbContext> options)
        : AdminDbContext(options);
}
