# ADR-012: Appointment change-log redaction + diff-at-update email

**Status:** Accepted
**Date:** 2026-06-06
**Verified by:** code-inspect + unit tests

## Context

OLD kept a custom `spm.AppointmentChangeLogs` table (per-field old/new), showed a filterable
log, and emailed a per-field diff table to all stakeholders on every intake edit. OLD stored
AND emailed raw PHI (SSN, DOB, names) with no redaction. NEW already has ABP audit logging
(`[Audited]` on `Appointment` + the child intake entities) and an SSN-masking convention.
Group K (records G-02-01/02/03, G-10-05) replicates the OLD behaviour HIPAA-safely.

## Decision

1. **No custom audit table.** The change-log VIEW reads ABP `EntityChange` (the appointment +
   its injury / body-part / claim-examiner / primary-insurance children), exploded to per-field
   rows. The intake-changed EMAIL diffs the appointment-row fields in-memory at update time.
   One email per save = natural dedup, so OLD's `IsMailSent` latch is NOT ported.
2. **Server-side field allowlist (deny-by-default).** `AuditFieldPolicy` is the single source
   of truth; only listed non-sensitive fields reveal old/new values, everything else is masked
   ("updated", no values), and audit noise (ids / timestamps / concurrency) is dropped. Both
   the view and the email route through `AuditFieldDiff`, so neither can leak a value the other
   masks. Redaction happens before any DTO leaves the AppService and before the ETO is published.

## Allowlist (show old/new; everything else masked)

- `Appointment`: AppointmentDate, AppointmentTypeId, LocationId, DoctorAvailabilityId,
  PanelNumber, DueDate, AppointmentStatus, AppointmentLanguageId, NeedsInterpreter.
- `AppointmentInjuryDetail`: DateOfInjury.
- Masked (examples): SSN, DOB, names, address, phone, email, claim #, WCAB ADJ,
  body-part / injury text, examiner name -- and any field not on the list.

## Consequences

- The view and the email can never disagree on what is safe to show.
- No migration; the feature is additive on existing ABP audit data.
- The email diff covers the human-readable appointment-row fields (date/time, panel #, due
  date); FK fields (type / location / slot) appear in the view as ids -- name resolution is a
  later refinement.
- The change-log view omits the "Who" / username column: `GetEntityChangeListAsync` returns no
  username without an extra `AuditLog` join -- a refinement.
- The audit-query glue (`IAuditLogRepository`) is verified live, not unit-tested (ABP writes
  `EntityChange` rows itself; seeding them in the SQLite test base is impractical). The pure
  redaction policy, change-log builder, and intake diff ARE unit-tested.

## Alternatives Considered

- **Custom OLD-style table + `IsMailSent` latch** -- rejected: duplicates ABP audit infra,
  adds an entity + manager + migration; contradicts "use the NEW framework natively".
- **Show raw values to internal users only (gated reveal)** -- rejected for this slice: two
  render paths for marginal benefit; revisit if staff ask.
