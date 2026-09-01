# Demo-feedback implementation plan (ordered)

Status: awaiting approval (Checkpoint B). Compiled 2026-06-03 from the per-item research notes
in this folder. Decisions are locked (see each note's `## Decision`). This is the SESSION plan;
each item still runs its own RPE flow (`/feature-research` -> `/feature-design` ->
`/feature-build`) one at a time after approval, writing its own `docs/plans/YYYY-MM-DD-<slug>.md`.

Ordering rule: (1) hard dependencies first, (2) then impact/severity, (3) then low-effort quick
wins. Effort S/M/L; Risk Low/Med/High.

## At a glance

| # | ID | Title | Type | Effort | Risk | Depends on |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | IR1 | Internal roles 3-model (Staff Supervisor = top tenant, soft-delete) | enh | S | Low | - |
| 2 | AF1 | Three appointment types (AME/IME/PQME) + flat 60-day horizon | enh | M | Med | - |
| 3 | E1 | Email To+CC plumbing (single addressed message, supersede Decision 2.1) | enh | M | Med | - |
| 4 | AF2 | Remove AME-attorney booking gate | enh | S | Low | - |
| 5 | IP1 | Appointment Types CRUD for Staff Supervisor + read-guard fix | enh | S | Low | IR1 |
| 6 | IP2 | Appointment Languages CRUD for Staff Supervisor | enh | S | Low | IR1 |
| 7 | IP5 | Wcab Offices CRUD for Staff Supervisor | enh | S | Low | IR1 |
| 8 | UM2 | Internal-user create for Staff Supervisor | enh | S | Low | IR1 |
| 9 | E2 | Email body login/register CTA (one shared body) | enh | S | Low | E1 |
| 10 | E3 | Document emails To uploader + CC parties | enh | M | Med | E1 |
| 11 | UM1 | Invite External User: add First/Last name (+ greeting fix OBS-27) | enh | S/M | Low | - |
| 12 | AF3 | Panel Number blocked for AME/IME | enh | S/M | Low | AF1 |
| 13 | AF4 | Panel Number required for PQME | enh | S/M | Low | AF1 |
| 14 | AF5 | Panel-strike-list document flag (IsPanelStrikeList) | enh | S | Low | AF1 |
| 15 | AF7 | Pre-submit document upload (two-phase) + 25/10MB cap fix | enh | M | Med | AF1 |
| 16 | AF6 | PQME checkbox -> strike-list mandatory to submit | enh | S/M | Low | AF5, AF7 |
| 17 | CI1 | Insurance + Claim Examiner per-appointment (schema migration) | tech-debt | L | Med | UM3-coord |
| 18 | CI4 | Remove Attention field (bundle migration with CI1) | enh | S | Low | CI1 |
| 19 | CI2 | Claim Information repeatable, injury-only fields | enh | S/M | Low | CI1 |
| 20 | CI3 | ADJ# required per-injury | bug | S | Low | CI2 |
| 21 | IP6 | Patient model simplify + relocate to User Management | enh | L | Med | - (security sub-task first) |
| 22 | UM3 | Relocate attorneys + new Claim Examiner master CRUD | enh | L | Med | CI1-coord |
| 23 | UM4 | Attorney/CE CRUD mirror Patients + add name fields (BUG-042) | enh | M | Low | IP6, UM3 |
| 24 | IP3 | Doctor entity: hide page, fix drift, keep dormant | tech-debt | S/M | Low | - |
| 25 | IP4 | Locations: harden CRUD + validation | enh | M | Low | IR1 |
| 26 | AP1 | Review/Edit + Reschedule actions + supervisor approval pages | ux | L | Med | IR1 |

---

## Wave 1 - Foundations (unblock the rest)

**1. IR1 - Internal roles 3-model.** [IR1 note](IR1-internal-roles-three-model.md)
Foundation for all role-gated CRUD. Confirms 3 custom roles (IT Admin host, Staff Supervisor
top-tenant, Clinic Staff); retires the Volo admin as a presented persona (break-glass only);
grants Staff Supervisor soft-Delete on all tenant entities + `InternalUsers.Create` + create
peers. All deletes stay soft (PHI/audit standard). Grant-only edit (seeder + permissions
reconcile + re-seed); no migration. **Do first** - items 5-8, 25 fold their grants into this.

**2. AF1 - Three appointment types + 60-day horizon.** [AF1 note](AF1-three-appointment-types.md)
Establishes the PQME seed GUID that items 12-16 key off, and the type set everything else
assumes. Hard-deletes 4 seeded types (dev DB is disposable / re-seeded), adds IME, renames Panel
QME -> PQME, and replaces the per-type name-substring horizon router with a flat 60-day rule.
Risk Med: seed deletion + the horizon router rework (renaming away from the AME/PQME substrings
is what forces it).

**3. E1 - Email To+CC plumbing.** [E1 note](E1-appointment-request-to-cc.md)
Foundation for E2/E3 and the whole ex-parte email model. Adds the net-new CC field across
`SendAppointmentEmailArgs` / `SendAppointmentEmailJob` / `NotificationDispatcher`, converts the
appointment-request fan-out to one To+CC message, and supersedes Decision 2.1. Independent of
items 1-2, so can run in parallel with them across sessions.

## Wave 2 - Quick wins on the foundations

**4. AF2 - Remove AME-attorney gate.** [AF2 note](AF2-remove-ame-attorney-gate.md)
Small server-side deletion; also clears the OBS-23 / PROPOSED-BUG-A1 "internal error" confusion.

**5-7. IP1 / IP2 / IP5 - master-data CRUD grants.**
[IP1](IP1-appointment-types-crud-supervisor.md) / [IP2](IP2-appointment-languages-crud-supervisor.md)
/ [IP5](IP5-wcab-offices-crud-supervisor.md). CRUD already exists; these are the IR1 grant set
applied to AppointmentTypes/Languages/WcabOffices. IP1 also tightens the bare `[Authorize]`
read-anomaly. Fold into IR1's grant loop rather than hand-listing.

**8. UM2 - Internal-user create for Supervisor.** [UM2 note](UM2-internal-user-add-supervisor.md)
Form + nav already exist; grant `InternalUsers.Create` to Staff Supervisor (IR1) and keep the
creatable-role allow-list (Supervisor may create Supervisors + Clinic Staff).

**9-10. E2 / E3 - email body + document emails.**
[E2](E2-email-login-register-cta.md) / [E3](E3-document-emails-to-cc.md). Ride on E1's To+CC
mechanism. E2 consolidates to one shared "log in or register" body; E3 routes document
upload/approval/rejection To the uploader with the parties CC'd (office excluded on approve/reject).

**11. UM1 - Invite External User names.** [UM1 note](UM1-invite-external-add-names.md)
Adds First/Last name to the invite form + Invitation entity (migration, host+tenant contexts) +
the email greeting; fixes the "Hi ," empty greeting (OBS-27). Independent of E1.

## Wave 3 - Appointment-form conditional logic (after AF1)

**12-13. AF3 / AF4 - Panel Number conditional.**
[AF3](AF3-panel-number-blocked-non-pqme.md) / [AF4](AF4-panel-number-required-pqme.md). Two
halves of one state machine keyed off the PQME seed GUID: disabled/cleared for AME/IME; required
for PQME. Server + UI on both the add and view forms. Build together.

## Wave 4 - Documents (after AF1)

**14. AF5 - Panel-strike-list flag.** [AF5 note](AF5-panel-strike-list-concept.md)
New `IsPanelStrikeList` boolean on `AppointmentDocument` (small migration + proxy regen).

**15. AF7 - Pre-submit upload.** [AF7 note](AF7-presubmit-document-upload.md)
The linchpin for AF6. Two-phase create-then-upload reusing the existing endpoint + validation;
zero backend schema change; also fixes the 25MB/10MB client cap mismatch (BUG-025).

**16. AF6 - PQME mandatory strike-list gate.** [AF6 note](AF6-pqme-strike-list-checkbox.md)
PQME-only checkbox between Claim Information and Additional Authorized User; client-side
conditional validator blocks submit until a strike-list file is staged. Needs AF5 + AF7.

## Wave 5 - Claim Information restructure (schema; one migration)

**17. CI1 - Insurance + CE per-appointment.** [CI1 note](CI1-insurance-ce-per-appointment.md)
The heaviest data-model change: move both FKs from `AppointmentInjuryDetailId` to `AppointmentId`
+ backfill, dual-DbContext config, Mapperly + proxy regen, persist/read rework. Reverses OBS-17.
CE becomes first-class + required and converges on `Appointment.ClaimExaminerEmail`.
**Coordinate with UM3** (CE master entity).

**18. CI4 - Remove Attention.** [CI4 note](CI4-remove-attention-field.md)
DropColumn - **bundle into CI1's migration** to avoid two migrations on the same table.

**19. CI2 - Injury-only repeatable block.** [CI2 note](CI2-claim-info-repeatable-injury-only.md)
After CI1, each injury block holds only: Cumulative Trauma, Date of Injury, Claim Number, WCAB
Office, ADJ#, Body Parts. Removes the per-injury insurance/CE coupling.

**20. CI3 - ADJ# required per-injury.** [CI3 note](CI3-adj-required-per-injury.md)
Server (DTO + domain) + UI required, one ADJ# per Date-of-Injury/Claim-Number. Do with CI2 since
both touch the injury block.

## Wave 6 - Patients + Users

**21. IP6 - Patient model simplify + relocate.** [IP6 note](IP6-patients-relocate-and-simplify.md)
Record-only Patient; **kill the shared-password auto-create at booking (security - see callout)**;
link-by-email on registration; nullable `Appointment.IdentityUserId`; retire the admin New Patient
form; relocate under User Management (re-parent nav + re-path the 2 routes + 3 hardcoded
`navigateByUrl`). Upstream of UM4.

**22. UM3 - Relocate attorneys + Claim Examiner master.** [UM3 note](UM3-relocate-attorneys-add-claim-examiner.md)
Re-parent AA/DA nav under User Management; add a new ClaimExaminer MASTER entity + CRUD parallel
to AA/DA. Coordinate with CI1 (appointment-level CE). Feeds UM4.

**23. UM4 - Attorney/CE CRUD mirror Patients.** [UM4 note](UM4-attorney-ce-crud-mirror-patients.md)
Mirror the simplified Patients shape onto AA/DA/CE masters; **add First/Last name columns to
ApplicantAttorney/DefenseAttorney (BUG-042)**; drop required identityUserId on standalone create.
After IP6 + UM3.

## Wave 7 - Doctor, Locations, Appointment actions

**24. IP3 - Doctor entity hide/dormant/fix-drift.** [IP3 note](IP3-doctor-entity-hide-dormant-fix-drift.md)
Hide the Doctors nav/page; strip the stale IdentityUserId UI bindings + always-blank columns;
reconcile ADR-004 + design doc + CLAUDE.md. Keep tables for a possible future multi-doctor model
(out of scope now). Independent; low risk.

**25. IP4 - Locations harden CRUD/validation.** [IP4 note](IP4-locations-harden-crud.md)
Standard industry CRUD/validation: duplicate-name guard, ParkingFee non-negative, ZipCode format,
friendly pre-delete handling. Soft-Delete grant comes from IR1.

**26. AP1 - Review/Edit + Reschedule + supervisor approval pages.** [AP1 note](AP1-review-edit-reschedule-actions.md)
Largest UX item, but the reschedule/cancel BACKEND is already fully built (OBS-12 is UI-only).
Add a clear Edit affordance on the Review page, remove the generated CRUD Edit modal (BUG-039
Edit-half), build Reschedule + Cancel request modals (internal = auto-approve) and the supervisor
Pending Change Requests approval pages. ng-bootstrap. Type change = manual cancel + rebook.

---

## Cross-cutting notes

- **Security callout (elevate within IP6):** the booking auto-create mints an IdentityUser with a
  shared hardcoded admin password (SEC-05 / Q-12). The "stop setting the shared password" sub-task
  is a small, localized, no-migration fix and should ship FIRST within IP6 (or as a standalone
  early task) ahead of the larger record-only/nullable-FK rework.
- **Shared-file contention:** `appointment-add.component.ts` is touched by AF3/AF4/AF6/AF7 and
  CI1/CI2/CI3. Sequence those waves so each rebases on the prior; expect merge attention.
- **Migrations:** AF1 (seed delete), AF5 (IsPanelStrikeList), CI1+CI4 (FK move + DropColumn -
  one migration), UM1 (Invitation names), UM3/UM4 (ClaimExaminer master + AA/DA name columns),
  IP6 (nullable Appointment.IdentityUserId). All EF changes hit BOTH DbContexts.
- **Proxy regen** (`abp generate-proxy`) after any DTO/endpoint change: AF5, CI1, CI4, UM1, UM3,
  UM4, IP6. Never hand-edit `angular/src/app/proxy/`.
- **Re-seed** (DbMigrator) after IR1 and the IP1/IP2/IP5/UM2 grants so existing tenants pick them up.
- **Server vs UI enforcement** (approved H): integrity/security rules are server-side + UI
  (AF4, CI3, CE-required, 60-day horizon, role gates); pure affordances are UI-only (AF3 disable,
  AF6 submit gate). Each note states its split.
- **Doc reconciliation:** AF1/AF2 supersede OBS-10/OBS-23 framing; IP1/IP2/IP5/UM2 supersede
  master-data-crud-design.md IT-Admin-only + OLD-parity notes; CI1 supersedes OBS-17; IP3
  supersedes the ADR-004/design-doc IdentityUserId claims; UM3 supersedes OBS-8.
- **Deferred (not in this plan):** the AA-intermediary email refinement (patient gets only
  patient-relevant emails when an AA is present); a true multi-doctor-per-tenant model; a guided
  "Cancel & Rebook" flow for type change; destructive hard-delete/purge.

## Next step
On approval, run RPE per item in the order above (one at a time), starting with IR1. Each item's
note is the research input; `/feature-design` writes its `docs/plans/` file; `/feature-build`
executes; `/ship-plan` cleans up post-merge.
