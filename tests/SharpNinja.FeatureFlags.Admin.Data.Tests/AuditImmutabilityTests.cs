using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Data;
using SharpNinja.FeatureFlags.Admin.Data.Entities;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Data.Tests;

/// <summary>FR-9 TR-9: Tests that protect the immutability guarantees of the Admin audit trail.</summary>
public sealed class AuditImmutabilityTests
{
    /// <summary>FR-9 TR-9: AppendAuditEntryAsync rejects null or whitespace Reason values with ArgumentException.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AppendAuditEntryAsyncRejectsWhitespaceReason(string reason)
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();
        var store = new EfCoreAdminRuntimeStore(fixture.Context);
        AdminAuditEntry entry = CreateAuditEntry(reason: reason);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await store.AppendAuditEntryAsync(entry, CancellationToken.None));
    }

    /// <summary>FR-9 TR-9: AppendAuditEntryAsync also rejects a null Reason with ArgumentException.</summary>
    [Fact]
    public async Task AppendAuditEntryAsyncRejectsNullReason()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();
        var store = new EfCoreAdminRuntimeStore(fixture.Context);
        AdminAuditEntry entry = CreateAuditEntry(reason: null!);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await store.AppendAuditEntryAsync(entry, CancellationToken.None));
    }

    /// <summary>FR-9: Appending a second audit row does not mutate the first row's persisted state.</summary>
    [Fact]
    public async Task AppendAuditEntryAsyncDoesNotMutatePreviousRows()
    {
        await using SqliteFixture fixture = await SqliteFixture.CreateAsync();
        var store = new EfCoreAdminRuntimeStore(fixture.Context);

        AdminAuditEntry firstEntry = CreateAuditEntry(
            action: AdminAuditAction.Created,
            reason: "Initial draft",
            revision: 1,
            occurredAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            defaultValue: "false");

        AdminAuditEntry persistedFirst = await store.AppendAuditEntryAsync(firstEntry, CancellationToken.None);

        AdminAuditEntry secondEntry = CreateAuditEntry(
            action: AdminAuditAction.Updated,
            reason: "Enable for cohort A",
            revision: 2,
            occurredAt: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            defaultValue: "true");

        _ = await store.AppendAuditEntryAsync(secondEntry, CancellationToken.None);

        IReadOnlyList<AdminAuditEntry> trail = await store.ListAuditTrailAsync(CancellationToken.None);

        Assert.Equal(2, trail.Count);

        AdminAuditEntry replayedFirst = trail[0];
        Assert.Equal(persistedFirst.Sequence, replayedFirst.Sequence);
        Assert.Equal(AdminAuditAction.Created, replayedFirst.Action);
        Assert.Equal("Initial draft", replayedFirst.Reason);
        Assert.Equal(1, replayedFirst.Revision);
        Assert.Equal("false", replayedFirst.DefaultValue);
        Assert.Equal(firstEntry.OccurredAt, replayedFirst.OccurredAt);
        Assert.Equal(firstEntry.FlagKey, replayedFirst.FlagKey);
        Assert.Equal(firstEntry.EnvironmentName, replayedFirst.EnvironmentName);

        AdminAuditEntry replayedSecond = trail[1];
        Assert.Equal(AdminAuditAction.Updated, replayedSecond.Action);
        Assert.Equal("Enable for cohort A", replayedSecond.Reason);
        Assert.Equal(2, replayedSecond.Revision);
        Assert.Equal("true", replayedSecond.DefaultValue);
    }

    /// <summary>FR-9: Reflection guard ensuring IAdminRuntimeStore exposes no mutation surface for AuditEntryEntity rows.</summary>
    [Fact]
    public void IAdminRuntimeStoreExposesNoAuditEntryMutationMethods()
    {
        string[] forbiddenPrefixes = ["Update", "Modify", "Delete", "Replace"];
        Type storeContract = typeof(IAdminRuntimeStore);
        MethodInfo[] methods = storeContract.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (MethodInfo method in methods)
        {
            bool hasForbiddenPrefix = false;
            foreach (string prefix in forbiddenPrefixes)
            {
                if (method.Name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    hasForbiddenPrefix = true;
                    break;
                }
            }

            if (!hasForbiddenPrefix)
            {
                continue;
            }

            bool takesAuditEntity = false;
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (parameter.ParameterType == typeof(AuditEntryEntity))
                {
                    takesAuditEntity = true;
                    break;
                }
            }

            Assert.False(
                takesAuditEntity,
                $"IAdminRuntimeStore.{method.Name} exposes an AuditEntryEntity mutation surface; audit rows must remain append-only.");
        }
    }

    private static AdminAuditEntry CreateAuditEntry(
        AdminAuditAction action = AdminAuditAction.Created,
        string flagKey = "checkout.enabled",
        string environmentName = "development",
        string? targetEnvironmentName = null,
        string valueType = "boolean",
        string defaultValue = "false",
        string reason = "Initial draft",
        long revision = 1,
        DateTimeOffset? occurredAt = null)
    {
        var rbac = new AdminRbacMetadata(
            TenantId: "tenant-one",
            PrincipalId: "operator-one",
            ProductIds: ["TruckMate"],
            RoleIds: ["Editor"]);

        return new AdminAuditEntry(
            Sequence: 0,
            Action: action,
            FlagKey: flagKey,
            EnvironmentName: environmentName,
            TargetEnvironmentName: targetEnvironmentName,
            ProductScope: ["TruckMate"],
            ValueType: valueType,
            DefaultValue: defaultValue,
            RuleDescriptions: ["Default rule set"],
            Reason: reason,
            RbacMetadata: rbac,
            Revision: revision,
            OccurredAt: occurredAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private SqliteFixture(SqliteConnection connection, SqliteAdminDbContext context)
        {
            this.connection = connection;
            this.Context = context;
        }

        public SqliteAdminDbContext Context { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            DbContextOptions<SqliteAdminDbContext> options = new DbContextOptionsBuilder<SqliteAdminDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new SqliteAdminDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new SqliteFixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class SqliteAdminDbContext : AdminDbContext
    {
        public SqliteAdminDbContext(DbContextOptions<SqliteAdminDbContext> options)
            : base(options)
        {
        }
    }
}
