---
feature: hipaa-scoped-external-user-lookup
date: 2026-06-22
status: in-progress
base-branch: development
related-issues: []
---

## Goal

Make `GetExternalUserLookupAsync` return, for an external caller, only the
co-parties named on appointments that caller can already see; internal staff
keep the tenant-wide search.

## Context

External callers currently get EMPTY from the lookup (interim safe state after
the 495de5ef HIPAA fix). The real fix: scope external results to co-parties on
the caller's shared appointments. Leak-equivalence holds -- an external caller
who can read an appointment already sees every party's name/email on its detail
(only internal comments + SSN are masked), so surfacing co-parties drawn from
VISIBLE appointments reveals nothing new. The visible-appointment set is the
security boundary. ABP 10.0.2 / .NET 10 / Angular 20. No migration expected.

## Approach

Reuse, do not reinvent, the visibility logic:

- Extract `ComputeExternalPartyVisibilityAsync` (private in AppointmentsAppService)
  into a shared `AppointmentVisibilityService` both app-services depend on, so
  the lookup and the appointment list cannot drift apart.
- Extract the leak-critical transform (visible appointments -> deduped co-party
  set, caller excluded, role-tagged per column) into a pure Domain function
  `ExternalCoPartyRules`, mirroring the existing `AppointmentAccessRules` idiom.
  TDD it.
- `GetExternalUserLookupAsync` external branch: get visible appointment ids ->
  load those appointments' party columns -> collect co-parties (pure fn) ->
  resolve emails to registered IdentityUsers (DTO needs IdentityUserId; the FE
  keys backfill on it) -> apply search + role filter -> return.

Rejected: re-implementing the 4-source visibility inside the lookup (drift risk,
the resume prompt's explicit concern). Rejected: returning unregistered
co-parties (DTO/FE require a real IdentityUserId; the separate exact-email flow
already attaches unregistered parties).

## Tasks

- T1: Extract `AppointmentVisibilityService` (Application layer,
  ITransientDependency) exposing `GetVisibleAppointmentIdsAsync()` ->
  `IReadOnlyCollection<Guid>?` (null = internal / no narrowing). Move the body
  of `ComputeExternalPartyVisibilityAsync` in verbatim; have
  AppointmentsAppService delegate to it (behavior identical).
  - approach: test-after
  - files-touched: [src/.../Application/Appointments/AppointmentVisibilityService.cs,
    src/.../Application/Appointments/AppointmentsAppService.cs]
  - acceptance: existing appointment-list/home tests stay green; AppointmentsAppService
    behavior unchanged.

- T2: Add pure `ExternalCoPartyRules.CollectCoParties(callerEmail, rows)` in
  Domain: given the party columns of visible appointments, return distinct
  co-parties (email, role, first/last, firm) EXCLUDING the caller's own email,
  one entry per (email, role).
  - approach: tdd
  - files-touched: [src/.../Domain/Appointments/ExternalCoPartyRules.cs,
    test/.../Domain.Tests/Appointments/ExternalCoPartyRulesTests.cs]
  - acceptance: caller's own email never returned; a co-party on a supplied
    appointment is returned with the correct role; duplicates across appointments
    collapse; blank columns ignored.

- T3: Wire `GetExternalUserLookupAsync` external-caller branch to use
  AppointmentVisibilityService + ExternalCoPartyRules, resolve co-party emails to
  registered IdentityUsers (IdentityUserId, FirstName/LastName/Firm), then apply
  the existing search + role filter. Internal staff path unchanged.
  - approach: test-after (DEVIATION 2026-06-22 from tdd) -- the leak-critical
    transform is unit-tested in T2 (ExternalCoPartyRules); the visibility set is
    unchanged behavior covered by existing AppointmentsAppService integration
    tests. A seeded co-party integration test would mean extending shared seed
    data (risk to other tests) or brittle inline FK seeding for coverage the live
    pass proves more authentically. Composition is verified LIVE in Verification.
  - files-touched: [src/.../Application/ExternalSignups/ExternalSignupAppService.cs]
  - acceptance: external caller finds a co-party on a SHARED appointment and does
    NOT find an unrelated party (LIVE); empty filter still returns empty; internal
    staff still search tenant-wide; existing visibility integration tests stay green.

- T4: FE search-as-you-type. `appointment-add.component.ts` (loadExternalAuthorizedUsers)
  and `appointment/components/appointment-view.component.ts` call the lookup with
  no filter (now empty); rewire the AA/DA pickers to send the typed term, mirroring
  the patient lookup UX.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment-add.component.ts,
    angular/src/app/appointments/appointment/components/appointment-view.component.ts,
    + their templates if the input shape changes]
  - acceptance: typing a term queries the lookup and shows scoped matches; no
    blanket load-all on init.

- T5: `DeleteTestUsersAsync` cascade. It soft-deletes the IdentityUser but leaves
  DA/CE master rows orphaned. Also remove DA/CE masters; confirm AA/Patient.
  - approach: code
  - files-touched: [src/.../Application/ExternalSignups/ExternalSignupAppService.cs]
  - acceptance: after delete, no orphaned AppDefenseAttorneys/AppClaimExaminers row
    for the removed identity.

- T6: Doc reconciliation. USER-ROLES-AND-ACTORS.md + angular appointments/CLAUDE.md
  (+ ExternalSignups/CLAUDE.md lookup row) still say "all 4 roles surface in the
  lookup"; correct to the scoped-search model.
  - approach: code
  - files-touched: [docs/business-domain/USER-ROLES-AND-ACTORS.md,
    angular/src/app/appointments/CLAUDE.md,
    src/.../Application/ExternalSignups/CLAUDE.md]
  - acceptance: docs describe staff=tenant-wide, external=co-party-scoped search.

- T7: Residual live UX verification (no new code): DA self-edit
  (/user-management/attorneys/my-profile) + CE self-edit
  (/user-management/claim-examiners/my-profile) render + save; send-back
  street/city/state/zip each mutate the record.
  - approach: code
  - files-touched: []
  - acceptance: Playwright pass with evidence; any defect found becomes its own task.

## Risk / Rollback

- Blast radius: T1 touches the appointment-list/home security hot path (mitigated
  by pure delegation + existing tests). T3 is the HIPAA path (mitigated by TDD +
  leak-equivalence). T4 is UI-only. T5 is a dev-only [AllowAnonymous] utility.
- Rollback: revert per-task commits; no migration to unwind.
- Follow-up (NOT in scope): `/api/app/appointments/applicant-attorney-details-for-booking?email=`
  may itself be unscoped for external callers -- a possible relative; flag, do not fix
  without approval.

## Verification

1. `dotnet build src/HealthcareSupport.CaseEvaluation.HttpApi.Host`; run new tests
   (`dotnet test --filter ExternalCoPartyRules` + the lookup scoping test).
2. Live: register synthetic DA + CE (tenant Falkinstein
   09f46f32-6119-0d8f-f552-3a2202649ed3; userType 4=DA/2=CE; DA needs firmName),
   book an appointment naming both, then confirm an external party's lookup search
   returns ONLY co-parties on shared appointments, never an unrelated party.
   Playwright as stafsuper1@gesco.com. Clean up via DELETE test-users and confirm
   masters are gone too (T5).
