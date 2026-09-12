# Backend Changes Required for the Redesign

Concrete list of backend work (ABP v10 / .NET, `CaseEvaluation.*`) needed for the redesigned frontend to integrate. Compiled by reviewing each redesigned page against its current Angular equivalent and the `proxy/` contracts. Tags: **[Schema]** entity/DB + migration · **[API]** new/changed endpoint · **[Behavior]** server rule change · **[Perm]** permission/role · **[Seed]** seed/config data.

## A. Roles & access model
1. **[Perm][Seed] Consolidate internal roles to three**: `IT Admin` (host), `Staff Supervisor`, `Intake Staff`. Merge/retire any separate "Tenant Admin" role — Staff Supervisor absorbs its grants. Migrate role assignments.
2. **[Perm][Behavior] Make Staff Supervisor host-scoped**: supervisors (like IT Admin) can switch between tenants and operate inside each (tenant-switch endpoint / host login with tenant impersonation). **Intake Staff stays tenant-scoped** — no switching.
3. **[Perm] Intake Staff**: revoke `Appointments.Delete`; keep create/view/reschedule/cancel; grant `UserManagement.InviteExternalUser` (already granted); no internal-user creation, no tenant management.
4. **[Behavior] Internal-user creation** (`internal-users` endpoint): allow **Staff Supervisor** to pick any tenant (today only IT Admin can); creatable roles remain Staff Supervisor + Intake Staff.
5. **[Schema][Behavior] Remove Doctor as an entity/role concept**: each tenant is one doctor's practice. Drop `Doctors` CRUD usage + `doctorId` scoping on availabilities (availabilities become tenant-level). Keep a migration that collapses existing per-doctor availabilities into the tenant.

## B. Statuses & appointment workflow
6. **[Schema][Seed][Behavior] 5-status model**: keep `Pending(1)`, `Approved(2)`, `Rejected(3)`, `Cancelled(5/6)`, `Rescheduled`. Deprecate `NoShow(4)`, `CheckedIn(9)`, `CheckedOut(10)`, `Billed(11)` — remove from seeds/lookups, migrate existing rows (map 9/10/11 → Approved or Cancelled per business rule), and hide from all status dropdowns. `RescheduleRequested(12)` / `CancellationRequested(13)` remain workflow sub-states surfaced under Rescheduled/Cancelled buckets (or move onto the change-request entity). Add **`InfoRequested(14)`** as a first-class status with its own chip/color (see §I).
7. **[Behavior] Allow staff Reschedule/Cancel on Pending** (before approval). Relax server guards that currently require `Approved` for the staff-side reschedule/cancel endpoints. External request endpoints keep their existing status rules.
8. **[Schema][Behavior] Decision deadline re-semantics**: `Appointment.DueDate` = `CreationTime + SystemParameter.AppointmentDueDays` (was appointment-date-based). Change **`AppointmentDueDays` default 5 → 3** (per-tenant override stays). Dashboard counter `requestsApproachingLegalDeadline` → count of **Pending** requests within/past that window. Remove all CCR §31.5 / 60-day framing from localization strings.

## C. Booking form
9. **[Schema][API] `AppointmentTypeFieldConfig` + `Required`**: add `required: bool` to Create/Update/Dto; backend form validation must honor hidden/readOnly/required/defaultValue per appointment type. Build list endpoint per type (UI edits the full field set at once — accept a batch upsert).
10. **[Schema][Behavior] Insurance optional with confirmation**: add `noInsurance: bool` (parallel to applicant `selfRepresented` / defense `noneAssigned` flags — add those booleans if not yet persisted). When set, insurance section fields are not required.
11. **[Behavior] Panel number rule via config**: required only when type = Panel QME (move from hardcoded validator to the field-config mechanism).
12. **[Schema][Seed] Gender enum: add `Other(3)`** (patient + booking form + profile).
13. **[API] Slot generation — explicit day list**: extend `DoctorAvailability.generatePreview/createRange` input with `selectedDates: DateOnly[]` (mutually exclusive with fromDate/toDate+selectedDays sentinel) for irregular patterns.
14. **[Behavior] External immutability after submit** (verify/enforce): externals cannot edit a submitted request; changes go through staff or change requests. (UI promises this in the submit-confirm modal.)

## D. Documents
15. **[Schema][API] `AppointmentDocumentType.IsRequired`**: drives the required-docs tracker. Add an endpoint (or extend appointment GET) returning per-appointment required-vs-received doc-type status.
16. **[Schema][API] Document review state**: `AppointmentDocument.Status` (Accepted/Pending/Rejected) + `ReviewNote` (rejection reason) + accept/reject endpoints (staff). UI shows badges + reasons everywhere.
17. **[API] My Documents aggregation**: `GET /api/app/appointment-documents/mine` — all documents across the caller's appointments, grouped per appointment, with required-tracker summary.

## E. Change requests
18. **[API] Unified inbox**: combined pending list (both types) + counts; expose **opposing-counsel consent status** (pending/agreed/declined) and requester display info on `AppointmentChangeRequestDto` for the inbox row.
19. **[Behavior] Approve applies the change directly** (replaces the outcome-dropdown step); reject keeps required reason. Approving while consent is pending/declined is allowed but should be audited (UI warns).

## F. Users, invites & tenants
20. **[API] Pending invitations management**: `GET /external-users/invites` (email, role, invitedBy, sentAt, expiresAt, status pending/accepted/expired/revoked) + `POST …/resend` + `POST …/revoke`.
21. **[Schema] Invite DTO + `firmName`** (optional, attorneys only) — pre-fills the attorney record on registration.
22. **[API] Internal users list**: name/email/role/tenant/active + deactivate/reactivate + admin-triggered password-reset email (wrap Identity).
23. **[API] Tenants page data**: tenant list with subdomain, edition, user count, appointment count; create (with admin email), edit, activate/deactivate; "switch into tenant" (host). Reuse Volo SaaS where possible.

## G. Notifications & admin
24. **[API] NotificationTemplates**: add `send-test` endpoint; expose a **variable catalog** per template code (for the chip UI); per-tenant override = existing `tenantId` null/set distinction (badge in UI).
25. **[API] In-app notifications feed** (new): lightweight per-user notifications (appointment approved/rejected, documents requested/reviewed, change-request updates, consent events) powering the external bell dropdown and internal topbar bell. Mark-read + recent-N. (Email dispatch via templates continues unchanged; **no opt-out** — preferences UI was deliberately removed.)
26. **[API] Dashboard aggregates**: requests-over-time series, status breakdown, today's schedule, recent activity (derivable from change logs/audit), host per-tenant totals.
27. **[API] Reports**: add CSV export alongside PDF; **remove SSN from the report DTO**; support the column subset.
28. **[Perm] Permission matrix UI** writes via standard ABP permission management API — ensure groups/names align with the matrix groupings (Appointments, Change requests, Scheduling, Configuration, People, Users & access, Reports).

## H. Data exposure / privacy
29. **SSN**: never in list/report DTOs; masked on detail; write-only updates; audited reveal endpoint stays (`ssn-input` flow already exists). 
30. **Fax**: keep fields in DTOs/forms; no list-column exposure (frontend-only, no change).
31. **Claim # vs ADJ #**: both already on `AppointmentInjuryDetail`; surface both on external/internal appointment list DTOs (list rows need them).
32. **Lookup protection**: block delete of system-seeded lookup rows and rows in use (usage counts on list DTOs for Types/Statuses/DocTypes/Languages/States/Locations/WCAB).

## I. Send back / Request more information (new feature)
33. **[Schema] `AppointmentInfoRequest` entity**: appointmentId, round #, note (500), createdBy/At, state (open / resubmitted / superseded), resubmittedAt; child collection of flagged items `{ fieldKey | documentTypeId | injuryDetailId, hint (150) }`. Field keys align with the field-config catalog (C9) so the picker and fix-it page share one field registry.
34. **[Schema][Seed] `AppointmentStatusType.InfoRequested = 14`** + migration/localization. Non-terminal: staff reschedule/cancel/approve/reject remain available while in it.
35. **[API]** `POST /appointments/{id}/request-info` (flagged items + hints + note → status 14, queues email) · `GET /appointments/{id}/info-request` (open request w/ flagged keys — powers the fix-it page) · `POST /appointments/{id}/resubmit` (validates all flagged items addressed → status Pending, stamps round, writes field-level diff to the change log).
36. **[Behavior] Field-level authorization while status = 14**: external callers may modify ONLY the flagged fields/documents/claim rows; all other updates rejected server-side (the UI disables them, the server must enforce).
37. **[Seed] Two NotificationTemplates**: `InfoRequested` (includes the staff note + deep link to the fix-it page) and `InfoResubmitted` (to staff). New SystemParameter `InfoRequestReminderDays` (default 2) driving a reminder job when no resubmission.
38. **[API] Diff payload for staff review**: per flagged item old → new (SSN masked), surfaced on the appointment detail; every round logged in the change log. Permission: granted to ALL internal roles incl. Intake Staff (it's communication, not a decision).
39. **[Behavior] Decision clock unaffected** by send-back (keeps running); the dashboard decision-deadline alert counts Pending only — Info Requested rows surface via their own chip.

## Suggested sequencing
1. A1–A4 (roles) + B6 (status migration) — unblock everything else.
2. C9/C12, D15–D16 (schema adds) with migrations.
3. B7–B8, C10–C11, C14 (behavior rules).
4. E18–E19, F20–F23, G24–G27 (new endpoints).
5. C13, D17, G25 (nice-to-have endpoints last).
6. Section I (send-back feature) — after C9, since the flagged-field registry reuses the field-config catalog.
