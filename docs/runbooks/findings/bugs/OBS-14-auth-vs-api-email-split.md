---
id: OBS-14
title: Auth-emails go through AuthServer; appointment/notification emails go through API host
severity: observation
found: 2026-05-14
flow: email-architecture
---

# OBS-14 — Two email pipelines, two services

## The split (per Adrian 2026-05-14)
**AuthServer** (`main-authserver-1`, port 44368) handles all **authentication-related** emails:
- Email verification (Welcome / Resend Verification)
- Password reset
- 2FA / email security codes

The override `CaseEvaluationAccountEmailer` (`src/HealthcareSupport.CaseEvaluation.AuthServer/Emailing/CaseEvaluationAccountEmailer.cs`) is registered in the AuthServer's DI container with `[Dependency(ReplaceServices = true)]` + `[ExposeServices(typeof(IAccountEmailer))]`. ABP's `AccountAppService` (Razor pages + the AuthServer-hosted `/api/account/*` endpoints) resolves `IAccountEmailer` from that container → the override fires.

**HttpApi.Host** (`main-api-1`, port 44327) handles all **appointment + business-flow** emails:
- Booking notifications (StatusChange/Stakeholders/Responsible)
- Packet-attached emails (Patient packet, Doctor packet, AttyCE packet)
- Document upload notifications
- Change-request emails (W3-pending)
- Daily-digest emails

These go through `SendAppointmentEmailJob` → `IEmailSender` → MailKit. The job uses `INotificationTemplateRepository` (DB-backed templates) directly — no ABP `IAccountEmailer` involvement. So the Scriban-version-mismatch path is sidestepped end to end.

## What confused me
I probed `POST http://falkinstein.localhost:44327/api/account/send-password-reset-code` (API host port 44327) and got 500. The stack trace showed ABP's stock `AccountEmailer` running because:
- The API host registers `AbpAccountHttpApiModule` (for completeness)
- But the `IAccountEmailer` override is in the **AuthServer** project — not in the API host's DI scope
- So the API host falls back to ABP's stock emailer, which then fails on Scriban

**This is not a bug** — it's an architectural separation. The real user flow for password-reset never hits the API host's `/api/account/*` route. The SPA / Razor pages always hit the AuthServer for these.

## To test auth-related email delivery
Use the AuthServer's Razor flow:
- `http://falkinstein.localhost:44368/Account/ForgotPassword`
- `http://falkinstein.localhost:44368/Account/ResendVerification?email=...&autosend=1`

NOT the API host's `/api/account/*`.

## Lesson for future probing
- Auth emails: hit AuthServer routes / endpoints (port 44368)
- Appointment emails: triggered as a side-effect of API host's appointment-status changes (port 44327)

## Related
- [[BUG-018]] — SMTP throttle + ACS-era log strings (applies to BOTH services; same `IEmailSender` and `MailKit` underneath)
- The previously-filed BUG-019 was based on this misunderstanding and has been deleted.
