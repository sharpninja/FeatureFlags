using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Abstractions.Options;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Tests;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: Admin runtime authoring and immutable audit tests.</summary>
public sealed class AdminRuntimeServiceTests
{
    /// <summary>Create and update actions append audit entries without mutating prior history.</summary>
    [Fact]
    public async Task CreateAndUpdateDraftAppendAuditWithoutMutatingHistory()
    {
        using ServiceProvider provider = CreateProvider();
        IAdminRuntimeService runtime = provider.GetRequiredService<IAdminRuntimeService>();

        await runtime.CreateDraftAsync(CreateMutation(defaultValue: "false", reason: "Initial launch gate"));
        await runtime.UpdateDraftAsync(CreateMutation(defaultValue: "true", reason: "Enable for first cohort"));

        IReadOnlyList<AdminAuditEntry> auditEntries = runtime.GetAuditTrail();

        Assert.Collection(
            auditEntries,
            first =>
            {
                Assert.Equal(1, first.Sequence);
                Assert.Equal(AdminAuditAction.Created, first.Action);
                Assert.Equal("false", first.DefaultValue);
                Assert.Equal("Initial launch gate", first.Reason);
            },
            second =>
            {
                Assert.Equal(2, second.Sequence);
                Assert.Equal(AdminAuditAction.Updated, second.Action);
                Assert.Equal("true", second.DefaultValue);
                Assert.Equal("Enable for first cohort", second.Reason);
            });
    }

    /// <summary>Product scope validation rejects flags outside the registered product catalog.</summary>
    [Fact]
    public async Task CreateDraftRejectsUnknownProductScope()
    {
        using ServiceProvider provider = CreateProvider();
        IAdminRuntimeService runtime = provider.GetRequiredService<IAdminRuntimeService>();
        FeatureFlagDraftMutation mutation = CreateMutation(
            productScope: ["unknown-product"],
            rbacMetadata: CreateRbac(productIds: [SharpNinjaProductCatalog.TruckMate]));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await runtime.CreateDraftAsync(mutation));

        Assert.Contains("v1 product catalog", exception.Message, StringComparison.Ordinal);
        Assert.Empty(runtime.GetAuditTrail());
    }

    /// <summary>Promotion copies source draft state to the target environment and records an explicit audit entry.</summary>
    [Fact]
    public async Task PromoteDraftRecordsSourceAndTargetEnvironmentAudit()
    {
        using ServiceProvider provider = CreateProvider();
        IAdminRuntimeService runtime = provider.GetRequiredService<IAdminRuntimeService>();

        await runtime.CreateDraftAsync(CreateMutation(environmentName: "dev"));
        FeatureFlagDraft promoted = await runtime.PromoteAsync(new FeatureFlagPromotionAction(
            "checkout.enabled",
            "development",
            "staging",
            "Promote validated draft to staging",
            CreateRbac()));

        AdminAuditEntry promotionAudit = Assert.Single(
            runtime.GetAuditTrail(),
            entry => entry.Action == AdminAuditAction.Promoted);

        Assert.Equal("staging", promoted.EnvironmentName);
        Assert.Equal("development", promotionAudit.EnvironmentName);
        Assert.Equal("staging", promotionAudit.TargetEnvironmentName);
        Assert.Equal("Promote validated draft to staging", promotionAudit.Reason);
    }

    /// <summary>FR-9 TR-9: PublishAsync records a Published audit entry for an existing draft.</summary>
    /// <remarks>FR-9 requires that every publish action is immutably recorded in the audit trail.</remarks>
    [Fact]
    public async Task PublishAsyncAppendsPublishedAuditEntryForExistingDraft()
    {
        using ServiceProvider provider = CreateProvider();
        IAdminRuntimeService runtime = provider.GetRequiredService<IAdminRuntimeService>();

        await runtime.CreateDraftAsync(CreateMutation(defaultValue: "false", reason: "Initial draft"));

        AdminAuditEntry publishEntry = await runtime.PublishAsync(new FeatureFlagPublishAction(
            "checkout.enabled",
            "development",
            "Approved for production rollout",
            CreateRbac()));

        IReadOnlyList<AdminAuditEntry> auditTrail = runtime.GetAuditTrail();

        Assert.Equal(AdminAuditAction.Published, publishEntry.Action);
        Assert.Equal("checkout.enabled", publishEntry.FlagKey);
        Assert.Equal("development", publishEntry.EnvironmentName);
        Assert.Equal("Approved for production rollout", publishEntry.Reason);
        Assert.Equal(2, auditTrail.Count);
        Assert.Equal(AdminAuditAction.Created, auditTrail[0].Action);
        Assert.Equal(AdminAuditAction.Published, auditTrail[1].Action);
    }

    /// <summary>Per-Product RBAC metadata is enforced and preserved on successful audit entries.</summary>
    [Fact]
    public async Task RbacProductScopeIsEnforcedAndTenantMetadataIsAudited()
    {
        using ServiceProvider provider = CreateProvider();
        IAdminRuntimeService runtime = provider.GetRequiredService<IAdminRuntimeService>();
        FeatureFlagDraftMutation deniedMutation = CreateMutation(
            productScope: [SharpNinjaProductCatalog.TruckMate, SharpNinjaProductCatalog.DriverMate],
            rbacMetadata: CreateRbac(productIds: [SharpNinjaProductCatalog.TruckMate]));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await runtime.CreateDraftAsync(deniedMutation));

        await runtime.CreateDraftAsync(CreateMutation(
            productScope: [SharpNinjaProductCatalog.TruckMate],
            rbacMetadata: CreateRbac(tenantId: "tenant-alpha", principalId: "payton")));

        AdminAuditEntry auditEntry = Assert.Single(runtime.GetAuditTrail());

        Assert.Equal("tenant-alpha", auditEntry.RbacMetadata.TenantId);
        Assert.Equal("payton", auditEntry.RbacMetadata.PrincipalId);
        Assert.Equal([SharpNinjaProductCatalog.TruckMate], auditEntry.RbacMetadata.ProductIds);
        Assert.Contains("Editor", auditEntry.RbacMetadata.RoleIds, StringComparer.Ordinal);
    }

    private static ServiceProvider CreateProvider() =>
        new ServiceCollection()
            .AddSharpNinjaFeatureFlagsAdminRuntime()
            .BuildServiceProvider();

    private static FeatureFlagDraftMutation CreateMutation(
        string flagKey = "checkout.enabled",
        string environmentName = "development",
        IReadOnlyCollection<string>? productScope = null,
        string valueType = "boolean",
        string defaultValue = "false",
        IReadOnlyCollection<string>? ruleDescriptions = null,
        string reason = "Authoring test",
        AdminRbacMetadata? rbacMetadata = null) =>
        new(
            flagKey,
            environmentName,
            productScope ?? [SharpNinjaProductCatalog.TruckMate],
            valueType,
            defaultValue,
            ruleDescriptions ?? ["Default rule set"],
            reason,
            rbacMetadata ?? CreateRbac());

    private static AdminRbacMetadata CreateRbac(
        string tenantId = "tenant-one",
        string principalId = "operator-one",
        IReadOnlyCollection<string>? productIds = null,
        IReadOnlyCollection<string>? roleIds = null) =>
        new(
            tenantId,
            principalId,
            productIds ?? [SharpNinjaProductCatalog.TruckMate],
            roleIds ?? ["Editor", "Publisher"]);
}
