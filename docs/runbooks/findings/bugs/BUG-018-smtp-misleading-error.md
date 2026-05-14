---
id: BUG-018
title: SMTP rate-limit reported as misleading "Configure ACS credentials" warning
severity: medium
status: open
found: 2026-05-14 during Workflow C approval flow
flow: notification-emails
component: src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs:104,156
---

# BUG-018 — Misleading "Configure ACS credentials" log on SMTP rate-limit

## Severity
medium (operationally important: makes triaging email-delivery failures slow because the message blames the wrong subsystem)

## Status
**Open** — for fix session.

## Symptom
After Workflow B successfully delivered 7 emails at ~20:05, Workflow C's approval at ~20:17 triggered another 7 emails. ALL 7 of Workflow C's emails failed with this warning in the api logs:

```
[WRN] SendAppointmentEmailJob: SMTP delivery failed (StatusChange/Approved/Stakeholders/...)
       to SoftwareSix@gesco.com.
       Configure ACS credentials to deliver. Job will not retry until Attempts policy is raised.
MailKit.Net.Smtp.SmtpCommandException: 4.5.127 Message rejected.
       Excessive message rate from sender. For more information see https://aka.ms/EXOSmtpErrors.
```

The log message **"Configure ACS credentials to deliver"** suggests an authentication/configuration problem. The actual exception underneath is a transient **rate-limit** error from Exchange Online (`4.5.127`).

## Root cause
`SendAppointmentEmailJob.cs:90-108` (and the analogous SendWithAttachmentAsync at line ~146) catches all `Exception` types and emits the same warning message regardless of cause:
```csharp
catch (Exception ex)
{
    Logger.LogWarning(
        ex,
        "SendAppointmentEmailJob: SMTP delivery failed ({Context}) to {To}. Configure ACS credentials to deliver. Job will not retry until Attempts policy is raised.",
        args.Context, args.To);
}
```
The inner exception type / SMTP status code is not surfaced in the message; the operator has to read the stack trace below to find the real cause.

## Recommended fix
1. Distinguish SMTP failure categories before logging:
   - `SmtpCommandException` with status 4.5.127 / 4.7.x → rate-limit, retry after delay
   - `SmtpCommandException` with status 5.7.x → authentication / configuration
   - `SocketException` / `IOException` → network / connectivity
   - other → unknown
2. Emit a more accurate log per category. The current "Configure ACS credentials" line should only fire for the auth/config category.
3. Implement an automatic retry with backoff for rate-limit failures (currently the job is dropped with "will not retry until Attempts policy is raised"). EXO's `4.5.127` is documented as a transient throttle; a 30-60s backoff + 3-5 retries would recover automatically.
4. Optionally surface a metric / alert when rate-limit warnings exceed a threshold per hour.

## Workaround used this session
None. We accepted the temporary rate-limit and continued workflows that don't depend on email verification, planning to re-test email delivery after the EXO rate window cleared.

## Additional scope (added 2026-05-14): stale ACS references in source

Adrian's note 2026-05-14: *"We are not using ACS anymore. Why do these ACS creds keep leaking in. We are using the In-house SMTP now."*

Even though the actual SMTP provider is now in-house (which forwards to Microsoft Exchange Online, per the `4.5.127` error code), the codebase still references the abandoned ACS approach in 4 source files. These need a global rename to generic "SMTP credentials" terminology:

| File | Line | Stale ref |
| --- | --- | --- |
| `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs` | 24 | comment: `"Succeeded" and never retries. Intentional while ACS placeholder` |
| ... | 27 | comment: `delivery did not happen. When real ACS credentials land, remove the` |
| ... | 104 | log warning: `Configure ACS credentials to deliver` |
| ... | 156 | log warning: `Configure ACS credentials to deliver` (attachment path) |
| `src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs` | 112 | comment: `delivery in the Docker dev stack even after real Azure ACS SMTP` |
| ... | 201-202 | comment: `"REPLACE_WITH_ACS_USERNAME"` / `"REPLACE_WITH_ACS_PASSWORD_CONNECTION_STRING"` |
| ... | 205 | comment: `Real ACS credentials never start with` |
| `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/IExternalSignupAppService.cs` | 31 | comment: `silently until ACS credentials land per S-5.7)` |

Also worth checking: the `appsettings.secrets.json` placeholder values (per the comments above) may still use `REPLACE_WITH_ACS_*` labels. If so, rename to generic `REPLACE_WITH_SMTP_*` since the consumer no longer cares about provider identity.

## Related
- [[BUG-010]] (synthetic-user SMTP silently fails) — separate problem (synthetic `.test` domains never reach a real MX). Combined with BUG-018, the email-delivery surface has multiple log-message-vs-root-cause mismatches.
