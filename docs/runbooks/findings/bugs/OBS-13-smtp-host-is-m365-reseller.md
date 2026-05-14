---
id: OBS-13
title: `mail.securemailprotocol.com` SMTP host is a Microsoft 365 reseller; inherits EXO limits
severity: observation
found: 2026-05-14
flow: notification-emails
---

# OBS-13 — `securemailprotocol.com` runs on M365 Exchange Online

## Symptom
The app's `appsettings.secrets.json` points `Abp.Mailing.Smtp.Host` at `mail.securemailprotocol.com:587`. Despite the branding suggesting a self-contained "secure mail" provider, the SMTP rejection error from this host explicitly references Microsoft's documentation:

```
4.5.127 Message rejected. Excessive message rate from sender.
For more information see https://aka.ms/EXOSmtpErrors.
```

`aka.ms/EXOSmtpErrors` is Microsoft's own URL for Exchange Online SMTP error codes — a genuinely independent SMTP server (Postfix, Exim, SendGrid, Mailgun, Mailpit, etc.) would never reference `aka.ms`. So `securemailprotocol.com` is a reseller / wrapper on top of Microsoft 365 Exchange Online.

## Implications
The `patientportal@securemailprotocol.com` mailbox is subject to M365 SMTP AUTH per-mailbox limits:
- 30 messages/minute rolling cap
- 10,000 recipients/day ceiling
- Stricter burst protection that rejects ~7 emails sent in <3 seconds (the symptom we hit at 20:17)

## Operational impact
This is **not** a problem with our code — it's a deliberate Microsoft throttle on shared infrastructure. Two responses are appropriate:

1. **Throttle proactively in the app** (Adrian's choice 2026-05-14; see [[BUG-018]] for the implementation spec).
2. **Long-term consideration**: move transactional email off `securemailprotocol.com` to a true transactional-email service (SendGrid / Mailgun / Postmark / Amazon SES) once Phase 1A wraps. Those services are designed for higher transactional throughput and have no surprise burst protection.

## Related
- [[BUG-018]] — implementation ticket: throttle to 2-3 emails/sec + retry on rate-limit + relabel ACS-era log strings
- [[BUG-010]] — synthetic-user SMTP delivery (separate; `.test` domains never reach M365's MX)
