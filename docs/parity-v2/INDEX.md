# Parity v2 -- OLD vs NEW gap analysis (single source of truth)

> **OLD** = `P:\PatientPortalOld` (legacy Patient Portal -- the behavioral source of truth, read-only).
> **NEW** = this repo (`feat/replicate-old-app`, .NET 10 / ABP Commercial / Angular 20).
> Generated 2026-05-29 by an exhaustive fresh re-read of both codebases
> (OLD: ~418 C# + ~952 Angular files; NEW: ~819 C# + ~172 Angular files).
> This set **replaces** the old `docs/parity/` tree (deleted; recoverable via git history).

---

## What this is

A behavioral-parity gap analysis. The mission is to replicate the OLD app's
**intent and behavior** on the new stack -- **not** its exact code, schema, or
feature decomposition. So:

- A **gap** is only raised when an OLD *user-visible capability or business rule*
  is missing, partial, or behaves differently in NEW.
- Pure **implementation** differences (SQL stored proc -> EF/LINQ, two tables ->
  one entity with a flag, custom RBAC tables -> ABP permissions, AWS -> Azure/MinIO,
  DOCX deliverable -> PDF, different Angular layout, renamed fields) are listed in
  each doc under **"Equivalent -- different implementation"** for coverage, and are
  **not** decisions you need to make.

## How to use it

Each area doc lists its gaps as `G-NN-MM` rows. Every gap has:
**Class** (Missing / Partial / Intent deviation) - **OLD** (`file:line`) -
**NEW** (`file:line` or *absent*) - **What it is** - **Why it existed** -
**What it does / user impact** - **Plain-English** - and a **`Keep in NEW?`**
checkbox: `( ) Yes ( ) No ( ) Decide later`.

Walk the docs, tick a box on each gap. Ticked answers become the to-build /
won't-build backlog for the rest of the replication.

---

## Roll-up

| # | Area | Doc | Missing | Partial | Intent dev. | Equivalent | OLD-bug |
|---|------|-----|:-:|:-:|:-:|:-:|:-:|
| 01 | Booking workflow | [01-booking.md](01-booking.md) | 4 | 3 | 1 | 12 | 3 |
| 02 | Approval / reschedule / cancel | [02-approval-reschedule-cancel.md](02-approval-reschedule-cancel.md) | 3 | 3 | 2 | 12 | 2 |
| 03 | Documents & packets | [03-documents-packets.md](03-documents-packets.md) | 3 | 2 | 1 | 9 | 5 |
| 04 | Emails & SMS | [04-emails-sms.md](04-emails-sms.md) | 4 | 3 | 3 | 8 | 4 |
| 05 | Jobs & scheduler | [05-jobs-scheduler.md](05-jobs-scheduler.md) | 1 | 3 | 1 | 6 | 3 |
| 06 | Auth & RBAC | [06-auth-rbac.md](06-auth-rbac.md) | 3 | 4 | 2 | 11 | 5 |
| 07 | Admin & master data | [07-admin-master-data.md](07-admin-master-data.md) | 5 | 4 | 4 | 9 | 3 |
| 08 | Reporting & export | [08-reporting-export.md](08-reporting-export.md) | 4 | 0 | 0 | 0 | 2 |
| 09 | Dashboard / notes / query | [09-dashboard-notes-query.md](09-dashboard-notes-query.md) | 3 | 3 | 1 | 4 | 2 |
| 10 | Data model | [10-data-model.md](10-data-model.md) | 9 | 4 | 3 | 14 | 2 |
| | **Total** | | **39** | **29** | **18** | **85** | **31** |

**~86 raw decision points** (Missing + Partial + Intent deviation). The true
number of *distinct* decisions is lower -- the data-model area (10) deliberately
mirrors feature-area gaps from the storage angle (see the de-dup map below), so
several rows are the same decision seen twice.

OLD bugs: **31 found, none need porting** -- each is either a defect NEW already
fixes or a dead/commented-out path in OLD. Listed per-area under "OLD bugs (do not
port)" so they are not mistaken for parity gaps.

---

## The biggest decisions (whole capabilities)

These are the large "do we want this at all" calls, not small rule tweaks:

1. **Reporting & export -- entirely absent in NEW** (08). No Appointment Request
   Report grid, no Excel export, no report-PDF, no per-appointment demographics
   print sheet. This was OLD's primary reporting surface for staff/admin.
   (`G-08-01..04`.) Two further reports -- Schedule Report and the Excel ODBC pivot
   link -- were *specified but never built in OLD*, so they are not parity gaps;
   build-or-not is a fresh scope call.
2. **Internal Notes** (09 / `G-10-01`). Threaded per-appointment staff notes with
   edit history. Fully coded in OLD but its launcher was commented out, so it was
   *latent* in OLD's shipped UI. Absent in NEW.
3. **Submit-Query / Contact-Us** (09 / `G-09-02`, `G-09-03`, email `G-04-02`).
   In-app "ask a question" channel that emailed staff. Absent in NEW.
4. **SMS channel** (04 `G-04-01` / 05 `G-05-INTENT-01`). All Twilio SMS legs are
   gone. In OLD, live SMS existed only for staff digests behind an `isSMSEnable`
   flag; all patient-facing SMS was already commented out. Email-only in NEW.
5. **Admin configuration UIs** (07 `G-07-01`). Backends + proxies exist, but there
   are **no Angular screens** to edit System Parameters, Custom Fields, Notification
   Templates, the Document catalog, Package Details, or per-type field configs.
6. **Document-type master library** (03 `G-03-01` / 07 `G-07-07` / 10 `G-10-09`).
   OLD had a managed document-category lookup; NEW uses a free-text name.
7. **Reval / re-request booking UI** (01 `G-01-07`). The back-end reval/re-submit
   endpoints exist but no screen reaches them and they would not pre-load the prior
   case's intake data (OLD copied patient/injuries/attorneys forward).

## Smaller behavioral gaps (rule-level)

- **Booking validations not enforced** (01): future/pre-birth injury dates
  (`G-01-01`), cumulative-trauma date range (`G-01-02`), cross-stakeholder email
  uniqueness (`G-01-05`), accessor role-conflict (`G-01-06`).
- **`IsPatientAlreadyExist` flag dropped on save** (01 `G-01-03`) -- NEW computes
  the returning-patient result then discards it; every appointment records "new".
- **Role x appointment-type matrix collapsed to AME-only** (01 `G-01-04` / 06
  `G-06-01`) -- OLD had a configurable per-role/per-type grid; NEW hardcodes the one
  AME-attorney rule. Matches intent *if* AME-attorney was the only real rule.
- **Reschedule-reject leaves an orphaned slot hold** (02 `G-02-04`) -- flagged as a
  likely bug from the slot-rework; the reserved slot is never released.
- **Change-log**: no global searchable change-log list (02 `G-02-01`); intake-change
  diff email + `IsMailSent` dedup gone (02 `G-02-03` / 04 `G-04-03`); child-entity
  field changes not logged (02 `G-02-02`).
- **Auth**: no single-session enforcement (06 `G-06-06`); registration seeds a
  Patient row with placeholder Gender/DOB/phone (06 `G-06-08`, HIPAA-adjacent);
  Adjuster/ClaimExaminer dual enum dead-end (06 `G-06-05`).
- **Emails**: password-changed receipt not wired (04 `G-04-04`); approval email does
  not CC the clinic-staff list (04 `G-04-06`).
- **Jobs**: JDF-specific reminder cadence folded into the package-doc job (05
  `G-05-02`); staged T-7/T-3/T-1 reminders fire once (05 `G-05-03`) -- OLD's real
  cadence lived outside the repo and cannot be confirmed from source.

## Cross-area de-dup map (same decision, seen from two angles)

| Decision | Behavioral area | Data-model row |
|---|---|---|
| Internal Notes | 09 `G-09-04` | 10 `G-10-01` |
| Submit-Query | 09 `G-09-02/03`, 04 `G-04-02` | 10 `G-10-02` |
| Document-type master | 03 `G-03-01`, 07 `G-07-07` | 10 `G-10-09` |
| Reval pairing (`ReEvalId`) | 01 `G-01-07`, 07 `G-07-02` | 10 `G-10-11` |
| Doctor e-signature storage | 03 (packet signature) | 10 `G-10-07` |
| Accessor as free contact vs registered user | 01 `G-01-06`, 06 | 10 `G-10-08` |
| Change-mail dedup (`IsMailSent`) | 02 `G-02-03`, 04 `G-04-03` | 10 `G-10-05` |
| SMS channel | 04 `G-04-01` | 05 `G-05-INTENT-01` |

---

## Coverage & provenance

- **Method:** ten parallel area auditors, each exhaustively reading its slice of
  OLD + NEW source (and OLD `Documents_and_Diagrams\` reference docs where useful),
  diffing behavior, and writing its own doc. Every gap cites `file:line` on both
  sides. Each doc's **Coverage** section lists the exact files reviewed.
- **Source of truth:** live code only. The prior `docs/parity/` audit set was *not*
  trusted as input (it was stale -- a 2026-05-15 snapshot pre-dating the slot-rework
  wave, doctor invariant, NEW-003 localization, and the self-represented modal); it
  is deleted and replaced by this set.
- **Open questions** that need a running app or OLD seed data to resolve are listed
  at the bottom of each area doc (e.g., whether OLD's `RoleAppointmentType` held any
  rule beyond AME; whether OLD's `isSMSEnable` was ever on in production; OLD's true
  reminder cadence).
- **Not gaps, by project mission:** DOCX -> PDF deliverables; AWS S3/SES -> MinIO or
  Azure/ACS; multi-tenancy scaffolding. These are intentional and recorded as
  Equivalent.
