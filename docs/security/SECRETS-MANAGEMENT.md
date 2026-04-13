[Home](../INDEX.md) > Security > Secrets Management

# Secrets Management

> For known security vulnerabilities and remediation status, see [Security Issues](../issues/SECURITY.md).

Inventory of where secrets live, how they are injected, and what is expected of operators. Active remediation items (SEC-01 secret rotation) are tracked in the linked issues file.

**Last verified:** 2026-04-13

---

## Secret Types and Locations

| Secret | Dev location | Prod location (intended) | Git status |
|---|---|---|---|
| ABP commercial license code | `appsettings.secrets.json` in AuthServer, HttpApi.Host, DbMigrator, TestBase | GitHub Secret `ABP_LICENSE_CODE`, injected at CI build time | `appsettings.secrets.json` **is gitignored** |
| ABP NuGet feed API key | `NuGet.Config` (generated from `NuGet.Config.template`) | GitHub Secret `ABP_NUGET_API_KEY`, injected via `sed` in CI | `NuGet.Config` gitignored; template tracked |
| OpenIddict PFX cert password | `appsettings.Local.json` in AuthServer | Environment variable `AuthServer__Certificate__Password` or Azure Key Vault | `appsettings.Local.json` gitignored |
| String encryption passphrase | `appsettings.Local.json` in HttpApi.Host | Environment variable `StringEncryption__DefaultPassPhrase` | gitignored |
| SQL SA password (Docker) | `.env` file at repo root, injected via Compose | Docker secrets or cloud DB connection string | `.env` gitignored; `.env.example` tracked |
| SQL connection strings | `appsettings.Local.json` | Environment variable `ConnectionStrings__Default` | gitignored |
| Kestrel cert password (Docker dev) | `.env` interpolated in compose | Not applicable in cloud (TLS termination at load balancer) | `.env` gitignored |

**Historical exposure:** SEC-01 documents that the string encryption passphrase, PFX cert password, SQL SA password, and Kestrel cert password were previously committed to source in plaintext. These have been replaced with placeholders / env var references, **but the original values remain in git history**. See [SEC-01 remediation](../issues/SECURITY.md#sec-01-secrets-committed-to-source-control).

---

## CI Secret Injection

`.github/workflows/ci.yml` injects two secrets at build time:

1. **`ABP_NUGET_API_KEY`**: substituted into `NuGet.Config.template` via `sed` before `dotnet restore`.
2. **`ABP_LICENSE_CODE`**: written to `appsettings.secrets.json` files in AuthServer, HttpApi.Host, DbMigrator, TestBase, and the ConsoleTestApp before `dotnet build`.

If either secret is absent, the CI step still creates an empty `{}` secrets file so the build does not fail -- but the resulting build will not be fully functional at runtime (ABP framework will complain about missing license).

---

## Required Operator Setup

### Local development

1. Copy `NuGet.Config.template` to `NuGet.Config`, replace `${ABP_NUGET_API_KEY}` with your key.
2. Create `appsettings.secrets.json` in AuthServer, HttpApi.Host, DbMigrator with `{ "AbpLicenseCode": "..." }`.
3. Create `appsettings.Local.json` in AuthServer and HttpApi.Host with PFX password and string encryption passphrase.
4. If using Docker Compose, copy `.env.example` to `.env` and fill in `SA_PASSWORD`, `CERT_PASSWORD`, and any other variables.
5. If using Docker secrets file, copy `docker/appsettings.secrets.json.example` to `docker/appsettings.secrets.json` with ABP license.

### Production (intended; not yet deployed)

Secrets must be injected via environment variables or a cloud secret store. Application uses ASP.NET Core configuration providers, which merge environment variables over `appsettings.json` values. Variable naming uses double-underscore for nesting (e.g., `ConnectionStrings__Default`).

---

## Scanning and Prevention

- **`.gitleaks.toml`:** Gitleaks configuration is present at repo root. Consult it for the current ruleset.
- **Husky pre-commit hook:** `.husky/` contains pre-commit hooks. Verify whether gitleaks runs in pre-commit.
- **`.gitignore` entries:** `appsettings.secrets.json`, `appsettings.Local.json`, `NuGet.Config`, `.env`, `*.pfx` files are gitignored.

---

## Gaps

1. **No secret rotation runbook.** If any secret is exposed, there is no documented process for rotating it and invalidating cached tokens / sessions.
2. **PFX certificate rotation.** After SEC-01 remediation, the signing cert should be regenerated to invalidate any copies derived from the historical password. Not yet done.
3. **No cloud secret store integration.** Azure Key Vault / AWS Secrets Manager integration is not wired. Required before any production deploy.
4. **Historical git commits still contain secrets.** SEC-01 calls this out; history rewrite (`git filter-repo` or similar) required to scrub.

---

## Related Documents

- [SEC-01 Secrets in Source Control](../issues/SECURITY.md#sec-01-secrets-committed-to-source-control)
- [Threat Model: AuthServer component](THREAT-MODEL.md#component-3-authserver-port-44368)
- [HIPAA Compliance](HIPAA-COMPLIANCE.md)
- [CI Workflow](../../.github/workflows/ci.yml)
