using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags.Admin;

internal sealed class AdminTestAuthenticationOptions : AuthenticationSchemeOptions
{
    public string PrincipalHeaderName { get; set; } = SharpNinjaAdminDefaults.TestPrincipalHeaderName;

    public string TenantHeaderName { get; set; } = SharpNinjaAdminDefaults.TestTenantHeaderName;

    public string ProductsHeaderName { get; set; } = SharpNinjaAdminDefaults.TestProductsHeaderName;

    public string RolesHeaderName { get; set; } = SharpNinjaAdminDefaults.TestRolesHeaderName;

    public string DisplayNameHeaderName { get; set; } = SharpNinjaAdminDefaults.TestDisplayNameHeaderName;
}

internal sealed class AdminTestAuthenticationHandler : AuthenticationHandler<AdminTestAuthenticationOptions>
{
    public AdminTestAuthenticationHandler(
        IOptionsMonitor<AdminTestAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string principalId = Request.Headers[Options.PrincipalHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(principalId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string tenantId = ReadHeaderOrDefault(Options.TenantHeaderName, "tenant-test");
        string displayName = ReadHeaderOrDefault(Options.DisplayNameHeaderName, principalId);
        string[] products = ReadDelimitedHeader(Options.ProductsHeaderName, SharpNinjaProductCatalog.TruckMate);
        string[] roles = ReadDelimitedHeader(Options.RolesHeaderName, AdminRoleNames.Viewer);

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, principalId.Trim()),
            new(ClaimTypes.Name, displayName.Trim()),
            new(SharpNinjaAdminDefaults.TenantClaimType, tenantId.Trim()),
        ];

        foreach (string product in products)
        {
            claims.Add(new Claim(SharpNinjaAdminDefaults.ProductsClaimType, product));
        }

        foreach (string role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string ReadHeaderOrDefault(string headerName, string defaultValue)
    {
        string value = Request.Headers[headerName].ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private string[] ReadDelimitedHeader(string headerName, string defaultValue)
    {
        string value = Request.Headers[headerName].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = defaultValue;
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => part.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
