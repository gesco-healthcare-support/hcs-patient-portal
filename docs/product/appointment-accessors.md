[Home](../INDEX.md) > [Product Intent](./) > Appointment Accessors

# Appointment Accessors -- Intended Behavior

**Status:** draft -- Phase 2 T6, interview in progress
**Last updated:** 2026-04-24
**Primary stakeholder:** [UNKNOWN -- pending T6 interview]

> Captures INTENDED behaviour for the `AppointmentAccessor` entity -- per-appointment access grants specifying which users can View or Edit a given appointment. Cross-reference the sibling files [applicant-attorneys.md](applicant-attorneys.md) (attorney record) and [appointment-applicant-attorneys.md](appointment-applicant-attorneys.md) (semantic link, which may overlap with access-grants).

## Purpose

**At MVP, portal access to an appointment is strictly tied to formal legal-party membership on the case.** No ad-hoc access grants -- a user sees or edits an appointment only if they are one of the formal parties (attorney, patient, insurance, claim adjustor, doctor's office) named on the booking. Paralegals, assistants, or other non-party helpers do not get their own access; if they need portal visibility, they log in using the party's registered email (e.g., an attorney's paralegal uses the attorney's login). [Source: Adrian-confirmed 2026-04-24]

Rationale: strict access minimises potential for data or legal leakage. Broader access models (individual access grants, delegation, sub-users) are explicitly post-MVP and can be added later if operational reality demands it.

Consequence for the `AppointmentAccessor` entity: at MVP it is effectively **redundant with `AppointmentApplicantAttorney`** (and the equivalent future link for defense attorneys, per `applicant-attorneys.md`). Portal visibility for an attorney is driven by the legal-party join, not by a separate access-grant record. The `AccessType` enum (View / Edit) and the entity itself remain available in the schema for post-MVP expansion but are not actively used as a separate access mechanism at MVP. [Source: Adrian-confirmed 2026-04-24]

## Personas and goals

Cross-reference: see [appointments.md](appointments.md) and [applicant-attorneys.md](applicant-attorneys.md).

## Intended workflow

### Granting access

[UNKNOWN -- queued for interview: when is an `AppointmentAccessor` record created? Candidate triggers: (a) automatically when a booker / attorney is linked to an appointment (implicit grant), (b) deliberately by the doctor's admin or another authority granting view/edit on a case-by-case basis, (c) both. The AccessType enum (View / Edit) suggests deliberate grants, but no Angular UI exists for this today.]

### Revoking access

[UNKNOWN -- queued for interview: can access be revoked, and by whom?]

### Relationship to AppointmentApplicantAttorney

[UNKNOWN -- core interview question: does the join entity already encode the access grant, making `AppointmentAccessor` redundant for attorneys? Or are they two separate concerns (legal-party-on-case vs. portal-view-permission) that just happen to correlate?]

## Business rules and invariants

### Confirmed

- **No ad-hoc access at MVP.** Portal access to an appointment is strictly driven by legal-party membership. No "grant this user access to this appointment" action at MVP. [Source: Adrian-confirmed 2026-04-24]
- **Staff use the party's registered credentials.** If an attorney's paralegal, an insurance company's assistant, or another non-party helper needs portal access, they log in using the party's registered email. No sub-user / delegation model at MVP. [Source: Adrian-confirmed 2026-04-24]
- **AppointmentAccessor is redundant with the legal-party join at MVP.** The `AccessType` enum (View / Edit) and the entity itself are preserved for post-MVP expansion but are not an active access mechanism at MVP. [Source: Adrian-confirmed 2026-04-24]

## Integration points

- **Appointments** -- `EfCoreAppointmentRepository` queries the AppointmentAccessor table to filter which appointments a given external user can see.
- **AppointmentApplicantAttorney** -- relationship / overlap is [UNKNOWN].

## Edge cases and error behaviors

[UNKNOWN -- to resolve during interview.]

## Success criteria

First-pass sketch:

- No portal UI exists at MVP for granting ad-hoc access to an appointment (no "share with user X" button, no access-management screen).
- If FEAT-09 / multi-tenant filter is fixed correctly (per `patients.md` and the Appointments repository work), access visibility is strictly tenant-scoped.
- The `AppointmentAccessor` table remains in the schema (do not drop) so that a post-MVP ACL feature can build on it without a migration.

## Known discrepancies with implementation

- `[observed, not authoritative]` No Angular UI exists for managing accessors; they are created programmatically during the appointment booking flow. Intent on whether a manual "share this appointment with user X" UI is needed is [UNKNOWN].
- `[observed, not authoritative]` `AccessType` enum uses non-sequential values (View=23, Edit=24). Suggests the values come from an external system or legacy database.
- `[observed, not authoritative]` Permissions defined in `CaseEvaluationPermissions.AppointmentAccessors.*` are NOT enforced on the AppService -- it uses generic `[Authorize]` only. Intent on who should be allowed to manage accessors is [UNKNOWN].
- `[observed, not authoritative]` Uses `FullAuditedEntity` (not `FullAuditedAggregateRoot`) -- unusual for this codebase. May indicate it's meant as a pure relational-table-like record, not a domain-rich entity.
- `[observed, not authoritative]` No tests exist for AppointmentAccessors.

## Outstanding questions

[UNKNOWN entries roll up to OUTSTANDING-QUESTIONS.md.]

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
