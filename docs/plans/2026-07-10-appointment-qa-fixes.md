---
feature: appointment-qa-fixes
date: 2026-07-10
status: in-progress
base-branch: main
deploy-target: development
related-issues: []
---

## Goal

Fix five stakeholder-reported issues found while reviewing the appointment request
wizard, the accessor-invite email, rate-limiting, the request-more-info feature, and
tenant impersonation. Ship as two PRs off `main`: PR1 = four small QA fixes; PR2 =
appointment view/review field parity.

## Context

Triaged 2026-07-10 (findings verified against code at main @ 117b90ee). Stakeholder
decisions captured inline per task. Work happens in the `appointment-qa` worktree so the
running `main` dev stack is untouched.

Investigation established:
- The wizard REVIEW step already renders all 64 non-excluded fields. The `/appointments/view/:id`
  route is role-split: the INTERNAL detail component shows all 64 except the Custom Fields
  section; the EXTERNAL component is condensed (38 fields dropped).
- The accessor-invite email has two confirmed bugs (dead password link + empty location).
- Login is already failure-only (ABP lockout); registration counts all attempts (fixed-window).
- Only the Patient "Unit #" address-line-2 is a request-more-info gap (Insurance + Claim
  Examiner suites already work; Employer/Attorney sections have no suite field).
- Impersonation role label differs by design (supervisor -> office `admin`; intake -> shadow
  `Intake Staff`); stakeholder wants a clearer label only, not an access-model change.

## Approach

Two PRs off `main`, PR1 first (small QA fixes), then PR2 (parity). One worktree
(`appointment-qa`), a descriptive branch per PR. Verification: unit/build for backend and
frontend logic in-worktree; a single live browser pass for the UI items once a dev stack is
available (second stack on offset ports via `add-worktree.sh`, or pause `main`).

Rejected: bundling all five into one PR (delays the HIGH-severity email fix behind the larger
parity work); adding new suite fields to Employer/Attorney (stakeholder declined).

## Tasks

### PR1 -- QA quick fixes (branch: fix/appointment-qa-quickfixes) -- MERGED #343 (squash 6845c0c0)

- T1 (item 2, HIGH -- accessor email): fix the dead password link + empty location.
  - approach: test-after
  - files: src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/AccessorInvitedEmailHandler.cs
    (+ a render-level test in test/HealthcareSupport.CaseEvaluation.Application.Tests)
  - detail: pass BARE substitution keys `["URL"]` / `["Email"]` (currently `["##URL##"]`/`["##Email##"]`,
    which the substitutor double-wraps into `####URL####` -> renders literally -> dead "Set your
    password" button). Resolve the practice name from `appointment.TenantId` (as
    DocumentEmailContextResolver.cs already does) instead of `_currentTenant.Name`, which is null
    inside the `Change(tenantId)` scope -> "at ." Location = practice/doctor name per stakeholder.
  - acceptance: a render-level test proves the sent body contains a real set-password URL, the
    invited email, and "for {patient} at {practice}" with a non-empty practice name; no literal
    `##...##` tokens remain.

- T2 (item 3 -- registration rate cap): raise the per-IP registration limit.
  - approach: code
  - files: src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs (signup partition ~L664-673)
  - detail: raise `PermitLimit` from 5 to 15 for the `signup:` fixed-window partition (per-IP, 1h
    window). Login is unchanged (already failure-only via ABP lockout, 10 fails/user/hr).
  - acceptance: the signup limiter permits 15/hr/IP; no other limiter regressed.

- T3 (item 4a -- Patient "Unit #" requestable): wire the existing Patient address-line-2 into request-more-info.
  - approach: test-after
  - files: angular/src/app/appointments/appointment/send-back-fields.ts (Patient group),
    src/HealthcareSupport.CaseEvaluation.Application/AppointmentInfoRequests/InfoRequestFields.cs (Patient specs)
  - detail: add the Patient "Unit #" (control `address` -> `Patient.Address`) to both the frontend
    flaggable registry and the backend correction registry, matching the existing Insurance/Claim-Examiner
    suite entries. Do NOT confuse with `patientApptNumber` (Patient.ApptNumber), which is not an address field.
  - acceptance: staff can flag Patient "Unit #" in the request-info modal; a correction to it writes back
    to Patient.Address (backend unit test on InfoRequestFields parse/apply).

- T4 (item 5 -- impersonation relabel): make the header label clear during impersonation.
  - approach: code
  - files: angular/src/app/.../internal-shell-layout.component.ts/.html (role label + impersonation state)
  - detail: keep the access model unchanged. When impersonating, show the operator's own internal role
    plus an impersonation indicator (e.g. "Supervisor -> viewing as Office Admin") rather than a bare
    "Administrator". Exact wording confirmed with stakeholder during build.
  - acceptance: header clearly distinguishes a supervisor-impersonation session from a genuine office admin;
    intake impersonation still reads as intake. Verified live in the browser.

### PR2 -- appointment view/review field parity (branch: fix/appointment-view-field-parity)

Research correction (2026-07-10): the Custom Fields section is NOT frontend-only. Saved custom-field
answers (`CustomFieldValue` rows) never cross the API outbound -- the read DTO
`AppointmentWithNavigationPropertiesDto` omits them and no GET endpoint exposes them (only the
Create/Update inputs carry `CustomFieldValueInputDto`). Stakeholder chose (2026-07-10) to add a small
additive read endpoint in PR2 so both views show real saved values -> TRUE parity. The other ~38 external
fields ARE already client-side (parent `form` + nav-props), so that half stays frontend-only.

- T5a (backend -- custom-field values read endpoint): expose saved custom-field values for an appointment.
  - approach: test-after (pure static mapping helper + unit test; ABP integration host crashes on the
    license blocker, so no DB-seeding integration test -- matches the T1/T3 pattern)
  - files: src/HealthcareSupport.CaseEvaluation.Application.Contracts/CustomFields/CustomFieldValueDisplayDto.cs (new),
    src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/IAppointmentsAppService.cs,
    src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs
    (new GetAppointmentCustomFieldValuesAsync + static BuildCustomFieldDisplay helper; inject
    ICustomFieldRepository), + a unit test in test/.../CustomFields
  - detail: GetAppointmentCustomFieldValuesAsync(appointmentId) reuses EnsureCanReadAsync (same
    `[Authorize]` gate as GetWithNavigationPropertiesAsync), loads the ACTIVE CustomFields for the
    appointment's type (mirrors CustomFieldsAppService.GetActiveForAppointmentTypeAsync -- AppointmentTypeId
    match + IsActive, ordered by DisplayOrder), LEFT-joins the appointment's saved CustomFieldValue rows,
    and returns {customFieldId, fieldLabel, fieldType, value(nullable), displayOrder}. Conventional route
    mirrors GetAppointmentDefenseAttorneyAsync -> GET /api/app/appointments/{appointmentId}/custom-field-values.
  - proxy (hand-added): appointment.service.ts getAppointmentCustomFieldValues + custom-fields/models.ts
    CustomFieldValueDisplayDto.
  - acceptance: unit test proves the helper orders by DisplayOrder, left-joins (unanswered field -> null
    value), and returns [] for no active fields.

- T5b (item 1a -- internal view Custom Fields): add the missing Custom Fields / "Additional Details" section
  to the internal appointment detail component (fed by T5a).
  - approach: test-after
  - files: angular/src/app/appointments/appointment/components/internal-appointment-detail.component.html
    (+ shared fetch on appointment-view.component.ts parent so both views load it once)
  - acceptance: internal view renders every wizard field incl. Custom Fields (filled or empty).

- T6 (item 1b -- external view full parity): surface all currently-dropped fields on the external detail
  component (38 fields + Custom Fields), filled or empty. Stakeholder confirmed external parties may see all
  party details.
  - approach: test-after
  - files: angular/src/app/appointments/appointment/components/external-appointment-detail.component.{ts,html}
    (+ a pure custom-field-display.util.ts value formatter with a karma spec)
  - acceptance: external view renders all ~63 fields (filled or empty), matching the wizard/review set.

## Risk / Rollback

- Blast radius: PR1 T1/T2/T3 are small and backend-config-bounded; T4 + most of PR2 are Angular template/label
  changes. PR2 adds ONE additive backend read endpoint (T5a: GetAppointmentCustomFieldValuesAsync + display DTO)
  -- no entity/schema change, no EF migration, no write path. Existing ~1300-test backend suite guards regressions.
- T1 is a regression-fix for a live lockout (invited accessors currently get a dead link); prioritize.
- External-view expansion (T6) exposes all party details to external users -- confirmed intended by stakeholder.
- Rollback: revert the branch; all changes additive/self-contained.

## Verification

- Backend (T1, T2, T3-backend): `dotnet test` Domain + Application suites in the worktree.
- Frontend (T3-frontend, T4, T5, T6): `yarn lint` + `ng build` + karma component tests.
- Live browser pass (T4, T5, T6): once a dev stack is available -- impersonate as supervisor and intake to
  confirm the T4 label; open an appointment on both internal + external views to confirm parity; open the
  request-info modal to confirm the Patient "Unit #" option. Needs a second stack on offset ports
  (`add-worktree.sh`) or pausing the `main` stack (WSL RAM ceiling ~2 concurrent stacks).
