[Home](../../INDEX.md) > [Issues](../) > Research > SEC-01

# SEC-01: Secrets Committed to Source Control -- Research

**Severity**: Critical
**Status**: Open, partially mitigated (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json`
- `src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.json`
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json`
- `etc/docker-compose/docker-compose.yml`

---

## Current state (verified 2026-04-17)

Current committed tree already uses placeholders:

- `HttpApi.Host/appsettings.json` line 23: `StringEncryption.DefaultPassPhrase: "REPLACE_ME_LOCALLY"`.
- `AuthServer/appsettings.json` line 16: `CertificatePassPhrase: "REPLACE_ME_LOCALLY"`; line 19: `StringEncryption.DefaultPassPhrase: "REPLACE_ME_LOCALLY"`.
- Docker Compose uses `${SA_PASSWORD}` / `${CERT_PASSWORD}` env-var interpolation.
- `appsettings.secrets.json` (ABP commercial license) is now in `.gitignore`.

What is still open:

- Real values live in developer-local `appsettings.Local.json` files -- the architecture has moved the risk off the repo but every dev workstation holds production-equivalent secrets.
- Git history was NOT purged. The original pre-placeholder values are still recoverable via `git log -p` on the two appsettings files.
- No documented rotation of the original values has happened.

So the "Critical" severity is no longer on the `main` branch HEAD; it lives in git history + dev workstations. Still treat as compromised.

---

## Official documentation

- [ABP String Encryption](https://abp.io/docs/latest/framework/infrastructure/string-encryption) -- `AbpStringEncryptionOptions.DefaultPassPhrase` defaults to the well-known value `gsKnGZ041HLL4IM8`; docs explicitly require replacing this.
- [ABP Configuring OpenIddict (Deployment)](https://abp.io/docs/latest/deployment/configuring-openiddict) -- production requires `AddProductionEncryptionAndSigningCertificate("openiddict.pfx", "<password>")`; PFX password is a secret.
- [Microsoft: Safe storage of app secrets in development](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0) -- Secret Manager / User Secrets for dev; explicitly "development purposes only", unencrypted on disk.
- [Microsoft: Azure Key Vault configuration provider in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-8.0) -- canonical production secret store.
- [Microsoft: Azure Key Vault protects secrets](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/secure-net-microservices-web-applications/azure-key-vault-protects-secrets) -- Managed Identity + Key Vault architecture.
- [GitHub Docs: Removing sensitive data from a repository](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository) -- recommends `git filter-repo` over the deprecated `filter-branch`.
- [BFG Repo-Cleaner](https://rtyley.github.io/bfg-repo-cleaner/) -- faster alternative; requires `git gc --prune=now --aggressive` after.
- [GitHub Docs: Remediating a leaked secret](https://docs.github.com/en/code-security/secret-scanning/working-with-secret-scanning-and-push-protection/remediating-a-leaked-secret) -- authoritative remediation order: rotate FIRST, purge history SECOND.

## Community findings

- [ABP Support #4549 -- appsettings.secrets.json leaks](https://abp.io/support/questions/4549/how-to-published-correctly---appsettingssecretsjson-leaks) -- Volo answer: do not deploy `appsettings.secrets.json`; use environment variables or a secret store.
- [ABP Support #9506 -- ABP Key in appsettings.secrets.json in Production](https://abp.io/support/questions/9506/ABP-Key-in-appsettingssecretsjson-in-Production) -- same answer: license key via env vars.
- [abpframework/abp #16625](https://github.com/abpframework/abp/issues/16625) -- ABP templates now gitignore `appsettings.secrets.json`; confirms the current direction is aligned with ABP itself.
- [abpframework/abp #6084 -- Optimal usage of StringEncryptionService](https://github.com/abpframework/abp/issues/6084) -- default passphrase is weak and well-known; changing it invalidates previously-encrypted payloads.

## Recommended approach

1. **Rotate first.** Treat the original values as compromised regardless of the placeholder move. For each secret:
   - StringEncryption passphrase (HttpApi.Host + AuthServer) -- generate new random 32+ char values.
   - OpenIddict PFX certificate password -- regenerate the PFX and its password (`openssl pkcs12 ...` or PowerShell `New-SelfSignedCertificate` + `Export-PfxCertificate`).
   - SQL SA password -- reset on any environment where the old value was ever used.
   - ABP license key -- contact Volo before attempting rotation; licences are perpetual and tied to subscription.
2. **Dev-time storage**: `dotnet user-secrets` per project (HttpApi.Host, AuthServer, DbMigrator). Keep `REPLACE_ME_LOCALLY` placeholders in committed appsettings so the shape is self-documenting.
3. **Production storage**: environment variables injected via Docker Compose / Kubernetes, or Azure Key Vault via `AddAzureKeyVault`.
4. **Purge history** with `git filter-repo --replace-text` (preferred) or BFG; force-push; notify collaborators and rebase open PRs. Do this AFTER rotation so the purge doesn't race a still-active credential.

## Blast radius if StringEncryption passphrase leaks

An attacker with the passphrase AND access to the settings DB or audit log payloads can decrypt any value ABP stored via `IStringEncryptionService` (includes setting values flagged `IsEncrypted=true`). Does NOT affect ASP.NET Core Data Protection (`IDataProtector`, separate key ring) or OpenIddict token signing (separate PFX). Confidence: MEDIUM -- docs describe the service, but concrete list of encrypted settings needs local inspection.

## Gotchas / blockers

- Rotating StringEncryption breaks decryption of anything previously encrypted with the old passphrase -- inventory encrypted data before rotating.
- Rotating OpenIddict PFX invalidates issued tokens -- all users must re-auth; persisted OpenIddict authorisations referencing the old key ID also invalidate.
- `git filter-repo` / BFG force-push invalidates every clone, every open PR, every CI run tied to old SHAs; collaborators must re-clone.
- User Secrets store is unencrypted on disk at `~/.microsoft/usersecrets/<id>/secrets.json` -- better than repo, but assume dev-workstation compromise == secret compromise.
- ABP licence key may not be rotatable; check with Volo first.

## Open questions

- Has the original (pre-placeholder) `DefaultPassPhrase` actually been rotated on any environment, or is the new placeholder still referencing the same underlying value in `appsettings.Local.json`?
- Do any encrypted settings rows currently exist in the settings DB? If yes, rotation will require a re-encrypt pass.
- Has ABP Commercial licensing confirmed whether `AbpLicenseCode` can be cycled?

## Related

- [SEC-02](SEC-02.md) -- PII logging (settings-level operational leak adjacent to this)
- [docs/issues/SECURITY.md#sec-01-secrets-committed-to-source-control](../SECURITY.md#sec-01-secrets-committed-to-source-control)
