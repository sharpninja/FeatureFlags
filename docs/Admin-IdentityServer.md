# SharpNinja Admin IdentityServer - operator guide

The SharpNinja Feature Flags Admin host embeds Duende IdentityServer 7.4.x as the
sole OIDC provider in the v1 stack. This document covers everything an operator
needs to deploy, license, configure, and troubleshoot the embedded IdentityServer.

See also: [ADR-003](adr/ADR-003-admin-identity-server.md) for the architectural
decision and external-IdP upgrade path.

## Components

| Project | Purpose |
|---|---|
| `SharpNinja.FeatureFlags.Admin.IdentityServer` | Library: options, `AdminIdentityDbContext`, `SharpNinjaAdminUser`, `SharpNinjaAdminProfileService`, `SeedData`, DI extensions. |
| `SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres` | EF Core migration assembly for PostgreSQL. |
| `SharpNinja.FeatureFlags.Admin.IdentityServer.SqlServer` | EF Core migration assembly for SQL Server. |
| `SharpNinja.FeatureFlags.Admin` (host) | Composes the IdentityServer host and the Admin REST API in one process, behind a single port. |
| `SharpNinja.FeatureFlags.Admin.Blazor` (host) | OIDC client (cookie + oidc schemes) backed by the embedded IdentityServer. |
| `SharpNinja.FeatureFlags.Distribution` (host) | Resource server protecting `/admin/diagnostics` with `JwtBearer`. |

## Configuration

All hosts read the following keys (binding-style: nested keys `AdminIdentityServer:*`
or environment variables `AdminIdentityServer__*`).

| Key | Default | Used by | Purpose |
|---|---|---|---|
| `AdminIdentityServer:Authority` | `http://admin:8080` | Admin, Admin.Blazor, Distribution | Internal (in-cluster) issuer URI. |
| `AdminIdentityServer:PublicIssuer` | falls back to Authority | Distribution | External issuer URI exposed through a load balancer. |
| `AdminIdentityServer:Audience` | `sharpninja.admin.api` | Admin, Distribution | API scope name accepted by JwtBearer. |
| `AdminIdentityServer:ClientId` | `sharpninja-admin` | Admin.Blazor | OAuth client id of the Blazor host. |
| `AdminIdentityServer:RedirectUris__0..N` | `http://admin-blazor:8080/signin-oidc` | Admin | Allowed Blazor redirect URIs registered with IS. |
| `AdminIdentityServer:PostLogoutRedirectUris__0..N` | `http://admin-blazor:8080/signout-callback-oidc` | Admin | Allowed Blazor post-logout redirect URIs. |
| `AdminIdentityServer:ServiceClientSecret` | dev-only fallback | Admin | Shared secret for the `sharpninja-admin-service` client_credentials client. |
| `Duende:LicenseKey` | empty | Admin | Duende commercial license key (see License section). |

### Dual-URL issuer workaround inside Docker

When running with `docker compose`, the Admin host is reachable on two URIs:

- Inside the bridge network: `http://admin:8080` (used by Distribution and Admin.Blazor for backchannel calls).
- From the host: `http://localhost:18080` (used by the browser during the OIDC redirect dance).

Duende IdentityServer publishes its issuer in the discovery document; if the issuer
does not match what the browser sees, the OIDC client rejects the id_token. The
workaround:

1. Set `AdminIdentityServer:Authority=http://admin:8080` (internal, backchannel).
2. Set `AdminIdentityServer:PublicIssuer=http://localhost:18080` (external, browser).
3. On Distribution, `ValidIssuers` includes BOTH URIs so a token issued under either
   issuer URL validates.

For production behind an HTTPS load balancer there is only one issuer
(`https://admin.yourdomain.com`) and `Authority`/`PublicIssuer` should be set to
the same value.

## License

Duende IdentityServer is commercially licensed for production deployments above
small-volume Bootstrap limits. The Admin host reads the license key from
`Duende:LicenseKey` and passes it to `IdentityServerOptions.LicenseKey`.

- **Development / CI / demo**: no key required; Bootstrap mode is sufficient.
- **Production**: set `DUENDE_LICENSE_KEY` in your secret store and inject as
  `Duende__LicenseKey=...`.

Without a valid license key the server emits warnings during startup and may
suppress functionality at runtime. Consult <https://duendesoftware.com> for the
current tier matrix.

## Signing credentials

Three modes are supported via `AdminIdentityServer:SigningCertificate:*`:

- **PFX on disk**: set `:Path` and optional `:Password`.
- **Windows certificate store**: set `:Thumbprint`, `:StoreName`, `:StoreLocation`.
- **None configured (dev fallback)**: `AddDeveloperSigningCredential(persistKey: false)`
  generates an ephemeral RSA key per process. Tokens issued during a process restart
  will NOT validate against tokens issued before the restart.

For production deployments always supply a stable signing certificate so JWKS
rotation is operator-controlled.

## Per-provider migrations

The migration assemblies ship the initial ASP.NET Identity baseline schema as a
single `InitialIdentity` migration covering all seven AspNet* tables. Subsequent
schema changes must follow the one-table-per-migration rule documented in
`ArchitectureTests.PublicApiConstructorTests.EachMigrationMutatesOnlyOneTable`.

To apply migrations at runtime, call
`AdminIdentityServerApplicationBuilderExtensions.EnsureAdminIdentityDatabaseAsync`
during host startup. The helper detects the configured provider and:

- Relational providers (Postgres, SQL Server): runs `MigrateAsync()`.
- Sqlite (used in tests): runs `EnsureCreatedAsync()` since no migration history is
  needed for ephemeral in-memory databases.
- In-memory (used by the default `Admin` Program.cs when no connection string is
  supplied): runs `EnsureCreatedAsync()`.

## Seeded clients, scopes, and identity resources

`SeedData.ApplyDefaults` registers the following defaults:

- **Identity resources**: `openid`, `profile`, `sharpninja_rbac` (carries
  `sharpninja:tenant`, `sharpninja:products`, `role` claims).
- **API scope**: `sharpninja.admin.api` (claims projected into access tokens).
- **Clients**:
  - `sharpninja-admin` (Blazor): authorization-code + PKCE, no client secret,
    refresh tokens enabled.
  - `sharpninja-admin-service`: client_credentials, requires shared secret. Used
    for service-to-service flows and integration tests.

The deterministic seed user (created when `EnsureAdminIdentityDatabaseAsync` is
called with `seedUser` and `seedPassword`) carries the SharpNinja RBAC claim shape
projected by `SharpNinjaAdminProfileService` at token-issue time.

## Distribution / admin diagnostics

The Distribution service maps a NEW protected endpoint at `/admin/diagnostics`
that surfaces per-tenant counters in Prometheus format. Authentication is via
`JwtBearer` against the Admin IdentityServer authority. Anonymous routes (`/`,
`/health`, `/metrics`) are unchanged.

## Troubleshooting

- **Browser redirects loop**: verify `PublicIssuer` matches the URL the browser
  actually sees. The OIDC handler validates the issuer in the id_token.
- **JwtBearer returns 401 with `Bearer error="invalid_token"`**: most often a
  signing-key rotation problem. Confirm the signing certificate has not changed
  since the token was issued.
- **`sub` claim missing**: `MapInboundClaims = false` is set on JwtBearer; the raw
  JWT claim names are used as-is.
