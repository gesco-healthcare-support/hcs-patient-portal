---
id: BUG-014
title: SPA + AuthServer URLs hardcoded as falkinstein.localhost:4200/:44368 in email templates
severity: medium
status: fixed-by-redesign
fixed: 2026-05-22
last-replayed: 2026-05-22
fixed-on: feat/parallel-worktree-stacks (merged via #208) + #210 + #222
found: 2026-05-14
flow: notification-emails
component: Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs (setting defaults)
---

> **Fixed-by-redesign 2026-05-22** — three commits landed the fix in
> stages, each more architecturally complete than the prior:
>
> 1. **`5cdae28` feat(notifications): config-driven email URLs + tenant composer**
>    (merged via #208 on `feat/parallel-worktree-stacks`). Injected
>    `IConfiguration` into `CaseEvaluationSettingDefinitionProvider`; the
>    `PortalBaseUrl` + `AuthServerBaseUrl` defaults now source from
>    `App:AngularUrl` + `AuthServer:Authority` (docker-compose env vars
>    `App__AngularUrl`, `App__SelfUrl`, `AuthServer__Authority`). Added a
>    new `TenantUrlComposer` static helper that rewrites the bare-localhost
>    host token to `{tenantName}.localhost` using the same regex
>    (`(^|//)localhost(?=([:/]|$))`) as `angular/src/tenant-bootstrap.ts:99`,
>    so the SPA bootstrap and backend email rendering share one substitution
>    rule. Hardcoded `"http://falkinstein.localhost:..."` fallbacks removed:
>    the resolver now throws a clear "set the env var" error if both the DB
>    setting and the env var are empty -- no silent emission of Falkinstein
>    URLs from a non-Falkinstein stack.
>
> 2. **`c53b12e` fix(notifications): centralize tenant-aware email URL composition (#210)**.
>    Introduced `IAccountUrlBuilder` + `AccountUrlBuilder` service. The
>    16 disparate call sites that previously each read a setting and
>    sometimes wrapped with `TenantUrlComposer` (and sometimes forgot)
>    collapsed to a single dispatch through the service. The builder takes
>    an **explicit `Guid tenantId` argument** -- removing the silent-null
>    `ICurrentTenant.Name` failure mode where background-job execution
>    contexts had ID set but Name null (per ABP framework source,
>    `ICurrentTenant.Change(Guid?)` defaults `Name=null`).
>
> 3. **`be2749f` fix(notifications): tenant-aware email URL composition (#222)**.
>    Final round of caller migration + packet-email integration; closes the
>    loop on the email-URL story across all atty/CE/patient/doctor packet
>    paths.
>
> **Recommended-fix audit** (every item from the original report below):
>
> | Item | Status |
> |---|---|
> | 1. Inject `IConfiguration` into `CaseEvaluationSettingDefinitionProvider` | Done (5cdae28) |
> | 2. Replace literal `defaultValue` with `App:AngularUrl` + `AuthServer:Authority` | Done -- `_configuration["App:AngularUrl"]?.TrimEnd('/')` at provider lines 65-77 |
> | 3. Add env vars to docker-compose AuthServer + api blocks | Done -- `App__SelfUrl`, `App__AngularUrl`, `AuthServer__Authority` at compose lines 146/157/171/229/233/248 |
> | 4. Phase 2 multi-tenant subdomain from `ICurrentTenant.Name` | Done -- `AccountUrlBuilder.ResolveTenantNameAsync` looks up `tenant.Name` from `ITenantStore` via explicit `tenantId` (sidesteps the `ICurrentTenant.Name=null` trap entirely) |
> | 5. Delete the 4 dead `DefaultPortalBaseUrl` consts | Done -- all 4 caller files refactored. Only remaining `falkinstein.localhost` strings in the codebase are XML doc-comment examples |
>
> **Test coverage**: 24 unit tests in
> `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/`
> (`TenantUrlComposerUnitTests.cs` 101 lines + `AccountUrlBuilderTests.cs`
> 287 lines). Cover the regex (bare-localhost, already-prefixed,
> no-localhost-token, null/empty tenant) and all 5 service methods + the
> 3-step fallback + the `Guid.Empty` guard. All pass.
>
> **Live-verified 2026-05-22** against `main-api-1` / `main-authserver-1`
> on the rebuilt stack:
>
> - `POST http://falkinstein.localhost:44368/api/account/send-password-reset-code`
>   with body `{"email":"patient1@gesco.com","appName":"Angular",...}` -> HTTP 204
>   in ~1.7s (real SMTP send to `mail.securemailprotocol.com` actually happened).
> - AuthServer log line: `Request finished HTTP/1.1 POST .../send-password-reset-code - 204 ... 2957.7678ms`.
> - No remaining production-code references to hardcoded
>   `falkinstein.localhost:4200` / `:44368` (`grep -r "falkinstein\.localhost"`
>   on `src/**/*.cs` returns only XML doc-comment lines).
> - `SELECT ... FROM AbpSettings WHERE Name LIKE '%PortalBaseUrl%' OR Name LIKE '%AuthServerBaseUrl%'`
>   returns 0 rows -- settings resolve through the **default chain**
>   (IConfiguration env -> `TenantUrlComposer`), not from a per-tenant override,
>   exercising the fixed code path end-to-end.
>
> Architecture references:
> - `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs:65-77`
>   (IConfiguration-sourced defaults)
> - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/IAccountUrlBuilder.cs`
>   (5-method contract)
> - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/AccountUrlBuilder.cs`
>   (3-step fallback + `Guid.Empty` guard + tenant-name lookup)
> - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/TenantUrlComposer.cs`
>   (regex-based host-token rewrite, idempotent for already-prefixed URLs)

# BUG-014 — Hardcoded SPA + AuthServer URLs in email templates

## Severity
medium (blocks non-canonical-port testing; breaks multi-tenant Phase 2)

## Status
**Fixed-by-redesign** — see top quote block.

## Affected
Every email template that contains a portal/authserver link: verification, password reset, booking confirmation, packet ready, etc.

## Symptom
Every email bakes URLs as `http://falkinstein.localhost:4200/...` and `http://falkinstein.localhost:44368/...`. On non-canonical-port stacks, links 404. In a multi-tenant deployment, the subdomain prefix `falkinstein.` is wrong for any tenant other than Falkinstein.

## Root cause
The per-tenant settings `Notifications.PortalBaseUrl` and `Notifications.AuthServerBaseUrl` are defined with literal default values in `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs`:
```csharp
Define(context,
    CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl,
    defaultValue: "http://falkinstein.localhost:4200");          // line 43
Define(context,
    CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl,
    defaultValue: "http://falkinstein.localhost:44368");         // line 53
```

The setting framework returns this default whenever the tenant hasn't overridden the value (which is always, since the setting is not seeded per-tenant). So every caller of `_settingProvider.GetOrNullAsync(PortalBaseUrl)` gets the hardcoded string — the per-class `DefaultPortalBaseUrl` consts in `CaseEvaluationAccountEmailer.cs`, `BookingSubmissionEmailHandler.cs`, `ExternalAccountAppService.cs`, `AccessorInvitedEmailHandler.cs` are dead code that never fire.

## Recommended fix
1. Inject `IConfiguration` into `CaseEvaluationSettingDefinitionProvider`.
2. Replace the literal `defaultValue` with values read from `App:AngularUrl` (and `AuthServer:Authority` for the AuthServer URL). `App:AngularUrl` is already env-var-driven in `docker-compose.yml` via `appsettings.json:4` → overridable via `App__AngularUrl`.
3. Add the corresponding env vars to the AuthServer block in `docker-compose.yml`:
   ```yaml
   App__AngularUrl: "http://falkinstein.localhost:${NG_PORT:-4200}"
   AuthServer__Authority: "http://falkinstein.localhost:${AUTH_PORT:-44368}"
   ```
4. (Phase 2) when multi-tenant ships, derive the tenant-subdomain prefix from `ICurrentTenant.Name` rather than the literal `falkinstein.`. Out of scope for the immediate fix; track separately.
5. Optional cleanup: delete the dead `DefaultPortalBaseUrl` consts in the four caller classes once the SettingDefinition default is configuration-driven.

## Related
- [[BUG-013]] (CORS missed AuthServer self-port).
- [[BUG-015]] (dynamic-env.json never read by SPA).
- [[BUG-016]] (OpenIddict subdomain wildcards).

All four form a trio around multi-environment URL handling; fix together for end-to-end config-driven URLs.

## Source pointers
- Setting defaults: `src/HealthcareSupport.CaseEvaluation.Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs:43,53`
- Dead consts (4 places):
  - `src/HealthcareSupport.CaseEvaluation.AuthServer/Emailing/CaseEvaluationAccountEmailer.cs:66`
  - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/BookingSubmissionEmailHandler.cs:77-78`
  - `src/HealthcareSupport.CaseEvaluation.Application/ExternalAccount/ExternalAccountAppService.cs:368`
  - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/AccessorInvitedEmailHandler.cs:55`
