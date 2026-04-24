# Scope answers -- LOCKED 2026-04-24

**Phase 4 output, updated after Q&A.** All 20 open scope questions are answered. No capability is blocked on an open question. This file now documents the LOCKED answer for each question + its impact on the plan.

## Q&A session outcomes

12 live questions + 8 batch-accepted defaults, closed 2026-04-24 via interactive Q&A with Adrian. Reference: the Q&A transcript was synthesised into `C:/Users/RajeevG/.claude/plans/you-are-picking-up-vast-papert.md` (plan file, ExitPlanMode approved).

## Locked answers by question

### Q1 / Q5 -- 13-state lifecycle
**Answer: partial enforcement; MVP state subset.**
- IN: `Pending`, `Approved`, `Rejected`, **`MoreInfoRequested` (new, not in OLD's 13)**, `RescheduleRequested`, `CancellationRequested`, `CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`, `RescheduledLate`.
- OUT: `CheckedIn`, `CheckedOut`, `Billed`, `NoShow`.
- Impact: `appointment-state-machine` effort drops from M (3d) to S-M (1.5d). New `MoreInfoRequested` state needed.

### Q21 + Q22 -- Internal roles + external permissions
**Answer: 3 internal tiers + baseline grants seeded for all.**
- Internal roles (host-scoped for IT Admin, tenant-scoped for the others): IT Admin, Staff Supervisor, Clinic Staff.
- External roles (tenant-scoped): Patient, Claim Examiner, Applicant Attorney, Defense Attorney.
- Permission matrix locked (see detailed matrix in the Q&A plan file or the `internal-role-seeds` brief revision).
- Key constraints:
  - Patient role: `Patients.Default` + `Patients.Edit` (own profile); NOT `Patients.Create`, NOT `Appointments.Edit`.
  - Claim Examiner, Applicant Attorney, Defense Attorney: get `Patients.Create` to onboard clients.
  - All 3 internal tiers can `Edit` appointment-change-requests (approve / reject / more-info).
  - Slot generation (`DoctorAvailabilities.Create`): Staff Supervisor + IT Admin only.
  - Hard-delete: IT Admin only.
  - Defense Attorney gets `Appointments.Create`.
- Impact: `internal-role-seeds` effort rises from S to S-M (~1-2d); permission-matrix authoring is a real design task, not a copy-paste.

### Q1 / Q2 -- Attorney entity model
**Answer: split into separate entities (Option B).**
- Keep existing `ApplicantAttorney` (WCAB-named, plaintiff side).
- Add new `DefenseAttorney` entity parallel to it.
- Add new `AppointmentDefenseAttorney` join, parallel to existing `AppointmentApplicantAttorney`.
- Defense Attorney role gets permissions on: `DefenseAttorneys.*`, `AppointmentDefenseAttorneys.*`.
- Impact: `attorney-defense-patient-separation` effort rises from S-M to M (~5 days). Two parallel entity surfaces instead of one.

### MVP capability scope (composite answer to scope-drop candidates)
**Answer: 5 IN-MVP, 4 OUT of MVP.**

IN-MVP:
- `document-packages` (OLD has scaffold; admin CRUD only, no runtime auto-attach).
- `user-query-contact-us` (patient-initiated "contact admin" form for form-data change requests).
- `custom-fields` (reinterpreted: per-AppointmentType field VISIBILITY/PRE-FILL/DISABLE config; NOT dynamic form builder).
- `appointment-documents` (OLD full scaffold present; core upload + admin view/download + status).
- `appointment-injury-workflow` (injury + body parts + claim examiner + primary insurance sub-forms; rationale: these ARE the "insurance / injury / claim examiner" sub-sections on the appointment + re-evaluation forms).

OUT of MVP:
- `appointment-notes` (post-MVP; OLD's list UI was a ghost `<h1>Note</h1>` placeholder anyway).
- `anonymous-document-upload` (post-MVP; no current use case).
- `joint-declarations` (post-MVP; no current use case).
- `appointment-request-report-export` (post-MVP; admins filter the list UI but no CSV/XLSX download).

### Q17 -- Blob storage
**Answer: Option C (hybrid).** DB BLOB for MVP; config-only swap to S3 once AWS BAA + IAM are in place. No code rework required for the migration.

### Q18 -- Background jobs runtime
**Answer: Option B (Hangfire in Wave 0 mandatory).**
- Rationale: Adrian's Q8 answer revealed legal-mandated recurring jobs for appointment-schedule enforcement, which require Hangfire from day one (ABP's built-in one-shot is insufficient).
- Impact: `background-jobs-infrastructure` promoted from deferred to Wave 0 mandatory; ~2-3d wiring effort.

### Q7 -- Email template source
**Answer: Option A.** ABP TextTemplateManagement (already wired). Template bodies temporarily copied from OLD's `wwwroot/EmailTemplates/*.html`; `##token##` translated to Scriban `{{ token }}`. Admins edit via `/text-template-management/**` UI post-MVP.

### Q8 -- System parameter strategy + settings
**Answer:**
- Part A: Option A2 (ABP SettingManagement; no port of OLD's `SystemParameter` entity).
- Part B: Settings IN-MVP: SMTP config, `CcEmailIds`, default timezone, clinic contact info, `AppointmentLeadTime`, max-time limits per AppointmentType, `AutoCancel-cutoff`, reminder-cadence (legal values; see Q9).

### Q9 -- Legal / regulatory recurring jobs (new question, not in original README 32)
**Answer: scoped framework, values pending Adrian's legal staff + manager sign-off.**

3 Hangfire recurring jobs for MVP:
1. `RequestSchedulingReminderJob` (daily 08:00 local) -- CCR 8 Sec. 31.5 90-day rule; reminders at day 30 / 60 / 75 / 85 / 90.
2. `CancellationRescheduleReminderJob` (daily 08:00 local) -- CCR 8 Sec. 34(e) 60-day reschedule rule; reminders at day 45 / 55.
3. `AppointmentDayReminderJob` (daily 07:00 local) -- clinic UX; emails at T-7 days + T-1 day.

4 UI validations:
- Admin cancel-on-behalf-of-doctor with < 6 business days: warning + require good-cause reason (CCR 8 Sec. 34(d)).
- Patient / attorney cancel or reschedule with < 6 business days: warning + require good-cause reason; oral cancellations must be confirmed in writing within 24 hours (CCR 8 Sec. 34(h)).
- Admin unavailability edit with < 30 days notice: warning banner (CCR 8 Sec. 33).
- Doctor cumulative unavailability approaching 120 days/year: block further unavailability (CCR 8 Sec. 33).

10 ABP Settings (keys; legal values pending manager sign-off):
- `CaseEvaluation.Schedule.InitialSchedulingDaysLegalLimit` (default 90, CCR 8 Sec. 31.5)
- `CaseEvaluation.Schedule.InitialSchedulingDaysExtendedLimit` (default 120, CCR 8 Sec. 31.5 with waiver)
- `CaseEvaluation.Schedule.RescheduleAfterCancelDays` (default 60, CCR 8 Sec. 34(e))
- `CaseEvaluation.Schedule.PartyCancelMinBusinessDays` (default 6, CCR 8 Sec. 34(h))
- `CaseEvaluation.Schedule.QMECancelMinBusinessDays` (default 6, CCR 8 Sec. 34(d))
- `CaseEvaluation.Schedule.AppointmentNoticeBusinessDays` (default 5, CCR 8 Sec. 34(a))
- `CaseEvaluation.Schedule.QMEUnavailabilityNoticeDays` (default 30, CCR 8 Sec. 33)
- `CaseEvaluation.Schedule.QMEUnavailabilityMaxDaysPerYear` (default 120, CCR 8 Sec. 33)
- `CaseEvaluation.Schedule.RequestReminderDays` (default "30,60,75,85", job 1 cadence)
- `CaseEvaluation.Schedule.PatientReminderDaysBefore` (default "7,1", job 3 cadence)
- `CaseEvaluation.Schedule.ClinicTimeZone` (default "America/Los_Angeles")

Post-MVP (not covered by MVP state-machine subset): 30-day report-service rule (CCR 8 Sec. 38), extension-notice rule, missed-appointment-fee liability. All depend on check-in/check-out/bill which are OUT of MVP.

Research source: DIR.ca.gov CCR Title 8 Sec. 33, 34, 31.5, 38. Accessed 2026-04-24.

### Q14 -- Change-log audit
**Answer: Option A + dedicated screen.** Rely on ABP's native `AbpEntityChanges` + `AbpEntityPropertyChanges` tables. Tag Appointment, AppointmentChangeRequest, Patient, ApplicantAttorney, DefenseAttorney, AppointmentInjuryDetail, etc. (~8 entities) with `[Audited]` attribute. Add thin Angular component at `/appointments/view/:id/change-log` that calls `/api/audit-logging/audit-logs/entity-changes?EntityTypeFullName=Appointment&EntityId=<guid>`. Effort: S-M (~2.5d).

### Batch defaults accepted (8 items)
All defaults locked:
1. Email sender: ABP `SmtpEmailSender` + AWS SES SMTP on port 587 (STARTTLS).
2. SMS (Q CC-02): OUT of MVP. Track-10 erratum 2 confirms OLD's SMS is 100% disabled in production.
3. Token lifetime (Q19): OIDC default (1h access + 14d refresh). OLD's 12h was replaced.
4. Single-device login (Q20): NOT enforced. Multi-device login is OK.
5. ADR-006 cascade-delete: write it (0.5d doc only).
6. `appointment-request-report-export` (Q12 / 03-G11 / G-API-13): OUT of MVP.
7. Seed strategy (Q23): code-first `IDataSeedContributor` for reference entities (States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, WcabOffices); synthetic 2-row demo seed for Locations; PROD HCS clinic snapshot is a deployment-time concern (post-MVP).
8. Book demo (Q32): safe to remove post-MVP; 0 effort in MVP.

### External-user signup flow (new question, not in README)
**Answer: email-invite only.**
- Admin (IT Admin or tenant-scoped Staff Supervisor / Clinic Staff) creates pending `IdentityUser` via `/identity/users`, selects tenant + role + name + email.
- System generates password-reset token; emails invite link.
- User clicks link, sets password, completes registration.
- For Patient role invite: system ALSO creates `Patient` entity via `PatientManager.FindOrCreateAsync` (patient-auto-match logic prevents duplicates).
- Self-login (post-registration) remains ABP Account standard.
- Anonymous self-signup via `ExternalSignupAppService.RegisterAsync` is REMOVED (not just NEW-SEC-04 "fix hardcoded defaults"; the endpoint is gutted).
- Impact: rewrites `new-sec-04-external-signup-real-defaults` brief; elevates `appointment-accessor-auto-provisioning` from specialised accessor-only method to canonical external-user invite flow.

### Remaining process items (confirmed by Adrian)
- Q29 PROD schema parity: Adrian provides sys.tables + sys.procedures before launch cut-over.
- Q31 Track-9 follow-up UI capture: already in-flight in a parallel Claude session.
- Q32 Book demo feature: safe to remove post-MVP; 0 effort in MVP.
- Q25-27 security / compliance: acknowledged, no OLD anonymous-endpoint replication, AWS keys already flagged for rotation.

## Nothing is blocked on scope answers anymore

Every capability's `Blocked by open question` field is satisfied. Phase 3 dependency graph stands modulo the effort adjustments captured in `dependencies.md` (Hangfire promotion + capability IN/OUT changes + brief-specific effort recalibrations).
