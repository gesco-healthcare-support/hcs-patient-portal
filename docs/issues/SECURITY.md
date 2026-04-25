[Home](../INDEX.md) > [Issues](./) > Security

<!-- Last reorganized 2026-04-24 against docs/product/ + docs/gap-analysis/ -->

# Security Issues

> **2026-04-24 reorganization note**: This file lists 5 issues (SEC-01..SEC-05) discovered during the original 2026-04-02 audit. The 2026-04-23 deep-dive captured 5 additional MVP-blocking security/quality items (NEW-SEC-01..NEW-SEC-05 and NEW-QUAL-01) that should appear in this register. See `docs/gap-analysis/10-deep-dive-findings.md` Part 2 + Part 5. A pointer table is added at the bottom under "Additional security gaps from 2026-04-23 deep-dive".

Five security issues were identified during the codebase audit and confirmed via E2E testing on 2026-04-02. Two are critical and require remediation before any production deployment. All file paths and line numbers are current as of the audit date.

> **Test Status (2026-04-02)**: SEC-01 confirmed via B16.2.1-2, SEC-02 confirmed via B16.6.1, SEC-03 confirmed via exploratory test E4 (8 users with PII exposed to Patient role), SEC-04 confirmed via E11 config audit, SEC-05 confirmed via B13.4.1. See [TEST-EVIDENCE.md](TEST-EVIDENCE.md).

---

## SEC-01: Secrets Committed to Source Control

**Severity:** Critical
**Status:** Open -- partial mitigation in place (placeholders); full rotation + history scrub still pending. The "Affected Files and Values" table below already reflects the placeholder-and-Local.json pattern in current code; remaining work is rotation + history audit.

### Description

Multiple credentials and secret values are committed directly to tracked source files. Any developer with repository access -- or any person who gains access to the git history -- has immediate access to these credentials.

### Affected Files and Values

| File | Secret | Value |
|---|---|---|
| `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json` line 23 | String encryption passphrase | Now uses `REPLACE_ME_LOCALLY` placeholder; real value in `appsettings.Local.json` |
| `src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.json` | OpenIddict PFX certificate password | Now uses `REPLACE_ME_LOCALLY` placeholder; real value in `appsettings.Local.json` |
| `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json` | ABP commercial licence key | Base64-encoded licence key string (gitignored) |
| `etc/docker-compose/docker-compose.yml` | SQL Server SA password | Now uses `${SA_PASSWORD}` env var interpolation |
| `etc/docker-compose/docker-compose.yml` | Kestrel certificate password | Now uses `${CERT_PASSWORD}` env var interpolation |

### Impact

- Anyone with repo access can impersonate the application's OpenIddict token issuer by using the committed PFX passphrase.
- The SQL SA password grants full database administrative access including dropping tables and reading all tenant data.
- The ABP licence key can be used to activate other ABP commercial projects under the original licence.

### Recommended Fix

1. Rotate all committed credentials immediately -- the existing values are compromised regardless of any code change.
2. Remove secrets from all tracked files and replace with placeholder comments or environment variable references.
3. Use `.gitignore` to exclude `appsettings.secrets.json` and any `*.pfx` files.
4. Use environment variables or a secrets manager (Azure Key Vault, AWS Secrets Manager, or Docker secrets) to inject values at runtime.
5. Audit git history with a tool such as `git log -S "<passphrase>"` to confirm no other secrets are embedded in older commits.

```json
// appsettings.json -- replace hardcoded values with environment variable placeholders
{
  "StringEncryption": {
    "DefaultPassPhrase": ""   // set via environment variable StringEncryption__DefaultPassPhrase
  }
}
```

---

## SEC-02: PII Logging Enabled by Default

**Severity:** High
**Status:** Open -- guard logic (`App:DisablePII` defaults to false meaning PII-on) unchanged in current code per 2026-04-24 review; opt-out semantics still inverted.

### Description

The HttpApi.Host startup module enables full PII (Personally Identifiable Information) logging unless an explicit opt-out configuration key is set. Because the key defaults to `false` (meaning "do not disable PII"), PII logging is **active in every environment where `App:DisablePII` is not explicitly set to `true`**.

### Affected File

`src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` lines 67--71:

```csharp
if (!configuration.GetValue<bool>("App:DisablePII"))
{
    IdentityModelEventSource.ShowPII = true;
    IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}
```

### Impact

When active, this configuration causes the Microsoft identity model event source to include full JWT token payloads, user claims, email addresses, and session identifiers in the Serilog log output. Log files (written to `Logs/`) then contain raw access tokens and user PII.

### Recommended Fix

Invert the guard so that PII logging requires an explicit opt-in, and restrict it to the `Development` environment:

```csharp
// Only enable PII logging in Development when explicitly requested
if (env.IsDevelopment() && configuration.GetValue<bool>("App:EnablePII"))
{
    IdentityModelEventSource.ShowPII = true;
    IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}
```

Add `App:DisablePII: true` (or remove the key entirely after the fix) to all non-development `appsettings.json` files.

---

## SEC-03: External User Lookup Endpoint Unauthenticated and Unprotected

**Severity:** High
**Status:** Open -- corroborated independently by `docs/gap-analysis/10-deep-dive-findings.md` Part 2 (NEW-SEC-02 generalises this to most AppService Create/Update/Delete methods missing method-level `[Authorize]`).

### Description

`ExternalSignupAppService.GetExternalUserLookupAsync` returns the name, email, and role of every external user registered on a tenant. The method has no `[Authorize]` attribute, the class has no class-level `[Authorize]`, and there is no permission check. Any authenticated user -- including a `Patient` who registered with no elevated privileges -- can call this endpoint to enumerate all attorneys and their contact details.

Additionally, the endpoint returns results without pagination applied (there is no `MaxResultCount` guard on the repository call), so a single request can return all external users.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` line 62

```csharp
public virtual async Task<List<ExternalUserLookupDto>> GetExternalUserLookupAsync(ExternalUserLookupRequestDto input)
// No [Authorize] attribute -- no class-level [Authorize] on this class
```

### Impact

- Unauthenticated or low-privilege users can obtain a full list of all attorneys (names, emails) registered on any tenant.
- With no rate limiting in place, this endpoint can be used to scrape all user data in a single call.

### Recommended Fix

1. Add a specific permission check:

```csharp
[Authorize(CaseEvaluationPermissions.Appointments.Default)]
public virtual async Task<List<ExternalUserLookupDto>> GetExternalUserLookupAsync(...)
```

2. Apply `MaxResultCount` to the repository query (cap at 50--100 results).
3. Verify whether the neighbouring anonymous endpoints on `ExternalSignupController` (`/register`, `/tenant-options`) are also correctly attributed.

---

## SEC-04: CORS Policy Is Wide Open

**Severity:** Medium
**Status:** Open -- INDETERMINATE on current code state. <!-- TODO: re-verify against current `CaseEvaluationHttpApiHostModule.cs` to confirm wildcard subdomain + AllowAnyHeader/Method are still live -->.

### Description

The CORS policy defined in `CaseEvaluationHttpApiHostModule.cs` allows any header, any method, and credentials for all origins listed in `CorsOrigins`. The `CorsOrigins` value includes a wildcard subdomain pattern (`https://*.CaseEvaluation.com`), which expands the permitted origin surface to any subdomain.

### Affected File

`src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` lines 228--237:

```csharp
policy.WithOrigins(corsOrigins)
    .SetIsOriginAllowedToAllowWildcardSubdomains()
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials();
```

### Impact

Combining `AllowCredentials()` with `AllowAnyHeader()` and `AllowAnyMethod()` means any page served from a permitted origin (including any `*.CaseEvaluation.com` subdomain) can make credentialed cross-origin requests with arbitrary headers and HTTP methods, including `DELETE` and `PATCH`. If a subdomain is compromised or used for user-generated content, this becomes a vector for CSRF-like attacks.

### Recommended Fix

Restrict permitted methods and headers to only those actually used by the Angular SPA:

```csharp
policy.WithOrigins(corsOrigins)
    .SetIsOriginAllowedToAllowWildcardSubdomains()
    .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With", "X-XSRF-TOKEN")
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .AllowCredentials();
```

Review whether the wildcard subdomain pattern is necessary and replace with explicit origins where possible.

---

## SEC-05: Password Policy Fully Relaxed

**Severity:** High
**Status:** Open -- **Confirmed via E2E testing (2026-04-02, test B13.4.1)**. INDETERMINATE on whether ABP Identity settings have since been tightened. <!-- TODO: re-verify password complexity options in current `appsettings.json` / Settings UI seed -->.

### Description

The ASP.NET Identity password policy has all complexity requirements disabled: `RequireUppercase=false`, `RequireLowercase=false`, `RequireDigit=false`, `RequireNonAlphanumeric=false`. Only minimum length of 6 characters is enforced. This means passwords like `123456` or `aaaaaa` are accepted.

### Test Evidence

```
B13.4.1: Registered user with password "abc123" (no uppercase, no special char)
  Result: Accepted successfully
  
All seeded users use the same default password which happens to be complex,
but the policy does not require it.
```

### Impact

- Users can set trivially guessable passwords
- In a healthcare/legal context, weak passwords expose PHI and case data
- Combined with SEC-03 (user enumeration), an attacker can brute-force patient accounts

### Affected Files

- Identity configuration in `CaseEvaluationHttpApiHostModule.cs` or ABP module config
- ABP Identity settings (configurable via Settings UI or `appsettings.json`)

### Recommended Fix

Enable password complexity requirements:

```csharp
Configure<IdentityOptions>(options =>
{
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
});
```

---

## Additional security gaps from 2026-04-23 deep-dive

The following 5 MVP-blocking security/quality items were captured in `docs/gap-analysis/10-deep-dive-findings.md` Part 2 and Part 5 after this register was first written. They should be folded into this file in a subsequent pass; pointers added here so the audit trail is complete.

| ID | Severity | Summary | Source |
|---|---|---|---|
| NEW-SEC-01 | MVP-blocking | `/appointments/view/:id` and `/appointments/add` routes only have `authGuard`, not `permissionGuard` -- any authenticated user can view/add appointments regardless of permission grants | `docs/gap-analysis/10-deep-dive-findings.md` Part 2 |
| NEW-SEC-02 | MVP-blocking | Most AppService `CreateAsync`/`UpdateAsync`/`DeleteAsync` methods lack method-level `[Authorize(...Create)]` attributes; HTTP-level permission enforcement is missing for mutations. Generalises SEC-03. | `docs/gap-analysis/10-deep-dive-findings.md` Part 2 |
| NEW-SEC-03 | MVP-blocking | `DoctorTenantAppService.CreateAsync` runs with `isTransactional: false`; partial failure leaves orphaned `SaasTenant` rows. Hardcoded `Gender.Male` + empty `LastName` in Doctor creation. | `docs/gap-analysis/10-deep-dive-findings.md` Part 2 |
| NEW-SEC-04 | MVP-blocking | `ExternalSignupAppService.RegisterAsync` creates Patient with hardcoded `Gender.Male`, `DateOfBirth = today`, `PhoneNumberType.Home`. Data-quality + legal issue. | `docs/gap-analysis/10-deep-dive-findings.md` Part 2 |
| NEW-SEC-05 | MVP-blocking | NEW does not send `Strict-Transport-Security` header; OLD does. HTTPS downgrade vulnerability in production. | `docs/gap-analysis/10-deep-dive-findings.md` Part 2 |
| NEW-QUAL-01 | MVP-blocking | Zero tests for tenant provisioning, permission enforcement, external signup, multi-tenancy filter. Demo-regression risk. | `docs/gap-analysis/10-deep-dive-findings.md` Part 2 |

## Related Documentation

- [Issues Overview](OVERVIEW.md) -- All issues by category and severity
- [Test Evidence](TEST-EVIDENCE.md) -- Full E2E test results
- [Middleware & Pipeline](../api/MIDDLEWARE-AND-PIPELINE.md) -- CORS and authentication pipeline
- [Authentication Flow](../api/AUTHENTICATION-FLOW.md) -- OpenIddict token issuance
- [User Roles & Actors](../business-domain/USER-ROLES-AND-ACTORS.md) -- Permission model
- [Gap Analysis: Deep-Dive Findings](../gap-analysis/10-deep-dive-findings.md) -- Source for the NEW-SEC-* / NEW-QUAL-* items above
