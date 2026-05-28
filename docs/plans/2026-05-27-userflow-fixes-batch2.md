---
feature: userflow-fixes-batch2
date: 2026-05-27
status: in-progress
base-branch: main
related-issues: [internal-new-appt-routing, attorney-section-scope, claim-toggles, authserver-deadends, external-list-display]
---

## Refinements absorbed at approval (2026-05-27)

After the deep code re-read (no agent assumptions on critical paths), four
refinements:

- **R1 (T5)** `home.component.ts` does NOT import `DatePipe` or `CommonModule` --
  template-only changes won't compile. Add `import { DatePipe } from '@angular/common'`
  and include `DatePipe` in the standalone `imports: [...]` array.
- **R2 (T3)** The existing spec `appointment-add-claim-information.component.spec.ts`
  (Plan 2) at lines 39-40 calls `setValue(false)` on `injuryInsuranceEnabled` +
  `injuryClaimExaminerEnabled` to keep the form valid. After T3 (toggles removed,
  CE+Insurance always-required), those calls no longer relieve the validators ->
  all 4 derivation tests fail. T3 must extend `fillRequiredScaffold` to set the
  now-required Insurance + CE fields with synthetic values.
- **R3 (T2)** The original "make `reset()` role-aware" note is unnecessary.
  Existing `patchValue({applicantAttorneyEnabled:true, defenseAttorneyEnabled:true})`
  at `appointment-add.component.ts:1016` works for both AA-mandatory bookers
  (AA-role: stays enabled, validators required) and non-AA bookers (toggle
  starts ON, user can toggle off). No `reset()` change needed.
- **R4 (T4)** AuthServer Account-page audit done -- `ResetPassword.cshtml:80-82`
  is the ONLY other partial dead-end (just "Request one" -> ForgotPassword;
  success auto-signs-in). All other pages (AccessDenied, LockedOut, LoggedOut,
  Logout, ConfirmUser, EmailConfirmation, ResendVerification, ForgotPassword)
  already link or redirect to /Account/Login. ResetPassword sign-in link is the
  optional secondary fix.

## Goal

Fix six user-flow issues found in live review: internal-user "New Appointment
Request" routing, the over-corrected attorney-section requirements (BUG-044),
claim-modal CE/Insurance toggles, an AuthServer post-registration dead-end, and
the external "My Appointments Requests" list display (date format + empty
Claim #/Date Of Injury).

## Context (live-replicated + source-verified 2026-05-27)

All six replicated live on the Falkinstein tenant; root causes confirmed by
reading the code (frontend + backend + AuthServer). Stack: Angular 20 SPA
(baked nginx image), .NET ABP AuthServer (Razor + `global-scripts.js`), EF Core.

## Decisions locked with Adrian (2026-05-27)

- **D1** Internal bookers (staff / IT-admin reaching `/appointments/add` via #1)
  follow the **same** attorney/claim rules as external users.
- **D2** "All external users" for DA-required + claim no-toggle **includes
  Patient self-bookers**.
- **D3** Removing the claim-modal CE + Insurance toggles makes those sections
  **always rendered AND required** (all their fields mandatory) -- OLD parity.
- **D4 (SSN)** No SSN code change. The SSN read path is correct end to end
  (capture -> persist -> project full Patient -> F4-01 redact -> ssnMask). The
  column is empty only when no SSN is stored. Owner (patient viewing own) sees
  full SSN; non-owner external roles see redacted -- per F4-01. OPEN sub-question
  below if you want the owner view always-redacted too.

## Tasks

- T1 (Issue 1): Route the staff list "New Appointment Request" to the full
  booking form instead of the generic ABP CRUD modal.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment/components/appointment.component.html]
  - detail: button at `appointment.component.html:4-12` is `(click)="create()"`
    -> `appointment.abstract.component.ts:57-60` -> `serviceDetail.showForm()`
    (opens `<app-appointment-detail-modal>`). Replace with
    `[routerLink]="['/appointments/add']"` + `[queryParams]="{ type: 1 }"`
    (RouterLink already imported in `appointment.component.ts`; `/appointments/add`
    is authGuard-only and already supports internal on-behalf booking via
    `isExternalUserNonPatient` -> patient-select + create-on-behalf). Keep the
    `*abpPermission="'CaseEvaluation.Appointments.Create'"` gate. No TS change.
  - acceptance: as clistaff1, clicking "New Appointment Request" lands on the
    full `/appointments/add` form (with patient picker), NOT the CRUD modal.

- T2 (Issues 2+3): Make the Applicant Attorney section optional-with-toggle for
  everyone except an Applicant-Attorney booker; keep Defense Attorney
  required-no-toggle for everyone.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment-add.component.html, angular/src/app/appointments/appointment-add.component.ts]
  - detail:
    - HTML `:117` AA: `[mandatory]="true"` -> `[mandatory]="isApplicantAttorney && !isItAdmin"`.
      When false, the child shows the Include toggle (`appointment-add-attorney-section`
      `.html:10-20`) and the body + required validators follow `applicantAttorneyEnabled`.
    - HTML `:129` DA: keep `[mandatory]="true"` (already correct).
    - TS: AA-role booker is already prefilled + `applicantAttorneyEnabled=true`
      (`applyOwnRoleAttorneyPrefill` + `loadApplicantAttorneyForCurrentUser`),
      so "required + prefilled" holds. For non-AA bookers make `applicantAttorneyEnabled`
      default sensible (proposed: default ON/visible, user may toggle off) and
      ensure `reset()` (`:1016`, which hardcodes both `*Enabled=true`) becomes
      role-aware so it does not force AA back on for non-AA bookers. DA stays
      locked true.
  - acceptance: as Patient/CE/DA booker -> AA section shows a working Include
    toggle (optional); as Applicant-Attorney booker -> AA required + prefilled,
    no toggle. DA always required, no toggle, for all.
  - SUB-DECISION (minor): default state of the AA toggle for non-AA bookers
    (ON-and-visible vs OFF). Proposed: ON.

- T3 (Issue 4): Remove the Insurance + Claim Examiner "Include" toggles in the
  claim modal; always render both sections and make their fields required (D3).
  - approach: test-after
  - files-touched: [angular/src/app/appointments/sections/appointment-add-claim-information.component.html, angular/src/app/appointments/sections/appointment-add-claim-information.component.ts]
  - detail: remove the Insurance switch (`.html:243-255`) and CE switch
    (`.html:365-377`); change the body `@if (...Enabled)` guards (`:257`, `:379`)
    to always render. Keep `injuryInsuranceEnabled`/`injuryClaimExaminerEnabled`
    forced true (or drop the controls and apply validators unconditionally) so
    `applyInsuranceRequiredValidators` + `applyClaimExaminerRequiredValidators`
    make Insurance Company Name + the 8 CE fields required. `makeEmptyInjuryDraft`
    already defaults both `isActive:true`. `isInsuranceFieldsetDisabled` /
    `isClaimExaminerReadOnly` become irrelevant for the toggle (CE read-only
    prefill for CE bookers still applies to the fields).
  - acceptance: claim modal shows Insurance + Claim Examiner with no Include
    toggle; submit blocked until their required fields are filled.

- T4 (Issue 5): Un-hide the existing "Already have an account? Sign in" link on
  the Register page (which also fixes the post-registration screen).
  - approach: code (manual verify)
  - files-touched: [src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-styles.css]
  - ROOT CAUSE (hard-verified, NOT the earlier guess): the link is NOT missing --
    the stock Register page server-renders
    `<h5 class="mb-2">Already have an account? <a href="/Account/Login">Sign in</a></h5>`
    inside `.account-module-form`, BEFORE `<form id="registerForm">` (confirmed by
    `curl` of /Account/Register). It is **deliberately CSS-hidden** by
    `global-styles.css:193-195`:
    `.account-module-form > h5:has(a[href="/Account/Login"]) { display: none; }`
    (comment dated 2026-05-19: "Adrian's directive: drop both that heading and the
    post-success Sign in button"). Verified live: the `<h5>` has computed
    `display:none`, `offsetParent === null`, 0x0 rect -- present in DOM, invisible.
    This task REVERSES that prior directive.
  - FIX: remove (or neutralize) the rule at `global-styles.css:193-195`. Because
    the `<h5>` is a SIBLING OUTSIDE `#registerForm` (and `showSignupSuccess` only
    replaces `#registerForm.innerHTML`), un-hiding it restores the sign-in LINK on
    BOTH the register form AND the post-registration "Account created" screen --
    one change fixes both halves of issue 5. It is a LINK (anchor), satisfying
    "no buttons".
  - acceptance (live): the "Already have an account? Sign in" link is visible on
    /Account/Register AND still visible above the "Account created / Resend
    verification" banner after a successful registration.
  - SECONDARY (verify, then decide): the prior directive also removed a post-
    success "Sign In" button from `showSignupSuccess` (`global-scripts.js:743-790`,
    docstring references it; current code emits only "Resend verification"). With
    the h5 un-hidden this is redundant, so NO JS change is planned -- but confirm
    live after the CSS fix that the post-success h5 is visible; if not, add an
    `<a href="/Account/Login">Sign in</a>` to the injected HTML as fallback.
  - OTHER AuthServer pages (agent-surveyed, to spot-check live during build):
    Login / ResendVerification / ForgotPassword already have a sign-in link;
    `ResetPassword.cshtml:80-82` has none (only "Request one" -> ForgotPassword) --
    optional add; `ConfirmUser` + `EmailConfirmation` are redirect-only stubs (no
    fix). No other hidden-link CSS rules found in global-styles.css.
  - note: AuthServer change -> requires AuthServer image rebuild to verify
    (static asset baked in).

- T5 (Issue 6a): Human-readable dates in the external "My Appointments Requests"
  list.
  - approach: test-after
  - files-touched: [angular/src/app/home/home.component.html]
  - detail: `:189` Appointment Date renders raw `{{ row.appointment.appointmentDate }}`
    -> add a date pipe (proposed `| date:'MM/dd/yyyy hh:mm a'`, matching
    `appointment-view.component.html`). `:207` Date Of Injury also raw once data
    flows (T6) -> add `| date:'MM/dd/yyyy'`.
  - acceptance: Appointment Date shows e.g. "06/10/2026 09:00 AM"; DOI shows
    "03/15/2025".

- T6 (Issue 6b): Populate Claim # + Date Of Injury in the external list by
  loading injury details on the appointments list path.
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs, test/...EntityFrameworkCore.Tests or Application.Tests]
  - detail: ROOT CAUSE -- the list projection `GetQueryForNavigationPropertiesAsync`
    (`:205-227`) joins Patient/IdentityUser/Type/Location/DoctorAvailability but
    never sets `AppointmentInjuryDetails` (stays empty `new()`); the injury loader
    `LoadInjuryDetailsAsync` (`:136-183`) runs ONLY on the single-item
    `GetWithNavigationPropertiesAsync`. Add a BATCHED injury-detail load to
    `GetListWithNavigationPropertiesAsync` (`:196-203`) keyed by the page's
    appointment ids (one extra query, not per-row N+1), populating each row's
    `AppointmentInjuryDetails`. The nav DTO + Angular bindings (`home.component.html:197-210`)
    are already correct and will then render.
  - acceptance: external list shows Claim # "CLM-BP-001" + DOI for A00001; staff
    list unaffected; one batched query per page (no N+1).
  - blast radius: `GetListWithNavigationPropertiesAsync` is shared by the staff
    list too -- extra injury data is harmless there; verify list perf with the
    batched query.

## Issue 6 SSN (no task -- verified working)

The SSN column is correctly wired end to end (capture -> persist -> project full
Patient -> F4-01 `RedactForCaller` -> `ssnMask`). Empty only when no SSN stored.
Owner sees full; non-owner external roles see `***-**-####`. No code change.
OPEN sub-question: do you want the list to redact even for the owner (patient
viewing their own)? That is a separate F4-01 policy change if so.

## Risk / Rollback

- Blast radius: T1/T2/T3/T5 = Angular (booking form + staff list + home list);
  T4 = AuthServer static JS; T6 = EF Core list query (shared by staff + external
  lists). No DB migration. No proxy regen (no DTO changes -- the nav DTO already
  carries injuries + SSN).
- Rebuilds to verify: Angular image (T1/T2/T3/T5), AuthServer image (T4), API
  image (T6). Coordinate per the usual `replicate-old-app`-healthy gate.
- Rollback: revert the PR; all changes are additive/behavioral, no schema.
- T2/T3 reverse part of the BUG-044 fix -- confirm the approval-time T8 injury
  gate (claim still required) is unaffected (it is: claim requirement unchanged).

## Verification (after build, per issue)

1. (T1) clistaff1 -> /appointments -> "New Appointment Request" -> lands on
   `/appointments/add` full form (patient picker present), not the CRUD modal.
2. (T2) Patient booker: AA toggle present + usable, AA optional; DA required no
   toggle. Applicant-Attorney booker: AA required + prefilled, no toggle.
3. (T3) Claim modal: no Insurance/CE toggle; both required.
4. (T4) Register a throwaway user -> success screen has a working "Sign in" link.
5. (T5) Home list: Appointment Date + DOI human-readable.
6. (T6) Home list: Claim # + DOI populated for an appointment that has a claim;
   staff list still loads.
7. SSN: enter an SSN at booking (UI) -> appears (full for owner / redacted for a
   linked attorney). `dotnet test` + `yarn test` green.
