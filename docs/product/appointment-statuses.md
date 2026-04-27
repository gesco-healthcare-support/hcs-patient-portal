[Home](../INDEX.md) > [Product Intent](./) > Appointment Statuses

# Appointment Statuses -- Intended Behavior

**Status:** draft -- Phase 2 T8, lookup cluster. Decision: **DROP the entity at MVP.** [Source: Adrian-confirmed 2026-04-24, Q-M]
**Last updated:** 2026-04-24
**Primary stakeholder:** None at MVP (entity is being dropped).

> Captures INTENDED behaviour for the `AppointmentStatus` lookup entity -- a table-based catalogue that parallels the `AppointmentStatusType` enum. Adrian has confirmed the entity is dropped at MVP: the enum plus existing en.json localization already provides display labels for the portal's status-relevant subset. Every claim source-tagged.

## Purpose

**At MVP: the `AppointmentStatus` entity is dropped.** The `AppointmentStatusType` enum (13 values at `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs:3-18`) is the appointment-lifecycle state machine. Display labels for statuses come from localization keys (`Enum:AppointmentStatusType.1` through `.13`) in `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`. The standalone `AppointmentStatus` entity (a CRUD table with no inbound FK and no UI consumer outside its own CRUD) is functionally inert. Removal is a follow-up code-build item; T8 captures the DROP decision so subsequent cross-cutting docs (T11 Appointment Lifecycle) do not re-document the zombie table. [Source: Adrian-confirmed 2026-04-24, Q-M]

## Personas and goals

N/A at MVP. The entity has no user-facing workflow; no persona engages with it.

## Intended workflow

No workflow at MVP -- the entity is being dropped. Follow-up build item: remove the entity, AppService, DTOs, permissions, Angular components, controller, and migration; no replacement is needed because the enum + en.json already covers what the portal displays.

The portal-relevant statuses are a subset of the 13-value enum. Per Adrian's 2026-04-24 lifecycle framing, the portal is the FIRST STEP in the appointment lifecycle: users book a new appointment or reschedule a re-evaluation; once approved, all parties are notified and data is handed off to the doctor's office for intake pre-fill. After that hand-off, the portal is done with the appointment unless there are subsequent changes / reschedule / re-evaluation. So the portal's statuses at runtime are the pre-approval slice (Pending, Approved, Rejected) plus the reschedule / cancel flow (RescheduleRequested, CancellationRequested, CancelledNoBill, RescheduledNoBill). Post-approval states (CheckedIn, CheckedOut, Billed, NoShow, CancelledLate) belong to downstream systems (Case Tracking, MRR AI, billing) and do not flow through the portal. [Source: Adrian-confirmed 2026-04-24, Q-M answer text]

This portal-scope clarification narrows the T2 / T11 lifecycle coverage. See Known Discrepancies for the tension with `appointments.md` (which carries the full 13-state lifecycle).

## Business rules and invariants

- **Enum drives the state machine.** `AppointmentStatusType` is the single source of truth for status values. All business logic reads the enum. [Source: verified via code review 2026-04-24]
- **Localization provides display labels.** `Enum:AppointmentStatusType.1..13` keys exist in `en.json` with human-readable names. Additional languages require adding the same keys to the target locale file. [Source: verified via reading `en.json`]
- **Portal scope is pre-approval + reschedule / cancel.** Post-approval states (CheckedIn, CheckedOut, Billed, NoShow, CancelledLate) are not used operationally in the portal. The enum keeps them for historical compatibility or for reading downstream-handed-back data; the portal does not transition into them. [Source: Adrian-confirmed 2026-04-24, Q-M answer text]
- **Entity is dropped at MVP.** No host or tenant admin manages status labels at MVP. [Source: Adrian-confirmed 2026-04-24, Q-M]

## Integration points

- **None at MVP.** No inbound FK from any other entity. Enum consumers: `Appointment.AppointmentStatus` (int column).
- **Cross-references.** `docs/business-domain/APPOINTMENT-LIFECYCLE.md` (the ratified lifecycle doc) + the T11 cross-cutting doc (forthcoming). The T11 doc should narrow to the portal-scope subset; the full 13-state diagram in `docs/business-domain/APPOINTMENT-LIFECYCLE.md` covers the broader system, not just the portal.
- **Research.** `docs/issues/research/Q-02.md` carries the prior enum-vs-table analysis and recommended the same drop decision.

## Edge cases and error behaviors

No behavior at MVP. The follow-up removal build item must:

- Delete the entity class, the AppService, the DTOs, the Angular components, the HTTP controller, the permissions registration, and the migration.
- Regenerate the Angular proxy so the frontend no longer references the entity's endpoints.
- Verify no existing code path expects the table (confirmed zero consumers as of 2026-04-24).

## Success criteria

- Follow-up build item removes the `AppointmentStatus` entity and all its surfaces from code.
- No user-visible change (the table was never consumed).
- T11 Appointment Lifecycle cross-cutting doc narrows the portal's operational statuses to the pre-approval + reschedule / cancel subset; the full 13-state diagram stays in `docs/business-domain/APPOINTMENT-LIFECYCLE.md` as a broader-system reference.

## Known discrepancies with implementation

- `[observed, not authoritative]` Two parallel representations of appointment status exist (enum + table) with no linkage. `Appointment.AppointmentStatus` is the enum; the `AppointmentStatus` table has no FK consumer. The DROP decision resolves this.
- `[observed, not authoritative]` Full CRUD AppService + per-action permissions (`CaseEvaluation.AppointmentStatuses.Create / Edit / Delete`) + Angular list + detail modal exist. The drop removes all of them.
- `[observed, not authoritative]` No seeding logic synchronises the 13 enum values with table rows. The enum + localization path already covers display labels; no sync is needed.
- `[observed, not authoritative]` The feature CLAUDE.md (`src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/CLAUDE.md`) explicitly flags the "Confusing naming -- AppointmentStatus entity vs AppointmentStatusType enum" gotcha. The drop decision eliminates the confusion.
- `[observed, not authoritative]` `appointments.md` (T2) carries the full 13-state lifecycle in its Intended Workflow / Business Rules sections. Adrian's 2026-04-24 Q-M framing narrows the portal's operational scope to pre-approval + reschedule / cancel. This is a T11 cross-cutting concern -- the T2 content is NOT modified in T8; T11 should narrow the appointment lifecycle to the portal-scope subset (captured as an outstanding cross-cutting concern here and in the T8 change-log entry).

## Outstanding questions

No manager questions arising from T8 on AppointmentStatuses. The drop decision is Adrian-confirmed and consistent with the prior Q-02 research recommendation. The T11 cross-cutting lifecycle doc will re-scope `appointments.md` to match the portal's actual operational boundary (pre-approval + reschedule / cancel); this is noted in the T8 change-log entry for T11 to pick up.
