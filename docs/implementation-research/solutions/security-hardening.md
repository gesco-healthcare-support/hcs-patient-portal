# Security hardening: CORS lock-down + password-policy tighten

## Status (post-scope-lock 2026-04-24+)

Added to Wave 0 from the pre-Wave-0 readiness audit. Bundles two SECURITY.md leaf fixes (SEC-04 and SEC-05) into a single PR because both are HttpApi.Host module config tweaks with the same review surface and similar test plans.

- **SEC-04**: CORS policy is wide-open (`AllowAnyMethod` + `AllowAnyHeader` + wildcard subdomain `https://*.CaseEvaluation.com` + `AllowCredentials`). Combined with `AllowCredentials` this becomes a CSRF-amplifier on any compromised subdomain.
- **SEC-05**: ASP.NET Identity password policy disables every complexity requirement. Length 6 minimum, no uppercase/lowercase/digit/non-alphanumeric required. E2E test B13.4.1 (2026-04-02) confirmed `abc123` was accepted.

Effort XS (~0.5 day) total. Single PR, no migration, no DTO churn.

## Source gap IDs

- SEC-04 -- `../../issues/SECURITY.md` lines 140-178 (CORS Policy Is Wide Open). Severity Medium. Status: Open INDETERMINATE per SECURITY.md TODO -- this brief re-verifies against current code.
- SEC-05 -- `../../issues/SECURITY.md` lines 181-225 (Password Policy Fully Relaxed). Severity High. Status: Open. Test evidence: B13.4.1 confirmed `abc123` accepted on 2026-04-02.

## NEW-version code read

### CORS (SEC-04)

- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` lines 228-237 (per SECURITY.md citation): policy uses `WithOrigins(corsOrigins) + SetIsOriginAllowedToAllowWildcardSubdomains() + AllowAnyHeader() + AllowAnyMethod() + AllowCredentials()`. The `corsOrigins` value comes from `appsettings.json`'s `App:CorsOrigins` and includes the wildcard subdomain `https://*.CaseEvaluation.com`.
- The wildcard subdomain pattern combined with `AllowCredentials()` is the OWASP-flagged anti-pattern: any subdomain (including a compromised/UGC subdomain) can issue credentialed cross-origin requests with arbitrary methods and headers.
- ABP middleware `app.UseCors(...)` lives in `OnApplicationInitialization` (standard ABP placement). No change needed at that layer.

### Password policy (SEC-05)

- ASP.NET Identity options are configured via `Configure<IdentityOptions>(...)`. SECURITY.md cites `CaseEvaluationHttpApiHostModule.cs` or ABP module config as the affected file. ABP Commercial seeds Identity settings via `Volo.Abp.Identity.Settings`; concrete defaults flow through `IdentitySettingsManager` and can be overridden in `appsettings.json` at the `IdentityOptions:Password:*` keys, or in code via `Configure<IdentityOptions>`.
- Per E2E test B13.4.1 (`docs/issues/SECURITY.md:191`), the live system accepted `abc123`. Confirms current effective policy: `RequireUppercase=false`, `RequireLowercase=false`, `RequireDigit=false`, `RequireNonAlphanumeric=false`, `RequiredLength=6`.

## Live probes

None executed in this brief. SEC-04 and SEC-05 are both static-config defects already confirmed by the original 2026-04-02 audit (E11 config audit + B13.4.1 test). Re-running a CORS preflight or a registration POST against a running instance would confirm but is not load-bearing -- the source-of-truth lookup is in `CaseEvaluationHttpApiHostModule.cs` and the ABP Identity settings configuration. Both will be re-verified during the build phase by reading the file post-edit.

## OLD-version reference

Not applicable. SEC-04 and SEC-05 are NEW-side defects:
- OLD's CORS configuration is irrelevant; NEW is a different SPA + API split with different deployment topology.
- OLD's password policy is also irrelevant; NEW must independently meet contemporary HIPAA-aligned credential-strength expectations.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict on port 44368, HttpApi.Host on port 44327. CORS lock-down must continue to allow the Angular SPA's allowed methods + headers (Authorization, Content-Type, Accept, X-Requested-With, X-XSRF-TOKEN; GET, POST, PUT, DELETE).
- Row-level `IMultiTenant` (ADR-004), doctor-per-tenant. Neither CORS nor password policy interacts with multi-tenancy.
- Riok.Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003), no ng serve (ADR-005). Unaffected.
- HIPAA applicability: SEC-05 directly affects credential strength on accounts that may access PHI. Tightening to length 12 + full complexity aligns with HIPAA's Administrative Safeguard 164.308(a)(5)(ii)(D) "Password Management" expectations. CORS hardening reduces CSRF/exfiltration risk on PHI-bearing endpoints.
- Capability-specific:
  - SEC-04 fix MUST NOT block legitimate Angular SPA requests. The allowed methods list must include any HTTP verb the SPA uses (audit `angular/src/app/proxy/` for verbs in use; expected: GET, POST, PUT, DELETE).
  - SEC-04 fix MAY remove the wildcard subdomain pattern and replace with explicit origins, OR keep wildcard subdomain but tighten methods/headers. Pre-Wave-0 plan defers the wildcard decision; this brief recommends KEEPING the wildcard (deployment topology is undecided per Q29) and tightening methods/headers.
  - SEC-05 fix MUST NOT lock out existing seeded test users whose passwords may be shorter than the new minimum. The password change applies to NEW user creation and password resets; existing valid logins remain valid until the user changes their password (ASP.NET Identity default behavior). Verify against the seeded admin user (`admin@abp.io` with default seeded password) -- if the seed password violates the new policy, the seeder must be updated in the same PR.
  - Pre-Wave-0 plan locks length 12 (stricter than SECURITY.md's recommendation of 8). Justification: HIPAA-touching system; length is the cheapest dimension to harden.

## Research sources consulted

All accessed 2026-04-24.

- ASP.NET Core CORS guide: https://learn.microsoft.com/en-us/aspnet/core/security/cors -- canonical placement of `AddCors`, `UseCors`, and the `WithOrigins`/`WithMethods`/`WithHeaders` builder methods. Confirms `AllowCredentials()` is incompatible with `AllowAnyOrigin()` per spec; explicit origin lists are required.
- OWASP CORS guidance: https://owasp.org/www-community/attacks/CORS_OriginHeaderScrutiny -- explains the CSRF-amplifier pattern and why `AllowAnyMethod + AllowAnyHeader + AllowCredentials + wildcard origin` is exploitable.
- ASP.NET Core Identity password options: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration -- documents `IdentityOptions.Password.*` properties. Default `RequiredLength = 6`; recommended production minimum varies, NIST SP 800-63B suggests 8 minimum, but length-over-complexity research argues 12+ with no rotation is stronger.
- ABP Identity password settings: https://docs.abp.io/en/abp/latest/Modules/Identity#settings -- lists ABP-specific settings keys (`Abp.Identity.Password.RequiredLength`, etc.) that override `IdentityOptions.Password.*` when set via Settings UI. Pre-Wave-0 plan's abp-settings-definitions PR will define the keys; this brief sets the defaults via `Configure<IdentityOptions>` so the policy is correct on first run.
- NIST SP 800-63B Digital Identity Guidelines: https://pages.nist.gov/800-63-3/sp800-63b.html -- length over complexity, no forced rotation, blocklist common passwords. Adopted as orientation for the recommended values.
- HIPAA Administrative Safeguards 164.308(a)(5)(ii)(D): https://www.law.cornell.edu/cfr/text/45/164.308 -- "password management" requirement is non-prescriptive but expects "procedures for creating, changing, and safeguarding passwords". Internal-control framing; length 12 + complexity satisfies this without further procedure changes.

## Alternatives considered

### SEC-04 alternatives

- **A. Remove `AllowAnyHeader` + `AllowAnyMethod`; replace with explicit `WithHeaders(...)` + `WithMethods(...)` lists; keep wildcard subdomain pattern.** Chosen. Tightens CSRF surface without destabilizing the not-yet-finalized deployment topology (Q29). The wildcard subdomain stays so dev/staging/prod hostnames under `*.CaseEvaluation.com` continue to work without per-environment config rotation. Recommended by SECURITY.md SEC-04 fix.
- **B. Remove the wildcard subdomain entirely; enumerate explicit origins per environment.** Conditional / rejected for MVP. Cleaner but requires Q29 (deployment region/topology) to be decided first. Defer to post-MVP if the deployment lands on a known fixed origin set.
- **C. Drop `AllowCredentials` and use bearer-token-only auth (no cookies).** Conditional / rejected. Requires a holistic auth review; ABP's OpenIddict flow includes refresh-token cookies in some configurations. Out of scope for a leaf SECURITY.md fix.
- **D. Move CORS enforcement to a reverse proxy / WAF.** Rejected for MVP. No reverse proxy is in scope today; app-layer CORS remains the source of truth.

### SEC-05 alternatives

- **A. Tighten `IdentityOptions.Password` via `Configure<IdentityOptions>` in `CaseEvaluationHttpApiHostModule`: length 12, RequireUppercase=true, RequireLowercase=true, RequireDigit=true, RequireNonAlphanumeric=true.** Chosen. Pre-Wave-0 plan locks these values. ASP.NET Core idiomatic. Single source of truth; ABP Settings can later override per-tenant if needed.
- **B. Set the policy via ABP Settings JSON only (`Abp.Identity.Password.RequiredLength` etc.).** Conditional. Settings-based configuration is idiomatic ABP, but defaults still need to be present in code so a fresh tenant gets a correct baseline. Combine A + B: code sets defaults, Settings provide override. (See Recommended solution.)
- **C. Length 8 + complexity (SECURITY.md's original recommendation).** Rejected. Pre-Wave-0 plan locks length 12. HIPAA-touching system; the difference between 8 and 12 is one line of config and substantially raises brute-force cost.
- **D. NIST SP 800-63B-style: long passphrase (12+) + no complexity + breach-corpus blocklist.** Conditional. Closer to modern best practice but requires a passphrase blocklist source (Have I Been Pwned, k-anonymity API). Adds operational dependency. Defer to post-MVP if Adrian wants to upgrade.

## Recommended solution for this MVP

Two edits to `CaseEvaluationHttpApiHostModule.cs`, plus a verification of the seeded admin password.

### SEC-04 -- CORS

In `ConfigureServices` (or wherever the CORS policy block currently sits, lines 228-237 per SECURITY.md):

```csharp
policy.WithOrigins(corsOrigins)
    .SetIsOriginAllowedToAllowWildcardSubdomains()
    .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With", "X-XSRF-TOKEN")
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .AllowCredentials();
```

Notes:
- `WithHeaders` replaces `AllowAnyHeader`. List must include every header the Angular SPA actually sends. Standard ABP/Angular SPA set is the five above; extend if the SPA sends custom headers (e.g., `X-Tenant-Id` if Adrian later opts for a custom tenant-resolution header).
- `WithMethods` replaces `AllowAnyMethod`. List excludes `OPTIONS` (handled automatically by `UseCors`), `PATCH` (not used in current code), `HEAD` (not used). Add if the SPA actually uses them.
- The wildcard subdomain stays (`SetIsOriginAllowedToAllowWildcardSubdomains`) until Q29 (deployment region) decides. Documented as a follow-up.

### SEC-05 -- Password policy

In `ConfigureServices`:

```csharp
Configure<IdentityOptions>(options =>
{
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 1;
});
```

Notes:
- Length 12 per pre-Wave-0 plan. SECURITY.md suggested 8; pre-Wave-0 lock supersedes.
- `RequiredUniqueChars = 1` is the ASP.NET default; explicit for clarity.
- ABP Settings (`Abp.Identity.Password.*`) will override these at runtime when the abp-settings-definitions PR ships. Until then, code defaults are the source of truth.

### Seed verification

Audit the admin-user seed contributor (likely under `src/HealthcareSupport.CaseEvaluation.Domain/Identity/`) to confirm the seeded admin password meets the new length 12 + complexity rule. If not, update the seeder to use a compliant default. Verify:

- `IdentityDataSeedContributor` (ABP-provided): seeds `admin` user with default password. Confirm the default password value used (likely `1q2w3E*`); update if non-compliant.
- Any custom seeders that hardcode passwords (e.g., for test users): same audit.

If existing seeded passwords are shorter than 12, the new policy will reject NEW user creation but allow EXISTING users to continue logging in until they change their password (ASP.NET Identity does not retroactively invalidate passwords).

### Tests

- Application test: register a new user via `IdentityUserAppService.CreateAsync` with password `abc123` -- must throw `BusinessException` with code `Volo.Abp.Identity:PasswordTooShort` (or equivalent).
- Application test: register with `Password1234!` (length 12, complexity) -- must succeed.
- Manual smoke (post-deploy): preflight a CORS request from `https://example.com` (NOT in CorsOrigins) -- must return 403 / CORS denial. Preflight from `https://app.CaseEvaluation.com` (matches wildcard) -- must succeed for GET/POST/PUT/DELETE only.
- Regression: existing seeded admin user can log in (whatever the seed password is, it was valid before).

## Why this solution beats the alternatives

- Two-line config change per fix. Single PR. No migration, no DTO change, no Angular change.
- Aligns with both SECURITY.md recommendations (SEC-04 explicit lists; SEC-05 complexity flags) and pre-Wave-0 plan (length 12).
- Preserves the wildcard subdomain pattern until Q29 decides deployment topology -- avoids two rounds of churn.
- Keeps the door open for ABP Settings overrides (per-tenant policy variation) without changing the code default.

## Effort

XS (~0.5 day) total:
- 0.1d: SEC-04 CORS edit (3 builder calls).
- 0.1d: SEC-05 IdentityOptions edit (6 property assignments).
- 0.1d: seed-contributor audit + adjustment if needed.
- 0.2d: tests (one xUnit registration test for the password rejection path; manual CORS smoke).

## Dependencies

- **Blocks**: none. Both fixes are leaf-of-graph in Wave 0.
- **Blocked by**: none. Both target HttpApi.Host module config that exists today.
- **Blocked by open question**: Q29 (deployment region) only affects whether the wildcard subdomain stays or is replaced with explicit origins. Defer wildcard removal until post-Q29; the rest of the CORS hardening lands in this PR.

## Risk and rollback

- **Blast radius**: HttpApi.Host CORS middleware + ASP.NET Identity password validator. Zero data layer involvement.
- **CORS rollback**: revert the two builder calls to `AllowAnyHeader().AllowAnyMethod()`. SPA functionality immediately restored.
- **Password rollback**: revert the `Configure<IdentityOptions>` block. Existing user passwords remain valid (they were valid pre-tighten). New-user creation reverts to the relaxed policy.
- **Risk: SPA breakage from SEC-04**. If the Angular SPA sends a header not in the explicit list, requests fail with CORS preflight rejection. Mitigation: enumerate the SPA's actual headers via a `grep -r "headers:" angular/src/app/` audit during build; the standard 5-header set covers all typical ABP-generated proxy code.
- **Risk: existing seeded test passwords reject after tighten**. Mitigation: seed audit step (above). If the seeded admin password is non-compliant, update the seed in the same PR.
- **Risk: Q29 not yet decided**. Mitigation: keep wildcard subdomain; revisit when deployment topology is locked.

## Open sub-questions surfaced by research

- Should `RequiredUniqueChars` be raised from the default 1 to 4 (to reject `aaaaaaaaaaa1A!` style trivial-but-policy-compliant passwords)? Recommendation: post-MVP, after observing real password choices.
- Should `Volo.Abp.Identity.Settings.Password.LockoutEnabled` and `MaxFailedAccessAttemptsBeforeLockout` also be tightened in the same PR? They are sibling password-policy settings. Recommendation: include in this PR if Adrian wants belt-and-suspenders; otherwise defer to post-MVP review.
- Should the SPA's exact header list be source-controlled in a constant rather than hardcoded in the CORS policy? Recommendation: post-MVP refactor when the SPA stabilizes; YAGNI for MVP.
- Q29 deployment region: when decided, this brief's wildcard-subdomain note should trigger a follow-up PR to switch from `*.CaseEvaluation.com` to explicit origin enumeration.
