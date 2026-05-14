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

## To do (fix session)
Track down the throw site, fix the input it's trying to decode (probably a non-base64 string being passed where base64 is expected, OR a try/catch that should validate before parsing).

## Related
- [[BUG-009]] (BusinessException auto-localization gap) — similar shape of "exception swallowed, generic log" but observable in different surface.
