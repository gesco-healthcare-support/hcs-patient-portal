---
id: IP3
title: Doctor entity -- keep dormant, hide the page, fix the IdentityUserId drift
type: tech-debt
components:
  - angular/src/app/doctors/doctor/components/doctor-detail.component.html
  - angular/src/app/doctors/doctor/components/doctor.component.html
  - angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts
  - angular/src/app/doctors/doctor/providers/doctor-base.routes.ts
  - docs/decisions/004-doctor-per-tenant-model.md
  - docs/design/staff-supervisor-doctor-management-design.md
  - src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md
related_known_bugs: [SEED-2, OBS-26]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

Decide the fate of the Doctor entity; do not leave the Doctors page empty in the demo.
Locked direction: keep the entity DORMANT (no drop), HIDE the Doctors nav item/route, and
FIX the stale IdentityUserId UI/doc drift (dead form controls, broken
`getIdentityUserLookup` bindings, always-blank Identity User/Tenant columns, and the
three out-of-date docs). Record when to revisit if multi-doctor ever returns to scope.

## Current behavior (from investigation)

Confirmed: nothing operational depends on a Doctor ROW, so the page is empty by
construction and the IdentityUserId references are dead.

- Booking dropdowns query `AppointmentType`/`Location` directly, no Doctor join:
  `AppointmentsAppService.cs:508-521` and `:522-536` (comment "no separate Doctor entity
  rows exist"; restated `AppointmentsAppService.cs:510,525`).
- Slot type-gate reads `doctorAvailability.AppointmentTypes`, NOT `doctor.AppointmentTypes`
  (`AppointmentsAppService.cs:915-916`). `DoctorAvailability` has no `DoctorId` FK
  (`Domain/DoctorAvailabilities/DoctorAvailability.cs` -- only `TenantId`, `LocationId`).
- Demo tenant has ZERO Doctor rows: `FalkinsteinTenantDataSeedContributor.cs:93` calls
  `_tenantManager.CreateAsync` directly, bypassing `DoctorTenantAppService` (= SEED-2).
- IdentityUserId column was dropped in DB+DTO but the Angular layer still binds it:
  - DB drop: migration `20260502000305_Drop_Doctor_IdentityUserId.cs`.
  - Backend clean: `Doctor.cs` / `DoctorDto` / `DoctorWithNavigationProperties` omit it;
    proxy `proxy/doctors/doctor.service.ts` has no `getIdentityUserLookup`.
  - Dead Angular bindings: `doctor-detail.component.html:80-84` binds
    `formControlName='identityUserId'` to `[getFn]='service.getIdentityUserLookup'`;
    `doctor.component.html:54-63` (filter) and `:188-200` (Identity User / Tenant columns);
    `doctor-detail.abstract.service.ts:67-68` still builds `identityUserId`+`tenantId`
    controls. `getIdentityUserLookup` is undefined on the doctor service, so the control
    renders non-functional and the columns safe-navigate to blank.
- Broken doc citation: `doctor-detail.abstract.service.ts:27` cites
  `docs/research/proxy-regen-identity-lookup-fix.md`, which does not exist (only
  `proxy-regen-stringvalues-fix.md` is present).
- Stale docs claiming a live identity link / Create button / M2M-driven filtering:
  ADR-004 `docs/decisions/004-doctor-per-tenant-model.md:26-28`; design doc
  `staff-supervisor-doctor-management-design.md:156,180-181,418-419,446-447`;
  `Domain/Doctors/CLAUDE.md` (lists "optional IdentityUserId FK", an `_userManager` sync,
  and an identityUserId repo filter -- none exist; `DoctorsAppService.UpdateAsync:207-212`).
- `DoctorPreferredLocationsAppService` doc over-claims it "scopes the booking-form Location
  dropdown" / "Phase 11 will consume it"; booking never reads it (`GetLocationLookupAsync`
  queries `Location` directly). It has a backend proxy but NO Angular component.
- Dormant-safety hazards to NOT break: `DashboardAppService.cs:99-101,122` counts Doctor
  rows ONLY in host scope (tenant scope is hardcoded `0`); `DoctorTenantAppService` still
  materializes a Doctor on SaaS-UI tenant provisioning (out of demo path). Keeping the
  entity + tables intact leaves both untouched.

## Relevant code locations

- `angular/src/app/doctors/doctor/components/doctor-detail.component.html` (remove
  identityUser lookup control, lines 80-84)
- `angular/src/app/doctors/doctor/components/doctor.component.html` (remove identityUser
  filter 54-63 and Identity User/Tenant columns 188-200)
- `angular/src/app/doctors/doctor/services/doctor-detail.abstract.service.ts` (remove
  `identityUserId`/`tenantId` form controls 67-68; fix/remove stale comment+citation 22-27)
- `angular/src/app/doctors/doctor/providers/doctor-base.routes.ts` (the nav route to hide)
- `docs/decisions/004-doctor-per-tenant-model.md:26-28`
- `docs/design/staff-supervisor-doctor-management-design.md`
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md`
- `src/HealthcareSupport.CaseEvaluation.Application/DoctorPreferredLocations/DoctorPreferredLocationsAppService.cs` (doc/XML-comment over-claim)

## Phase 3 cross-reference

- SEED-2 (demo-doctor-seed-missing): the empty page IS SEED-2's symptom. Hiding the nav
  resolves the user-facing complaint without a seed (honors the no-seed-proposal rule).
  Mark SEED-2 as superseded/won't-fix-by-seed in the parity log while here.
- OBS-26 (slot-gen location-scoped conflict): confirms scheduling is per-location, not
  per-Doctor-row -- corroborates "tenant IS the doctor"; reference, no change needed.
- Do NOT touch the abstract list component file (`doctor.abstract.component.ts`); ABP Suite
  regeneration depends on it (per angular/src/app/CLAUDE.md). Edit only concrete templates
  + the detail service.

## Research findings

- Internal patterns / prior art:
  - ADR-004 establishes "Doctor is the tenant"; its IdentityUserId-FK evidence is now
    stale and must be corrected, not relied on.
  - `app.routes.ts:96` lazy-loads `doctor-management/doctors -> DOCTOR_ROUTES`; the nav
    item itself is declared in `doctor-base.routes.ts` (an ABP `ABP.Route[]` with
    `requiredPolicy: 'CaseEvaluation.Doctors'`).
  - Removed-cleanup precedent: cleanup commit `d1bbdab` removed the entity FK +
    `GetIdentityUserLookupAsync`; this item finishes that cleanup on the Angular side.
- External docs (ABP / Angular):
  - ABP route hiding: omit/remove the route from the registered `ABP.Route[]` (or gate it
    behind a policy that is not granted) so the nav menu does not render it -- no component
    deletion needed. Keeping the lazy route in `app.routes.ts` but dropping the menu entry
    is the least-blast-radius way to "hide the page" while preserving the dormant feature.

## Approaches considered (with tradeoffs)

- A. Drop the Doctor entity entirely. Best satisfies "page should not be empty" (delete it)
  and kills drift at the source, but: largest blast radius (entities + 2 AppServices + 2
  controllers + EF config in BOTH DbContexts + Mapperly + proxy regen + Angular deletion +
  4 permissions + dashboard rework), a DESTRUCTIVE drop-table migration, touches the
  tenant-provisioning UoW (`DoctorTenantAppService.CreateDoctorProfileAsync`), and reverses
  an Accepted ADR -- irreversible if Phase 2 ever needs multi-doctor. Rejected NOW:
  architectural reversal, too heavy and risky to do reactively.
- B + hide (CHOSEN). Keep entity inert, fix the drift, hide the nav. Smallest, lowest-risk:
  removing bindings that already resolve to `undefined` cannot regress; doc edits are
  non-executable; no schema/migration; tenant-provisioning + host dashboard untouched.
  Keeps the door open for multi-doctor without rebuilding from scratch.
- C. Wire up first-class Doctor CRUD (Create button + Staff Supervisor CRUD + real
  linkage). Rejected: re-adds the deliberately dropped IdentityUserId column and
  `getIdentityUserLookup`, and couples a currently-working, Doctor-independent booking flow
  to Doctor rows the demo lacks -- a booking-blocking regression for a multi-doctor need
  that is explicitly out of scope (ADR-004: one office = one doctor). Over-engineering.

Why the decision wins: nothing operational reads a Doctor row, so B+hide removes the only
user-visible symptom (empty page) and the only real defect (stale IdentityUserId drift) at
near-zero risk, while preserving the entity for a possible future multi-doctor model.

## Decision (locked 2026-06-03)

Keep the Doctor entity and all tables DORMANT (no drop, no CRUD wiring). HIDE the Doctors
nav item/route. Remove the dead `identityUserId`/`tenantId` form controls, the broken
`getIdentityUserLookup` bindings, and the always-blank Identity User/Tenant columns.
Reconcile ADR-004 + `staff-supervisor-doctor-management-design.md` + `Doctors/CLAUDE.md` to
current reality (no live identity link, no M2M-driven dropdown filtering, no Create path).
Correct the `DoctorPreferredLocations` over-claim and the broken doc citation. Ensure the
dormant entity + hidden page do not break the dashboard (host Doctor count) or
tenant-provisioning (`DoctorTenantAppService`). Revisit only if multi-doctor-per-tenant
re-enters scope -- at which point Option A (superseding ADR + drop) or C becomes the call.

## Implementation outline (no code)

1. Angular -- remove dead controls (UI-only affordance cleanup, no server mirror needed):
   - `doctor-detail.abstract.service.ts`: delete the `identityUserId` and `tenantId` form
     controls (lines 67-68) and their destructured reads; remove/replace the stale comment
     + broken citation (22-27). No `getTenantLookup` consumer should remain.
   - `doctor-detail.component.html`: remove the identityUser lookup control (80-84).
   - `doctor.component.html`: remove the identityUser filter (54-63) and the Identity User
     and Tenant columns (188-200).
   - Do NOT modify `doctor.abstract.component.ts` (ABP Suite regen dependency).
2. Angular -- hide the page: remove (or policy-gate-off) the menu entry in
   `doctor-base.routes.ts`. Prefer dropping the `ABP.Route[]` menu item so the nav does not
   render; keep the lazy route registration in `app.routes.ts:96` so the dormant feature
   still compiles. No proxy regen required (no backend DTO/AppService change).
3. Docs -- reconcile to reality:
   - ADR-004 `:26-28`: strike the "creates a Doctor profile linked to that user" /
     IdentityUserId-link language; add a short "Status: dormant; nav hidden 2026-06-03;
     revisit if multi-doctor returns" note.
   - `staff-supervisor-doctor-management-design.md`: correct the identity-link, Create
     button, and M2M-driven-dropdown claims (156, 180-181, 418-419, 446-447); mark design
     as not-implemented / deferred.
   - `Domain/Doctors/CLAUDE.md`: remove "optional IdentityUserId FK", the `_userManager`
     identity-sync, and the identityUserId repo filter mentions.
   - `DoctorPreferredLocationsAppService` doc/XML comment: remove the "scopes booking-form
     Location dropdown" / "Phase 11 consumes it" over-claim.
4. Verify dormancy is safe (no code change expected, just confirm): host `DashboardAppService`
   Doctor count still resolves (entity kept), and `DoctorTenantAppService` provisioning path
   is unaffected. No migration. No server-side validation change (no integrity rule touched).

Flags: no migration; no proxy regen; enforcement is UI-only (dead-affordance removal +
nav hide) plus documentation -- nothing here is a server-enforced integrity rule.

## Dependencies

- Independent of the email (E1/E2/E3), roles, claim-examiner, and appointment-types items.
- Adjacent to IP6 (Patients relocation under User Management) -- both touch the
  `'::Menu:DoctorManagement'` nav parent; coordinate so hiding Doctors does not leave an
  orphaned/empty Doctor Management menu container once Patients moves out. Sequence with
  IP6 but no hard blocker.

## Residual open questions

- Minor: whether to delete the broken doc citation outright or repoint it at the real
  `proxy-regen-stringvalues-fix.md`. Recommend remove (the lookup is gone). Non-blocking.
- Minor: if hiding Doctors empties the Doctor Management menu parent, confirm with IP6
  whether the parent container is removed/renamed (not a Doctor-entity concern per se).
