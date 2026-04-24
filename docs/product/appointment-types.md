[Home](../INDEX.md) > [Product Intent](./) > Appointment Types

# Appointment Types -- Intended Behavior

**Status:** draft -- Phase 2 T8, lookup cluster. Type-derived fields queued for manager (Q27).
**Last updated:** 2026-04-24
**Primary stakeholder:** Host admin (global list) + tenant admin (per-tenant hide/show) [Source: Adrian-confirmed 2026-04-24]

> Captures INTENDED behaviour for the `AppointmentType` lookup entity -- the catalogue of IME (Independent Medical Examination) types. Every claim is source-tagged.

## Purpose

`AppointmentType` represents a kind of medical examination a medical examiner can perform (e.g., QME orthopedic, AME neurological, IME general). Each record has a Name and an optional Description. The catalogue is a common global list maintained by Gesco's host admin; each tenant can hide types it does not offer from its own users' dropdowns. **Picking a type on the booking form drives multiple other fields on the appointment (e.g., default duration, pricing).** The exact set of fields and the rule per type are [UNKNOWN -- queued for manager (Q27)]. [Source: Adrian-confirmed 2026-04-24, Q-A + Q-A2 + Q-I; derivation specifics queued via Q27]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` for persona definitions.

- **Host admin (Gesco).** Owns the global AppointmentType list; adds, edits, deactivates entries. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION; consistent with Q-A2 implying tenants cannot ADD entries globally]
- **Tenant admin (doctor's admin).** Hides types the practice does not offer from its users' dropdowns. Cannot add to the global list. Separately, picks which types each doctor in the practice performs via `DoctorAppointmentType` M2M (the coverage layer, confirmed in T4 Doctors). [Source: Adrian-confirmed 2026-04-24, Q-A2 for hide/show; T4 for coverage M2M]
- **Bookers.** Pick an appointment type on the booking form before seeing availability. The selected type filters which doctors' slots appear AND pre-fills other fields on the appointment (see Business Rules). [Source: Adrian-confirmed via T2 + Q-I]

## Intended workflow

### At install

Host admin seeds the initial AppointmentType list manually (no automatic `DataSeedContributor`). [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION]

### Per-tenant hide/show

Tenant admin hides types not offered by that practice. Hidden types do not appear in the booker's type dropdown for users of that tenant. [Source: Adrian-confirmed 2026-04-24, Q-A2]

### Per-doctor coverage (within tenant)

Each doctor's `DoctorAppointmentType` M2M records which types they are qualified to perform. Joint authority between host admin and doctor's admin, per T4. [Source: Adrian-confirmed 2026-04-22 via T4 Doctors]

### Per-slot tagging (doctor's admin)

Each availability slot may be tagged with an AppointmentType; the booker's selected type filters visible slots. [Source: Adrian-confirmed via T3]

### Booking-side consumption

Booker picks an AppointmentType before seeing availability. The availability list shows only slots of that type from doctors covering that type at the selected Location. On submission, the type's pre-fills are applied to the appointment record. [Source: Adrian-confirmed via T2 + Q-I]

### Type-derived fields (Q-I -> Q27 for specifics)

When the booker selects a type, the appointment record is pre-filled with type-driven defaults. Adrian confirmed multiple fields are driven by type but did not specify which -- routed to the manager as Q27 in OUTSTANDING-QUESTIONS.md. Candidate fields under consideration: default slot duration, billing rate, required prep documents, default prep instructions. [Source: Adrian-confirmed 2026-04-24, Q-I; specifics are [UNKNOWN -- queued for manager, Q27]]

## Business rules and invariants

- **Common global base.** All tenants share one master list; only Gesco's host admin edits it. [Source: Adrian-confirmed 2026-04-24]
- **Per-tenant hide/show.** Each tenant hides types it does not offer. Cannot add types. [Source: Adrian-confirmed 2026-04-24, Q-A2]
- **Required on Appointment.** Every Appointment MUST have an AppointmentType; FK is NoAction on delete (deletion blocked while any appointment references the type). [Source: verified via code review 2026-04-24]
- **Required on the booking form.** Booker cannot submit without picking a type. [Source: Adrian-confirmed 2026-04-22 via T2]
- **Type drives multiple fields.** Picking a type sets defaults on the appointment for multiple fields. Exact fields [UNKNOWN -- queued for manager, Q27]. [Source: Adrian-confirmed 2026-04-24, Q-I]
- **Name uniqueness.** Intended unique; no DB enforcement. [Source: Adrian best-guess -- NEEDS CONFIRMATION]

## Integration points

Inbound FKs:

- `Appointment.AppointmentTypeId` (NoAction) -- required
- `DoctorAvailability.AppointmentTypeId` (SetNull) -- optional type tag per slot
- `Location.AppointmentTypeId` (SetNull) -- optional default type for a Location
- `DoctorAppointmentType.AppointmentTypeId` (M2M join to `Doctor`; Cascade)

Booker form: type dropdown (tenant-filtered by hide/show) displayed before availability.

Notifications / Packet: the appointment's type name appears on the Packet sent to the doctor's office for intake pre-fill. Notification-template-by-type is deferred post-MVP until FEAT-05 ships. [Source: inferred from `appointments.md` Packet intent + Q-I answer]

## Edge cases and error behaviors

- **Deleting a type in use.** NoAction on `Appointment` (deletion blocked). SetNull on `Location` and `DoctorAvailability` (references silently cleared -- inconsistent with the Appointment block; minor gap). [Source: observed, not authoritative; intent is that NoAction should apply uniformly once in-use, best-guess pending Adrian confirmation]
- **Deactivating vs deleting.** No `IsActive` flag on `AppointmentType`. Hiding a type from a tenant happens via the per-tenant hide/show flag (intent, not in code yet). Hiding globally would require an `IsActive` flag or outright deletion. Intent: add `IsActive` post-MVP or simply delete rarely; MVP uses hide/show per tenant for day-to-day hiding. [Source: Adrian best-guess -- NEEDS CONFIRMATION]
- **Type renaming.** Editing Name changes the label for every existing appointment and availability by reference. `AppointmentTypeId` is stable; only the label changes. Acceptable for minor renames (clarification); riskier for category shifts.
- **Description edits.** Description field is captured; its audience (admin-facing, booker-facing, Packet-included) is [UNKNOWN -- queued for Adrian on review]. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION; code observation: not rendered in any Angular view]

## Success criteria

- Host admin can add a global AppointmentType; all tenants see it (subject to their own hide/show choices).
- Tenant admin can hide a type not offered by their practice from their dropdowns.
- Doctor's admin can add a doctor to a type via `DoctorAppointmentType`.
- Booker picking a type sees only doctors covering that type at the selected Location; the appointment record is pre-filled with type-driven defaults on submit.
- Deletion blocked on types with any appointment (guaranteed by NoAction FK).

## Known discrepancies with implementation

- `[observed, not authoritative]` No per-tenant visibility flag exists -- per-tenant hide/show is intent-only; a new `TenantAppointmentTypeVisibility` table (or equivalent filter mechanism) needs to be added.
- `[observed, not authoritative]` No type-driven pre-fill logic exists in code. Picking a type does not currently populate any other field on the appointment (duration, pricing, etc.).
- `[observed, not authoritative]` No `IsActive` flag. Deactivation requires M2M removal or deletion.
- `[observed, not authoritative]` `DoctorAppointmentType.OnDelete = Cascade` on both sides. Deleting the type cascades through the M2M; deleting the doctor clears their M2M entries.
- `[observed, not authoritative]` FK behaviour inconsistent across referring entities: NoAction on `Appointment`, SetNull on `Location` and `DoctorAvailability`.
- `[observed, not authoritative]` Description field is freeform (max 200); no usage guidance at entity level; not rendered in Angular.
- `[observed, not authoritative]` No test coverage for `AppointmentTypesAppService` (FEAT-07).
- `[observed, not authoritative]` Entity config appears in both `CaseEvaluationDbContext` (host-database guarded) and `CaseEvaluationTenantDbContext`, a refactoring residue; host-scope intent is authoritative.

## Outstanding questions

- Full set of Type-driven fields and per-type defaults: Q27 in OUTSTANDING-QUESTIONS.md.
- Description field audience ([UNKNOWN -- queued for Adrian]).
- `IsActive` flag intent at MVP ([UNKNOWN -- queued for Adrian]).
- FK-behaviour inconsistency resolution ([UNKNOWN -- queued for Adrian]).
