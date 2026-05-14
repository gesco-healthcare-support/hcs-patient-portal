---
slug: 2026-05-14-multi-user-workflows
status: approved
audience: Adrian
session: main-worktree userflow testing
---

# Multi-user workflow plan -- main worktree, 2026-05-14 (v2)

## Context

PR #197 (auth + register fixes) and PR #198 (lookup-select OnPush fix)
have unblocked the gating bugs. Single-actor flows have been verified
end to end. This v2 of the plan addresses Adrian's 2026-05-14 feedback:

- Use `SoftwareOne@evaluators.com` (Staff) + `SoftwareTwo@evaluators.com`
  (Supervisor) as internal users so internal email delivery gets
  exercised too.
- Wipe existing appointments; start fresh.
- Seed doctor availabilities for AME / QME / Re-Evaluation /
  Consultation across June + July so the 3-day-lead-time gate stops
  blocking us.
- Fill EVERY form section every walk; pre-fill values live in
  `docs/plans/2026-05-14-form-prefill-content.md`.
- The CE booking flow IS implemented via the Claim Information modal
  in the booking form (`appointment-add-claim-information.component.html`
  lines 343-500). I will use the UI, not SQL.
- Explore like a real user: open every collapsible, modal, action menu.
  Predicted observations include exploration branches, not just happy
  paths.

## Identities + pre-fill data

See `docs/plans/2026-05-14-form-prefill-content.md` for the full
field-by-field synthetic-data table. Identities below are quick
reference.

| Role | Email | Notes |
| --- | --- | --- |
| Patient | SoftwareThree@gesco.com | Patient booker |
| Applicant Attorney | SoftwareFour@gesco.com | AA booker + AA-link target |
| Defense Attorney | SoftwareFive@gesco.com | DA-link target |
| Claim Examiner | SoftwareSix@gesco.com | CE-link target via Claim Information modal |
| Clinic Staff + Staff Supervisor | SoftwareOne@evaluators.com | already seeded as `admin`; needs Clinic Staff + Staff Supervisor added as ADDITIONAL roles |
| Clinic Staff + Staff Supervisor | SoftwareTwo@evaluators.com | already seeded as `admin`; needs Clinic Staff + Staff Supervisor added as ADDITIONAL roles |
| Tenant admin | admin@falkinstein.test | for role grants + master-data CRUD |
| IT Admin (host) | it.admin@hcs.test | for tenant-level + host-scope pages |

## Pre-execution prep (register externals + DB reset + Doctor UI + role grants)

These steps are prerequisites for the workflow walks. Each is an
isolated, reversible step.

### Prep 0 -- Register the 4 external test users

The alt-port stack uses database `CaseEvaluationTesting` (per the
overlay recipe). This DB has only the seeded internal users +
SoftwareOne/Two. SoftwareThree/Four/Five/Six exist only in the
canonical `CaseEvaluation` DB. We register them fresh via the
public registration flow so the testing DB is self-contained.

Steps for each user (4 iterations):
1. Logout entirely if any session is active.
2. Navigate `http://falkinstein.localhost:44369/Account/Register`.
3. Fill: First Name `Software`, Last Name `Three` (or Four / Five /
   Six), Email per identity table, Password `1q2w3E*r`, role per
   identity table (Patient / Applicant Attorney / Defense Attorney
   / Claim Examiner).
4. Submit. Expect 204 + success panel.
5. Ask Adrian for the verification link from each inbox.
6. Navigate the link. Expect SPA to confirm + show "Email
   verified" state.
7. Sign in once to confirm the account works. Logout.

### Prep 1 -- Reset appointment state

Goal: blank slate. Keep users + doctors + tenants. Wipe appointments
and their dependents.

Reset via SQL (the only legitimate non-UI step in this plan; data
reset is not a workaround for a missing UI, it's housekeeping):

```sql
DELETE FROM AppEntity_AppointmentDocumentSignatures;
DELETE FROM AppAppointmentDocuments;
DELETE FROM AppEntity_AppointmentApplicantAttorneys;
DELETE FROM AppEntity_AppointmentDefenseAttorneys;
DELETE FROM AppEntity_AppointmentClaimExaminers;
DELETE FROM AppEntity_AppointmentPrimaryInsurances;
DELETE FROM AppEntity_AppointmentInjuryDetails;
DELETE FROM AppEntity_AppointmentAuthorizedUsers;
DELETE FROM AppEntity_AppointmentCustomFieldValues;
DELETE FROM AppEntity_AppointmentChangeRequests;
DELETE FROM AppAppointments;
DBCC CHECKIDENT('AppAppointments', RESEED, 0);
```

I will discover the actual FK chain before running this -- the list
above is my prediction from prior reads of the schema; I will dump
the live FK dependencies first.

### Prep 2 -- Doctor Management UI walk: create doctors + availabilities

Per OLD parity (`InternalUsersDataSeedContributor.cs:19-21` + OLD's
`P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\`),
Doctors are a **non-user reference entity managed by Staff
Supervisor via the Doctor Management UI**. They are not auto-seeded;
no `DemoDoctorDataSeedContributor` exists in NEW yet (logged as
SEED-2 follow-up).

This prep step creates **just enough** doctor data for the workflows
to find available slots. The full Doctor Management surface tour
(every CRUD page + every edge case) is its own workflow (Workflow I,
below the priority workflows).

Steps:
1. Logout. Sign in as `SoftwareTwo@evaluators.com` (post Prep 3, will
   have Staff Supervisor role).
2. Navigate the Doctor Management area. Locate the "Add Doctor" page
   or button.
3. Create Doctor: `Dr. Demo One`, `dr.demo.one@falkinstein.test`,
   synthetic phone, license info per pre-fill content doc.
4. Set Preferred Locations: Demo Clinic North.
5. Set Appointment Types: AME, QME, Re-Evaluation, Consultation.
6. Set Availabilities: for each appointment type, add slots across
   June 2026 and July 2026. Aim for 5-10 dates per type with 2-3 slots
   per date. Exact UI flow will be discovered during the walk.
7. Repeat (optional) for `Dr. Demo Two` if multi-doctor scope matters.
8. Verify the AppointmentDate datepicker in the booking form lights
   up the seeded dates (the `markDisabled` + `available-day` highlight
   should reflect them).

Out of scope for Prep 2: deep exploration of Doctor edit flows,
deactivation, conflict resolution, signature upload (that's Workflow
I + Workflow G).

### Prep 3 -- Add Clinic Staff + Staff Supervisor as additional roles to SoftwareOne + SoftwareTwo

Both users are auto-seeded with the tenant `admin` role per
`InternalUsersDataSeedContributor.cs:48-52,152-159`. To put them on
every role-targeted email fan-out (Clinic Staff notifications,
Staff Supervisor notifications) and exercise every internal action,
add both roles as ADDITIONAL roles on top of admin.

Real admin action via the UI, not SQL.

Steps:
1. Logout. Sign in as `admin@falkinstein.test`.
2. Navigate `/identity/users`.
3. Edit `SoftwareOne@evaluators.com`. Roles tab. CHECK in addition
   to existing `admin`: "Clinic Staff", "Staff Supervisor". Save.
4. Edit `SoftwareTwo@evaluators.com`. Roles tab. CHECK in addition
   to existing `admin`: "Clinic Staff", "Staff Supervisor". Save.
5. Verify each user can sign in. They land on whichever home page
   the routing prefers (admin's, most likely). Both should now be
   on Clinic Staff + Staff Supervisor email distribution lists.

## Workflows (priority order)

Each workflow has Goal, Steps, Exploration mandate, Predicted
observations, Estimated time. Steps reference the pre-fill content
doc rather than re-listing field values.

### Workflow B (top priority) -- Patient self-books with AA included, Clinic Staff approves

**Goal:** verify cross-actor notification fan-out. A Patient who
self-books while adding an AA email should see (a) the AA receive
a booking notification, (b) AA receive an approval notification
once Staff approves, (c) AA's `/home` shows the appointment, and
(d) internal staff email to SoftwareOne arrives.

**Steps:**
1. Logout. Sign in as `SoftwareThree@gesco.com` (Patient).
2. Navigate `/appointments/add`.
3. **Section 1 (Schedule):** Appointment Type = AME, Location =
   Demo Clinic North, Panel Number per pre-fill, Date = first
   June-2026 slot, Time = first available.
4. **Section 2 (Patient Demographics):** verify auto-fill from
   profile. Override any field that auto-filled differently from
   pre-fill values. Fill SSN by typing a synthetic 9-digit string.
5. **Section 3 (Employer Details):** fill ALL 7 fields per pre-fill.
6. **Section 4 (AA):** toggle Include ON. Fill ALL 9 fields per
   pre-fill, pointing at SoftwareFour.
7. **Section 5 (DA):** toggle Include OFF (or leave default off).
8. **Section 6 (Claim Information):** click Add. Modal opens. Fill
   the 6 top fields per pre-fill. Insurance toggle ON, fill 9
   subsection fields. Claim Examiner toggle ON, fill 9 subsection
   fields. Click Add to close modal. Verify row appears in the
   Claim Information table.
9. **Section 7 (Additional Authorized User):** leave empty (drive-by
   observation: confirm Add modal opens cleanly).
10. **Section 8 (Custom Fields):** confirm absent (Falkinstein has
    no custom fields seeded).
11. Submit. Expect A00001 Pending (after DB reset).
12. Logout. Sign in as `SoftwareOne@evaluators.com` (Clinic Staff,
    post role-grant). Navigate `/appointments` Pending list.
13. Open A00001 -> Actions -> Review -> Approve. Responsible User =
    `SoftwareOne@evaluators.com` (themselves).
14. Wait ~30s for packets + email jobs. Watch api logs for
    `SendAppointmentEmailJob: delivered` lines.
15. Logout. Sign in as `SoftwareFour@gesco.com` (AA). Verify `/home`
    shows A00001.
16. Ask Adrian to spot-check inboxes:
    - SoftwareThree (Patient): booking notification + approval +
      packet-attached email.
    - SoftwareFour (AA): booking notification + approval +
      packet-attached email.
    - SoftwareOne (Staff): internal approval-result notification (if
      configured).

**Exploration mandate:**
- After step 8 (Claim Info modal Add), re-open the modal in Edit
  mode and confirm round-trip preserves all values.
- After step 11 (submit), explore `/home` -> the new appointment
  should be visible to the booker. Open the detail view and inspect
  every action button + tab + collapsible.
- After step 14 (post-approval), check the appointment-view page
  for: document download link for the patient packet, signature
  panel state, status timeline if present.

**Predicted observations:**
- *Good:* AA-by-typed-email auto-links via the
  `appointment-claim-examiners` (CE) + `AppointmentApplicantAttorneys`
  rows on submit; AA sees the appointment in their `/home`.
  Stakeholder emails fan out to Patient + AA + booker.
- *Risk:* AA-typed-email may persist as typed-name-only without
  resolving the email to an existing IdentityUser. If so, SoftwareFour
  wouldn't see A00001 in their list. File a bug.
- *Risk:* CE-typed-email may behave the same way -- the Claim
  Information modal CE row may store the typed email without linking
  to SoftwareSix's IdentityUserId. Workflow D will catch the
  follow-up regression.
- *Risk:* the Stakeholder email fan-out may only include the booker
  not all parties. Verify via api logs.
- *Risk:* the "Responsible User" dropdown on Approve may be empty or
  may not include SoftwareOne even after role grant. If so, the role
  grant didn't propagate; investigate the user-role cache.

**Estimated time:** 20-25 min (longer than v1 due to full-form fill).

### Workflow C -- DA scope + #114 access-guard regression

**Goal:** verify DA scope and the BUG-114 access-guard 403 on
out-of-scope detail navigation.

**Steps:**
1. As `SoftwareThree@gesco.com`, book A00002 with DA included, AA
   OFF. Date = different June slot from A00001. Fill ALL 7 sections
   (skip Authorized User table). Claim Examiner toggle ON in the
   modal, point at SoftwareSix again (for cross-check).
2. As `SoftwareOne@evaluators.com` (Staff), approve A00002.
3. Logout. Sign in as `SoftwareFive@gesco.com` (DA). Verify `/home`
   shows A00002 only -- A00001 must NOT appear.
4. Direct URL fetch on A00001's detail page. Expect 403 with
   `Appointment:AccessDenied`.
5. Bonus: DA uploads an ad-hoc doc on A00002. Should succeed once
   Approved.

**Exploration mandate:**
- Walk the DA home page. Are there filter/sort/search controls?
  Do they work?
- Open the appointment detail page. What actions are visible to a
  DA vs the Patient or Staff?

**Predicted observations:**
- *Good:* DA list shows exactly A00002. Direct URL access blocked.
- *Risk:* DA list empty -- typed-email link didn't resolve. File a
  bug.
- *Risk:* DA can see fields they shouldn't (SSN, DOB if PHI-redaction
  not applied per role).

**Estimated time:** 12-15 min.

### Workflow D -- CE booking via Claim Information modal + CE scope

**Goal:** verify the CE-typed-email auto-links to SoftwareSix and
their `/home` scope works. NOTE: I incorrectly suggested SQL in v1;
v2 uses the UI per the Claim Information modal that's part of the
booking form.

**Steps:**
1. As `SoftwareThree@gesco.com`, book A00003 with AA + DA both OFF,
   but the Claim Information modal's Claim Examiner toggle ON,
   pointing at SoftwareSix per pre-fill.
2. As `SoftwareOne@evaluators.com`, approve A00003.
3. Logout. Sign in as `SoftwareSix@gesco.com` (CE). Verify `/home`
   shows A00003 only (and A00001 + A00002 if Workflows B + C also
   added CE rows pointing at SoftwareSix).
4. Direct URL fetch on a non-CE appointment. Expect 403.
5. Test case-insensitivity: SoftwareSix's CE row email is the typed
   string from the modal. The matching join in
   `AppointmentInjuryDetails -> AppointmentClaimExaminers.Email`
   is case-insensitive per ABP convention. Verify by lowercasing
   the email in one row (via UI Edit on the modal if reachable) and
   re-checking `/home`.

**Exploration mandate:**
- Open the Claim Information modal in Edit mode on A00003 (as Staff
  or via the appointment detail page). Are CE fields editable? Are
  there constraints?
- Try removing the CE row and re-saving. Does SoftwareSix lose
  access? Re-add and confirm access returns.

**Predicted observations:**
- *Good:* CE row's email resolves to SoftwareSix's IdentityUserId at
  submit time, list shows their scoped appointments, access guard
  fires on out-of-scope.
- *Risk:* CE-typed-email persists without IdentityUser resolution.
  If so, CE's `/home` would be empty. File a bug -- same shape as
  the AA + DA risks.
- *Risk:* CE removal from a row may not revoke `/home` visibility
  if there's no audit-side cleanup.

**Estimated time:** 15-20 min (CE modal has 17 fields + the 6
top-injury fields = 23 fields per row).

### Workflow E -- Confirm reschedule + cancellation UI status

**Goal:** verify whether reschedule + cancellation UI got added since
the parity audit.

**Steps:**
1. As `SoftwareThree@gesco.com`, navigate A00001 detail (Approved).
   Walk every action button + menu + tab + collapsible. Note any
   "Request Reschedule" / "Request Cancellation" controls.
2. As `SoftwareTwo@evaluators.com` (Supervisor), navigate the
   Supervisor dashboard. Look for a "Pending Change Requests" tile
   or menu entry.
3. If either UI exists, walk it on the spot and capture screenshots.

**Exploration mandate:**
- Look for change-request affordances in unexpected places: a kebab
  menu, a right-click context menu, a settings page.

**Predicted observations:**
- *Good (more likely):* confirm the parity-audit "UI not built"
  status is current truth.
- *Good (less likely):* UI was added -- walk the flow.

**Estimated time:** 5-8 min.

### Workflow F -- Anonymous verification-code document upload

**Goal:** test `/upload/:appointmentId/:code` anonymous route + the
Phase 14b rate-limiter on `/api/public/appointment-documents/*/upload-by-code/*`.

**Steps:**
1. From any Approved appointment, find the verification-code link.
   It comes via email -- ask Adrian to forward the verification
   link from one of the inboxes.
2. Logout entirely. Navigate the link in an incognito tab.
3. Upload a synthetic PDF. Verify the AppointmentDocument row shows
   status=Uploaded and the blob is in MinIO.
4. Wrong-code path: replace the code segment with garbage. Expect
   403 + `Document:UnauthorizedVerificationCode` ("Un unauthorized
   user" OLD-verbatim).
5. Rate-limit: burst 6 upload-by-code POSTs from the same IP within
   an hour. Expect the 6th returns 429.

**Exploration mandate:**
- What does the anonymous upload page look like? Is there any PHI
  leakage on the page (patient name, appointment date) that an
  attacker with the link could harvest?
- What MIME types are accepted? Try a .exe, a .docx, a 0-byte file.

**Predicted observations:**
- *Good:* rate-limiter fires; wrong-code returns localized message;
  right-code uploads land in MinIO.
- *Risk:* the verification-code may not be embedded in the email
  body yet (audit doc says it is; Phase 1A may be partial).
- *Risk:* the upload page may show too much PHI to anonymous users.

**Estimated time:** 20-25 min.

### Workflow G -- Staff Supervisor signature + Bill marking

**Goal:** verify the Staff Supervisor flow for signature upload and
the "mark Billed" state transition.

**Steps:**
1. As `SoftwareTwo@evaluators.com` (Supervisor), navigate user
   settings / profile. Look for a signature-upload page or panel.
2. Upload a synthetic PNG (any 1-by-1 transparent square).
3. From the appointments list, find a CheckedOut appointment. If
   none exist, walk an appointment through CheckIn -> CheckOut first
   (these are real UI actions, not bypassed via SQL).
4. Mark the appointment Billed via the UI. Verify
   `AppointmentStatus = Billed (11)`.

**Exploration mandate:**
- What signature formats does the upload accept (PNG/JPG/SVG)?
- Where does the signature appear after upload -- on the
  Supervisor's profile, on every appointment they touch, on a
  per-appointment basis?
- What's the difference between Supervisor signature and Doctor
  signature in the packet generation flow?

**Predicted observations:**
- *Good:* signature upload + bill marking both work, sig appears in
  subsequent packet PDFs.
- *Risk:* CheckIn/CheckOut UI may be missing entirely -- file as
  parity gap, do not SQL-workaround.

**Estimated time:** 20-25 min.

### Workflow I -- Doctor Management surface tour (Supervisor)

**Goal:** explore the full Doctor Management CRUD surface to surface
parity gaps and bugs. Prep 2 already created `Dr. Demo One` with
just-enough data; this workflow exercises every page and edge case.

**Steps:**
1. As `SoftwareTwo@evaluators.com` (Supervisor), navigate to the
   Doctor Management list page.
2. Open `Dr. Demo One` in Edit mode. Walk every tab/sub-panel:
   demographics, preferred locations, appointment types,
   availabilities, signature (if separate from user-profile signature
   from Workflow G).
3. Create a second doctor `Dr. Demo Two`. Set different locations +
   types + availabilities so booking shows a multi-doctor picker.
4. Try edge cases:
   - Add an availability slot that overlaps an existing one. Expect
     a server-side conflict error.
   - Delete a slot that already has an appointment booked against
     it (use one of the workflow-B/C/D appointments). Expect a
     server-side FK guard.
   - Deactivate a doctor. Verify they disappear from the booking
     form's doctor lookup (if exposed) and the availability picker.
5. Try the Doctor Management page as a Patient or AA / DA / CE user.
   Expect 403 / hidden nav -- doctors are Supervisor-only.

**Exploration mandate:**
- Capture screenshots of every page Visited. Document the field
  inventory of the Doctor entity for the parity audit.
- Note any field name or label that diverges from OLD's
  `DoctorDomain.cs` schema.
- Check the OLD app docs at
  `P:\PatientPortalOld\Documents_and_Diagrams\` for any
  Doctor-Management-specific workflow diagrams that NEW should
  mirror; compare.

**Predicted observations:**
- *Good:* the surface is mostly there. CRUD works. Edge-case guards
  fire.
- *Risk:* unwrapped `<abp-lookup-select>` instances in Doctor pages
  break dropdowns the same way the booking form did pre-BUG-007.
- *Risk:* a divergence from OLD's Doctor schema (missing field,
  added field, label drift).
- *Risk:* permission-leak -- a non-Supervisor can reach the page.

**Estimated time:** 30-40 min.

### Workflow H -- IT Admin host-scope tour

**Goal:** verify host-scope IT Admin pages render and behave.

**Steps:**
1. Logout. Sign in as `it.admin@hcs.test` at
   `http://localhost:4201` (alt port, no subdomain).
2. Navigate `/saas/tenants`, `/text-template-management`,
   `/it-admin/system-parameters`, `/it-admin/custom-fields`,
   `/it-admin/package-details`. Open each page, click into any list
   items, look at edit forms.
3. Verify each page renders without console errors and any
   lookup-selects populate.

**Exploration mandate:**
- Are there other host-scope pages reachable from the sidebar that
  the parity audit didn't enumerate?
- Open the notification-template editor. Do the template codes match
  what `DemoExternalUsersDataSeedContributor` references?

**Predicted observations:**
- *Good:* pages render; capture OBS-style notes only.
- *Risk:* admin master-data CRUD components still use unwrapped
  `<abp-lookup-select>` (BUG-007 fix only swapped the appointment
  booking form). Document each broken dropdown.

**Estimated time:** 25-35 min.

## Execution order + time budget

Priority workflows (B + C + D + E + F) walked first, then security
(F is already counted), then Supervisor-flavoured (G + I), then host
admin (H). Budget ~3.5 hours uninterrupted.

1. Prep 1 + 2 + 3 (~30 min: SQL reset, Doctor UI walk, role grants)
2. Workflow B (20-25 min)
3. Workflow C (12-15 min)
4. Workflow D (15-20 min)
5. Workflow E (5-8 min)
6. Workflow F (20-25 min)
7. Workflow G (20-25 min)
8. Workflow I -- Doctor Management surface tour (30-40 min)
9. Workflow H (25-35 min)

If anything blocks the test session (e.g. AA-typed-email doesn't
resolve and breaks B), I'll skip ahead to the next workflow that
doesn't depend on that path, and document the blocker.

## Out of scope

- Cross-tenant security (Phase 1A is single-tenant).
- Doctor login (Doctor is an entity, not a user role).
- Hangfire dashboard exploration (separate ops concern).
- Any structural source edits. Bugs found go into
  `docs/runbooks/findings/2026-05-13-userflow-findings.md` in the
  BUG-* / OBS-* format. Adrian surfaces fixes to the fix-session at
  `W:\patient-portal\replicate-old-app\`.

## Verification

After each workflow:
- Append a `Status: VERIFIED` or `Status: BUG-NNN filed` line
  beneath the workflow in this plan file.
- Bugs go to findings file using the Part 11 ticket template.
- Capture screenshots into `tests/screenshots/2026-05-14-multi-user/`.

## Approval

Approved by Adrian 2026-05-14 with feedback (v1 -> v2 deltas above).
Beginning execution after Prep 1/2/3.
