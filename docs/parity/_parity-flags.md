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
| PF-002 | Defense Attorney section toggle | OLD `patientappointment-portal/.../appointment-add.component.html:724-781` had a DA "Include" toggle (`isActive`, default on) with NO confirmation modal | NEW re-adds the DA toggle (matching OLD) for every booker except a DA-role booker, AND adds a toggle-off confirmation modal "Is a Defense Attorney assigned to this case?" (Yes = assigned, keep; No = none, remove). The modal is a NEW addition not present in OLD; the toggle restoration reverses the interim NEW-only decision D2 (DA-mandatory, no toggle). | Toggle restoration is OLD parity; the confirmation modal is a deliberate NEW UX addition mirroring the Applicant Attorney self-represented modal (`ed12db0`). Adrian directive 2026-05-29. | needs-test (F4, 2026-05-29) |
| PF-003 | Document upload timing | OLD `AppointmentDocumentDomain.cs:90-107` (UpdateValidation) blocks document upload until the appointment is Approved/RescheduleRequested | NEW also allows upload at request time (status Pending) on all gated paths (package-doc, joint-declaration, anonymous verification-code), and creates the package-document rows at submission (`AppointmentSubmittedEto`) instead of at approval. | Users must supply documents to GET an appointment approved; blocking upload until approval is backwards. The due-date gate and verification-code email timing are unchanged. Adrian directive 2026-05-29. | needs-test (F3, 2026-05-29) |
