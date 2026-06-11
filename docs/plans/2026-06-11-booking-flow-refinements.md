---
name: booking-flow-refinements
date: 2026-06-11
status: in-progress
branch: feat/booking-flow-refinements
base: main @ aa4014c (#307)
scope: (A) grant Intake Staff slot CRUD; (B) patient lookup = scoped 2-char email search (no default list) for all on-behalf bookers + CE scoping; (C) patient email optional with AA fallback
---

# Plan: booking-flow refinements (3 small tasks)

Locked decisions (Adrian, 2026-06-11):
- **Q1:** Intake Staff gets DoctorAvailabilities **Create + Edit + Delete** (full slot management).
- **Q2:** the no-default 2-char email search applies to **all on-behalf bookers** (AA/DA/CE + internal staff); internal staff lose the preloaded list too.
- **Q3:** **explicit substitution** -- when patient email is empty, patient-targeted notifications resolve to the Applicant Attorney's address.
- Email-required rule: required only when the patient books for themselves (`!isExternalUserNonPatient`) OR self-represented (`!applicantAttorneyEnabled`); optional otherwise.

## Current state (verified 2026-06-11)
- **A:** `IntakeStaffGrants()` (`InternalUserRoleDataSeedContributor.cs:434-537`) grants `DoctorAvailabilities.Default` only (read). Create needs `CaseEvaluation.DoctorAvailabilities.Create` (`DoctorAvailabilitiesAppService.CreateAsync` + Angular Add button + route guard). Reserved for Supervisor/IT Admin today.
- **B:** `GetPatientLookupAsync` (`AppointmentsAppService.cs:376`) scopes AA (`CreatorId` or `AppointmentApplicantAttorney.IdentityUserId`) + DA; searches `Patient.Email.Contains(filter)`; `DisplayName=Patient.Email` (mapper:377). **CE unscoped** (sees all tenant patients). No min-length/no-default guard. Frontend: static `<select>` from `patientListCache` preloaded via `loadPatientListCache()` -> `getPatientLookup({filter:''},max 500)` (`appointment-add.component.ts:2407`); rendered in `appointment-add-patient-demographics.component.html:17-26` under `isExternalUserNonPatient`.
- **C:** patient email already optional server-side (DTO no `[Required]`; `AppointmentCreateDtoValidator:53-55` format-only-when-present; `Appointment.PatientEmail`/`Patient.Email` nullable). Required only on the client: control `Validators.required` (`appointment-add.component.ts:508`) + new-patient submit guard `requiredForNew` (line 1800). `AppointmentRecipientResolver` skips empty patient email silently and already resolves `appointment.ApplicantAttorneyEmail` separately.

## Part A -- Intake Staff slot CRUD
- **A1 [code]** `IntakeStaffGrants()`: add `Create("DoctorAvailabilities")`, `Edit("DoctorAvailabilities")`, `Delete("DoctorAvailabilities")` (Default already present). Verify the data seeder re-applies grants to the EXISTING Intake Staff role on db-migrator re-run (ABP permission seeding is idempotent/additive); confirm via click-test (clistaff1 can create a slot).
- **A2 [code]** Verify the Angular doctor-availability route/menu guard permission so Intake Staff can navigate to the slot page once granted Create; adjust only if the route guard requires a permission Intake Staff still lacks.

## Part B -- scoped patient email search
- **B1 [code]** `GetPatientLookupAsync`: (1) return empty `PagedResultDto` when `input.Filter` is null/whitespace or trimmed length < 2 (no default list, min 2 chars -- PII guard, applies to all callers); (2) add CE scoping -- when `CurrentUser.IsInRole("Claim Examiner")`, restrict to patients on appointments where `Appointment.ClaimExaminerEmail == CurrentUser.Email` (mirror the existing email-match read-scope at ~line 251). AA/DA scoping unchanged; internal staff stay unscoped but still subject to the 2-char guard.
- **B2 [code]** Frontend: replace the static `<select>` (B section) with `<abp-lookup-select [getFn]="..." formControlName="patientId" (change)=...>` (precedent: the appointment-list filters use `LookupSelectComponent` with `getFn`). Pass `getPatientLookup` from the parent as an Input to the demographics section; remove the `loadPatientListCache()` preload + `patientListCache`. Server guard delivers "no default + 2-char + email match"; DisplayName already = email.
- **B3 [test]** Unit-test the min-length guard (pure check) if extractable; CE scoping is integration-level -> verify via click-test.

## Part C -- optional patient email + AA fallback

**DEVIATION (2026-06-11, found during build):** the original C3 (substitute the AA
in `AppointmentRecipientResolver`) is WRONG. Investigation of every patient-facing
handler shows:
- StatusChange, Document Accepted/Rejected/Uploaded, and AppointmentReminder all
  dispatch via the **booker/CC model** (`BookerCcDispatcher` / per-handler To+CC):
  To = booker (or uploader), CC = the resolved parties. The AA is always a resolved
  party (via the `AppointmentApplicantAttorney` link OR
  `Appointment.ApplicantAttorneyEmail`), so the AA ALREADY receives these. A resolver
  substitution would be a first-wins dedup no-op (dead code).
- The ONE genuine gap is `PatientPacketEmailHandler` (the fillable patient packet):
  it addresses `ctx.PatientEmail` ONLY and **skips entirely when empty**
  (PatientPacketEmailHandler.cs:75-81). It does NOT use `AppointmentRecipientResolver`,
  so a resolver change cannot fix it. With no patient email, the patient packet
  reaches no one today.

Corrected C3/C4 below target the real gap. `ctx.PatientEmail` =
`patient?.Email ?? appointment.PatientEmail`; `DocumentEmailContext` carries no AA
email today, so add one.

- **C1 [code]** Frontend: conditional `email` validator -- required iff `!isExternalUserNonPatient || !applicantAttorneyEnabled`, else `[maxLength(50), email]` only. Re-evaluate on the AA-enabled toggle (extend the existing `applicantAttorneyEnabled` valueChanges subscriber) and on booker-role resolution.
- **C2 [code]** Frontend: drop `raw.email` from the new-patient `requiredForNew` submit guard (line 1800) under the same condition as C1.
- **C3 [tdd]** Backend `PatientPacketEmailHandler`: when `ctx.PatientEmail` is empty, fall back to the AA email so the patient packet still reaches the AA. Add `ApplicantAttorneyEmail` to `DocumentEmailContext` (populated from `appointment.ApplicantAttorneyEmail` in `DocumentEmailContextResolver`). Extract a pure `ResolvePatientPacketRecipientEmail(patientEmail, aaEmail)` helper and TDD it; skip the send only when BOTH are empty.
- **C4 [code]** Confirm no other server-side patient-only required-email site (handlers 1-3 above already CC the AA; the resolver already skips empty patient email silently). No DTO change, no resolver change.

## Migration / proxy
- **No EF migration** (Part A = seeded permission grants, applied by db-migrator re-run; B/C = behavior, no schema). **No proxy regen** (no DTO shape change). Deploy = rebuild api + db-migrator (perm grant) + angular; no `abp generate-proxy`.

## Test plan
- Backend: pure unit tests for the lookup min-length guard + the empty-patient-email -> AA substitution (where extractable). Full Application.Tests green. Build via Docker.
- Click-test: (A) Intake Staff (clistaff1) creates/edits/deletes a slot; (B) AA/DA/CE see only shared patients, no list until 2 chars, search by email; internal staff also search-only; (C) AA books a patient with no email -> submit succeeds, status email reaches the AA.

## Risks / rollback
- A: front-desk can now delete slots (Adrian chose full CRUD) -- blast radius = role grant; rollback = remove the grants + re-seed.
- B: internal staff lose the preloaded patient list (now search-only) -- approved (Q2). Server empty-guard prevents any default PII dump.
- C: if AA present but AA email also empty, neither is emailed -- mitigated because the AA section's required-validators force AA email when the AA section is enabled.
- Rollback: revert the PR; no migration to undo.

## STOP-and-report gates
1. After this plan -> STOP for Adrian's approval before code. (current)
2. Before any Docker rebuild -> `docker compose ps`; coordinate.
3. Build + unit tests green -> STOP, summarize, wait before commit/push/PR.
