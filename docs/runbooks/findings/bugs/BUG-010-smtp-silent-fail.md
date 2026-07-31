---
id: BUG-010
title: Synthetic-user SMTP delivery silently fails (no MailKit pickup folder)
severity: medium
status: needs-rehydration
found: 2026-05-13
flow: notification-emails
component: docker/appsettings.secrets.json (SMTP config) + MailKit pickup folder runbook
---

# BUG-010 — Synthetic-user SMTP delivery silently fails

> **Verification 2026-05-22: OPEN (confidence 90%). Description below is partly inaccurate -- see below.**
>
> The "MailKit pickup folder" claim in this doc was **aspirational, never implemented**. Confirmed via configuration audit of current source:
>
> - `docker/appsettings.secrets.json` only sets the M365 reseller host (`mail.securemailprotocol.com:587`, plaintext password). No pickup folder configured.
> - `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:73-134` configures only `AbpMailKitOptions.SecureSocketOption = StartTls`. No synthetic-domain detection, no `PickupFolder` config, no per-recipient routing. The only "no-op" path is replacing `IEmailSender` with `NullEmailSender` when SMTP creds match the `REPLACE_*` placeholder -- this *deletes* the email entirely rather than spooling to disk.
> - `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs:90-107` swallows every exception in a warning + does not retry. When MailKit attempts to send to `@falkinstein.test`, M365 will reject the recipient with `5.1.x`; the warning is logged and Hangfire marks the job Succeeded -- exactly the "silent fail" symptom this doc described.
>
> **No related fix commits since 2026-05-13.** Sibling-bug scope check:
> - BUG-020 (2026-05-19, encrypted-setting flip) only suppressed decrypt-warning noise; does not change recipient handling.
> - BUG-018 is still open and concerns rate-limit log strings, not synthetic recipients.
> - OBS-14 confirms two pipelines (AuthServer + API host) both share the same MailKit `IEmailSender`, so neither helps `.test` recipients.
>
> Stale documentation pointer: `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md:266` still says "Their mail goes to MailKit's pickup folder" -- that line is wrong; the source has never implemented it.
>
> **Action: FIX (paired with BUG-018, same catch block).** Update this doc's suspected-fix section: the pickup-folder approach was never built. Choose one of:
> 1. Route `*.test` / `*.localhost` recipients to a `SpecifiedPickupDirectory` MailKit sender (or a logging-only sink) before MailKit attempts SMTP transmission.
> 2. Pre-filter synthetic-domain recipients before enqueueing the Hangfire job.
>
> Pair with BUG-018 since they share `SendAppointmentEmailJob.cs:90-107`. Live verification should boot the docker stack, trigger an appointment status change against `@falkinstein.test`, and confirm whether a `[WRN] SMTP delivery failed` line appears with a `5.1.x` reject from the M365 reseller (proves the silent-fail path).
>
> Cited files:
> - `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs:73-134`
> - `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs:90-107`
> - `docker/appsettings.secrets.json`
> - `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md:266` (stale claim -- correct alongside the fix)

## Severity
medium

## Status
**Needs rehydration.** Documented in earlier session compact summary; full repro to be added when re-encountered.

## What's known from earlier session
- For tests against `@falkinstein.test` / `@evaluators.com` synthetic users, the runbook claims emails should land in a local MailKit pickup folder.
- In practice no folder is created and emails go nowhere.
- For real-inbox tests (`@gesco.com`), ACS SMTP works fine.

## To do
- Inspect `docker/appsettings.secrets.json` for any pickup-folder config.
- Check the `BackgroundJobManager`/MailKit setup in `CaseEvaluationDomainModule.ConfigureMailing` (or wherever).
- Verify the runbook claim against actual source: maybe the pickup folder is mentioned in docs but never wired.

## Suspected root cause
SMTP config likely uses real-network SMTP host for ALL recipients, including `@*.test` domains that don't actually resolve to a real MX. Either:
- Synthetic-domain detection is missing — should redirect to local pickup folder when domain ends in `.test`.
- Or the pickup-folder fallback was never implemented; only real-network SMTP exists.

## Workaround
Use real Gmail inboxes (`@gesco.com`) when email delivery needs to be verified end-to-end.
