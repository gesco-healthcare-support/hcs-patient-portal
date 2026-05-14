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
