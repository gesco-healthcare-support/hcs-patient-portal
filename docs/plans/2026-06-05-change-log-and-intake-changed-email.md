---
feature: change-log-and-intake-changed-email
date: 2026-06-05
status: in-progress
base-branch: feat/replicate-old-app
related-issues: []
---

## Goal

Replicate OLD's appointment change-log (global + per-appointment, incl. child-entity
changes) and the "intake changed" notification email on the NEW stack, built on ABP audit
logging, with HIPAA-safe server-side PHI redaction.

## Context

Phase 3 / Group K (records G-02-01, G-02-02, G-02-03, G-10-05). OLD kept a custom
`spm.AppointmentChangeLogs` table (FieldName/TableName/OldValue/NewValue/ChangedById/
ChangedDate/IsInternalUserUpdate/IsMailSent), diffed appointment + child views by
reflection, showed a filterable list, and emailed a per-field old/new diff table to all
stakeholders on every intake edit. **OLD stored and emailed raw SSN/DOB/names with zero
redaction -- a HIPAA regression we will NOT port.**

NEW already has: ABP audit logging enabled (`[Audited]` on `Appointment` + the 4 children
`AppointmentInjuryDetail`, `AppointmentBodyPart`, `AppointmentClaimExaminer`,
`AppointmentPrimaryInsurance`); a per-appointment change-log viewer
(`angular/src/app/appointments/appointment-change-logs/`) that calls
`AuditLogsService.getEntityChangesWithUsername({ entityId, entityTypeFullName })` with the
`Appointment` FQN hardcoded; `SsnVisibility.MaskToLast4`; the in-process notification
dispatcher + email handler pattern (`Application/Notifications/Handlers/*`) + Hangfire
`SendAppointmentEmailJob`; `docs/decisions/009-audited-ssn-reveal.md`.

### Locked decisions (Adrian 2026-06-05)

1. **Architecture** -- lean on ABP audit (`EntityChange` / `EntityChangeDetail`) for the
   change-log VIEW; compute the field diff IN-MEMORY at update time for the EMAIL. No custom
   `AppointmentChangeLogs` table, no `IsMailSent` latch -- one email per save transaction =
   natural dedup (G-10-05 satisfied structurally).
2. **PHI** -- server-side field ALLOWLIST. Non-sensitive fields show old/new; sensitive
   fields are masked / shown as "updated" with no values. Redact at the boundary, before any
   DTO leaves the AppService and before any email body is rendered.

## Approach

- **Single redaction policy, single diff engine (T1).** One pure, unit-tested source of
  truth -- `AuditFieldPolicy` (an allowlist keyed by `{entityType, propertyName}`) + a
  `FieldDiff` helper. Both the audit-VIEW path (T2) and the email path (T4) call it, so the
  UI and the email can never disagree on what is safe to show. PHI = security path -> TDD.
- **Audit VIEW = ABP `EntityChange` aggregation (T2).** A new
  `IAppointmentChangeLogAppService` queries ABP's audit store via `IAuditLogRepository` for
  the appointment's own `EntityChange` rows PLUS the rows of its child entity ids (resolved
  from the appointment), maps them to a flat, entity-labeled, REDACTED
  `AppointmentChangeLogDto`, and exposes a filtered/paged global list. This closes G-02-02
  (child changes) and G-02-01 (global list) with one query. Reusing ABP audit means no new
  audit table or migration.
- **Email = in-memory diff at update (T4).** `AppointmentsAppService.UpdateAsync` loads the
  pre-update entity, computes the redacted diff against the incoming `AppointmentUpdateDto`,
  and publishes `AppointmentIntakeChangedEto` after commit; a new handler dispatches the
  `AppointmentChangeLogs` template to the appointment stakeholders, plus a one-shot reschedule
  email when the date/time field changed. One publish per save = no dedup latch.
- **Angular (T3)** points the existing viewer at the new aggregating endpoint (so child
  changes appear) and adds the internal-only global list page; proxy regenerated, not
  hand-edited.

### Alternatives rejected

- **Custom OLD-style `AppointmentChangeLogs` table + reflection diff + `IsMailSent`** --
  rejected: duplicates ABP's audit infra (two audit systems), adds an entity + manager +
  migration, and contradicts the "use the NEW framework natively" directive. (Adrian, locked.)
- **Show raw old/new values to internal users only (gated reveal)** -- rejected for this
  slice: two render paths + a second reveal-audit surface for marginal benefit; the allowlist
  already gives internal users the useful non-PHI deltas. Revisit only if staff ask.
- **Email child-entity (injury/body-part) edits too** -- out of scope for the email: on the
  view page those are read-only (booking is the canonical add/edit surface), so an intake
  edit via `UpdateAsync` touches appointment-row fields + custom fields only. Child changes
  still surface in the VIEW via ABP audit. Flagged as a refinement.

### PHI allowlist (the design deliverable)

Single source of truth in `AuditFieldPolicy`. Default = MASK (deny-by-default); only
listed fields SHOW old/new values. Matching is by entity type + property name.

- **SHOW old/new (non-sensitive):** `Appointment.AppointmentDate`,
  `Appointment.AppointmentTypeId`, `Appointment.LocationId`,
  `Appointment.DoctorAvailabilityId`, `Appointment.PanelNumber`, `Appointment.DueDate`,
  `Appointment.AppointmentStatus`, `Appointment.AppointmentLanguageId`,
  `Appointment.NeedsInterpreter`, `AppointmentInjuryDetail.DateOfInjury` (date only -- treat
  as non-PHI per OLD display) -- **confirm this last one with Adrian during build**.
- **MASK -> render as "updated" with no values (PHI / sensitive):** any patient name
  (`First/Middle/Last`), `SocialSecurityNumber`, `DateOfBirth`, address fields
  (`Address/Street/City/State/ZipCode`), `PhoneNumber`/`CellPhoneNumber`, all email fields
  (`PatientEmail`, `ApplicantAttorneyEmail`, `DefenseAttorneyEmail`, `ClaimExaminerEmail`),
  `ClaimNumber`, WCAB office/ADJ, body-part descriptions, injury narrative, examiner name,
  and ANY field not on the SHOW list.
- Internal book-keeping props (`*Id` audit noise except the FK display fields above,
  `*ModifiedDate`, concurrency stamps, `CreatorId`/`LastModifierId`) are dropped from the
  diff entirely (matches OLD's reflection skip-list).

## Tasks

- T1: Redaction policy + diff engine (pure, unit-tested).
  - approach: tdd
  - files-touched:
    - `src/.../Application/Appointments/Auditing/AuditFieldPolicy.cs` (static allowlist
      `ShouldShowValue(entityType, propertyName) : bool`; deny-by-default; the SHOW list above)
    - `src/.../Application/Appointments/Auditing/AuditFieldDiff.cs` (given a property name +
      old/new strings, return a redacted diff row: value shown only when policy allows, else
      `null`/"updated"; drops skip-list props)
    - `test/.../Application.Tests/Appointments/Auditing/AuditFieldPolicyTests.cs`
  - acceptance: tests prove every PHI field masks (SSN/DOB/name/address/phone/email/claim#),
    every SHOW field passes through, and skip-list props are dropped. No value ever leaks for
    an unlisted field (deny-by-default).

- T2: Audit-aggregation AppService (parent + child `EntityChange`, redacted, filterable).
  - approach: test-after
  - files-touched:
    - `src/.../Application.Contracts/AppointmentChangeLogs/AppointmentChangeLogDto.cs`,
      `GetAppointmentChangeLogsInput.cs` (filters: confirmation#/appointmentId, fieldName,
      entityType, changeType, status, date range; paging+sorting),
      `IAppointmentChangeLogsAppService.cs`
    - `src/.../Application/AppointmentChangeLogs/AppointmentChangeLogsAppService.cs`
      (`[RemoteService(IsEnabled=false)]`, extends `CaseEvaluationAppService`,
      `[Authorize(CaseEvaluationPermissions.AppointmentChangeLogs.Default)]`): resolve the
      appointment + its child entity ids, query ABP audit via `IAuditLogRepository`
      (`GetEntityChangeListAsync` for the list; `GetEntityChangesWithUsernameAsync` /
      per-entity for detail), map `EntityChange`/`EntityChangeDetail` -> redacted
      `AppointmentChangeLogDto` via T1, respect `EntityChange.TenantId`)
    - `src/.../HttpApi/Controllers/AppointmentChangeLogs/AppointmentChangeLogsController.cs`
      (`[Route("api/app/appointment-change-logs")]`)
    - Mapperly entries in `CaseEvaluationApplicationMappers.cs` if a mapper is needed
    - `test/.../EntityFrameworkCore.Tests/.../AppointmentChangeLogsAppServiceTests.cs`
  - acceptance: given seeded audit rows for an appointment + a child entity, the endpoint
    returns both, with PHI fields redacted per T1, scoped to the current tenant; the global
    list honors the filters + paging. **Confirm the exact `IAuditLogRepository` query method
    against ABP 10.0.2 at the start of this task (verify, do not assume).**

- T3: Angular -- child-aware viewer + global list page + proxy.
  - approach: code
  - files-touched:
    - `angular/src/app/appointments/appointment-change-logs/appointment-change-logs.component.ts`
      (call the new `/api/app/appointment-change-logs` endpoint by appointmentId instead of
      the hardcoded-FQN `AuditLogsService`, so child changes appear)
    - a new global list page under `angular/src/app/appointment-change-logs/` (internal-only
      route, filter bar, paged table, row -> appointment) reusing the
      `CaseEvaluation.AppointmentChangeLogs` permission guard
    - `angular/src/app/proxy/**` (regenerate; stage only real changes via
      `git diff --ignore-cr-at-eol`, revert EOL churn; never hand-edit)
    - route registration
  - acceptance: per-appointment viewer shows child-entity changes; global list filters +
    paginates; `npx ng build --configuration development` green; prettier-clean.

- T4: Intake-changed email (diff at update + dispatch + reschedule one-shot).
  - approach: test-after
  - files-touched:
    - `src/.../Domain.Shared/Notifications/Events/AppointmentIntakeChangedEto.cs` (carries the
      appointmentId, tenantId, and the pre-redacted diff rows)
    - `src/.../Application/Appointments/AppointmentsAppService.cs` `UpdateAsync` (load the
      pre-update entity, compute the redacted diff via T1, publish the ETO AFTER the UoW
      commits; skip publish when the diff is empty)
    - `src/.../Application/Notifications/Handlers/IntakeChangedEmailHandler.cs` (subscribe to
      the ETO; resolve stakeholders via the existing recipient pattern; dispatch the
      `AppointmentChangeLogs` template; when a date/time field is in the diff, also dispatch
      the reschedule template once)
    - `src/.../Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs` +
      `Localization/CaseEvaluation/en.json` (add/confirm the `AppointmentChangeLogs` template
      code + reschedule code + any new keys; template HTML asset)
    - `test/.../Application.Tests/.../IntakeChangedEmailTests.cs`
  - acceptance: an intake edit publishes exactly one ETO carrying only redacted rows; the
    handler dispatches one stakeholder email (+ one reschedule email iff date/time changed);
    no email when nothing changed; no PHI value appears in the rendered variables. Activated
    now behind the per-tenant notification setting (flag for Adrian).

- T5: Docs in the same change.
  - approach: code
  - files-touched:
    - `src/.../Application/AppointmentChangeLogs/CLAUDE.md` (new feature note: ABP-audit-backed
      view, redaction policy, no custom table, email-on-update model)
    - `docs/decisions/0NN-audit-change-log-redaction.md` (short ADR recording the allowlist +
      the diff-at-update email decision -- fills the never-written "Phase-0 audit-redaction
      spike")
  - acceptance: docs reflect the shipped design + the PHI allowlist.

## Risk / Rollback

- Blast radius: additive AppService + Angular page + one new ETO/handler + a diff hook inside
  `AppointmentsAppService.UpdateAsync`. The only change to an existing hot path is the
  post-commit diff+publish in `UpdateAsync`; guarded to no-op on an empty diff.
- Top risk: **PHI leakage** -- mitigated by deny-by-default allowlist (T1) unit-tested before
  any value is rendered, applied on BOTH the view and email paths. Second: ABP audit-query
  API shape -- de-risked by confirming `IAuditLogRepository` methods at the top of T2.
- Rollback: revert the branch; the feature is additive (no migration, no schema change). The
  email is behind a per-tenant notification setting that can be switched off.

## Verification

(End-to-end, on the Docker stack -- DEFERRED per Adrian until after Phases 3-4, but the
procedure stands.)
1. `dotnet test` -- T1 policy tests + T2 aggregation tests + T4 email tests green.
2. `npx ng build --configuration development` green.
3. Live: edit an appointment's date/time + a masked field (e.g. patient phone). Confirm:
   (a) the per-appointment viewer shows the date/time old/new and the phone as "updated"
   (no value); (b) the global list filters to the row; (c) one stakeholder email arrives
   showing the date/time delta and the phone masked, plus one reschedule email; (d) editing
   a child entity surfaces in the viewer; (e) no cross-tenant rows leak.
