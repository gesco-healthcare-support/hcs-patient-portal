---
feature: booking-form-parity-bugfixes
date: 2026-05-27
status: in-progress
base-branch: main
related-issues: [BUG-042, BUG-043, BUG-044, OBS-42]
---

## Goal

Fix the three booking-form/appointment-view bugs from the 2026-05-27
audit as one coordinated change set, regression-tested together:
attorney name loss + identity-gated display (BUG-042), missing
claim-information requirement (BUG-043), and the optional-attorney
"Include" toggles (BUG-044). OBS-41 (structured body parts) is a
separate slice (`docs/plans/2026-05-27-structured-body-parts.md`);
OBS-42 (cross-session mirroring) needs no code.

## Context

All three bugs sit in one area (booking form, the shared
attorney-section component, the appointment view, and the
`AppointmentsAppService` attorney/injury paths), so fixing them together
avoids touching the shared component and the upsert path twice. BUG-042
and BUG-043 are parity **regressions**; BUG-044 is an intentional
deviation from OLD (product decision). Decisions locked with Adrian
2026-05-27:

- **Attorney name: split FirstName + LastName** (not a single field).
  Display the stored name AND firm always; source the name from the
  stored attorney record (never the IdentityUser) so booked == shown.
- **Both** Applicant AND Defense attorney sections are **mandatory** --
  remove the Include toggle from both.
- **Claim Information required for ALL appointment types** (for now).

Source facts that ground the design (read 2026-05-27):
- `ApplicantAttorney`/`DefenseAttorney` entities have **no name column**
  (only Firm/address/Email/IdentityUserId). Symmetric.
- `ApplicantAttorneyDetailsDto`/`DefenseAttorneyDetailsDto` **already
  carry** `FirstName`/`LastName`; the SPA already SENDS them
  (`appointment-add.component.ts:1815-1816`). The server upsert + getter
  drop/ignore them.
- The getters `GetAppointment{Applicant,Defense}AttorneyAsync`
  (`AppointmentsAppService.cs:1033,1250`) early-return null when
  `IdentityUser == null` and read name from `IdentityUser.Name/Surname`.
- Booking attorney UI is a single "Name *" field bound to
  `{prefix}FirstName` (`appointment-add-attorney-section.component.html:
  24-30`); no Last Name input. The view already has split First/Last.
- `wireAttorneySectionToggle` / `applyAttorneySectionValidators`
  (`attorney-section-validators.ts`) gate required validators on the
  `{prefix}Enabled` toggle.
- Claim info is an unvalidated in-memory `injuryDrafts[]`;
  `persistInjuryDraftsIfProvided` no-ops when empty
  (`appointment-add.component.ts:175, 2396`). OLD blocked submit via
  `checkInjuryDetailFormGroupValidation` (OLD :1375).

## Approach

Restore split-name persistence end to end, make both attorney sections
mandatory via a new component mode, and add an all-types claim-required
guard (client + server). Prefer stored attorney name over IdentityUser
in display so there is no booked-vs-registered divergence; fall back to
the IdentityUser name only for legacy rows where stored name is null.

**Rejected:** single `Name` field (Adrian: keep split); displaying the
IdentityUser name over the stored name (reintroduces divergence);
bundling structured body parts (separate slice).

## Tasks

- T1: Add `FirstName` + `LastName` (nullable, max 50) to
  `ApplicantAttorney` + `DefenseAttorney` entities and their `*Consts`;
  EF Core migration (additive, nullable -- no backfill).
  - approach: code
  - files-touched: [src/.../Domain/ApplicantAttorneys/ApplicantAttorney.cs, src/.../Domain.Shared/ApplicantAttorneys/ApplicantAttorneyConsts.cs, src/.../Domain/DefenseAttorneys/DefenseAttorney.cs, src/.../Domain.Shared/DefenseAttorneys/DefenseAttorneyConsts.cs, src/.../EntityFrameworkCore/.../CaseEvaluationDbContext.cs (+ tenant context), new Migrations/*_AddAttorneyName.cs]
  - acceptance: migration applies; both attorney tables have FirstName +
    LastName columns.

- T2: Thread `firstName`/`lastName` through the managers + the upsert
  AppService methods; add to the master `ApplicantAttorneyDto`/
  `DefenseAttorneyDto` + their Mapperly mappers.
  - approach: tdd
  - files-touched: [src/.../Domain/ApplicantAttorneys/ApplicantAttorneyManager.cs (Create/Update), src/.../Domain/DefenseAttorneys/DefenseAttorneyManager.cs, src/.../Application/Appointments/AppointmentsAppService.cs (Upsert{Applicant,Defense}AttorneyForAppointmentAsync), src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyDto.cs (+ Defense), src/.../Application/CaseEvaluationApplicationMappers.cs]
  - acceptance: unit test -- upsert with FirstName/LastName + no
    IdentityUser persists both names on the master row.

- T3: Fix the display getters to return the stored name and not require
  an IdentityUser. `GetAppointment{Applicant,Defense}AttorneyAsync`:
  return when `ApplicantAttorney != null` (drop the `IdentityUser ==
  null` condition); set `FirstName = a.FirstName ?? u?.Name`, `LastName =
  a.LastName ?? u?.Surname`, `Email = a.Email ?? u?.Email`.
  - approach: tdd
  - files-touched: [src/.../Application/Appointments/AppointmentsAppService.cs:1033-1061, :1250-1278]
  - acceptance: unit tests -- (a) unregistered attorney returns name +
    firm + email; (b) registered attorney with a different identity name
    returns the STORED booked name.

- T4: Booking form -- add a **Last Name** input to the attorney-section
  component (relabel current "Name" to "First Name"), bind to
  `{prefix}LastName`; add `LastName` to the required-suffix list so it is
  required when the section is mandatory.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/sections/appointment-add-attorney-section.component.html, angular/src/app/appointments/shared/attorney-section-validators.ts (ATTORNEY_SECTION_SUFFIXES), angular/src/app/proxy/** (regenerate via abp generate-proxy after T2 DTO change -- do NOT hand-edit)]
  - acceptance: booking with First "Aria" / Last "Stone" (attorney NOT
    registered) persists FirstName='Aria', LastName='Stone'.

- T5: Appointment view -- bind/display stored First/Last for both AA and
  DA (view already has split fields); ensure
  `bindApplicantAttorneyFromResponse` + the DA bind prefer the stored
  name over IdentityUser.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment/components/appointment-view.component.ts (bindApplicantAttorneyFromResponse, DA bind), .html]
  - acceptance: live -- view shows the booked attorney First/Last for an
    unregistered attorney; for a registered one, still the booked name.

- T6: Make both attorney sections mandatory (BUG-044). Add
  `[mandatory]` input to the attorney-section component that hides the
  Include switch, forces `{prefix}Enabled=true` (disabled control), and
  always applies required validators; apply to both AA + DA in booking +
  view; in `wireAttorneySectionToggle` skip the toggle path when
  mandatory.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/sections/appointment-add-attorney-section.component.ts + .html, angular/src/app/appointments/shared/attorney-section-validators.ts, angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointments/appointment/components/appointment-view.component.ts + .html]
  - acceptance: no Include toggle on either section in booking or view;
    submit blocked if either attorney section is incomplete.

- T7: Require >= 1 Claim Information for ALL appointment types,
  CLIENT-ONLY (BUG-043, option (c) -- OLD parity). Block submit + inline
  message on the Claim Information card when `injuryDrafts.length === 0`.
  NO server create-flow guard: the booking creates the appointment FIRST
  (`POST /appointments`, emails-only payload) and attaches injuries +
  attorneys via SEPARATE post-create endpoints
  (`appointment-add.component.ts:970-985`), so `CreateAsync` has no
  injury/attorney data to validate. This matches OLD, which enforced via
  its client-side validation summary.
  - approach: test-after (client)
  - files-touched: [angular/src/app/appointments/appointment-add.component.ts (onSubmit guard), angular/src/app/appointments/sections/appointment-add-claim-information.component.* (inline message)]
  - acceptance: submit blocked with no claim; booking with one claim
    still succeeds. (No server change in this task.)

- T8: Approval-time server gate (option (b), defense-in-depth) -- block
  the Pending->Approved transition when the appointment lacks >= 1
  injury detail OR is missing an applicant/defense attorney link.
  SEQUENCED LAST and gated on tests because it changes approve behavior.
  - approach: tdd
  - files-touched: [src/.../Domain/Appointments/AppointmentManager.cs (ApplyTransitionAsync Approve branch -- inject injury + attorney-link repos), src/.../Domain.Shared/CaseEvaluationDomainErrorCodes.cs (+ message), test/.../Application.Tests + Domain.Tests]
  - acceptance: ApproveAsync throws a localized BusinessException when an
    appointment has 0 injuries or a missing attorney link; approve
    succeeds when both prerequisites exist.
  - RISK GATE (must verify before merge): every existing approve
    test/flow that approves an appointment without injuries/attorneys
    will start failing. Before committing T8: (1) run the full suite,
    (2) update legitimate existing approve tests to seed the
    prerequisites, (3) confirm the demo approve flow (clistaff1
    approving a properly-booked appointment) still works end to end. If
    T8 proves to ripple too widely, STOP and split it into its own slice
    rather than destabilize the (c) fixes -- (c) ships independently.

## Risk / Rollback

- Blast radius: booking + appointment view + attorney/injury server
  paths. T1 migration is additive + nullable (no backfill; legacy rows
  fall back to IdentityUser name -- preserves current behaviour). The
  shared attorney-section component is touched by T4 + T6 -- single PR to
  avoid conflicts.
- Proxy regen (T4) must use `abp generate-proxy`; never hand-edit
  `angular/src/app/proxy/**`.
- Making attorneys + claim mandatory will block previously-valid empty
  submissions -- intended, but confirm no seed/automation path depends on
  omitting them.
- Rollback: revert PR; nullable migration can be dropped or left unused.

## Verification (end-to-end, after all tasks)

Clean `docker compose down -v` boot, then:

1. Slot gen as `stafsuper1` (QME @ Demo Clinic North).
2. Book as `patient1@gesco.com`:
   - Confirm **neither** attorney section has an Include toggle, both
     required (BUG-044).
   - Fill Applicant Attorney First "Aria" / Last "Stone"
     (email appatty1@gesco.com, NOT registered); Defense Attorney First
     "Dana" / Last "Defense".
   - Confirm submit is **blocked** with no Claim Information; then add one
     claim and submit (BUG-043).
3. SQL: `AppApplicantAttorneys.FirstName='Aria', LastName='Stone'`;
   `AppDefenseAttorneys.FirstName='Dana', LastName='Defense'`;
   `AppAppointmentInjuryDetails` has 1 row.
4. View as `stafsuper1`: Applicant Attorney shows "Aria Stone" + firm +
   email (unregistered); Defense shows "Dana Defense" (BUG-042).
5. Register defatty1 via invite, log in, view: Defense Attorney still
   shows the booked "Dana Defense" (not the registered identity name).
6. API negative tests: create with no claim -> rejected; create omitting
   an attorney -> rejected (T7/T8).
7. `dotnet test` (Application.Tests + Domain.Tests) green.
8. Visual screenshot pass of booking + view (per visual-checks rule).
