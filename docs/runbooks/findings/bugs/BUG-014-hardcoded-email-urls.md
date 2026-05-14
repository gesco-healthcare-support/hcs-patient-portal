---
id: BUG-014
title: SPA + AuthServer URLs hardcoded as falkinstein.localhost:4200/:44368 in email templates
severity: medium
status: open
found: 2026-05-14
flow: notification-emails
component: Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs (setting defaults)
---

# BUG-014 — Hardcoded SPA + AuthServer URLs in email templates

## Severity
medium (blocks non-canonical-port testing; breaks multi-tenant Phase 2)

## Status
**Open** — for fix session.

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
