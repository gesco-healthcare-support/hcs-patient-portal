[Home](../INDEX.md) > Security > Secrets Management

# Secrets Management

> Purpose: Inventory of secrets, injection points, and operator setup requirements. Audience: developers and operators. Last verified: 2026-09-03 vs main.

> For known security vulnerabilities and remediation status, see [Security Issues](THREAT-MODEL.md).

Inventory of where secrets live, how they are injected, and what is expected of operators. Active remediation items (SEC-01 secret rotation) are tracked in the linked issues file.

**Last verified:** 2026-09-03

---

## Secret Types and Locations

On the deployed server, **every** secret below is supplied by one file:
`secrets/env.prod`, mode 600, git-ignored and docker-ignored, passed to Compose via
`--env-file`. `env.prod.example` at the repo root is the tracked template and documents
each key. The "Deployed location" column names the variable in that file.

| Secret | Dev location | Deployed location (`secrets/env.prod`) | Git status |
| --- | --- | --- | --- |
| ABP commercial license code | `appsettings.secrets.json` in AuthServer, HttpApi.Host, DbMigrator, TestBase | `ABP_LICENSE_CODE` (also a GitHub Secret of the same name, for CI) | `appsettings.secrets.json` **is gitignored** |
| ABP NuGet feed API key | `NuGet.Config` (generated from `NuGet.Config.template`) | `ABP_NUGET_API_KEY` (also a GitHub Secret, injected via `sed` in CI) | `NuGet.Config` gitignored; template tracked |
| OpenIddict PFX passphrase | `appsettings.Local.json` in AuthServer | `AUTHSERVER_CERT_PASSPHRASE` -> `AuthServer__CertificatePassPhrase` | `secrets/` gitignored |
| String encryption passphrase | `appsettings.Local.json` in HttpApi.Host | `STRING_ENCRYPTION_PASSPHRASE` -> `StringEncryption__DefaultPassPhrase` | gitignored |
| SQL SA password | `.env` at repo root, injected via Compose | `MSSQL_SA_PASSWORD` | `.env` gitignored; `.env.example` tracked |
| SQL connection strings | `appsettings.Local.json` | Composed from `MSSQL_SA_PASSWORD` in `docker-compose.prod.yml` | gitignored |
| MinIO root credentials | `.env` at repo root | `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD` | gitignored |
| SMTP relay credentials | `docker/appsettings.secrets.json` (`Settings:Abp.Mailing.Smtp.*`) | `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS` -> `Settings__Abp.Mailing.Smtp.*` | gitignored |
| Case Tracker tokens | `.env` at repo root | `CASE_TRACKER_INTAKE_TOKEN` (issued to us), `CASE_TRACKER_INTEGRATION_TOKEN` (issued by us) | gitignored |
| TLS wildcard cert + key | `scripts/hosting/gen-local-certs.sh` (mkcert) | `TLS_CERT_PATH`, `TLS_KEY_PATH`, pointing at files under `secrets/` | `secrets/` gitignored |

### What `STRING_ENCRYPTION_PASSPHRASE` actually protects

Nothing persisted in the database is encrypted with it **in this codebase**. No entity
column uses it, `IStringEncryptionService` has no call site in `src/`, and the one setting
ABP would otherwise encrypt (`Abp.Mailing.Smtp.Password`) is explicitly set to
`IsEncrypted = false` in `CaseEvaluationSettingDefinitionProvider.cs:175`, with the
rationale in the comment above it.

Two caveats that keep this a real secret rather than a formality:

- The ABP Commercial modules are closed-source, so it cannot be proven that none of them
  declares an encrypted setting.
- ABP ships a **published default passphrase**, so leaving it unset is equivalent to
  having no secret at all.

Treat it as a value to set deliberately and not to change casually, rather than as a value
whose loss destroys stored data.

### Fail-fast validation

`HostingConfigValidator` (`src/HealthcareSupport.CaseEvaluation.Domain/Hosting/`) throws at
startup outside Development if `ConnectionStrings:Default`,
`StringEncryption:DefaultPassPhrase`, `Redis:Configuration`, `AuthServer:Authority` or
`App:SelfUrl` is blank or still a placeholder, plus `AuthServer:CertificatePassPhrase` when
a signing certificate is required. It names the offending keys and deliberately does not
print their values.

**Historical exposure:** SEC-01 documents that the string encryption passphrase, PFX cert password, SQL SA password, and Kestrel cert password were previously committed to source in plaintext. These have been replaced with placeholders / env var references, **but the original values remain in git history**. See [SEC-01 remediation](THREAT-MODEL.md).

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

### Deployed server

Copy `env.prod.example` to `secrets/env.prod`, fill in every `CHANGE_ME_*`, and `chmod 600`
it. Every Compose command must then pass `--env-file secrets/env.prod`, or all of these
resolve to blank strings and the stack is recreated unconfigured. Use
`scripts/hosting/dc.sh`, which injects the flag and refuses to run without the file.

Secrets reach the app as environment variables. ASP.NET Core configuration providers merge
those over `appsettings.json`, with double-underscore for nesting (e.g.
`ConnectionStrings__Default`).

Certificates are generated on the server, never committed: `scripts/hosting/gen-openiddict-cert.sh`
produces `secrets/openiddict.pfx` from `AUTHSERVER_CERT_PASSPHRASE`. Note that the same PFX
serves as **both** the OpenIddict signing and encryption certificate
(`CaseEvaluationAuthServerModule.cs:180`), so regenerating it invalidates every issued token
and signs every user out.

---

## Scanning and Prevention

- **`.gitleaks.toml`:** Gitleaks configuration is present at repo root. Consult it for the current ruleset.
- **Husky pre-commit hook:** `.husky/` contains pre-commit hooks. Verify whether gitleaks runs in pre-commit.
- **`.gitignore` entries:** `appsettings.secrets.json`, `appsettings.Local.json`, `NuGet.Config`, `.env`, `*.pfx` files are gitignored.

---

## Gaps

1. **No secret rotation runbook.** If any secret is exposed, there is no documented process for rotating it and invalidating cached tokens / sessions.
2. **PFX certificate rotation.** After SEC-01 remediation, the signing cert should be regenerated to invalidate any copies derived from the historical password. Not yet done.
3. **No secret store, and a single copy.** There is no Azure Key Vault / AWS Secrets Manager integration and no company vault. `secrets/env.prod` on the server is effectively the only copy of the deployed secrets, with no rotation process and no access audit.
4. **Historical git commits still contain secrets.** SEC-01 calls this out; history rewrite (`git filter-repo` or similar) required to scrub.

---

## Related Documents

- [SEC-01 Secrets in Source Control](THREAT-MODEL.md)
- [Threat Model: AuthServer component](THREAT-MODEL.md#component-3-authserver-port-44368)
- [HIPAA Compliance](HIPAA-COMPLIANCE.md)
- [CI Workflow](../../.github/workflows/ci.yml)
