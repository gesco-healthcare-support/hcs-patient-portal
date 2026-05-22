---
id: OBS-14
title: Email pipeline is unified at every layer below the trigger surface; AuthServer + API host share the same emailer + same job queue + same SMTP host
severity: observation
status: documented
found: 2026-05-14
corrected: 2026-05-22
resolved: 2026-05-22
flow: email-architecture
---

# OBS-14 — Email pipeline (corrected understanding)

> **Correction + resolution 2026-05-22 (confidence high, source-verified).** The original framing of "two email pipelines, two services" misled future investigations. **There is one shared emailer, one shared job queue, one shared SMTP host -- only the trigger surface differs.** The original DI-scope claim (override is "in the AuthServer project, not in the API host's scope") was also wrong: the override lives in the Application project and is registered in **both** containers because both modules `DependsOn(typeof(CaseEvaluationApplicationModule))`. The Scriban tripwire that motivated some of the original phrasing has been hardened with a compile-time guard (BannedSymbols.txt + Microsoft.CodeAnalysis.BannedApiAnalyzers) and a runtime regression test (`ScribanAvoidanceTests`). Closed.

## Corrected diagnostic (2026-05-22)

### Where the override actually lives + which containers see it

`CaseEvaluationAccountEmailer` is in the **Application** project, not the AuthServer:

- `src/HealthcareSupport.CaseEvaluation.Application/Emailing/CaseEvaluationAccountEmailer.cs:67-69` carries:
  - `[Dependency(ReplaceServices = true)]`
  - `[ExposeServices(typeof(IAccountEmailer))]`

Both module manifests reference the Application module:

- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:78` -- `typeof(CaseEvaluationApplicationModule)` (added 2026-05-08 so the AuthServer's Razor `ResendVerification.cshtml.cs` can resolve `IExternalAccountAppService`).
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:66` -- same.

So **both DI containers register the same override.** ABP's auto-registration sees `[ExposeServices(typeof(IAccountEmailer))]` and registers `CaseEvaluationAccountEmailer` as the `IAccountEmailer` in every scope where the Application module is included. The original "the override is in the AuthServer project so it's missing from the API host's DI" framing was wrong on two counts: the override is not in the AuthServer project, and both containers see it.

The 500 the original observation saw at `POST http://falkinstein.localhost:44327/api/account/send-password-reset-code` was **not** a Scriban fallback. Likely cause: the API host's manifest does not pull in `AbpAccountPublicHttpApiModule` (only the AuthServer's does, at line 68 of its manifest), so that route doesn't actually exist on port 44327 -- the 500 was a routing-layer failure unrelated to the emailer override.

### Pipeline reality (one path below the trigger)

```
AUTH FLOW  (AuthServer Razor + AbpAccountPublic* routes on port 44368)
  CaseEvaluationAccountEmailer.SendXAsync
    -> Render via TemplateVariableSubstitutor (NotificationTemplate repo + ##Var## placeholders)
    -> SendAppointmentEmailArgs DTO
    -> IBackgroundJobManager.EnqueueAsync (Hangfire)
                                                \
                                                 +-> Hangfire SERVER on API host
                                                /     (HttpApi.Host config:
APPT FLOW  (any trigger on the API host)        /      IsJobExecutionEnabled = true;
  INotificationDispatcher                                AuthServer enqueues only,
    -> handler (UserRegisteredEmailHandler, etc.)        does not dequeue)
    -> Render via TemplateVariableSubstitutor              -> SendAppointmentEmailJob
    -> SendAppointmentEmailArgs DTO                            -> IEmailSender (MailKit)
    -> IBackgroundJobManager.EnqueueAsync (Hangfire)             -> mail.securemailprotocol.com
                                                                    (see [[OBS-13]])
```

**Shared infrastructure (both trigger surfaces use these):**

| Component | Project | Singleton? |
|---|---|---|
| `CaseEvaluationAccountEmailer` | Application | per-container instance (`ITransientDependency`, but stateless) |
| `TemplateVariableSubstitutor` | Domain | yes |
| `INotificationTemplateRepository` (DB-backed templates) | Domain | yes |
| `SendAppointmentEmailArgs` DTO | Domain | n/a (transport) |
| `SendAppointmentEmailJob` (Hangfire consumer) | Domain | runs only on API host (`IsJobExecutionEnabled` flag) |
| `IEmailSender` (ABP MailKit) | Domain | per-container |
| `IAccountUrlBuilder` (PR #210/#222) | Application | per-container |
| `Notifications.AuthServerBaseUrl` setting (per-tenant) | Domain.Shared / settings | shared via DB |
| SMTP host: `mail.securemailprotocol.com:587` | docker/appsettings.secrets.json | shared |

**There is nothing to "separate" and nothing to "unify."** The split is purely *which container's trigger surface initiates the send*. Below the trigger, every path converges through identical infrastructure.

### Scriban tripwire status

ABP 10.0.2 ships expecting Scriban 6.3.0. We pin Scriban to 7.2.0 for CVE patches. The 7.x `ParserOptions` layout is binary-incompatible with 6.x, so any code path that resolves ABP's Scriban-backed `ITemplateRenderer` throws `System.TypeLoadException` at runtime.

Verified 2026-05-22: **no current ABP 10.x release upgrades the Scriban dep.** `Volo.Abp.TextTemplating.Scriban` 10.1.1 (latest in 10.x line, including ABP Commercial) still pins `Scriban >= 6.3.0`. Waiting on ABP is not a viable plan.

**Avoidance pipeline (current state):**

| Trigger | Routes through | Touches Scriban? |
|---|---|---|
| Registration verify email | `UserRegisteredEmailHandler` -> `INotificationDispatcher` -> `TemplateVariableSubstitutor` | No |
| `/Account/ConfirmUser` Verify button | `wwwroot/global-scripts.js` hijacks XHR -> `/api/public/external-signup/resend-verification` | No |
| Forgot Password / Reset Password / 2FA / Change Email | `CaseEvaluationAccountEmailer` override -> `TemplateVariableSubstitutor` -> Hangfire | No |
| Appointment / packet / digest emails | Notification handlers -> `TemplateVariableSubstitutor` -> Hangfire | No |

Verified live 2026-05-19 with Scriban 7.2.0: Forgot Password renders + delivers, no `TypeLoadException`, no Scriban errors in either container's logs.

### Guard added 2026-05-22 (resolves this observation)

Three artefacts, working together:

1. **`BannedSymbols.txt` at repo root** lists every Scriban-related type / namespace as banned. The `Microsoft.CodeAnalysis.BannedApiAnalyzers` package (added to `Directory.Build.props`) ingests this file as an `AdditionalFiles` entry, applying solution-wide. Any future source-level reference to `Scriban.*`, `Volo.Abp.TextTemplating.Scriban.*`, or related types fails the build with a clear message pointing at this doc.
2. **`Directory.Build.props` comment block** (lines 37-72) documents the Scriban-avoidance contract and points at `BannedSymbols.txt` as enforcement.
3. **`ScribanAvoidanceTests`** in `test/HealthcareSupport.CaseEvaluation.Application.Tests/Notifications/` carries three xUnit `[Fact]`s that assert `CaseEvaluationAccountEmailer` still carries `[Dependency(ReplaceServices = true)]`, `[ExposeServices(typeof(IAccountEmailer))]`, and implements `IAccountEmailer`. If anyone accidentally removes those attributes (or the implementation) during a refactor, CI fails before the change merges. Removing the attributes is the only realistic path for ABP's default Scriban-backed emailer to win the DI race.

Together: the analyzer catches source-level use at compile time; the unit tests catch DI-level regression at test time.

## To test auth-related email delivery (still relevant)

Use the AuthServer's Razor flow:
- `http://falkinstein.localhost:44368/Account/ForgotPassword`
- `http://falkinstein.localhost:44368/Account/ResendVerification?email=...&autosend=1`

NOT the API host's `/api/account/*` -- those routes don't exist on the API host (`AbpAccountPublicHttpApiModule` is only on the AuthServer).

## Related

- [[BUG-018]] -- SMTP throttle + ACS-era log strings (applies to the shared `IEmailSender` + MailKit + SMTP host pair).
- [[OBS-13]] -- shared SMTP host (`mail.securemailprotocol.com` is SiteGround shared hosting fronted by SpamExperts, **not** a Microsoft 365 reseller as the original OBS claimed).
- The previously-filed BUG-019 was based on the misread of the DI scope and has been deleted.

---

## Original observation (preserved for audit trail, superseded by 2026-05-22 correction above)

### The split (per Adrian 2026-05-14)
**AuthServer** (`main-authserver-1`, port 44368) handles all **authentication-related** emails:
- Email verification (Welcome / Resend Verification)
- Password reset
- 2FA / email security codes

The override `CaseEvaluationAccountEmailer` (`src/HealthcareSupport.CaseEvaluation.AuthServer/Emailing/CaseEvaluationAccountEmailer.cs`) is registered in the AuthServer's DI container with `[Dependency(ReplaceServices = true)]` + `[ExposeServices(typeof(IAccountEmailer))]`. ABP's `AccountAppService` (Razor pages + the AuthServer-hosted `/api/account/*` endpoints) resolves `IAccountEmailer` from that container -> the override fires.

> **Correction:** path above is wrong. The override lives in the Application project at `src/HealthcareSupport.CaseEvaluation.Application/Emailing/CaseEvaluationAccountEmailer.cs`, and both module manifests `DependsOn` the Application module -- so both containers see the override.

**HttpApi.Host** (`main-api-1`, port 44327) handles all **appointment + business-flow** emails:
- Booking notifications (StatusChange/Stakeholders/Responsible)
- Packet-attached emails (Patient packet, Doctor packet, AttyCE packet)
- Document upload notifications
- Change-request emails (W3-pending)
- Daily-digest emails

These go through `SendAppointmentEmailJob` -> `IEmailSender` -> MailKit. The job uses `INotificationTemplateRepository` (DB-backed templates) directly -- no ABP `IAccountEmailer` involvement. So the Scriban-version-mismatch path is sidestepped end to end.

### What confused me
I probed `POST http://falkinstein.localhost:44327/api/account/send-password-reset-code` (API host port 44327) and got 500. The stack trace showed ABP's stock `AccountEmailer` running because:
- The API host registers `AbpAccountHttpApiModule` (for completeness)
- But the `IAccountEmailer` override is in the **AuthServer** project -- not in the API host's DI scope
- So the API host falls back to ABP's stock emailer, which then fails on Scriban

**This is not a bug** -- it's an architectural separation. The real user flow for password-reset never hits the API host's `/api/account/*` route. The SPA / Razor pages always hit the AuthServer for these.

> **Correction:** see the corrected diagnostic above. The 500 was almost certainly route-layer (no `AbpAccountPublicHttpApiModule` on the API host), not a Scriban fallback.

### Lesson for future probing
- Auth emails: hit AuthServer routes / endpoints (port 44368)
- Appointment emails: triggered as a side-effect of API host's appointment-status changes (port 44327)

> Both of those points still hold and are useful guidance.
