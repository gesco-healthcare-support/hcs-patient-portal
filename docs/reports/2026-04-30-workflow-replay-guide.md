# Wave 2 Workflow Replay Guide

This document captures every workflow we executed against the Patient Portal so they can be replayed to verify each fix lands correctly and to catch regressions. Each workflow is self-contained: prerequisites, step-by-step actions, expected results, and the findings it specifically validates.

- **Branch:** `feat/mvp-wave-2`
- **Source report:** `docs/reports/2026-04-29-wave-2-demo-lifecycle.md`
- **Stack:** ABP Commercial 10.0.2 / .NET 10 / Angular 20 / Docker Compose
- **All actions are UI-driven via the Angular app at `http://localhost:4200`** unless explicitly noted.

---

## How to use this guide

After landing each fix from the priority order:

1. Run the workflow(s) listed in that fix's "Verifies" column.
2. Confirm every "Expected after fix" assertion passes.
3. Confirm the workflow's listed findings no longer reproduce.
4. If a fix unblocks a workflow that was previously truncated, run the rest of that workflow end-to-end.
5. Update this guide if you discover new failure modes.

**Last updated:** 2026-04-30 after the second-pass fix landing (steps 5.1-7.4 + 1.6 + D.1 + D.2).
The original Tier numbering (0.x-3.x) referenced below is preserved for archive cross-reference; the active progress tracker in `docs/reports/2026-04-29-wave-2-demo-lifecycle.md` uses the lifecycle step numbers (0.1-7.4 + 5.3b + 1.6 + D.1-D.3) which are what the workflows below verify.

| Fix # | Title | Verify with |
|---|---|---|
| 0.1 | W-X-2 anonymous external-user-lookup | **Probe-1** |
| 0.2 | W-A-10 SMTP no-op for dev (preserved at placeholder credentials only) | **Probe-9** |
| 1.1 | W-A-9 Packet job UoW | **Workflow A** Stage A.10 |
| 1.2 | `/Account/Register` hijack hooks (W-B-1) | **Workflow A** A.1 / **Workflow B** B.1 |
| 1.3 | Register form minimal (no First/Last/Tenant inputs) | **Workflow A** A.1 |
| 1.4 | Booking-form lookup `[Authorize]` demoted | **Workflow A** A.3 / **Workflow C** form-load |
| 1.5 | Tenant-fixedness for registered users | **Probe-10** + **Workflow B** B.1 |
| **1.6** | **W-REG-4 tenant-locked external register (NEW 2026-04-30)** | **Probe-3** + **Workflow B** B.1 + **Workflow H** H.4 |
| 2.1 | Booking form `/patients/me` gating (W-B-2) | **Workflow C** form-load / **Workflow D** form-load |
| 2.2 | Appointment-list visibility narrowing | **Probe-11** (visibility) |
| 2.3 | Home CTA buttons + datatable for external roles | **Workflow B/C/D** A.2-style |
| 2.4 | i18n keys for AppointmentStatusType present | **Workflow A** A.7 visual |
| 3.1 | WCAB Office option text via `displayName` | **Workflow A** A.4 |
| 3.2 | 28 ngModel directives in Claim Info modal | **Workflow A** A.4 |
| 3.3 | saveInjuryModal inline error | **Workflow A** A.4 (negative path) |
| 3.4 | Conditional required validators on AA/DA email | **Workflow A** A.3 (negative path) |
| 4.1 | Re-evaluation `?type=2` heading | **Probe-8** |
| 4.2 | Patient + AA lookup 403 fixes | **Workflow A** A.3 |
| 4.3 | Claim Information modal Add button | **Workflow A** A.4 |
| 4.4 | DA UI parity columns | **Workflow A** A.3 visual |
| 4.5 | Claim Examiner section extracted | **Workflow A** A.3 visual |
| 4.6 | Auto-populate on form load (Patient + AA pre-fill) | **Workflow A** A.3 / **Workflow B** B.3 |
| **5.1** | **4 party emails on Appointment** | **Workflow A** A.5 (DB column verify) |
| **5.2** | **Auto-link on registration** | **Workflow B** B.1 + **Probe-12** (post-register check) |
| **5.3** | **Queue Actions Review item** | **Workflow A** A.7 |
| **5.3b** | **`isExternalUserNonPatient` admin gate** | **Workflow A** A.8 (admin can edit on AwaitingMoreInfo +) + **Probe-13** |
| **5.4** | **DA + Claim Info on view page** | **Workflow A** A.8 |
| **5.5** | **Patient Demographics populate from saved data** | **Workflow A** A.8 |
| **5.6** | **Queue Patient column firstName + lastName** | **Workflow A** A.7 |
| **5.7** | **NullEmailSender gate on placeholder credentials** | **Probe-9** + **Workflow A** A.6 |
| **6.1** | **Email fan-out using stored party emails** | **Workflow A** A.6 + **Probe-9** (Mailtrap inbox) |
| **6.2** | **Email body confirmation # + login/register link** | **Workflow A** A.6 + **Probe-9** |
| **6.3** | **"Appointment requested" wording** | **Workflow A** A.6 + **Probe-9** |
| **7.1** | **i18n sweep + slot-form keys** | **Workflow A** visual + **Probe-14** (slot form) |
| **7.2** | **View re-fetch after Approve** | **Workflow A** A.8 status-pill |
| **7.3** | **AppointmentType.Name heading instead of "PQME"** | **Workflow A** A.8 visual |
| **7.4** | **Slot generation 0-slot UX message** | **Probe-14** |
| **D.1** | **Internal-role grants + W-UI-16 user seeder (Doctor role added)** | **Probe-10** + **Probe I-1 / I-2 / I-3 / I-4** |
| **D.2** | **Admin invite link feature** | **Workflow H** + **Probe-11** |
| D.3 | W-SLOT-3 time-string-to-GUID (no-op observation) | -- |

---

## Setup prerequisites

Required before any workflow runs.

### Docker stack health
```
cd /w/patient-portal/main
docker compose ps
# Expected: 5 services healthy (api, authserver, redis, sql-server, angular) + db-migrator exited 0
```

If unhealthy: `docker compose up -d --build` and wait. If the SQL Server volume is wiped (post-Docker reinstall), proceed to "Tenant + minimal data seed" below.

### Tenant + minimal data seed (only after a volume wipe)

If `/api/saas/tenants` returns `totalCount: 0`, do the following via the host-admin UI:

**As `admin@abp.io` / `1q2w3E*` (host login, no tenant):**

1. Navigate to `http://localhost:4200/saas/tenants` -> click `+ New tenant`.
2. Create tenant `Dr Rivera 2` with admin email `maria.rivera@hcs.test`, password `1q2w3E*`.
3. Create tenant `Dr Thomas 1` with admin email `anahit.thomas@hcs.test`, password `1q2w3E*`.
4. From the Tenants list, click Actions on `Dr Rivera 2` -> **Login with this tenant** -> click Login (default admin user).

**Note (D.1 fix, 2026-04-30):** `InternalUsersDataSeedContributor` runs at DbMigrator time and **automatically** creates the following internal users per existing tenant (gated on `ASPNETCORE_ENVIRONMENT=Development`). You no longer need to manually create internal users; reach for these credentials directly:

| Email | Role | Tenant |
|---|---|---|
| `it.admin@hcs.test` | IT Admin | host |
| `admin@dr-thomas-1.test` | admin | Dr Thomas 1 |
| `supervisor@dr-thomas-1.test` | Staff Supervisor | Dr Thomas 1 |
| `staff@dr-thomas-1.test` | Clinic Staff | Dr Thomas 1 |
| `doctor@dr-thomas-1.test` | Doctor (linked to Doctor entity) | Dr Thomas 1 |
| `admin@dr-rivera-2.test` | admin | Dr Rivera 2 |
| `supervisor@dr-rivera-2.test` | Staff Supervisor | Dr Rivera 2 |
| `staff@dr-rivera-2.test` | Clinic Staff | Dr Rivera 2 |
| `doctor@dr-rivera-2.test` | Doctor (linked to Doctor entity) | Dr Rivera 2 |

Password for every seeded user: `1q2w3E*`. The seeder is idempotent: re-running DbMigrator never duplicates users. After registering a brand-new tenant, restart the DbMigrator container (`docker compose up -d --force-recreate db-migrator`) to pick up the seeded internal users for the new tenant.

**Now in Dr Rivera 2 admin (impersonated):**

5. Navigate to `Doctor Management -> Doctors`. Click Actions on the seeded `Dr Rivera 2` doctor -> Edit.
6. On the Doctor tab, set `LastName = "Rivera"` (form rejects save without it).
7. On the **AppointmentTypes** tab, type-ahead-add all 6: `Qualified Medical Examination (QME)`, `Panel QME`, `Agreed Medical Examination (AME)`, `Record Review`, `Deposition`, `Supplemental Medical Report`. After each, click `Add`.
8. On the **Locations** tab, type-ahead-add `Demo Clinic North` and `Demo Clinic South`.
9. Click Save (in modal footer).
10. Navigate to `Doctor Management -> Doctor Availabilities -> Add`. Generate 6 slot blocks, one per type, on different dates (e.g.):
    - Demo Clinic North, 2026-05-04, 10:00-12:00, QME
    - Demo Clinic South, 2026-05-04, 10:00-12:00, Record Review
    - Demo Clinic North, 2026-05-05, 09:00-09:30, Panel QME
    - Demo Clinic North, 2026-05-05, 10:00-10:30, AME
    - Demo Clinic South, 2026-05-06, 09:00-09:30, Deposition
    - Demo Clinic South, 2026-05-06, 10:00-10:30, Supplemental Medical Report
11. Click Submit on each. Verify total slot count via `GET /api/app/doctor-availabilities` shows ~24 slots.

### Synthetic test users created during workflows

For replay, you can either reuse the existing users (if data persisted) or create fresh ones via the corresponding workflow's register step.

| Email | Password | Tenant | Role | Used in |
|---|---|---|---|---|
| `qa.patient.workflow-a@hcs.test` | `1q2w3E*` | Dr Rivera 2 | Patient | Workflow A |
| `qa.attorney.workflow-b@hcs.test` | `1q2w3E*` | Dr Rivera 2 | Applicant Attorney | Workflow B |
| `qa.da.workflow@hcs.test` | `1q2w3E*` | Dr Rivera 2 | Defense Attorney | Workflow C |
| `qa.ce.workflow@hcs.test` | `1q2w3E*` | Dr Rivera 2 | Claim Examiner | Workflow D |
| `qa.clinicstaff@hcs.test` | `1q2w3E*` | Dr Rivera 2 | Clinic Staff | Probe I-1 |
| `qa.staffsupervisor@hcs.test` | `1q2w3E*` | Dr Rivera 2 | Staff Supervisor | Probe I-2 |
| `qa.doctor@hcs.test` | `1q2w3E*` | Dr Rivera 2 | Doctor | Probe I-3 |
| `qa.itadmin@hcs.test` | `1q2w3E*` | Dr Rivera 2 | admin (proxying IT Admin) | Probe I-4 |

For brand-new replays use a date-suffix on each (e.g. `qa.patient.20260501-1@hcs.test`) so emails stay unique.

### Synthetic data values used

Same data was used in every workflow that submitted an appointment to keep findings comparable.

| Field | Value |
|---|---|
| Patient first name | `Marcus` |
| Patient last name | `Whitfield` |
| Patient middle name | `J` |
| Patient DOB | `07/12/1985` |
| Patient gender | Male |
| Patient cell | `5552013344` |
| Patient phone | `5552013345` (Work) |
| Patient SSN | `999-00-1111` |
| Patient address | `100 Demo Street, Pasadena, CA 91101` |
| Patient language | English |
| Employer | `Acme Construction Co` (Carpenter Foreman, `5553001010`) |
| Employer address | `200 Demo Way, Pasadena, CA 91102` |
| Applicant Attorney | `Helena Vargas` (`helena.vargas@aaattorney.test`, Vargas Law) |
| Applicant Attorney phone | `5554001111` |
| Applicant Attorney address | `300 Demo Boulevard, Pasadena, CA 91103` |
| Defense Attorney | `Brent Locke` (`brent.locke@dlaw.test`, Locke Defense) |
| Defense Attorney phone | `5555002222` |
| Defense Attorney address | `400 Demo Avenue, Pasadena, CA 91104` |
| Claim: Date of Injury | `2025-08-15` |
| Claim Number | `WF-A-DEMO-0001` (vary per workflow) |
| Claim WCAB | (any) |
| Claim ADJ # | `ADJ-WF-A-001` |
| Body Parts | `Lower back, right shoulder, neck (synthetic demo)` |

---

## Workflow A -- Patient as booker (full lifecycle)

**Verifies:** F1, F5, F6, F7, F8, F10, W-A-1..W-A-10, W-V-1, W-X-4, 2.10, 2.11, **5.1, 5.3, 5.3b, 5.4, 5.5, 5.6, 6.1-6.3, 7.2, 7.3**.

### A.1 Register a fresh Patient

1. Open `http://localhost:4200/account/login` in a clean browser context.
2. Click `switch` next to "Tenant: Not selected".
3. Type `Dr Rivera 2`, click Save.
4. Click "Not a member yet? Register".
5. **Expected after 1.6 fix (2026-04-30):** Page renders with:
   - A blue "Registering for **Dr Rivera 2**. To register at a different practice, use that practice's portal link." banner pinned at the top of the form.
   - Form shows: External User Role dropdown = Patient (default), User name, Email address, Password fields.
   - No tenant dropdown (this is intentional -- external users register only at the tenant whose portal link they followed).
6. Fill: User name = `qa.patient.<date>-<n>@hcs.test`, Email = same, Password = `1q2w3E*`.
7. Click Register. **Expected: redirect to `/Account/Login` (success).**

**Negative path (1.6):** Open `http://localhost:44368/Account/Register` in a brand-new browser session with no cookies. **Expected:** A red "Tenant required. To register, use the link from the email or page that brought you here." banner replaces the form, the form opacity drops to 0.4, and pointer-events disable so the user cannot submit. Verifies external register is impossible without a tenant context.

### A.2 Login as the new Patient

1. Back at the login form (tenant should still show `Dr Rivera 2`).
2. Fill credentials. Click Login.
3. **Expected:** Lands on `http://localhost:4200/` showing "Welcome, <name> (Patient)" + 2-3 tile buttons (Book Appointment, Book Re-evaluation, My Appointments table).

### A.3 Book Appointment form load + data fill

1. Click `Book Appointment`. Lands on `/appointments/add?type=1`.
2. **Expected after F1+1.3+W-B-2 fix:** No "An error has occurred / There is no entity Patient" modal pops. No 403 / 404 in console.
3. **Verify:** AppointmentType dropdown shows exactly **6 distinct entries** (QME, Panel QME, AME, Record Review, Deposition, Supplemental Medical Report). Location dropdown shows **2 distinct entries** (Demo Clinic North, Demo Clinic South). No duplicates. (F10 + 1.3.)
4. Pick AppointmentType = QME. **Expected: no 403 on `/api/app/appointment-type-field-configs/by-appointment-type/<id>`** (1.3 fix).
5. Pick Location = Demo Clinic North.
6. Pick AppointmentDate = `05/04/2026` and Appointment Time = `10:00 AM` (8 slots should populate from your seeded set).
7. Fill all Patient Demographics fields with the synthetic Marcus Whitfield data.
8. Pick state California, gender Male, language English, interpreter No.
9. Fill Employer Details (Acme Construction Co, etc.).
10. Verify Applicant Attorney section. Fill manually (Helena Vargas) -- DO NOT use the typeahead (which queries the unauthenticated `external-user-lookup` until W-X-2 lands). After W-X-2, the typeahead will require auth and may need re-checking.
11. Toggle Defense Attorney "Include" on. Fill (Brent Locke).

### A.4 Claim Information modal

1. Click `Add +` next to "Claim Information".
2. **Expected after F5+1.3+2.8+2.9 fix:**
   - Modal opens cleanly with **NO** `_rawValidators` console errors.
   - WCAB Office dropdown populates with **7 visible entries** showing real WCAB office names (`WCAB Anaheim`, `WCAB Bakersfield`, etc.) -- not blank text.
   - No 500 in API logs.
3. Pick "No" for Cumulative Trauma. Fill Date Of Injury `2025-08-15`, Claim Number `WF-A-<runid>-0001`, ADJ# `ADJ-WF-A-001`, Body Parts `Lower back, right shoulder, neck (synthetic demo)`. Pick a WCAB Office.
4. Click `Add` (modal footer). **Expected after 1.6 fix:** Modal closes; the parent form's "Claim Information" section now shows a row with the date / claim number / body parts.

### A.5 Submit the form

1. Click main `Save` button at the bottom of the form.
2. **Expected:** Redirect to `/` (home), and a new appointment appears in the user's "My Appointments Requests" table with confirmation `A00001` (or next).
3. **Verify in API:** `GET /api/app/appointments?MaxResultCount=10` shows the new appointment with `appointmentStatus: 1` (Pending).

### A.6 Email fan-out (submission)

1. Open `http://localhost:44327/hangfire/jobs/succeeded`. Wait 5 seconds for jobs to fire.
2. **Expected after 6.1 fix:** Multiple `SendAppointmentEmailJob` entries fired -- one per addressable recipient resolved by `AppointmentRecipientResolver`. The resolver walks the JOIN entities AND the 4 S-5.1 email columns on the Appointment row, then deduplicates by email.
3. **Per-recipient `Role` and `IsRegistered` (6.1):** Each job's args (click "details/<n>") carry:
   - `Role` enum (1=Patient, 2=ClaimExaminer, 3=AA, 4=DA, 5=OfficeAdmin)
   - `IsRegistered` flag (true when an IdentityUser with that email exists in the tenant; false otherwise)
   - `TenantName` (used to build register-URL links for non-registered recipients)
4. **Per-recipient template branching (6.2 + 6.3):**
   - Office mailbox: subject "New appointment request <conf#>", body says "A new appointment request was submitted" + "Open the appointments queue".
   - Booker / Patient (registered): subject "Appointment requested - <conf#>", body says "An appointment was requested" + "You are listed as the **patient** on this appointment. Log in to the patient portal..." + portal-login button.
   - AA / DA / CE registered: same as Booker but with role-specific verbiage.
   - **AA / DA / CE not registered (NEW): subject "Appointment requested - register to view <conf#>"**, body says "...you do not yet have a portal login for this practice. Register below to view this and future appointments..." + register button linking to `https://localhost:44368/Account/Register?__tenant=Dr+Rivera+2&email=helena.vargas%40aaattorney.test&role=ApplicantAttorney`.
5. **Verify after 5.7 fix (Mailtrap delivery):** API logs show `[INF] SendAppointmentEmailJob: delivered (Submission/<Role>/<id>) to <email>` for every recipient. `Probe-9` confirms emails arrive in Mailtrap inbox.
6. **NullEmailSender gate (5.7):** when `docker/appsettings.secrets.json` has `Abp.Mailing.Smtp.UserName` / `Password` starting with `REPLACE_*`, the API logs `SendAppointmentEmailJob: SMTP delivery failed ...` instead -- the swap is preserved for unconfigured environments. Verify by setting both keys back to `REPLACE_WITH_ACS_USERNAME` / `REPLACE_WITH_ACS_PASSWORD_CONNECTION_STRING`, restart api, and re-run.

### A.7 Tenant admin queue review

1. Logout: `http://localhost:44368/Account/Logout` -> clear localStorage -> back to login.
2. Login as `admin@dr-rivera-2.test` / `1q2w3E*` (D.1 seeded user; replaces the `admin@abp.io` host-impersonation path).
3. Navigate to `/appointments`.
4. **Expected:**
   - Row for the new appointment exists.
   - **Status column** shows `Pending` (NOT raw `Enum:AppointmentStatusType.1`) -- 2.4 / 2.1 fix verified.
   - **Patient column** shows `Marcus Whitfield` (NOT booker email) -- 5.6 fix verified. Falls back to email only when both firstName and lastName are blank.
   - **Actions dropdown** contains `Edit`, `Review`, `Delete` -- 5.3 fix verified. The Review item is a `routerLink` to `/appointments/view/<id>`.

### A.8 Review + Approve

1. Click Actions -> Review on the row.
2. **Expected:** Page navigates to `/appointments/view/<guid>`.
3. **Visual audit:**
   - **Heading reads (e.g.) `Qualified Medical Examination (QME) Appointment > Marcus Whitfield`** -- NOT "PQME" -- 7.3 fix verified. AME bookings render "Agreed Medical Examination (AME) Appointment", etc.
   - Action options in the dropdown read `Approve / Reject / Send Back` (English, not raw i18n keys) -- 2.4 fix.
   - **Patient Demographics textboxes have all the synthetic data filled (5.5 fix):**
     - Last Name = "Whitfield", First Name = "Marcus", Middle Name = "J"
     - Gender = Male radio selected
     - **DOB shows `07/12/1985` in the date input** -- 5.5 fix verified (the `parseDateOfBirthFromApi` helper converts the API ISO string to NgbDateStruct so ngbDatepicker renders it).
     - Email = qa.patient.<runid>@hcs.test (read-only)
     - Cell, Phone, SSN, Street, City, State, Zip all populated.
     - **"Unit #" field shows whatever was entered at booking** (5.5 fix verified the field now binds to `patientForm.apptNumber` not `patientForm.address`).
   - **Applicant Attorney section** populated with Helena Vargas data -- 4.6 / pre-existing.
   - **Defense Attorney section** is rendered with Brent Locke's filled data (5.4 fix). Layout mirrors AA card 1:1: same 11 fields (First/Last/Email readonly + Firm/Web/Phone/Fax/Street/City/State/Zip), same email-search row, Include toggle in card header.
   - **Claim Information section** rendered with the row from step A.4 as a read-only table (5.4 fix). Columns: Date Of Injury / Claim Number / WCAB / ADJ # / Body Parts / Insurance Company / Claim Examiner.
4. **Admin canEdit gate (5.3b):** Verify the field-level `[disabled]` state -- as `admin@dr-rivera-2.test`, all editable fields should be ENABLED on a Pending appointment. Pre-fix, admin was misclassified as "external" (no Patient role) and locked out.
5. Pick `Approve`, click Submit. Confirmation modal opens with English text. Click Approve.
6. **Expected after 7.2 fix:** Status pill at the card header flips from `Pending` to `Approved` **immediately on the same change-detection cycle** -- not after a 5-10 second delay. The `onActionSucceeded(dto)` handler patches `appointment.appointment` from the modal-returned dto first, then re-fetches in the background.
7. `appointmentApproveDate` set in the API. A `GenerateAppointmentPacketJob` enqueues.

### A.9 Email fan-out (approval)

1. Recheck Hangfire. **Expected:** Another wave of 6 `SendAppointmentEmailJob` entries with subject like `Appointment <conf#> approved`. Same recipient pattern as A.6.

### A.10 Packet generation + download

1. On the view page, scroll to the Doctor Packet card.
2. Wait ~5-10 seconds. **Expected after 1.1 fix:** Card transitions from "No packet has been generated yet" -> "Generating" -> "Generated".
3. Click `Download`. **Expected:** A multi-page PDF downloads with:
   - First page: MigraDoc cover sheet with confirmation #, patient name (Marcus Whitfield), date, type, location, claim #, ADJ, body parts.
   - Following pages: any approved Documents (none in this run).

### A.11 Document upload as office (Workflow A continuation)

1. Still as tenant admin on the view page, scroll to the Documents card.
2. Type a document name `Demo Medical Records 2026-Q1`. Click File, choose any benign PDF.
3. Click Upload. **Expected after 1.4 fix:** Status 200, document appears in the list with "Pending Approval" badge.
4. Click the green check on the row to Approve. Click the orange X on a separate test doc to Reject -> reject-reason modal opens.
5. **Reject reason negative paths:**
   - Submit blank -> inline error.
   - Type 600 chars -> truncated to 500 (W2-8 cap).
   - Submit with valid reason -> badge flips to Rejected.

### A.12 Packet regenerate after document changes

1. Click `Regenerate` on the packet card.
2. **Expected:** Packet status -> Generating -> Generated. Re-download PDF; only Approved documents are included; Rejected document is omitted.

### A.13 Packet failure path

1. Upload a deliberately-corrupt PDF (truncate a real PDF to 100 bytes, or save a ZIP renamed `.pdf`).
2. Approve it. Click Regenerate.
3. **Expected after 1.1 + good error handling:** Status flips to Failed (red), error message renders, and **Hangfire shows the job as Failed** (not retrying indefinitely). Office can delete the corrupt doc + re-upload + Regenerate to recover.

### Findings this workflow validates after fixes

| Finding | Verifier step |
|---|---|
| F1 (CORS) | A.1 (register works) |
| F5 (WCAB Mapperly) | A.4 (modal opens, WCAB populates) |
| F6 (`_rawValidators`) | A.4 (no console errors) |
| F7 (sticky toast) | A.4 (no Playwright pointer-events block) |
| F8 (slot dedup) | Setup step 10 (cross-location overlap allowed) |
| F10 (lookup dedup) | A.3 (6 distinct types, 2 distinct locations) |
| W-A-1 | A.6 (email body shows Marcus Whitfield, not email-as-name) |
| W-A-2 | A.6, A.9 (6 recipients each fan-out) |
| W-A-3 / 1.3 | A.3, A.4 (no 403 for Patient role) |
| W-A-4 | A.4 (modal Add closes + adds row) |
| W-A-5 / 2.1 | A.7, A.8 (English text everywhere) |
| W-A-6 / 1.7 | A.7 (Review item in Actions) |
| W-A-7 / 2.2 | A.8 (DA + Claim sections rendered) |
| W-A-8 / 2.7 | A.8 (heading uses real type, not "PQME") |
| W-A-9 / 1.1 | A.10 (packet generates + downloads) |
| W-A-10 / 0.2 | A.6 (no SMTP exception) |
| W-V-1 / 2.9 | A.4 (WCAB option text non-blank) |
| 2.10 | A.7 (Patient column = "Marcus Whitfield") |
| 2.11 | A.8 (status pill flips immediately) |
| 1.4 / W-G-1 | A.11 (upload returns 200) |

---

## Workflow B -- Applicant Attorney as booker

**Verifies:** W-B-1, W-B-2, W-A-1, AA auto-populate, fan-out for non-self-patient case.

### B.1 Register an AA via direct `/Account/Register` URL (W-B-1)

1. Open a clean browser context.
2. Browse directly to `http://localhost:44368/Account/Register`.
3. **Expected after 2.5 fix:** Form loads with Tenant dropdown + External User Role dropdown.
4. Pick Tenant = Dr Rivera 2, Role = Applicant Attorney, fill credentials, click Register.
5. **Expected:** Redirected to login OR a clear success/error indicator (NOT silent reset).
6. **Pre-fix workaround:** if direct URL fails silently, register via `/account/login` -> switch tenant -> Register link instead.

### B.2 Login as AA, open booking form

1. Login as the AA. Verify `currentUser.roles` includes `Applicant Attorney`.
2. Navigate to `/appointments/add?type=1`.
3. **Expected after W-B-2 / 2.6 fix:** No 404 from `/api/app/patients/me` (form should branch on role and call `/external-users/me` for AA). No global error modal.

### B.3 AA-section auto-populate

1. **Expected:** The Applicant Attorney section's First Name / Last Name / Email / firm fields are pre-filled from the booker's IdentityUser profile.
2. **After 2.4 / W-A-1 fix:** First Name + Last Name should be the AA's actual name (collected at register time), NOT the email and not "User".

### B.4 Fill remaining sections + submit

1. Fill Patient Demographics with synthetic data for the actual patient (e.g., a different name from the AA).
2. Fill Employer, Defense Attorney, Claim Information.
3. Submit. **Expected:** appointment created with `appointmentStatus: 1`.
4. **Email fan-out:** 6 recipients including booker (the AA), the patient (different person), DA, CE, office. Re-verify in Hangfire.

### Findings this workflow validates

| Finding | Verifier step |
|---|---|
| W-B-1 / 2.5 | B.1 (direct register URL works) |
| W-B-2 / 2.6 | B.2 (no /patients/me 404) |
| W-A-1 / 2.4 | B.3 (AA name not email-as-firstName) |
| W-A-2 / 1.2 | B.4 (6 recipients including a separate Patient) |

---

## Workflow C -- Defense Attorney as booker

**Verifies:** Whether DA section auto-populates similar to AA; whether DA can fill the AA section; same lookup-policy gates as Patient.

### C.1 Register + login as DA

1. Same as B.1 but pick Role = `Defense Attorney`. Email like `qa.da.<runid>@hcs.test`.
2. Login.

### C.2 Open booking form

1. Navigate to `/appointments/add?type=1`.
2. **Probe:** Does the DA section auto-populate with the booker's data? (Workflow B confirmed this for AA; need to verify for DA.)
3. **Probe:** Is the AA section editable / required? Does the form default-include or default-exclude AA when the booker is DA?
4. **Probe:** `/api/app/patients/me` should 404 (DA is not a Patient). Form should branch via 2.6 fix.

### C.3 Fill + submit

1. Same as Workflow A patient-side data + Helena Vargas as the AA + the DA auto-populated.
2. Submit.
3. **Email fan-out:** 6 recipients including the DA booker.

### Findings this workflow validates (in addition to Workflow B set)

- DA-side analogue of W-A-1, W-A-2, W-B-2.
- DA-section auto-populate parity with AA.

---

## Workflow D -- Claim Examiner as booker (negative test)

**Verifies:** Whether CE role can/should book at all (likely not -- claim examiners receive notifications, not initiate appointments).

### D.1 Register CE + login

1. Same shape as C.1 but Role = `Claim Examiner`. Email like `qa.ce.<runid>@hcs.test`.

### D.2 Probe home + booking form

1. Login. **Expected:** A specific home tile set; possibly NO `Book Appointment` action.
2. Try navigating to `/appointments/add?type=1` directly.
3. **Expected (after 1.3 lookup gates land):** Either redirect to `/403` OR the form loads but with the patient-side and AA-side sections gated off.

### D.3 If the form loads, attempt submit

- Document the actual permission boundary (what can CE do, what can't they).

### Findings this workflow validates

- Whether the role hierarchy treats CE as an external-user-with-bookers-perms or as a notify-only role. Currently CE has 0 CaseEvaluation permissions per Round 3 probes -- this workflow confirms that's the as-built behavior.

---

## Workflow E -- Send-back / resubmit (W2-9 read-only gate)

**Verifies:** F6, W-A-2 (send-back fan-out), the W2-9 read-only gate, race-free resubmit.

**Prerequisite:** A Pending appointment exists (run Workflow A through step A.5 then STOP -- do not approve).

### E.1 Tenant admin sends back

1. Login as tenant admin. Open the Pending appointment.
2. Pick Action = `Send back`. Click Submit.
3. Modal opens. Add a note: "Please provide a recent imaging report."
4. Flag a specific field (e.g., `panelNumber`).
5. Submit.
6. **Expected:** Status flips Pending -> AwaitingMoreInfo. `AppointmentSendBackInfo` row exists. 6 emails fire (one per recipient) with deep links `http://localhost:4200/appointments/view/<guid>`.

### E.2 Booker re-opens

1. Logout. Login as the original Patient booker.
2. Open the appointment-view page (link from email or via My Appointments table).
3. **Expected:**
   - AwaitingMoreInfo banner displays the flagged field + note.
   - **All ngModel inputs are disabled EXCEPT the flagged `panelNumber` field** (W2-9 gate).
   - The `Save & Resubmit` action is enabled.

### E.3 Edit + resubmit

1. Edit `panelNumber` to a new synthetic value (e.g., `PANEL-DEMO-0001`).
2. Click `Save & Resubmit`.
3. **Expected:**
   - In-flight Save completes BEFORE the Resubmit transition (no race).
   - Toast `Resubmitted to office` appears.
   - Status flips to Pending.
   - `isSaving` resets.
   - 6 emails fire announcing resubmit.

### Findings this workflow validates

- W2-9 read-only gate works.
- Send-back + resubmit emails fan out (W-A-2 fix verified across multiple status transitions).
- The save+resubmit isn't a race.

---

## Workflow F -- Reject flow

**Verifies:** Status enum, email subject differentiation.

**Prerequisite:** A Pending appointment exists.

### F.1 Reject

1. Login as tenant admin. Open the Pending appointment.
2. Pick Action = `Reject`, click Submit.
3. Confirmation modal opens, expected with English text (`Reject Appointment` / `Are you sure you want to reject?`) -- NOT raw i18n keys.
4. Click Reject.
5. **Expected:** Status flips to Rejected (whatever the enum value -- verify it's not `Approved` or `Pending`). Booker still sees the appointment in their My Appointments table with a Rejected badge.
6. **Email fan-out:** 6 emails with subject `Appointment <conf#> rejected`.

---

## Workflow G -- Document upload + approve cycle

**Verifies:** 1.4, document permission gates.

**Prerequisite:** An Approved appointment exists.

### G.1 Upload as office

1. As tenant admin, open the appointment view -> Documents card.
2. Type a document name. Click File -> choose `demo.pdf` (any benign PDF).
3. Click Upload. **Expected after 1.4 fix:** 200, row appears in the documents list.

### G.2 Magic-byte negative test

1. Save a `.txt` file as `evil.pdf`. Try to upload.
2. **Expected:** Server rejects with a `UserFriendlyException` toast naming the magic-byte mismatch.

### G.3 Multiple uploads + Reject reason cap

1. Upload 3 valid PDFs.
2. As tenant admin, click Approve on docs 1 and 2 (badges flip green).
3. Click Reject on doc 3 -> modal opens.
4. **Negative paths:**
   - Submit blank reason -> inline error.
   - Type 600 chars -> truncated to 500.
   - Submit with valid reason -> badge flips Rejected (red), reason renders below.

### G.4 Permission gate spot-check (external users hidden Approve/Reject)

1. Logout. Login as the Patient booker.
2. Open the same appointment view.
3. Documents card visible (Patient should see the docs uploaded for them).
4. **Expected:** Approve / Reject buttons are **hidden** (`CaseEvaluation.AppointmentDocuments.Approve` not granted). If they appear, severity = bug (HIPAA-relevant).

### G.5 Packet regen excludes Rejected docs

1. Re-login as tenant admin. Click Regenerate on the packet card.
2. Re-download. **Expected:** Approved docs (1, 2) are appended after the cover sheet; Rejected doc (3) is NOT included.

---

## Internal-role probes (I-1 / I-2 / I-3 / I-4)

**Verifies:** 1.5 (Clinic Staff / Staff Supervisor / Doctor grants), 3.5 (IT Admin role decision).

For each role:

### Setup
1. As tenant admin, navigate to `Identity > Users -> + New User`. Create a user with the corresponding role.
2. Logout.

### Per-role probe steps
1. Login as the role-test user.
2. **Verify menu visibility:** Which top-level menu items appear?
3. **Verify queue access:** Navigate to `/appointments`. Does the table render? Are rows visible?
4. **Verify approve action:** Open an appointment view. Does the action dropdown show Approve?
5. **Verify document approval:** On a doc row, do Approve/Reject icons appear?
6. **Verify packet:** Does the Generate Packet button appear and work?
7. **Verify dashboard:** Navigate to `/dashboard`. Counts populate?

### Expected after 1.5 fix

| Role | Queue | Approve appt | Approve docs | Generate Packet | Dashboard | Identity menu |
|---|---|---|---|---|---|---|
| **admin** | Yes | Yes | Yes | Yes | Yes | Yes |
| **Staff Supervisor** | Yes | Yes | Yes | Yes | Yes | No |
| **Clinic Staff** | Yes (own assigned) | No (only book/edit) | No | No | Yes (read) | No |
| **Doctor** | Yes (own appointments only) | No (read-only of own) | No | Maybe (own) | No | No |
| **IT Admin** (host or tenant) | No | No | No | No | No | Yes |

If any cell mismatches the seeded grants, that's a finding to log.

---

## Workflow H -- Admin invite link flow (D.2)

**Verifies:** D.2 link-only invite (no token, no expiry, no acceptance state machine), plus the 1.6 register-page handling of `?email=` and `?role=` query-string pre-fills.

### H.1 Open the invite UI

1. Login as `admin@dr-rivera-2.test` / `1q2w3E*` (D.1 seeded admin) OR `supervisor@dr-rivera-2.test` (Staff Supervisor) OR `it.admin@hcs.test` (host IT Admin).
2. Navigate manually to `http://localhost:4200/users/invite` (the route is registered in `app.routes.ts`; menu link is not added yet).
3. **Expected:**
   - Page title: "Invite External User".
   - **Yellow DEV-ONLY banner:** "Development-only feature. Invites are tenant-specific register links with no token, no expiry, and no acceptance state machine. The link is shown below after submission so you can copy + paste it manually..."
   - Form: Email input + Role dropdown showing exactly 4 options (Patient, Applicant Attorney, Defense Attorney, Claim Examiner).
   - **No internal-role options** (admin / Staff Supervisor / Clinic Staff / Doctor / IT Admin) -- D.2 enforces external-only via `IsExternalRoleType` check.

### H.2 Submit a valid invite

1. Fill Email = `qa.invitee.<runid>@hcs.test`, Role = `Applicant Attorney`.
2. Click "Create invite".
3. **Expected after D.2 fix:**
   - POST `/api/app/external-users/invite` returns 200 with body `{ inviteUrl, emailEnqueued: true, email, roleName: "Applicant Attorney", tenantName: "Dr Rivera 2" }`.
   - A green "Invite created" panel appears showing the 4 fields.
   - **The full invite URL is shown** in a read-only text input with a "Copy link" button. URL shape: `https://localhost:44368/Account/Register?__tenant=Dr+Rivera+2&email=qa.invitee.<runid>%40hcs.test&role=Applicant+Attorney` (URL-encoded).
   - Click "Copy link" -> "Copied!" confirmation, link in clipboard.
   - **Below the URL:** green check + "Email enqueued for delivery via Hangfire."

### H.3 Verify the invite email

1. Open Mailtrap inbox (https://mailtrap.io -> the project's sandbox).
2. **Expected:** New email "You have been invited to register at Dr Rivera 2" addressed to the invitee email.
3. Email body contains:
   - "**Dr Rivera 2** has invited you to register a portal account as **Applicant Attorney**."
   - "Register at Dr Rivera 2" button linking to the same URL.
   - Plain-text fallback link below the button.

### H.4 Open the invite link in a clean browser

1. Open the URL from H.2 in an incognito / clean browser session.
2. **Expected after 1.6 fix:**
   - Tenant banner: "Registering for **Dr Rivera 2**...".
   - User name + Email pre-filled with `qa.invitee.<runid>@hcs.test`.
   - Role dropdown pre-selected to `Applicant Attorney`.
   - Tenant indicator is read-only (no selector).

### H.5 Complete registration

1. Type a password (`1q2w3E*`).
2. Click Register.
3. **Expected:** Redirect to `/Account/Login`. Login with the email + password succeeds.

### H.6 Permission gate negative test

1. Logout. Login as a Patient user (e.g. `qa.patient.workflow-a@hcs.test`).
2. Navigate to `/users/invite`.
3. **Expected:** Page renders (no client-side guard blocks it -- guard is `authGuard` only).
4. Submit any invite. **Expected after D.2 role-based authz:** API returns 403 Forbidden because the AppService method has `[Authorize(Roles = "admin,Staff Supervisor,IT Admin")]`. The Angular UI surfaces the error in the red error panel: "Forbidden" or similar.

### Findings this workflow validates

- D.2 invite endpoint creates a working tenant-pre-filled URL.
- D.2 + 1.6: invite link round-trips correctly (banner shows tenant; email + role pre-filled).
- D.2 + 6.1: invite email body uses the tenant-specific register CTA template.
- D.2 + 5.7: email actually delivers (via Mailtrap) instead of NullEmailSender swallowing.
- D.2 role-based authz blocks external users from inviting.

---

## Probe-1 -- Anonymous external-user-lookup (W-X-2)

**Verifies:** 0.1.

### Steps
```
curl 'http://localhost:44327/api/public/external-signup/external-user-lookup?__tenant=Dr%20Rivera%202'
```

- **Pre-fix:** Returns full external-user list with names and emails.
- **Expected after 0.1 fix:** Returns 401 Unauthorized (or 403 if auth required + missing).

Repeat with a valid bearer token from any external user. **Expected:** 200 with the same list (now properly authenticated).

---

## Probe-2 -- Edit modal scope (W-X-4)

**Verifies:** 2.3.

### Steps
1. As tenant admin, navigate to `/appointments`.
2. Actions -> Edit on any row.
3. Inspect the modal.

- **Pre-fix:** Only 4 fields (panelNumber, appointmentDate, requestConfirmationNumber, dueDate).
- **Expected after 2.3 fix:** Either the Edit action is removed entirely (fix path A: send users to View page for edits), OR the modal shows the same sections as the booking form (fix path B).

---

## Probe-3 -- Direct `/Account/Register` URL (W-B-1)

**Verifies:** 2.5.

### Steps
1. Clean browser session.
2. Browse directly to `http://localhost:44368/Account/Register`.
3. Pick Tenant = Dr Rivera 2 from the dropdown.
4. Pick Role = Patient.
5. Fill credentials, click Register.

- **Pre-fix:** Form silently resets without creating the user.
- **Expected after 2.5 fix:** Either redirect to login OR explicit success message; user appears in Identity > Users.

---

## Probe-4 -- Identity > Users with role Doctor auto-creates Doctor entity (W-X-9)

**Verifies:** 3.1.

### Steps
1. As tenant admin, Identity > Users > + New User.
2. Set role = Doctor. Save.
3. `GET /api/app/doctors?MaxResultCount=20`.

- **Pre-fix:** New IdentityUser exists but Doctor count unchanged.
- **Expected after 3.1 fix:** Doctor row exists with the IdentityUser linked.

---

## Probe-5 -- Tenant creation assigns admin role to admin user

**Verifies:** 3.2.

### Steps
1. As host admin, Saas/Tenants -> + New tenant.
2. Tenant name, admin email = `qa.test-tenant-admin@hcs.test`, password.
3. After save, login directly as `qa.test-tenant-admin@hcs.test` (without going through "Login with this tenant").
4. Check `/api/abp/application-configuration` -> `currentUser.roles`.

- **Pre-fix:** `roles: []`.
- **Expected after 3.2 fix:** `roles: ['admin']` and granted policies match the admin role.

---

## Probe-6 -- External-user invite UI / endpoint (Original Finding 2)

**Verifies:** 3.6 (product decision pending).

### Pre-decision verification (current state)
1. As tenant admin, look in every plausible UI surface for an "Invite External User" / "Add External User" / "Send Invite" button:
   - `/identity/users` (tenant scope)
   - `/saas/tenants` -> Actions
   - any custom feature page
2. **Expected (current):** No invite UI exists.
3. Search Swagger at `http://localhost:44327/swagger/index.html` for `Invite`, `Send`, `Notify`.
4. **Expected (current):** No invite endpoint exists; only `/api/public/external-signup/{tenant-options,external-user-lookup,register}`.

### Path A -- "reframe demo as self-register" (no code change)
Document that the demo narrative explicitly says "tenant admin shares the registration URL with the candidate; candidate self-registers." No replay needed beyond this confirmation.

### Path B -- "build a real invite endpoint" (post-fix verification)
After the fix lands:
1. As tenant admin, navigate to the new Invite UI (likely `/identity/users` -> "Invite External User" button OR `/external-signup/invites` page).
2. Fill `qa.invitee.<runid>@hcs.test`, role = Patient, click Invite.
3. **Expected:** Toast confirms invite sent. Hangfire fires a `SendInviteEmailJob` to that address with subject `You're invited to register with Dr Rivera 2` and a body containing a tokenized link to `/Account/Register?token=...&tenant=Dr%20Rivera%202`.
4. In a clean browser context, open the link. The Register form pre-fills the email + tenant + role; only Password is editable.
5. Complete registration. **Expected:** User is created. The invite token is single-use (verify by re-opening the link -> "Invite expired or already used" message).

---

## Probe-7 -- Slot creation Doctor selector (Original Finding 9)

**Verifies:** 3.7 (product decision pending).

### Pre-decision verification (current state)
1. As tenant admin, navigate to `Doctor Management -> Doctor Availabilities -> Add`.
2. Inspect the form fields.
3. **Expected (current):** No Doctor dropdown. Slots bind to Location only. Slot list table has columns `Location, AppointmentDate, AvailableSlot, BookedSlot, ReservedSlot, TotalSlot, Action` -- no Doctor column.

### Path A -- "keep slots per-Location" (no code change)
Document this decision in `docs/features/doctor-availabilities/overview.md`. Update the demo narrative to explain "any doctor at this location can fulfill the slot."

### Path B -- "switch to slots per-Doctor" (post-fix verification)
After the fix lands:
1. Slot creation form should have a `Doctor *` dropdown listing the doctors in the tenant.
2. Slot list grid should have a `Doctor` column.
3. The booker's `Appointment Time` slot picker (in `/appointments/add?type=1`) should show the doctor's name on each slot.
4. Generate slots for two different doctors at the same location + date + time. **Expected:** Both blocks accepted (different doctors don't conflict). Same-doctor + same-time-window across locations should still be rejected.
5. Approve a booked slot -> verify the appointment is linked to the correct doctor (not just the location).

---

## Probe-8 -- Re-evaluation booking workflow (W-H-1)

**Verifies:** 3.4.

### Pre-decision verification (current state)
1. Navigate to `/appointments/add?type=1` -- save a snapshot of the section list.
2. Navigate to `/appointments/add?type=2` -- save a snapshot.
3. **Expected (current):** Both snapshots are byte-identical for section list and field set.

### Path A -- "drop the `?type=2` route" (no Re-eval distinction)
Remove the route from the home tile and the Angular route table. Verify `/appointments/add?type=2` 404s after the fix.

### Path B -- "implement Re-evaluation distinction" (post-fix verification)
After the fix lands:
1. `/appointments/add?type=2` shows a "Linked Appointment" picker at the top (selecting which prior appointment this is a re-eval of).
2. Some Demographic / Employer fields are pre-populated read-only from the linked appointment.
3. Email subjects use "Re-evaluation" wording.
4. Packet cover sheet labels the appointment as "Re-evaluation".

---

## Probe-9 -- Mailtrap delivery + 5.7 NullEmailSender gate

**Verifies:** 5.7 (gate flips on `REPLACE_*` placeholder credentials), 6.1-6.3 (per-recipient template branching delivers actual mail when SMTP is real).

### Pre-conditions

- `docker/appsettings.secrets.json` has real SMTP credentials (not starting with `REPLACE_`). Mailtrap sandbox values are sufficient for delivery verification.
- A Mailtrap inbox configured and visible at `https://mailtrap.io/inboxes/<id>/messages`.

### 9.A Real-credentials path (positive)

1. Submit an appointment via Workflow A through A.5.
2. Wait ~10 seconds for the Hangfire fan-out.
3. Open Mailtrap inbox.
4. **Expected:** N emails received (one per resolved recipient -- typically 4-6 depending on which party emails were filled).
5. Per-recipient verification:
   - **Office mailbox**: subject "New appointment request <conf#>", title "A new appointment request was submitted".
   - **Booker (registered)**: subject "Appointment requested - <conf#>", title "An appointment was requested", body has "Open patient portal" button to `http://localhost:4200`.
   - **Helena Vargas (AA, not registered)**: subject "Appointment requested - register to view <conf#>", body has "Register as applicant attorney" button to `https://localhost:44368/Account/Register?__tenant=Dr+Rivera+2&email=helena.vargas%40aaattorney.test&role=ApplicantAttorney`.
   - Same shape for DA + CE (if filled).
6. Open the register link from Helena's email. **Expected:** the 1.6 banner shows "Registering for **Dr Rivera 2**", email + role pre-filled.

### 9.B Placeholder-credentials path (negative -- regression for 5.7)

1. Edit `docker/appsettings.secrets.json` -- set:
   ```json
   "Abp.Mailing.Smtp.UserName": "REPLACE_WITH_ACS_USERNAME",
   "Abp.Mailing.Smtp.Password": "REPLACE_WITH_ACS_PASSWORD_CONNECTION_STRING"
   ```
2. `docker compose up -d --force-recreate api` to reload the module.
3. Submit a new appointment via Workflow A.
4. **Expected:**
   - API logs: `[INF] InternalUsersDataSeedContributor: skipping (not Development environment)` does NOT appear (we're in Development); but `CaseEvaluationDomainModule` registers the NullEmailSender swap because both keys start with `REPLACE_`.
   - Mailtrap inbox: NO new emails arrive (NullEmailSender swallows).
   - API logs: `[INF] SendAppointmentEmailJob: delivered (Submission/...)` does NOT appear (the no-op sender returns immediately).
5. Restore the real Mailtrap credentials and `--force-recreate api` to revert.

---

## Probe-10 -- Internal seeded users (D.1 + W-UI-16)

**Verifies:** D.1 (`InternalUsersDataSeedContributor` seeds users + assigns roles per tenant + links Doctor entity).

### Pre-conditions

- DbMigrator has run with `ASPNETCORE_ENVIRONMENT=Development` (the default in `docker-compose.yml`).

### 10.A Verify users exist via SQL

```bash
docker exec main-sql-server-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'myPassw@rd' -C -d CaseEvaluation -h-1 \
  -Q "SET NOCOUNT ON; SELECT u.UserName, r.Name FROM AbpUsers u JOIN AbpUserRoles ur ON ur.UserId=u.Id JOIN AbpRoles r ON r.Id=ur.RoleId WHERE u.Email LIKE '%.test' ORDER BY u.UserName;"
```

**Expected:** rows for every entry in the table from "Setup prerequisites" (admin@dr-thomas-1.test through it.admin@hcs.test, with the right role per row).

### 10.B Verify Doctor entity link

```bash
docker exec main-sql-server-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'myPassw@rd' -C -d CaseEvaluation -h-1 \
  -Q "SET NOCOUNT ON; SELECT d.Id, u.Email FROM AppDoctors d JOIN AbpUsers u ON u.Id = d.IdentityUserId WHERE u.Email LIKE 'doctor@%';"
```

**Expected:** one row per tenant -- `doctor@dr-thomas-1.test` and `doctor@dr-rivera-2.test` each linked to a Doctor entity.

### 10.C Login as each role + verify granted policies

For each user in the seeded users table (Probe pre-conditions):

1. Login at `/account/login` with the email + `1q2w3E*`.
2. Open `/api/abp/application-configuration` in dev tools / via curl with the bearer token.
3. **Expected `currentUser.roles` and `auth.grantedPolicies`** match the role's grant set per the Probe I-N expected matrix below.

### 10.D Idempotency

1. `docker compose up -d --force-recreate db-migrator` to re-run the seeder.
2. **Expected:** db-migrator log shows "skipping" or no "created user" lines (the seeder finds users by email and skips). No duplicate user rows in 10.A.

---

## Probe-11 -- Auto-link on registration + appointment-list visibility (5.2 + 2.2)

**Verifies:** 5.2 (`ExternalSignupAppService.RegisterAsync` backfills join rows for AA / DA when an appointment captured the email at booking time) + 2.2 (appointment list narrows to caller's involvement).

### Pre-conditions

- An appointment exists with `ApplicantAttorneyEmail = helena.vargas@aaattorney.test` and `DefenseAttorneyEmail = brent.locke@dlaw.test` (from Workflow A submission).
- Neither helena.vargas nor brent.locke is registered yet.

### 11.A Register Helena as AA via the invite link from Probe-9.A

1. Open the invite URL from Helena's email (or build manually with `?__tenant=Dr+Rivera+2&email=helena.vargas%40aaattorney.test&role=ApplicantAttorney`).
2. Complete registration with password `1q2w3E*`.

### 11.B Verify AppointmentApplicantAttorney row created

```bash
docker exec main-sql-server-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'myPassw@rd' -C -d CaseEvaluation -h-1 \
  -Q "SET NOCOUNT ON; SELECT aa.Id, aa.AppointmentId, u.Email FROM AppAppointmentApplicantAttorneys aa JOIN AbpUsers u ON u.Id = aa.IdentityUserId WHERE u.Email = 'helena.vargas@aaattorney.test';"
```

**Expected:** one row per appointment that named her email at booking. The 5.2 `AutoLinkAppointmentsForUserAsync` ran during her register and backfilled the join.

### 11.C Helena sees the appointment in her queue

1. Login as `helena.vargas@aaattorney.test`.
2. Navigate to `/appointments`.
3. **Expected after 2.2 + 5.2:** The Marcus Whitfield appointment appears in her list. `totalCount = 1+`.

### 11.D Brent (DA) parallel verification

1. Repeat 11.A-C for Brent Locke -- register via the DA invite URL.
2. **Expected:** A new `DefenseAttorney` entity row is created (D.2 / 5.2 `AutoLinkDefenseAttorneyAsync`), an `AppointmentDefenseAttorney` join row created, Brent's queue shows the appointment.

---

## Probe-12 -- Doctor user own-appointment view (D.1 / W-DOC-1 tracking)

**Verifies:** the seeded doctor user can log in and view appointments. Row-level "own appointments only" filter is NOT yet enforced (W-DOC-1 / next-pass tracking).

1. Login as `doctor@dr-rivera-2.test` / `1q2w3E*`.
2. Navigate to `/appointments`.
3. **Expected:** queue table renders; Doctor sees ALL appointments in the tenant (per W-DOC-1 -- to be tightened to own-doctor-only in a future pass when the multi-doctor model lands).
4. Open any appointment -> view page renders.
5. **Action dropdown:** Approve / Reject / Send Back are NOT visible (Doctor lacks `Appointments.Edit`); only Review-style read.
6. **Generate Packet:** button visible (Doctor has `AppointmentPackets.Regenerate`).
7. Navigate to `/doctor-management/doctor-availabilities` -> Add. Form loads (Doctor has `DoctorAvailabilities.{Default,Create,Edit}`).

---

## Probe-13 -- canEdit gate for admin (5.3b)

**Verifies:** admin user can edit non-AwaitingMoreInfo appointment fields (regression check for W-VIEW-10).

1. Login as `admin@dr-rivera-2.test`.
2. Open a Pending appointment view.
3. **Expected after 5.3b:**
   - Patient Demographics inputs are ENABLED for editing.
   - AA / DA section inputs are ENABLED.
   - Appointment-level fields (panelNumber, dueDate) are ENABLED.
   - canTakeOfficeAction returns true -> Approve/Reject/Send Back dropdown visible.
4. Edit a field, click Save. **Expected:** save succeeds, success toast.

---

## Probe-14 -- Slot generation 0-slot UX message (7.4)

**Verifies:** 7.4 inline error when slot generation produces 0 slots due to inverted/zero-duration inputs.

### Pre-conditions

- Login as `admin@dr-rivera-2.test` (or any user with `DoctorAvailabilities.Create`).

### 14.A Inverted date range

1. Navigate to `Doctor Management -> Doctor Availabilities -> Add`.
2. Fill SlotByDates mode, FromDate = `2026-06-01`, ToDate = `2026-05-01` (FromDate > ToDate).
3. Fill all other fields with valid values.
4. Click Generate (or whatever the button is).
5. **Expected after 7.4:** Inline message appears in the validationMessage block: "No slots were generated. Check that your start date is before your end date and your start time is before your end time." Submit button stays disabled.

### 14.B Inverted time

1. Fix dates to a valid range. Set FromTime = `17:00`, ToTime = `09:00`.
2. Click Generate.
3. **Expected:** Same message.

### 14.C Zero-duration

1. Fix times to FromTime = ToTime = `09:00`.
2. Click Generate.
3. **Expected:** Same message.

### 14.D Conflict path takes priority

1. Fix all inputs to valid + non-zero. Click Generate -> previews populate.
2. Submit. Then re-Add and try to generate the same block again.
3. **Expected:** "Some generated slots already exist..." (existing conflict message) appears, NOT the 7.4 zero-slot message.

---

## Cross-tenant isolation probe set (W-X-1)

**Verifies:** that the cross-tenant isolation passes survive each fix landing.

### Steps
1. Capture three appointment GUIDs from Dr Rivera 2 via `/api/app/appointments?MaxResultCount=10` while logged in as Dr Rivera 2 admin.
2. Logout. Login as a Dr Thomas 1 admin (or any user authenticated to Thomas).
3. For each Rivera GUID, attempt:
   - `GET /api/app/appointments/<rivera-guid>` -> expect 403 or 404
   - `GET /api/app/appointments/<rivera-guid>/packet` -> expect 403 or 404
   - `GET /api/app/appointments/<rivera-guid>/documents` -> expect 403 or 404
   - `GET /api/app/appointment-change-logs/by-appointment/<rivera-guid>` -> expect 403 or 404
4. Inspect Thomas dashboard counters -- should reflect Thomas data only, no Rivera leakage.

- **Currently passing.** Add this probe to the regression suite to catch any future leak.

---

## Email fan-out audit table (run after each Tier-1 + Tier-2 fix lands)

For each lifecycle action, count the `SendAppointmentEmailJob` entries in Hangfire's Succeeded queue and confirm against expected:

| Action | Expected jobs | Recipients |
|---|---|---|
| Submit (booker = Patient) | 6 | booker, AA, DA, CE, office, (patient = booker so possibly 5 deduped) |
| Submit (booker = AA) | 6 | booker, patient, AA = booker, DA, CE, office (5 deduped) |
| Send-back | 6 | same set |
| Resubmit | 6 | same set |
| Approve | 6 | same set |
| Reject | 6 | same set |

Pre-fix, every action above produces exactly 1 job (booker only). Post-1.2, expect 6 unique recipients (or 5 if dedup is desired).

---

## Lifecycle status transitions (run for each appointment per workflow)

Use this checklist when running an end-to-end workflow:

| Stage | Action | Pre status | Post status | Side effects |
|---|---|---|---|---|
| Submit | Booker clicks Save | -- | Pending | 1 row in /appointments; slot Available -> Reserved; 6 emails |
| Send back | Tenant admin Send Back | Pending | AwaitingMoreInfo | SendBackInfo row created; 6 emails |
| Resubmit | Booker Save & Resubmit | AwaitingMoreInfo | Pending | 6 emails |
| Reject | Tenant admin Reject | Pending | Rejected | 6 emails; appointment locked |
| Approve | Tenant admin Approve | Pending | Approved | appointmentApproveDate set; 6 emails; packet job enqueued; slot Reserved -> Booked |
| Packet ready | (Hangfire job completes) | Approved | Approved | Packet row created; PDF in storage |

---

## Where to record new findings

Any new finding discovered during a replay should be added to `docs/reports/2026-04-29-wave-2-demo-lifecycle.md` using the existing Finding-block format and added to the FIX PRIORITY ORDER section at the appropriate Tier.
