[Home](../INDEX.md) > [Product Intent](./) > Locations

# Locations -- Intended Behavior

**Status:** draft -- Phase 2 T8, lookup cluster
**Last updated:** 2026-04-24
**Primary stakeholder:** Host admin (initial seeding at tenant onboarding) + doctor's admin (ongoing edits) [Source: Adrian-confirmed 2026-04-24, Q-A + Q-A3]

> Captures INTENDED behaviour for the `Location` lookup entity -- physical offices or clinics where a medical examiner sees patients. **`Location` is tenant-specific at intent**; the current code has it host-scoped. This is the most substantial intent-vs-code gap flagged in T8. Every claim source-tagged.

## Purpose

`Location` represents a physical office, clinic, or facility where a medical examiner sees patients for an IME. Each record carries a Name, full address (street, city, zip), optional State reference, optional default AppointmentTypeId, ParkingFee, and an active/inactive flag. **Each tenant has its own list of locations.** A doctor at Tenant A cannot see or use a location from Tenant B; users logging in under a tenant see only that tenant's locations. Gesco's host admin creates the tenant's initial list at onboarding, and the doctor's admin maintains the list from then on. [Source: Adrian-confirmed 2026-04-24, Q-A ("Office locations should be tenant specific, each doctor/examiner will have different office locations and users logging in under a tenant should only see the office locations for that tenant") + Q-A3 (host initial + practice ongoing)]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` for persona definitions.

- **Host admin (Gesco).** Creates the tenant's initial Locations at onboarding (using address information the examiner provides). Once the tenant is running, host admin no longer maintains the list unless there is a problem. [Source: Adrian-confirmed 2026-04-24, Q-A3]
- **Doctor's admin (tenant admin).** Adds, edits, and deactivates Locations for their own tenant on an ongoing basis. Cannot see or modify other tenants' Locations. [Source: Adrian-confirmed 2026-04-24, Q-A + Q-A3]
- **Bookers (patient, attorneys, claim examiner).** Pick a Location on the booking form before seeing availability. Single-location tenants auto-select and collapse the field; multi-location tenants show the picker. [Source: Adrian-confirmed 2026-04-22 via T3 DoctorAvailabilities + T2 Appointments]

## Intended workflow

### Tenant onboarding (host admin)

When Gesco provisions a new tenant (one medical examiner's practice), the host admin creates the tenant's initial Locations based on information the examiner provides. [Source: Adrian-confirmed 2026-04-24, Q-A3]

### Ongoing list management (doctor's admin)

The doctor's admin can add a new Location (opening an office), edit an existing Location (correct an address, update parking fee), or deactivate (`IsActive = false`) a Location that closes. All edits are scoped to the practice's own list. Host admin typically does not touch the list after onboarding. [Source: Adrian-confirmed 2026-04-24, Q-A3]

### Per-doctor coverage (within tenant)

Each doctor's `DoctorLocation` M2M records which of the tenant's Locations they work at. Joint authority between host admin and doctor's admin, as confirmed in T4 Doctors. Because Locations are now tenant-specific, the M2M naturally falls within the tenant. [Source: Adrian-confirmed 2026-04-22 via T4 + derivable from tenant-specific Location scope]

### Booking-side consumption

Booker picks a Location; availability filters to slots at that Location for doctors in that tenant. Single-location tenants auto-select. [Source: Adrian-confirmed 2026-04-22 via T3 DoctorAvailabilities]

### Notification content (pending FEAT-05)

The booking-confirmation email should include the Location's address and -- at minimum -- the parking-fee (if set) so the patient can plan. Notification format is part of the FEAT-05 build; exact field-by-field content awaits the escalations/Item 2 manager answer. [Source: inferred from `appointments.md` Packet / notification intent; notification system not yet implemented in code]

## Business rules and invariants

- **Tenant-specific scoping.** Every Location belongs to exactly one tenant. Users see only their tenant's Locations. No cross-tenant visibility. [Source: Adrian-confirmed 2026-04-24, Q-A]
- **Onboarding-then-self-managed authority.** Host admin seeds at tenant provisioning; doctor's admin maintains thereafter. [Source: Adrian-confirmed 2026-04-24, Q-A3]
- **Required on Appointment and on DoctorAvailability.** Appointment MUST have a Location (NoAction FK). Availability slot MUST have a Location. [Source: verified via code review 2026-04-24]
- **Per-doctor coverage required.** A doctor accepts bookings only at Locations in their `DoctorLocation` list. [Source: inferred from T4 joint-coverage + T3 availability-filter logic]

## Integration points

Inbound FKs (currently observed):

- `DoctorAvailability.LocationId` (NoAction) -- required
- `Appointment.LocationId` (NoAction) -- required
- `DoctorLocation.LocationId` (M2M join to `Doctor`; Cascade on both)

Outbound FKs:

- `Location.StateId` (SetNull) -- optional State on the address
- `Location.AppointmentTypeId` (SetNull) -- optional default appointment type

Once `Location` is tenant-scoped (via `IMultiTenant`), the tenant filter applies automatically via ABP's data filter; no manual `WHERE TenantId = X` is needed in queries.

## Edge cases and error behaviors

- **Deleting a Location in use.** NoAction on `Appointment` + `DoctorAvailability` blocks deletion while either references the Location. Intent: preserve history; deactivate instead of delete. [Source: Adrian best-guess -- NEEDS CONFIRMATION]
- **Location address change after bookings exist.** Editing Name / Address / ParkingFee updates the record for every referring row (by reference). The T7 universal post-submit lock rule applies to form-captured appointment data; the Location row is reference data, not per-appointment form data. Intent: historical accuracy is best-effort; if the practice updates an address, historical bookings reflect the new address on read. [Source: Adrian best-guess -- NEEDS CONFIRMATION]
- **Doctor switches tenants.** Doctor is `IMultiTenant`; moving a doctor between tenants means their `DoctorLocation` links break (Cascade delete). Intent: [UNKNOWN -- queued for Adrian; likely a manual re-setup on the new tenant since Locations are also tenant-specific.]
- **ParkingFee display on the booking confirmation.** Whether the fee appears on the Packet / notification email or only in-portal is [UNKNOWN -- queued for Adrian]. [Source: Adrian best-guess -- NEEDS CONFIRMATION]

## Success criteria

- A doctor's admin can add a Location for their tenant; it appears in the booking flow for their doctors immediately; it does not appear for any other tenant.
- A booker picking a doctor sees only Locations attached to that doctor (and within that tenant).
- Deletion is blocked on a Location referenced by any appointment or availability.
- Inactive Locations are hidden from new bookings without breaking historical references.
- The Location's address shows up in booking-confirmation notifications once FEAT-05 ships.

## Known discrepancies with implementation

- `[observed, not authoritative]` **`Location` is host-scoped in code** (no `IMultiTenant`; configured under `IsHostDatabase()` guard). **Intent is tenant-specific.** This is a substantial schema and runtime gap: the entity needs to implement `IMultiTenant`, the DbContext config needs to move out of the `IsHostDatabase()` guard, and existing rows need tenant assignment during migration. The root CLAUDE.md currently states Location is host-scoped (in both "3 entities are host-only" and "intentionally shared across all tenants" sections) -- that content also diverges from the T8 intent and will need updating in a future turn (outside T8 scope).
- `[observed, not authoritative]` The OUTSTANDING-QUESTIONS.md glossary line "shared reference data (office locations ...) is the same across all practices" is incorrect for Locations and should be corrected in a later turn. The glossary is not updated in T8; see the T8 change-log entry.
- `[observed, not authoritative]` `Location.AppointmentTypeId` (optional default AppointmentType per Location) has no intent captured; Adrian can confirm whether this is an admin convenience, a booking-form pre-fill, or dead field on review.
- `[observed, not authoritative]` `ParkingFee` is a decimal field; its display surface (admin-only, in-portal confirmation, notification email, or Packet) is not specified in code or earlier intent docs.
- `[observed, not authoritative]` Soft-delete (`ISoftDelete`) not implemented; deletions are hard, subject to the FK NoAction guard.
- `[observed, not authoritative]` No notification / email sender in the Application project; the Location's address embedding in notifications awaits FEAT-05.

## Outstanding questions

- Doctor inter-tenant moves and `DoctorLocation` handling ([UNKNOWN -- queued for Adrian]).
- Default-AppointmentType-on-Location field purpose ([UNKNOWN -- queued for Adrian]).
- Historical-accuracy on Location address edits after bookings ([UNKNOWN -- queued for Adrian]).
- ParkingFee display surface ([UNKNOWN -- queued for Adrian]).
