# ADR-003: Embed Duende IdentityServer as the sole Admin OIDC provider

**Status:** Accepted; supersedes the placeholder "external OIDC" stub in `AdminAuthenticationOptions`.
**Date:** 2026-05-16
**Requirements:** TR-9, TR-10, TR-11

## Context

The SharpNinja Admin plane needs a deterministic OIDC provider for v1. Three options
were evaluated:

1. Bring-your-own external identity provider (Azure AD, Okta, Auth0).
2. Embed Duende IdentityServer in the Admin host.
3. Roll our own minimal token endpoint.

Option 1 imposes a hard dependency on a customer-controlled IdP for every deployment,
including the per-tenant developer/CI scenarios where there is no real IdP available.
Option 3 was rejected because tokens, discovery, JWKS rotation, PKCE, refresh tokens,
and consent are well-understood, easy to get wrong, and out of scope for what this
project should be hand-rolling.

## Decision

Embed Duende IdentityServer 7.4.x inside the Admin host
(`SharpNinja.FeatureFlags.Admin.IdentityServer` package) and treat it as the sole OIDC
provider in the system for v1. The Admin REST API consumes IS-issued tokens via
`JwtBearer`, the Admin.Blazor UI is registered as an OIDC client with cookie + oidc
schemes, and Distribution consumes IS-issued tokens to protect
`/admin/diagnostics`.

The package version was pinned to **7.4.7** (the latest stable release line at the
time of writing). Duende 8.x is only available as an alpha and was not deemed
production-ready for v1; the plan called for "8.x" but the substitution to the latest
stable 7.4.x line is the responsible choice.

ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`) backs the
user store, with per-provider EF Core migration assemblies
(`SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres` and
`SharpNinja.FeatureFlags.Admin.IdentityServer.SqlServer`).

## License

Duende IdentityServer is **commercially licensed for production deployments above
small-scale limits**. The Admin host reads a license key from the
`Duende:LicenseKey` configuration entry (mapped to the `DUENDE_LICENSE_KEY`
environment variable in docker-compose). When the key is absent the host runs in
Bootstrap mode, which is sufficient for development, internal demos, and CI but is
not licensed for unrestricted production use. Operators deploying SharpNinja
Feature Flags Admin to production are responsible for procuring the appropriate
Duende license tier.

## Signing credentials

For development and CI the host uses
`AddDeveloperSigningCredential(persistKey: false)`, which generates an ephemeral RSA
key per process. For production deployments operators supply either a PFX path +
password (via `AdminIdentityServer:SigningCertificate:Path` and `:Password`) or a
Windows certificate-store thumbprint + store name/location. The
`AdminIdentityServerSigningCertificateOptions` record captures all three resolution
modes.

## External-IdP upgrade path

If a customer later needs to federate with an external IdP, the
`SharpNinjaFeatureFlags.Admin.IdentityServer.AdminIdentityClientOptions` allows new
clients to be registered, and Duende supports OAuth2 token exchange and external
authentication providers natively. The Admin REST API consumer side
(`JwtBearer`) and the Admin.Blazor client side (`OpenIdConnect`) are wired to a
single configured authority - swapping that authority for an external IdP is a
configuration change, not a code change. The embedded IS host can be disabled by
removing the `AddSharpNinjaAdminIdentityServer` call from the Admin host's
`Program.cs`.

## Consequences

- **Positive**: deterministic local-only authentication for dev/CI; one canonical
  scope (`sharpninja.admin.api`) and one canonical RBAC identity resource
  (`sharpninja_rbac`); per-provider migrations support both Postgres and SQL Server.
- **Negative**: pulls Duende and ASP.NET Core Identity into the Admin host's
  dependency graph; introduces a commercial license obligation for some production
  deployments.
- **Neutral**: the ASP.NET Identity baseline schema is shipped as a single
  `InitialIdentity` migration (one migration, seven AspNet* tables) rather than
  the usual one-table-per-migration shape. This is a sanctioned deviation captured
  in `ArchitectureTests.PublicApiConstructorTests.IsExemptIdentityServerInitialMigration`.

## References

- TR-9, TR-10, TR-11 in `docs/Project/wiki/github/Technical-Requirements.md`
- `src/SharpNinja.FeatureFlags.Admin.IdentityServer/*.cs`
- `docs/Admin-IdentityServer.md` operator guide
