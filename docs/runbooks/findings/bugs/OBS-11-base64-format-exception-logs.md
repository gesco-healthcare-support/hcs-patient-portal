---
id: OBS-11
title: Repeated System.FormatException (Base-64) in api logs during packet generation
severity: observation
found: 2026-05-14 during Workflow B approval flow
---

# OBS-11 — Base-64 FormatException log noise during packet generation

## Symptom
After Workflow B approval (Staff approves Patient-booked appointment), the api logs emit several:
```
System.FormatException: The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.
```
These appear interleaved with `GenerateAppointmentPacketJob` success logs (packets generate fine, status flips to 2, emails deliver). The exceptions look like they are caught somewhere and don't surface to the user, but they pollute the log stream.

## Suspected source
Likely in:
- `AppointmentPacketManager` token decoding (verification-code style tokens)
- `PacketAttachmentProvider` blob-key parsing
- `GenerateAppointmentPacketJob.GenerateKindAsync` token-context construction

## Functional impact
None visible — packets generate and emails deliver successfully. The exceptions are caught and logged but execution proceeds.

## Root cause confirmed 2026-05-14 (fresh-DB verification)

After a clean `docker compose down -v` + `up -d --build`, `AbpSettings` table has **zero rows**. The Base-64 FormatException **still appears** on every email send, paired with the warning:
```
[WRN] Failed to decrypt the setting: Abp.Mailing.Smtp.Password. Returning the original value...
System.FormatException: The input is not a valid Base-64 string...
[INF] SendAppointmentEmailJob: delivered (...) to <recipient>.
```

So this is NOT a corrupted DB row. The chain is:
1. ABP's `Volo.Abp.Emailing` module defines `Abp.Mailing.Smtp.Password` with `IsEncrypted = true`.
2. `SettingEncryptionService.Decrypt(...)` is called on every read.
3. The value comes from `IConfiguration` (i.e. `docker/appsettings.secrets.json`) as plaintext.
4. The decryption service tries to Base-64-decode plaintext → FormatException.
5. ABP catches it, logs the warning, **falls back to using the raw plaintext**, which is the correct password.
6. SMTP connection authenticates successfully.

**Net effect**: the email goes through. The log noise is harmless but not silenceable without a code change.

Promoting to [[BUG-020]] for the proper fix-session ticket.

## Related
- [[BUG-020]] — proper fix: override ABP's setting definition so `Abp.Mailing.Smtp.Password` is not marked encrypted in our deployment, OR provide an actually-encrypted value at a higher-priority config source.

## Related
- [[BUG-009]] (BusinessException auto-localization gap) — similar shape of "exception swallowed, generic log" but observable in different surface.
