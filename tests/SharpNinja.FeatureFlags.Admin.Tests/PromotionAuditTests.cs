using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Abstractions.Options;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Tests;

/// <summary>FR-9 FR-11 TR-9: Promotion-path audit-entry tests for <see cref="IAdminRuntimeService"/>.</summary>
public sealed class PromotionAuditTests
{
    /// <summary>FR-9 FR-11: PromoteAsync writes an immutable AuditEntry whose Action is Promoted with source and target environments, reason, and a non-default OccurredAt.</summary>
    [Fact]
    public async Task PromoteAsyncWritesPromotedAuditEntryWithEnvironmentsAndReason()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSharpNinjaFeatureFlagsAdminRuntime()
            .BuildServiceProvider();

        IAdminRuntimeService runtime = provider.GetRequiredService<IAdminRuntimeService>();

        var rbac = new AdminRbacMetadata(
            TenantId: "tenant-one",
            PrincipalId: "operator-one",
            ProductIds: [SharpNinjaProductCatalog.TruckMate],
            RoleIds: ["Editor", "Promoter"]);

        await runtime.CreateDraftAsync(new FeatureFlagDraftMutation(
            FlagKey: "checkout.enabled",
            EnvironmentName: "development",
            ProductScope: [SharpNinjaProductCatalog.TruckMate],
            ValueType: "boolean",
            DefaultValue: "false",
            RuleDescriptions: ["Default rule set"],
            Reason: "Initial draft",
            RbacMetadata: rbac));

        DateTimeOffset before = DateTimeOffset.UtcNow;

        _ = await runtime.PromoteAsync(new FeatureFlagPromotionAction(
            FlagKey: "checkout.enabled",
            SourceEnvironmentName: "development",
            TargetEnvironmentName: "staging",
            Reason: "audit-test",
            RbacMetadata: rbac));

        AdminAuditEntry promotionAudit = Assert.Single(
            runtime.GetAuditTrail(),
            entry => entry.Action == AdminAuditAction.Promoted);

        Assert.Equal(AdminAuditAction.Promoted, promotionAudit.Action);
        Assert.Equal("checkout.enabled", promotionAudit.FlagKey);
        Assert.Equal("development", promotionAudit.EnvironmentName);
        Assert.Equal("staging", promotionAudit.TargetEnvironmentName);
        Assert.Equal("audit-test", promotionAudit.Reason);
        Assert.NotEqual(default, promotionAudit.OccurredAt);
        Assert.True(promotionAudit.OccurredAt >= before);
    }
}
