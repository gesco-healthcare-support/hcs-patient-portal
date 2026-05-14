---
id: BUG-020
title: SMTP password plaintext from appsettings.secrets.json fails ABP decrypt round-trip; noisy logs
severity: medium
status: open
found: 2026-05-14 confirmed on fresh-DB rebuild
flow: notification-emails
component: src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs (settings definition wiring)
---

# BUG-020 — `Abp.Mailing.Smtp.Password` decrypt round-trip throws on plaintext config value

## Severity
medium — operationally annoying, not user-impacting (emails still deliver), but logs are dense with stack traces during every email send and SettingEncryptionService is doing wasted work.

## Status
**Open** — for fix session.

## Symptom
Every single email send (whether from `SendAppointmentEmailJob` in the API host or from `CaseEvaluationAccountEmailer` enqueueing a job from the AuthServer) logs the following pair:
```
[WRN] Failed to decrypt the setting: Abp.Mailing.Smtp.Password. Returning the original value...
System.FormatException: The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.
   at System.Convert.FromBase64String(String s)
   at Volo.Abp.Security.Encryption.StringEncryptionService.Decrypt(String cipherText)
   at Volo.Abp.Settings.SettingEncryptionService.Decrypt(SettingDefinition settingDefinition, String encryptedValue)
[INF] SendAppointmentEmailJob: delivered ... (successful send happens right after)
```

The send always **succeeds** — ABP's catch-block falls back to using the raw value, which is the correct plaintext password.

## Root cause
1. ABP's `Volo.Abp.Emailing` module defines the setting `Abp.Mailing.Smtp.Password` with `IsEncrypted = true` by default. See `Volo.Abp.Emailing.EmailSettings.Smtp.Password` in the ABP source.
2. Our `docker/appsettings.secrets.json` provides the value as **plaintext** (which is the right value for the SMTP server).
3. ABP's `SettingEncryptionService.Decrypt(...)` runs on every `_settingProvider.GetOrNullAsync(EmailSettingNames.Smtp.Password)` call.
4. Base-64 decoding plaintext throws `FormatException`.
5. ABP's catch block (`Volo.Abp.Settings.SettingManager` or similar) logs the warning and returns the raw value.
6. SMTP connection authenticates with the raw value → delivery works.

**Verification on 2026-05-14**: full `down -v` + `up -d --build` produces zero rows in `AbpSettings` table. The FormatException reappears immediately on the first send. So the issue is NOT a stale corrupted DB row — it's intrinsic to the plaintext-config + encrypted-setting-definition mismatch.

## Recommended fix
**Option A (cleanest)** — override the setting definition so this app considers the SMTP password non-encrypted:
1. In `CaseEvaluationSettingDefinitionProvider.cs`, find or add an entry for `Abp.Mailing.Smtp.Password`.
2. Either re-`Define` it (which replaces ABP's default) with `IsEncrypted = false`, or
3. Use ABP's `Add` with `null` cipher policy.
4. Same for `Abp.Mailing.Smtp.UserName` if it's also encrypted (less common).

**Option B (less invasive)** — pre-encrypt the SMTP password using ABP's settings-encryption key:
1. Generate the encrypted form via a one-time `IStringEncryptionService.Encrypt(plaintext)` call.
2. Put the encrypted form into `docker/appsettings.secrets.json`.
3. Document the encryption key (per-deployment) so it can be regenerated for staging/prod.
4. Costs: every deployment has a different encryption key; rotating creds requires rotation of the encrypted form too.

**Option C (suppress logging only)** — add a log filter for the specific warning. Doesn't reduce CPU waste of the unsuccessful decrypt attempts, but stops the noise. Use as a stopgap if A and B aren't shippable quickly.

Recommended: Option A.

## Related
- [[OBS-11]] — original observation of the log noise; this ticket is the proper bug.
- [[BUG-018]] — SMTP throttle work; can be implemented independently.
- The fix-session's earlier diagnosis ("encrypted password corrupted in DB") was based on the same symptom but pointed at the wrong cause. The real cause is the plaintext-in-config + encrypted-by-default-definition mismatch, not a stale row.
