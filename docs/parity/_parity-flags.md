---
title: Parity flags -- intentional deviations from OLD
status: living
---

# Parity flags

Per `CLAUDE.md` "Bug and deviation policy", every intentional deviation
from the OLD app at `P:\PatientPortalOld` is recorded here. Each entry
states what differs, why, and the resolution status.

When a deviation is later reversed or accepted as permanent, update the
row's status -- do not delete the entry. The audit trail is the value.

| ID | Area | OLD source | Deviation | Reason | Status |
|---|---|---|---|---|---|
| PF-001 | SSN visibility | `appointment-add.component.html:168`, `appointment-view.component.html:70` (renders full SSN to every viewer as plain text) | NEW server-side redaction returns last-4-only to external attorneys / claim examiners who do not own the record. Internal staff (Clinic Staff / Staff Supervisor / IT Admin) and record owners (`Patient.IdentityUserId == CurrentUser.Id`) continue to see the full value. | OLD's plain-text-SSN-to-everyone was almost certainly an oversight, not a designed feature; HIPAA Minimum Necessary + NIST SP 800-122 both favor role-scoped access. Per CLAUDE.md "Clear bug -- fix it" rule, harden rather than replicate. | resolved (F4-01, 2026-05-25) |
