using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Blazor.Components.Pages;
using SharpNinja.FeatureFlags.Admin.Blazor.Services;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Blazor.Tests;

/// <summary>FR-9 TR-9 TEST-GATEWAY-006: Admin pages and mutations honor the authenticated actor.</summary>
public sealed class AdminAuthorizationBoundaryTests
{
    private static readonly AdminRbacMetadata ForgedPrivilege = new(
        "tenant-1", "forged-editor", ["truckmate"],
        [AdminRoleNames.Editor, AdminRoleNames.Publisher, AdminRoleNames.Promoter]);

    /// <summary>An anonymous caller cannot use forged mutation metadata for any write.</summary>
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("publish")]
    [InlineData("promote")]
    public async Task AnonymousMutationRejectsForgedPrivileges(string operation)
    {
        using Harness harness = new(new ClaimsPrincipal(new ClaimsIdentity()));
        harness.SeedDraft();
        int draftCount = (await harness.Store.ListDraftsAsync(CancellationToken.None)).Count;
        int auditCount = (await harness.Store.ListAuditTrailAsync(CancellationToken.None)).Count;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.InvokeAsync(operation));

        Assert.Equal(draftCount, (await harness.Store.ListDraftsAsync(CancellationToken.None)).Count);
        Assert.Equal(auditCount, (await harness.Store.ListAuditTrailAsync(CancellationToken.None)).Count);
    }

    /// <summary>A Viewer cannot elevate through the metadata supplied by a page.</summary>
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("publish")]
    [InlineData("promote")]
    public async Task ViewerMutationRejectsForgedPrivileges(string operation)
    {
        using Harness harness = new(Principal("real-viewer", AdminRoleNames.Viewer));
        harness.SeedDraft();
        int draftCount = (await harness.Store.ListDraftsAsync(CancellationToken.None)).Count;
        int auditCount = (await harness.Store.ListAuditTrailAsync(CancellationToken.None)).Count;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => harness.InvokeAsync(operation));

        Assert.Equal(draftCount, (await harness.Store.ListDraftsAsync(CancellationToken.None)).Count);
        Assert.Equal(auditCount, (await harness.Store.ListAuditTrailAsync(CancellationToken.None)).Count);
    }

    /// <summary>Authorized writes persist the actual principal and grants, not caller-supplied metadata.</summary>
    [Fact]
    public async Task EditorCreateAuditsAuthenticatedActor()
    {
        using Harness harness = new(Principal("real-editor", AdminRoleNames.Editor));

        FeatureFlagDraft draft = await harness.Accessor.CreateDraftAsync(Harness.Mutation("new-flag"));
        AdminAuditEntry audit = Assert.Single(await harness.Store.ListAuditTrailAsync(CancellationToken.None));

        Assert.Equal("real-editor", draft.LastRbacMetadata.PrincipalId);
        Assert.Equal("tenant-1", draft.LastRbacMetadata.TenantId);
        Assert.Equal(["truckmate"], draft.LastRbacMetadata.ProductIds);
        Assert.Equal([AdminRoleNames.Editor], draft.LastRbacMetadata.RoleIds);
        Assert.Equal("real-editor", audit.RbacMetadata.PrincipalId);
    }

    /// <summary>A real Editor without the requested product grant cannot borrow a forged grant.</summary>
    [Fact]
    public async Task EditorWithoutProductGrantCannotUseForgedProductMetadata()
    {
        using Harness harness = new(Principal("other-product-editor", AdminRoleNames.Editor, "drivermate"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await harness.Accessor.CreateDraftAsync(Harness.Mutation("new-flag")));

        Assert.Empty(await harness.Store.ListDraftsAsync(CancellationToken.None));
        Assert.Empty(await harness.Store.ListAuditTrailAsync(CancellationToken.None));
    }

    /// <summary>Every routable Admin component requires authenticated navigation.</summary>
    [Fact]
    public void EveryAdminRouteRequiresAuthorization()
    {
        Type[] routes = typeof(FlagCreate).Assembly.GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: true).Length > 0)
            .ToArray();

        Assert.NotEmpty(routes);
        Assert.All(routes, type =>
            Assert.NotEmpty(type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)));
    }

    private static ClaimsPrincipal Principal(string principalId, string role, string product = "truckmate") => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, principalId),
            new Claim(SharpNinjaAdminDefaults.TenantClaimType, "tenant-1"),
            new Claim(SharpNinjaAdminDefaults.ProductsClaimType, product),
            new Claim(SharpNinjaAdminDefaults.RolesClaimType, role),
        ],
        authenticationType: "test"));

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider provider;
        private readonly IServiceScope scope;

        public Harness(ClaimsPrincipal principal)
        {
            Store = new FakeAdminRuntimeStore();
            ServiceCollection services = new();
            services.AddSharpNinjaFeatureFlagsAdminRuntime();
            services.AddSingleton<IAdminRuntimeStore>(Store);
            services.AddSingleton<AuthenticationStateProvider>(new FixedAuthenticationStateProvider(principal));
            services.AddScoped<AdminRuntimeAccessor>();
            provider = services.BuildServiceProvider();
            scope = provider.CreateScope();
            Accessor = scope.ServiceProvider.GetRequiredService<AdminRuntimeAccessor>();
        }

        public FakeAdminRuntimeStore Store { get; }

        public AdminRuntimeAccessor Accessor { get; }

        public static FeatureFlagDraftMutation Mutation(string key) => new(
            key, "development", ["truckmate"], "boolean", "false", [], "test", ForgedPrivilege);

        public void SeedDraft() => Store.SeedDraft(new FeatureFlagDraft(
            "existing-flag", "development", ["truckmate"], "boolean", "false", [],
            "seed", ForgedPrivilege, 1, DateTimeOffset.UtcNow));

        public async Task InvokeAsync(string operation)
        {
            switch (operation)
            {
                case "create":
                    await Accessor.CreateDraftAsync(Mutation("new-flag"));
                    break;
                case "update":
                    await Accessor.UpdateDraftAsync(Mutation("existing-flag"));
                    break;
                case "publish":
                    await Accessor.PublishAsync(new FeatureFlagPublishAction(
                        "existing-flag", "development", "test", ForgedPrivilege));
                    break;
                case "promote":
                    await Accessor.PromoteAsync(new FeatureFlagPromotionAction(
                        "existing-flag", "development", "staging", "test", ForgedPrivilege));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }
}
