---
id: IP4
title: Harden Locations CRUD + validation for internal staff
type: enhancement
components: [angular/src/app/locations/location/, src/HealthcareSupport.CaseEvaluation.Domain/Locations/, src/HealthcareSupport.CaseEvaluation.Application/Locations/, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/]
related_known_bugs: [OBS-26, IR1]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Locations CRUD already works end to end (richest of the four internal lookup pages), but
it lacks the data-integrity guards a clinic-staff-facing master-data screen needs. Add
standard industry CRUD validation: duplicate-name guard, ParkingFee non-negative, ZipCode
format, and a friendly pre-delete path (soft-delete; block/inform when referenced instead
of a raw DB FK error). Clarify the AppointmentTypeId-per-location field (storage-only unless
a driver is confirmed). Soft-Delete grant for Staff Supervisor comes via IR1, not here.

## Current behavior (from investigation)
- Domain manager performs only not-blank + length checks; NO duplicate-name guard, NO
  ParkingFee range check, NO ZipCode format check
  (src/HealthcareSupport.CaseEvaluation.Domain/Locations/LocationManager.cs:22-51 -- both
  CreateAsync and UpdateAsync call Check.NotNullOrWhiteSpace(name) + Check.Length on
  name/address/city/zip only).
- Client form requires Name (maxLength 50) + ParkingFee, everything else maxLength only;
  no zip-format or numeric-range validators
  (angular/src/app/locations/location/services/location-detail.abstract.service.ts:41-55).
- DTO mirrors with DataAnnotations: Name [Required]+StringLength, ParkingFee is a plain
  decimal (NOT [Range]), no zip regex
  (src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/LocationCreateDto.cs).
- Delete is unsafe by reference: DoctorAvailability.LocationId and Appointment.LocationId
  are NoAction FKs, so deleting a referenced Location throws a raw DB exception with no UI
  preview (Locations/CLAUDE.md "FK asymmetry"; findings line 499). DoctorLocation.LocationId
  is Cascade (join rows auto-removed).
- AppointmentTypeId is a single optional FK that no business logic consumes beyond storage;
  intent unknown (Locations/CLAUDE.md "AppointmentTypeId semantics are undocumented").
- Role gate is ALREADY correct for write: Staff Supervisor holds Locations.Create+Edit
  (InternalUserRoleDataSeedContributor.cs:308-309); IT Admin full incl Delete (AllEntities
  line 147); Clinic Staff Default read only. So IP4 is robustness, not access.

## Relevant code locations
- src/HealthcareSupport.CaseEvaluation.Domain/Locations/LocationManager.cs:22-51 (add
  duplicate-name + ParkingFee + ZipCode guards; add pre-delete reference check or soft-delete)
- src/HealthcareSupport.CaseEvaluation.Domain/Locations/Location.cs (already FullAudited per
  tenant-entity standard; confirm ISoftDelete behavior is in effect for soft-delete path)
- src/HealthcareSupport.CaseEvaluation.Application/Locations/LocationsAppService.cs
  (Create line 88, Edit line 95, Delete line 82, DeleteByIds 102, DeleteAll 108 -- route
  delete through a manager pre-check)
- src/HealthcareSupport.CaseEvaluation.Application.Contracts/Locations/LocationCreateDto.cs
  (add [Range] on ParkingFee, [RegularExpression] on ZipCode; mirror on the Update DTO)
- angular/src/app/locations/location/services/location-detail.abstract.service.ts:41-55
  (add Validators.min(0) on parkingFee, Validators.pattern on zipCode; abstract file --
  the ABP-Suite base, regen-safe)
- angular/src/app/locations/location/components/location-detail.component.html (surface the
  new validation messages) and location.component.html (Delete line 276 -- inline conflict
  message)
- src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Locations/LocationController.cs
  (no change expected; thin delegator)

## Phase 3 cross-reference
- OBS-26 (slot-gen location-scoped conflict): slot conflict detection is location-scoped, so
  the pre-delete reference check must count DoctorAvailability slots, not just appointments --
  reuse the same location scoping while here so the "is this location in use" query is
  consistent with how slots are bound to locations.
- IR1 (Staff Supervisor soft-Delete grant): the soft-Delete permission for Staff Supervisor
  on tenant entities is granted in IR1; IP4 builds the safe-delete behavior that grant exposes.
  Coordinate so Supervisor delete lands together with the friendly pre-delete handling, not
  before it (granting delete without the guard re-exposes the raw FK error).
- IP1/IP2/IP5 (sibling lookup pages): NOT bundled into IP4. They are role-grant changes;
  IP4 is validation hardening. Keep separate, but the duplicate-name + format-validation
  pattern established here is the template those items can copy if they later harden too.

## Research findings
- Internal patterns / prior art:
  - LocationManager.cs:22-51 already uses ABP Check.* guards; the duplicate-name check fits
    the same style -- query repository, throw BusinessException on conflict (consistent with
    how other managers raise domain errors).
  - master-data-crud-design.md Exception 3 (findings line 508): OLD app had Location delete
    commented out and recommends NOT deleting locations with existing appointments / prefer
    soft-delete -- directly supports the decided friendly-pre-delete direction.
  - Locations/CLAUDE.md enumerates the exact gaps (no duplicate-name guard, NoAction FKs
    block delete with no UI preview, AppointmentTypeId unused, ParkingFee required no default).
  - FK-edge tests DeleteAsync_WhenLocationReferencedBy* are currently Skipped (SQLite ignores
    FK enforcement); they encode the target behavior and should be revisited as the safe
    delete path lands.
  - Every tenant entity is FullAudited/ISoftDelete per the locked ROLES decision (all deletes
    SOFT). Location delete should set IsDeleted, not hard-remove; the reference check decides
    whether delete is even offered.
- External docs:
  - ABP BusinessException + localized error codes are the idiomatic way to surface a friendly
    duplicate/in-use message to the client (preferred over throwing a generic exception that
    the UI cannot map). Cite ABP "Exception Handling" docs when implementing.
  - Angular reactive-forms Validators.min and Validators.pattern (official forms API) cover
    the client-side ParkingFee and ZipCode mirrors; the server remains authoritative.

## Approaches considered (with tradeoffs)
1. UI-only validation (rejected): add min/pattern validators to the Angular form only.
   Fast, but the VALIDATION decision mandates server-side enforcement for integrity rules;
   a direct API call would bypass UI guards. Duplicate-name and in-use-delete are integrity
   rules, not affordances.
2. Hard-delete with a friendly pre-check (rejected): pre-check references, then hard-delete
   when clear. Conflicts with the locked ROLES decision -- ALL tenant deletes stay SOFT for
   the PHI audit trail. No destructive hard-delete is built.
3. Soft-delete + manager-enforced guards mirrored in UI (CHOSEN): duplicate-name guard,
   ParkingFee non-negative, ZipCode format all enforced in LocationManager/DTO and mirrored
   in the Angular form; delete is soft and blocked/informed when DoctorAvailability or
   Appointment references exist. Matches the VALIDATION standard (server + UI), the ROLES
   soft-delete standard, and master-data-crud-design Exception 3.

## Decision (locked 2026-06-03)
Standard industry CRUD validation, nothing fancy:
- Duplicate-name guard in LocationManager (server-enforced; UI mirrors with a friendly message).
- ParkingFee non-negative (server [Range] + Validators.min(0)).
- ZipCode format validation (server regex + Validators.pattern; US 5 or 5+4).
- Friendly pre-delete handling: SOFT delete; when the Location is referenced by appointments
  or availability slots, block and inform the user instead of letting the DB FK throw.
- AppointmentTypeId stays storage-only -- no behavior wired unless a driver is confirmed.
- Staff Supervisor soft-Delete is granted via IR1 (not in this item). Create+Edit already
  exist for Supervisor; no role change is made here.

## Implementation outline (no code)
1. Domain (server, authoritative): in LocationManager.CreateAsync/UpdateAsync add a
   duplicate-name check (repository lookup, case-insensitive, exclude self on update) -> throw
   localized BusinessException. Add ParkingFee >= 0 and ZipCode format checks alongside the
   existing Check.* calls. Add a DeleteAsync pre-check (or a CanDelete query) that counts
   referencing DoctorAvailability + Appointment rows (location-scoped, per OBS-26) and throws
   a friendly BusinessException when non-zero; otherwise soft-delete.
2. AppService: route Delete/DeleteByIds/DeleteAll through the manager pre-check so bulk delete
   honors the same guard. No new endpoints.
3. DTO: add [Range(0, ...)] to ParkingFee and [RegularExpression] to ZipCode on the Create
   and Update DTOs (Application.Contracts).
4. Localization: add error-message keys to Domain.Shared en.json BEFORE any L()/abpLocalization
   reference (duplicate-name, parking-fee-negative, zip-format, location-in-use).
5. Angular (UI mirror, abstract file): add Validators.min(0) on parkingFee and
   Validators.pattern on zipCode in location-detail.abstract.service.ts; surface messages in
   location-detail.component.html. In location.component.html, show the in-use conflict
   message on Delete instead of a generic failure.
6. Proxy: NO regen needed for validators/guards. Regen ONLY if a new endpoint or DTO field is
   added (none planned). If DTO DataAnnotations change the generated model, run
   abp generate-proxy and do not hand-edit proxy/.
7. Migration: none expected -- no schema change (validation + soft-delete only; Location is
   already FullAudited/ISoftDelete per the tenant-entity standard). Confirm ISoftDelete is on
   Location before relying on soft-delete; if absent, that becomes a migration and must be
   flagged.
8. Tests: re-evaluate the Skipped DeleteAsync_WhenLocationReferencedBy* FK-edge tests against
   the new manager pre-check (manager-level guard is testable without FK enforcement).

## Dependencies
- Depends on IR1 for the Staff Supervisor soft-Delete grant (delete UX is meaningless for
  Supervisor until granted; sequence the friendly-delete behavior to land with or before IR1's
  grant so the grant never re-exposes the raw FK error).
- Soft references OBS-26 for location-scoped reference counting; no blocking dependency.
- Blocks none.

## Residual open questions
- ZipCode regex scope: US-only 5 or 5+4 assumed (synthetic data, CA workers-comp domain).
  Confirm no non-US locations are expected before locking the pattern.
- Confirm Location actually carries ISoftDelete today; if it is FullAudited-only without
  ISoftDelete, soft-delete needs a small migration (flagged in step 7).
