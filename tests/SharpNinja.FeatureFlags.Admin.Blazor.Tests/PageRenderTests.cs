using System.Globalization;
using Bunit;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Blazor.Components.Pages;
using SharpNinja.FeatureFlags.Experimentation;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Blazor.Tests;

/// <summary>bUnit component tests verifying the Admin Blazor pages render against a fake store.</summary>
public sealed class PageRenderTests : IDisposable
{
    private readonly TestContext ctx = new();
    private readonly FakeAdminRuntimeStore store;

    /// <summary>Initializes the bUnit test context with stub services.</summary>
    public PageRenderTests()
    {
        store = ctx.RegisterAdminRuntime();
    }

    /// <inheritdoc />
    public void Dispose() => ctx.Dispose();

    /// <summary>Dashboard renders empty metrics when no drafts exist.</summary>
    [Fact]
    public void DashboardRendersZeroCountsWhenEmpty()
    {
        IRenderedComponent<Dashboard> cut = ctx.RenderComponent<Dashboard>();

        Assert.Contains("Drafts: 0", cut.Find("[data-testid=draft-count]").TextContent, StringComparison.Ordinal);
        Assert.Contains("Audit Entries: 0", cut.Find("[data-testid=audit-count]").TextContent, StringComparison.Ordinal);
    }

    /// <summary>Dashboard groups drafts per environment.</summary>
    [Fact]
    public void DashboardCountsDraftsByEnvironment()
    {
        store.SeedDraft(CreateDraft("flag-a", "dev"));
        store.SeedDraft(CreateDraft("flag-b", "dev"));
        store.SeedDraft(CreateDraft("flag-c", "prod"));

        IRenderedComponent<Dashboard> cut = ctx.RenderComponent<Dashboard>();

        string devRow = NormalizeWhitespace(cut.Find("[data-testid='env-row-dev']").TextContent);
        string prodRow = NormalizeWhitespace(cut.Find("[data-testid='env-row-prod']").TextContent);
        Assert.Equal("dev2", devRow);
        Assert.Equal("prod1", prodRow);
    }

    /// <summary>FlagList page renders one row per seeded draft.</summary>
    [Fact]
    public void FlagListRendersRowsForEachDraft()
    {
        store.SeedDraft(CreateDraft("alpha", "dev"));
        store.SeedDraft(CreateDraft("beta", "prod"));

        IRenderedComponent<FlagList> cut = ctx.RenderComponent<FlagList>();

        Assert.Equal(2, cut.FindAll("[data-testid='draft-row']").Count);
    }

    /// <summary>FlagList environment filter narrows the rows.</summary>
    [Fact]
    public void FlagListFilterByEnvironment()
    {
        store.SeedDraft(CreateDraft("alpha", "dev"));
        store.SeedDraft(CreateDraft("beta", "prod"));
        store.SeedDraft(CreateDraft("gamma", "prod"));

        IRenderedComponent<FlagList> cut = ctx.RenderComponent<FlagList>();
        cut.Find("[data-testid='env-filter']").Change("prod");

        Assert.Equal(2, cut.FindAll("[data-testid='draft-row']").Count);
    }

    /// <summary>FlagCreate page renders the form inputs.</summary>
    [Fact]
    public void FlagCreateRendersForm()
    {
        IRenderedComponent<FlagCreate> cut = ctx.RenderComponent<FlagCreate>();

        Assert.NotNull(cut.Find("[data-testid='flag-key']"));
        Assert.NotNull(cut.Find("[data-testid='env-name']"));
        Assert.NotNull(cut.Find("[data-testid='submit']"));
    }

    /// <summary>FlagCreate form rejects submission when required fields are missing.</summary>
    [Fact]
    public async Task FlagCreateValidationRejectsEmptyKey()
    {
        IRenderedComponent<FlagCreate> cut = ctx.RenderComponent<FlagCreate>();
        cut.Find("[data-testid='flag-key']").Change(string.Empty);
        cut.Find("[data-testid='env-name']").Change(string.Empty);
        cut.Find("[data-testid='reason']").Change(string.Empty);
        cut.Find("form").Submit();

        // No draft should have been created because validation messages display and the runtime is not called.
        IReadOnlyList<FeatureFlagDraft> persisted = await store.ListDraftsAsync(CancellationToken.None).ConfigureAwait(true);
        Assert.Empty(persisted);
    }

    /// <summary>FlagEdit page renders form inputs.</summary>
    [Fact]
    public void FlagEditRendersForm()
    {
        IRenderedComponent<FlagEdit> cut = ctx.RenderComponent<FlagEdit>();
        Assert.NotNull(cut.Find("[data-testid='flag-key']"));
        Assert.NotNull(cut.Find("[data-testid='submit']"));
    }

    /// <summary>FlagPublish page renders form inputs.</summary>
    [Fact]
    public void FlagPublishRendersForm()
    {
        IRenderedComponent<FlagPublish> cut = ctx.RenderComponent<FlagPublish>();
        Assert.NotNull(cut.Find("[data-testid='flag-key']"));
        Assert.NotNull(cut.Find("[data-testid='env-name']"));
    }

    /// <summary>FlagPromote page renders source and target environment inputs.</summary>
    [Fact]
    public void FlagPromoteRendersForm()
    {
        IRenderedComponent<FlagPromote> cut = ctx.RenderComponent<FlagPromote>();
        Assert.NotNull(cut.Find("[data-testid='source-env']"));
        Assert.NotNull(cut.Find("[data-testid='target-env']"));
    }

    /// <summary>AuditTrail page renders an empty marker when there are no entries.</summary>
    [Fact]
    public void AuditTrailRendersEmptyState()
    {
        IRenderedComponent<AuditTrail> cut = ctx.RenderComponent<AuditTrail>();
        Assert.NotNull(cut.Find("[data-testid='empty']"));
    }

    /// <summary>AuditTrail page renders one row per seeded entry.</summary>
    [Fact]
    public void AuditTrailRendersRowsForSeededEntries()
    {
        store.SeedAudit(CreateAuditEntry("flag-a", "dev", AdminAuditAction.Created));
        store.SeedAudit(CreateAuditEntry("flag-a", "dev", AdminAuditAction.Published));

        IRenderedComponent<AuditTrail> cut = ctx.RenderComponent<AuditTrail>();
        Assert.Equal(2, cut.FindAll("[data-testid='audit-row']").Count);
    }

    /// <summary>Metrics page renders the runtime metric snapshot.</summary>
    [Fact]
    public void MetricsRendersAdminMetrics()
    {
        store.SeedDraft(CreateDraft("flag-a", "dev"));
        store.SeedAudit(CreateAuditEntry("flag-a", "dev", AdminAuditAction.Published));

        IRenderedComponent<Metrics> cut = ctx.RenderComponent<Metrics>();

        Assert.Contains("Drafts: 1", cut.Find("[data-testid='m-drafts']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Publishes: 1", cut.Find("[data-testid='m-publish']").TextContent, StringComparison.Ordinal);
    }

    /// <summary>Metrics page renders an experiment table when results are provided.</summary>
    [Fact]
    public void MetricsRendersExperimentResults()
    {
        ExperimentAnalysisResult result = new(
            "exp-1",
            "control",
            new[]
            {
                new VariantAnalysisResult("control", 0.10, 0.0, 1.0, 0.0, 0.0, false),
                new VariantAnalysisResult("treatment", 0.15, 0.5, 0.01, 0.1, 0.9, true),
            });

        IRenderedComponent<Metrics> cut = ctx.RenderComponent<Metrics>(parameters => parameters
            .Add(p => p.ExperimentResult, result));

        Assert.Equal(2, cut.FindAll("[data-testid='experiment-row']").Count);
    }

    /// <summary>Rbac page lists the canonical role grants.</summary>
    [Fact]
    public void RbacPageRendersRoles()
    {
        IRenderedComponent<Rbac> cut = ctx.RenderComponent<Rbac>();

        Assert.NotNull(cut.Find("[data-testid='role-viewer']"));
        Assert.NotNull(cut.Find("[data-testid='role-keyadmin']"));
        Assert.NotNull(cut.Find("[data-testid='policy-read']"));
    }

    private static string NormalizeWhitespace(string value)
    {
        char[] chars = value.Where(c => !char.IsWhiteSpace(c)).ToArray();
        return new string(chars);
    }

    private static readonly string[] TruckMateScope = ["truckmate"];
    private static readonly string[] EditorRoles = [AdminRoleNames.Editor];

    private static FeatureFlagDraft CreateDraft(string key, string env)
    {
        AdminRbacMetadata rbac = new("tenant-1", "user-1", TruckMateScope, EditorRoles);
        return new FeatureFlagDraft(
            key,
            env,
            TruckMateScope,
            "boolean",
            "false",
            Array.Empty<string>(),
            "seed",
            rbac,
            Revision: 1,
            DateTimeOffset.UtcNow);
    }

    private static AdminAuditEntry CreateAuditEntry(string key, string env, AdminAuditAction action)
    {
        AdminRbacMetadata rbac = new("tenant-1", "user-1", TruckMateScope, EditorRoles);
        return new AdminAuditEntry(
            Sequence: 0,
            action,
            key,
            env,
            TargetEnvironmentName: null,
            TruckMateScope,
            "boolean",
            "false",
            Array.Empty<string>(),
            $"reason-{action.ToString().ToLower(CultureInfo.InvariantCulture)}",
            rbac,
            Revision: 1,
            DateTimeOffset.UtcNow);
    }
}
