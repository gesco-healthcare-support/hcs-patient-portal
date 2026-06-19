---
feature: aa-da-self-edit-snapshot
date: 2026-06-19
status: draft
base-branch: feat/frontend-rework
lane: Session B
related-issues: []
backlog: 2026-06-17-frontend-rework-backlog.md
protocol: 2026-06-19-parallel-build-protocol.md
backlog-item: 9
---

## Goal

Snapshot each attorney's name + firm + contact onto the Appointment at booking so an
appointment's attorney block is immutable history, and give applicant/defense attorneys a
self-edit "My profile" page that updates only their canonical master record -- so editing
their identity never rewrites past appointments.

## Context

Backlog #9. Today attorney `FirstName/LastName/FirmName` (+ contact/address) live ONLY on
the shared master entities (`ApplicantAttorney`, `DefenseAttorney`). The appointment detail
resolves them by JOIN to the master in `EfCoreAppointmentRepository.GetWithNavigationProperties`,
so editing a master mutates EVERY past appointment linked to it. There is also no self-edit
path for attorneys (master edits are staff-only `CaseEvaluationPermissions.ApplicantAttorneys.Edit`
/ `DefenseAttorneys.Edit`), and no external attorney profile page (patients have
`/user-management/patients/my-profile`; attorneys have none).

Precedent: Appointment already denormalizes party EMAILS at booking
(`PatientEmail/ApplicantAttorneyEmail/DefenseAttorneyEmail/ClaimExaminerEmail`, migration
`20260430222449_AddAppointmentPartyEmails`, captured in
`AppointmentsAppService` ~929-937). This feature extends that exact pattern to name/firm/contact.

### Locked decisions (brainstorming with Adrian, 2026-06-19)

- Self-edit UPDATE semantics: MASTER ONLY. The attorney My-profile page updates the master
  (their identity for future bookings). No appointment snapshot is touched by self-edit, so
  every existing appointment keeps what it had.
- Unlock surface: a NEW attorney My-profile page (mirrors the patient my-profile), reached
  from the external navbar. Not inline on the appointment detail.
- Snapshot field set: name + firm + CONTACT (full per-appointment immutability), not just
  name+firm.
- Backfill: NONE. Old appointments keep null snapshot columns; the read path is
  `COALESCE(snapshot, master-join)`, so the master join is retained for legacy rows.

### Derived design decisions (stated reasoning; confirm at review)

- Exact snapshot columns = the displayed/edited attorney set MINUS email (already snapshotted):
  per attorney `FirstName, LastName, FirmName, WebAddress, PhoneNumber, FaxNumber, Street,
  City, StateId (Guid?), ZipCode` = 10 cols x 2 attorneys = 20 new nullable columns on
  Appointment. `StateId` is snapshotted as the Guid and resolved through the stable state
  lookup. Free-form `FirmAddress` is EXCLUDED (the detail renders the structured address).
- Capture point = whenever the appointment's attorney is set THROUGH the appointment: at
  booking link AND on the internal staff "edit appointment details" upsert AND on the
  external-signup auto-link. This keeps appointment-side edits visible (snapshot refreshed
  for THAT appointment only) while master-side edits (attorney self-edit, admin People-hub
  edit) never propagate. This resolves the "staff edits silently invisible" wrinkle.
- Read/exposure = add the 20 snapshot fields to `AppointmentDto`; the Angular
  `AppointmentViewComponent` form-patch reads `snapshot ?? master-nav-prop` in ONE place, so
  the detail shows frozen values with a clean legacy fallback. No scattered COALESCE.
- Self-edit auth = a new self-scoped app service resolving the caller's master strictly by
  `master.IdentityUserId == CurrentUser.Id` (+ role), deny-by-default, no staff permission
  grant. ABP auto-exposes it as an API (no manual controller).

## Parallel-build safety (Session A conflict check -- CLEAR)

- Session A's lane (#3 config hub/WCAB `2885512`, #10 dashboard line `41a9fe7`) is committed;
  no new A feature in flight (only proxy `index.ts` regen no-ops + backlog.md uncommitted).
- Backend file set is disjoint from A's (dashboards/config/wcab). EF migration is Session B's
  EXCLUSIVE lane -- A creates none.
- Shared files needed ONLY in the frontend phase: `app.routes.ts` (one external route) and
  `external-navbar` (one link). Coordinate via Adrian before editing; A's #3 deliberately did
  NOT touch `app.routes.ts`, so contention is unlikely.
- New self-edit endpoint -> after the backend commit, ask (via Adrian) for Session A to run
  `abp generate-proxy -t ng -u http://localhost:44377`; commit only the changed `models.ts` +
  `generate-proxy.json`. Never hand-edit proxies.
- Do NOT edit `2026-06-17-frontend-rework-backlog.md` during build (A has it modified).

## Tasks

Backend first (T1-T5), commit, regen handshake, then frontend (T6-T8).

- T1: Add the attorney snapshot columns + EF mapping + migration.
  - approach: code
  - files: src/.../Domain/Appointments/Appointment.cs (20 nullable props + an
    AppointmentConsts max-length const reuse); src/.../EntityFrameworkCore/.../CaseEvaluationDbContext.cs
    (property config mirroring the existing *Email block); NEW migration via
    `dotnet ef migrations add AddAppointmentAttorneySnapshot` (run from the EntityFrameworkCore
    project; one migration, my exclusive lane).
  - acceptance: migration applies cleanly via the db-migrator; columns are nullable; no
    ModelSnapshot conflict; `dotnet build` green.

- T2: Capture/refresh the snapshot from the master on appointment-attorney upsert.
  - approach: tdd
  - files: the appointment-applicant/defense-attorney link managers/app services + the
    booking orchestration in AppointmentsAppService + ExternalSignupAppService auto-link.
    Add a small pure helper `AttorneySnapshot.CaptureApplicant(master)/CaptureDefense(master)`
    that copies the 10 fields onto the Appointment (testable without DI).
  - tests: capturing copies all 10 fields; booking path snapshots; staff appointment-edit
    upsert refreshes THIS appointment's snapshot only; external-signup link snapshots; a
    second appointment for the same master is unaffected by a later edit.
  - acceptance: all three capture paths covered; unit tests green.

- T3: Expose the snapshot on the read DTO.
  - approach: code
  - files: src/.../Application.Contracts/Appointments/AppointmentDto.cs (+ the Mapperly
    profile if explicit). Add the 20 snapshot fields.
  - acceptance: `getWithNavigationProperties` returns the snapshot fields; build green;
    existing fields unchanged.

- T4: Self-scoped attorney My-profile app service (get + update, master only).
  - approach: tdd
  - files: NEW src/.../Application/.../MyAttorneyProfileAppService.cs (+ Contracts interface
    + Get/Update DTOs). Resolve the caller's ApplicantAttorney/DefenseAttorney by
    `IdentityUserId == CurrentUser.Id` (+ role); update name/firm/contact on the master ONLY;
    deny-by-default when the caller owns no matching master.
  - tests: an attorney updates only their own master; a caller with no matching master is
    denied; the update never writes any Appointment snapshot; cannot target another
    attorney's record by id.
  - acceptance: self-scoping enforced (security path); tests green; ABP auto-exposes the API.

- T5: Backend verification + commit gate.
  - approach: code
  - run `dotnet build` + the new unit tests; confirm migration applied. Commit T1-T4 by
    explicit path. THEN request Session A proxy regen (via Adrian) for the new endpoint +
    DTO fields. BLOCK T6 until the regenerated proxy lands.

- T6: Attorney My-profile Angular page.
  - approach: test-after
  - files: NEW angular/src/app/.../attorney-profile component (mirror
    patient-profile-redesign), a route in app.routes.ts (external-only; COORDINATE via Adrian),
    and an external-navbar entry (COORDINATE). Calls the regenerated my-attorney-profile proxy.
  - acceptance: an external AA/DA can view + save their name/firm/contact; saving updates the
    master; screenshot-verified.

- T7: Detail reads snapshot-or-master.
  - approach: test-after
  - files: angular/src/app/appointments/appointment/components/appointment-view.component.ts
    form-patch -> `applicantAttorney<X> = appt.applicantAttorney<X>Snapshot ?? navProp...` (one
    place, for both attorneys). No change to the detail templates.
  - acceptance: a snapshotted appointment shows frozen values; a legacy (null-snapshot)
    appointment falls back to the master; karma spec on the resolver.

- T8: Live verification on Falkinstein.
  - approach: code (verification)
  - As an attorney (appatty1): edit My-profile firm name; confirm a PAST appointment's detail
    is unchanged (snapshot) and a NEW booking captures the new value. Screenshot both.

## Execution order

T1 -> T2 -> T3 -> T4 -> T5 (commit backend, request regen) -> [await regen] -> T6 -> T7 -> T8.
Commit small + explicit-path after each task. One migration only (T1).

## Risk / Rollback

- Blast radius: 20 additive nullable columns (low risk); new self-scoped endpoint; one FE
  page + a shared-route/navbar edit (coordinated). Rollback: revert per-task commits; the
  migration is reversible (Down drops the columns).
- Forward-only immutability: pre-migration appointments keep null snapshots and remain
  master-join-resolved, so they STILL change if the master changes. This is the accepted
  consequence of the no-backfill choice -- document it; it is not a bug.
- Security: the self-edit service must resolve strictly by `IdentityUserId == CurrentUser.Id`;
  a slip is cross-party data exposure. Hence tdd on T4.
- Two/three capture paths risk an un-snapshotted appointment if one is missed -- T2 tests
  cover booking + staff-edit + external-signup.

## Verification

- Backend: T2 + T4 unit tests (tdd); `dotnet build`; migration applies.
- FE: T7 resolver karma spec; T6/T8 Falkinstein screenshots.
- Proxy regen: Session A (single-writer); commit only models.ts + generate-proxy.json.

## Proxy / migration / coordination

- EF migration: REQUIRED (T1) -- Session B exclusive; one at a time.
- Proxy regen: REQUIRED after T5 -- Session A single-writer; handshake via Adrian.
- Shared-file edits (frontend phase): app.routes.ts + external-navbar -- coordinate via Adrian.
- Not gated by any Session A item; #3 already landed.
