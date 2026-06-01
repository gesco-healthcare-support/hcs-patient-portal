---
feature: emails-injured-worker-wording
date: 2026-05-29
status: in-progress
base-branch: main
related-issues: []
sequence: 1 of 6 (do first; avoids conflicts with packet-access-by-role which touches the same email subsystem)
branch: feat/emails-injured-worker-wording
---

## Goal

In outbound emails only: rename the product "patient portal" to "appointment
portal", replace every reference to the person "Patient" with "Injured Worker",
and fix the grammar so the result reads correctly.

## Context

Adrian directive: outbound emails should refer to the person as "Injured
Worker"; the product name "patient portal" / "Patient Appointment portal"
should become "appointment portal" / "Appointment portal"; grammar must be
correct (these are external-facing emails).

### How email actually works here (verified in code + live, 2026-05-29)

There is ONE wired email pipeline. The earlier belief that the 41 `.html`
templates were "deferred / not wired" was a half-truth -- they ARE wired, but
only as the SEED source; runtime reads the database.

- Pipeline: a handler (`BookingSubmissionEmailHandler`,
  `StatusChangeEmailHandler`, `PatientPacketEmailHandler`,
  `AttyCEPacketEmailHandler`, or `CaseEvaluationAccountEmailer`) ->
  `NotificationDispatcher` -> `NotificationTemplateRenderer.RenderAsync(code)`
  which calls `EfCoreNotificationTemplateRepository.FindByCodeAsync` and reads
  the **`dbo.AppNotificationTemplates.BodyEmail`** column ->
  `TemplateVariableSubstitutor` does `##token##` substitution -> Hangfire
  `SendAppointmentEmailJob` (runs in the **api** container) -> ABP MailKit ->
  SMTP.
- The **runtime source of truth is the DB table**, NOT the `.html` files.
- The 41 `EmailBodies/*.html` (+ `EmailSubjects.cs`) are embedded resources
  that **seed** the DB rows via `NotificationTemplateDataSeedContributor`.
  Proven: the DB `BodyEmail` for `AppointmentRequestedRegistered` is
  byte-identical to the `.html` file.
- The DB holds **64 codes: 41 "real"** (a `.html` file exists -> real HTML
  body) **+ 23 "stub"** (no file -> body is literally
  `<p>Stub body for {code}. Per-feature phases will replace...</p>`). Stubs
  would be sent verbatim if their event fires.
- Seeder overwrite rule (`NotificationTemplateDataSeedContributor.cs:103-117`):
  on each seed run it OVERWRITES a row only when the code is resource-backed
  AND the stored Subject/Body differs from the resource. Stub codes and
  IT-admin-edited non-resource rows are preserved.
- The templates are also editable at runtime by IT-Admins via
  `NotificationTemplatesAppService` (the "somewhere else" they live: the DB +
  admin editor).
- The only truly-unused mechanism is ABP's empty Scriban
  `CaseEvaluationTemplateDefinitionProvider` (`Domain/Emailing/`).
- `en.json` is NOT read by emails (confirmed) -- editing it changes the UI only.

### Why editing `.html` alone appears to do nothing (the real gotcha)

Embedded resources are compiled into the api/migrator image. Editing a `.html`
changes nothing at runtime until BOTH: (1) the image is rebuilt so the new
resource is embedded, AND (2) `DbMigrator` re-runs so the seeder overwrites the
existing DB rows. This is the source of the "deferred" confusion.

### Empirical evidence captured (2026-05-29)

- Registered `patient2@gesco.com` (Olivia Turner) live; the real inbox received
  the rendered `UserRegistered` body ("Hello, Olivia / ... Click here to
  verify"). Log: `SendAppointmentEmailJob: delivered
  (AccountEmailer/EmailConfirmationLink/...) to patient2@gesco.com`.
- Booked appointment A00004 (QME) live; the cascade delivered to real inboxes:
  `AppointmentRequested/{ApplicantAttorney,DefenseAttorney,ClaimExaminer}` ->
  `appatty1@/defatty1@/claimE1@gesco.com`, `AppointmentRequested/Patient` ->
  the booker account email, `BookingSubmitted/ApproveReject` -> staff
  (`stafsuper1@/clistaff1@gesco.com` + `.test` staff).

## Approach

- Edit the wording in the 41 `EmailBodies/*.html` files and the prose in
  `EmailSubjects.cs`. These are the seed source; the build phase enumerates
  exact hits by grep.
- Two distinct replacements, kept separate:
  1. **Product name** -- "patient portal" / "Patient Appointment portal" ->
     "appointment portal" / "Appointment portal" (prose, CTA button labels,
     subject suffixes). ~20 `.html` files + `EmailSubjects.cs` lines 29, 68.
  2. **Person reference** -- visible "Patient" / "patient" referring to the
     person (e.g. `<strong>Patient:</strong>` summary label, "A patient has
     submitted...") -> "Injured Worker", fixing the article ("A patient" ->
     "An injured worker") and any subject/verb agreement.
- **Propagate to the DB** (the runtime source): rebuild the api + db-migrator
  images and re-run the migrator so the seeder overwrites the 41 resource-backed
  rows with the new wording. New tenants get it from the same seed.
- Do NOT touch: merge tokens (`##PatientFirstName##`, `##PatientLastName##`,
  `##PatientFullName##`); C# const identifiers (`PatientAppointmentPending`,
  etc.); `en.json`; the 23 stub codes (no prose to change -- see Deferred).

**Alternatives rejected:**
- Blanket find-replace of "Patient": corrupts merge tokens, const names, and
  the Patient role symbol. Reject.
- Editing only the `.html` without a rebuild + reseed: runtime keeps sending the
  old DB rows. Reject (this is the documented gotcha).
- Editing `en.json`: emails do not read it; would leak the rename into the UI.
  Reject.

## Tasks

- T1: Rename product-name references in email text.
  - approach: code
  - files-touched:
    - src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/*.html (the ~20 files containing "patient portal" prose / CTA buttons)
    - src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailSubjects.cs (lines 29, 68 -- "- Patient Appointment portal")
  - acceptance: case-insensitive grep of EmailBodies + EmailSubjects for
    "patient portal" / "Patient Appointment portal" returns zero prose hits.

- T2: Rename person-role references and fix grammar.
  - approach: code
  - files-touched:
    - AppointmentRequestedOffice.html, AppointmentRequestedRegistered.html,
      AppointmentRequestedUnregistered.html (`<strong>Patient:</strong>` label),
      ClinicalStaffCancellation.html ("A patient has submitted..." + label),
      PatientAppointmentNoShow.html, plus any other prose "Patient" the
      build-phase grep surfaces
  - acceptance: visible person reference reads "Injured Worker"; "A patient" ->
    "An injured worker"; subject/verb agreement correct; no `##...##` token or
    const identifier altered.

- T3: Propagate the new wording to the runtime DB rows.
  - approach: code
  - files-touched: none new -- a build + re-seed step (rebuild api + db-migrator
    images so the new embedded resources ship; re-run the DbMigrator so
    `NotificationTemplateDataSeedContributor` overwrites the 41 resource-backed
    rows). If any target row was hand-edited by an IT admin, note that the
    reseed intentionally overwrites it.
  - acceptance: after `docker compose up -d --build`, a DB query of
    `dbo.AppNotificationTemplates.BodyEmail`/`Subject` for the edited codes
    shows the new wording; the 23 stub rows are unchanged.

## Risk / Rollback

- Blast radius: outbound email text only -- no UI, no API, no schema. Token,
  const, and en.json safety enforced by the T1/T2 acceptance greps. The reseed
  only rewrites the 41 resource-backed rows (stub/admin rows preserved).
- Rollback: revert the PR; rebuild + reseed restores prior text.

## Verification

Empirical (preferred, method proven 2026-05-29; uses real SMTP):
1. Rebuild + reseed.
2. Trigger a registration (account path) and a booking (appointment cascade) on
   `falkinstein.localhost`; confirm the received emails read "Injured Worker"
   and "appointment portal" with correct grammar, and `##` tokens still resolve
   to real names.
3. Or, without sending: query `dbo.AppNotificationTemplates` for the edited
   codes and read the rendered DB body.

## Deferred / related confusion (NOT this PR -- Adrian to handle later)

- The **23 stub codes** still render `<p>Stub body for X</p>`; when real content
  is authored for them, it must follow the same wording convention.
- The **template-source confusion** (DB-vs-file, the empty Scriban
  `CaseEvaluationTemplateDefinitionProvider`, the build+reseed coupling) is the
  cleanup Adrian flagged for a separate effort.
