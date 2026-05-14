---
id: OBS-8
title: Firm Name is a per-role conditional field (AA + DA only)
severity: observation
found: 2026-05-14
flow: external-user-registration
---

# OBS-8 — Firm Name visibility matrix

The Firm Name input on the AuthServer Register page appears for:
- User Type = Applicant Attorney → visible
- User Type = Defense Attorney → visible
- User Type = Patient → NOT visible
- User Type = Claim Examiner → NOT visible

This matches OLD parity: attorneys belong to a firm; CE belongs to an insurance carrier captured per-claim, not per-user. Documenting the field-visibility matrix so future test plans don't have to re-discover.

## Related
- [[BUG-012]] (Firm Name's missing `required` attribute when it does appear).
