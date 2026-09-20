using Bunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Blazor.Services;

namespace SharpNinja.FeatureFlags.Admin.Blazor.Tests;

/// <summary>Test composition root for bUnit Admin Blazor tests.</summary>
internal static class TestServices
{
    /// <summary>Registers the Admin Blazor runtime accessor on a bUnit test context using a fake store.</summary>
    /// <param name="ctx">bUnit test context to configure.</param>
    /// <returns>The fake store seed instance shared with the runtime service.</returns>
    public static FakeAdminRuntimeStore RegisterAdminRuntime(this TestContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        FakeAdminRuntimeStore store = new();

        AdminRuntimeOptions options = new();
        ctx.Services.AddSingleton<IOptions<AdminRuntimeOptions>>(Options.Create(options));
        ctx.Services.AddSingleton<IAdminRuntimeStore>(store);
        ctx.Services.AddSingleton<IAdminRbacAuthorizer, AllowAllAuthorizer>();
        ctx.Services.AddSingleton<IAdminActorResolver, PageTestActorResolver>();
        ctx.Services.AddSingleton<AuthenticationStateProvider, PageTestAuthenticationStateProvider>();
        ctx.Services.AddSingleton<IAdminRuntimeService>(sp =>
            new InMemoryAdminRuntimeService(
                sp.GetRequiredService<IAdminRuntimeStore>(),
                sp.GetRequiredService<IAdminRbacAuthorizer>(),
                NullLogger<InMemoryAdminRuntimeService>.Instance));
        ctx.Services.AddScoped<AdminRuntimeAccessor>();
        return store;
    }
}

internal sealed class PageTestActorResolver : IAdminActorResolver
{
    public AdminActor Resolve(ClaimsPrincipal principal) =>
        new("page-test", "Page Test", "tenant-1", ["truckmate"], [AdminRoleNames.Editor]);
}

internal sealed class PageTestAuthenticationStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "page-test")], "test"))));
}

internal sealed class AllowAllAuthorizer : IAdminRbacAuthorizer
{
    public AdminAuthorizationResult Authorize(AdminActor actor, AdminRight right, AdminResourceScope scope)
        => AdminAuthorizationResult.Success;

    public AdminAuthorizationResult Authorize(AdminRbacMetadata metadata, AdminRight right, AdminResourceScope scope)
        => AdminAuthorizationResult.Success;
}
