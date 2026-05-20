# Patient Portal Hardening Test Suite

**Purpose.** Repeatable end-to-end + API + DB validation of the core booking
pipeline (slot-gen -> register -> book -> approve -> packet -> upload ->
scope -> auth), runnable against a Docker stack that starts near-empty (only
2 internal users seeded). Produces a deterministic finding set without
relying on hard-coded confirmation numbers or date offsets.

This doc is the **canonical** test plan for hardening passes. The earlier
ad-hoc plan at `docs/plans/2026-05-14-core-process-hardening.md` was a
one-off session log; this suite supersedes it.

Findings filed during a run land in `docs/runbooks/findings/bugs/` with
stable IDs (`BUG-NNN`, `OBS-N`, `SEED-N`). The findings index lives at
`docs/runbooks/findings/2026-05-13-userflow-findings.md` (rename the date
when starting a fresh quarter).

Template-review output for Phase 10 lands at
`docs/runbooks/findings/template-review-<run-date>.md`.

---

## Start prompt

Paste the block below into a fresh Claude session at `W:\patient-portal\main`
to begin a hardening run. The agent will load the suite, confirm
prereqs, walk Phase 0 through Phase 10 and Rounds 1-3, file findings, and
pause at every WAIT marker until Adrian confirms.

```
You are executing the Patient Portal Hardening Test Suite at
docs/runbooks/HARDENING-TEST-SUITE.md.

Process:
1. Read the suite in full. Confirm every Prerequisite is met before
   running any scenario. If a prereq fails, stop and surface the gap.
2. Create the run-state file at .hardening-run/<YYYY-MM-DD>.json with the
   schema documented in Part 2. Choose a runPrefix of the form "hrd-<MMDD>"
   (e.g. hrd-0519). Every synthetic email/name uses that prefix as a
   suffix so reruns do not collide.
3. Execute Phase 0 (slot generation as stafsuper1@gesco.com). Verify equal
   slot counts per appointment type before continuing.
4. Execute Phase 1 (registration). Two manual self-registrations + two
   invite-flow registrations. After capturing each verify/invite URL,
   print it inside a fenced block tagged
   "=== WAIT FOR ADRIAN: verify <email> ===" and STOP until Adrian
   responds "verified <email>". Persist progress in the state file.
5. Execute Phases 2-8, then Phase 9 (auth probes) and Phase 10 (template
   review). Every scenario reads identifiers from state.* -- NEVER from
   hard-coded confirmation numbers or "T+Nd" offsets.
6. Execute Round 2 (major failure-mode probes).
7. Execute Round 3 (replay sweep of open findings).
8. For every Fail, file a finding under docs/runbooks/findings/bugs/
   using the existing BUG-NNN / OBS-N / SEED-N naming. Include:
     - frontmatter (id, title, severity, status, found, flow, component)
     - Symptom (exact reproduction with HTTP status + body or DB row)
     - Hypothesis (3 competing theories when not obvious)
     - Recommended fix
     - Related (cross-link to other findings with [[BUG-NNN]] syntax)
9. Test data: synthetic only. Use the dictionary in Part 5 of this doc.
   Never invent real-looking SSN/MRN/DOB. Every synthetic email ends in
   @gesco.com or @example.test, never a real provider.
10. Do not commit or push. The hardening run produces docs + findings,
    not code changes.
11. After Phases + Rounds complete: write a summary message with pass/fail
    counts, list of new findings (ID + one-liner), and current DB state
    (appointment count by status). Ask before continuing to any
    follow-up work.

Constraints:
- Docker-only dev. NEVER run dotnet run or ng serve directly.
- No code edits unless explicitly asked.
- Treat DB writes as destructive; use SQL only for read verification.
- File-upload via Playwright DataTransfer is unreliable -- use the real
  file chooser (browser_file_upload tool) when a chooser is active,
  otherwise skip the scenario and note OBS-20.
- The ngb-datepicker is unreliable after multiple open/close cycles in
  one session. Reload the page (browser_navigate) before each booking
  scenario.
- For Round 2 + Round 3 fast-path probing, prefer direct API calls
  (fetch with Bearer token from localStorage) over UI cycling -- 5-10x
  faster and more deterministic.
- WAIT markers are HARD STOPS. Do not auto-click verify links via
  Hangfire arg extraction. Adrian verifies by hand.

If a finding indicates a blocker for the next scenario, document and
move on. Do not attempt to fix.
```

---

## Part 1: Prerequisites

Each must be true before any scenario runs. The agent must verify all
seven, in order, and stop if any fails.

| # | Check | How to verify |
| --- | --- | --- |
| 1 | Stack on canonical ports (4200 / 44368 / 44327 / 1433 / MinIO 9000 / Redis 6379 / Gotenberg 3000) | `docker compose ps` shows 7 services healthy |
| 2 | DB has tenant Falkinstein with non-empty `AppSystemParameters` row | SQL: `SELECT TenantId, AppointmentLeadTime FROM AppSystemParameters` -> at least 1 row matching the user's tenant |
| 3 | At least one active doctor + at least one active location exist (slots will be generated in Phase 0) | SQL: `SELECT COUNT(*) FROM AppDoctors WHERE IsActive=1` >= 1 AND `SELECT COUNT(*) FROM AppLocations WHERE IsActive=1` >= 1 |
| 4 | Only the two seeded internal users exist; the other 10 in Part 5 must NOT exist yet | SQL: `SELECT UserName FROM AbpUsers WHERE UserName IN ('stafsuper1@gesco.com','clistaff1@gesco.com')` returns exactly 2; same query with the other 10 returns 0 |
| 5 | `stafsuper1@gesco.com` has Staff Supervisor role grant; `clistaff1@gesco.com` has Clinic Staff role grant | SQL: `SELECT u.UserName, r.Name FROM AbpUserRoles ur JOIN AbpUsers u ON u.Id=ur.UserId JOIN AbpRoles r ON r.Id=ur.RoleId WHERE u.UserName IN ('stafsuper1@gesco.com','clistaff1@gesco.com')` |
| 6 | Standard test password works for both seeded users | `1q2w3E*r` (synthetic, documented) |
| 7 | SMTP creds present in `docker/appsettings.secrets.json` | bind-mounted to both auth + api containers; never echo the password back |

If any check fails: see the Prep section of
`docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md` for the canonical
bootstrap recipe.

---

## Part 2: Scenario format + run-state file schema

### Run-state file schema

Every run writes a single state file at
`.hardening-run/<runDate>.json` (gitignored). Every scenario reads from
and writes to this file. Hard-coded confirmation numbers and `T+Nd`
offsets are forbidden -- use the variables below.

```json
{
  "runDate": "YYYY-MM-DD",
  "runPrefix": "hrd-MMDD",
  "users": {
    "patient1": { "email": "patient1@gesco.com", "fullName": "Daniel Harper",
                  "registeredAt": "ISO", "verifiedAt": "ISO|null",
                  "role": "Patient", "registrationFlow": "manual|invite" },
    "patient2": { ... },
    "appatty1": { ... },
    "appatty2": { ... },
    "defatty1": { ... },
    "defatty2": { ... },
    "claimE1":  { ... },
    "claimE2":  { ... },
    "stafsuper1": { "email": "stafsuper1@gesco.com", "seeded": true, ... },
    "stafsuper2": { ... },
    "clistaff1":  { "email": "clistaff1@gesco.com", "seeded": true, ... },
    "clistaff2":  { ... }
  },
  "verifications": [
    { "email": "...", "url": "...", "capturedAt": "ISO",
      "verifiedByAdrianAt": "ISO|null", "status": "pending|verified|expired" }
  ],
  "slots": {
    "ame":        { "count": N, "earliestDate": "YYYY-MM-DD" },
    "qme":        { ... },
    "panelQme":   { ... },
    "deposition": { ... },
    "recordReview":         { ... },
    "supplementalReport":   { ... }
  },
  "appointments": {
    "p3.1-patient":     { "confirmationNumber": "A?????", "id": "guid",
                          "slotDate": "YYYY-MM-DD", "approvedAt": "ISO|null",
                          "rejectedAt": "ISO|null" },
    "p3.2-aa-booker":   { ... },
    "p3.3-da-booker":   { ... },
    "p3.4-ce-booker":   { ... },
    "p3.5-staff-booker":{ ... }
  }
}
```

The agent owns this file. If a scenario fails partway, the state file
preserves progress; reruns resume from the last completed step. Adrian's
"verified" replies update the `verifications[]` rows.

### Happy-path scenario (Round 1, Phases 0-10)

```
HRD-<phase>.<n>  <Title>
Role            <which logged-in user, by state.users[<key>]>
Booker / actor  <who initiates>
Inputs          <synthetic values; reference Part 5 dictionary + state.*>
Steps
  1. <action>
  2. <action>
Expected
  - <api/ui outcome>
  - <db state>
Verify SQL
  <one-line query that distinguishes pass from fail; parameterized by state.*>
Persist
  <state.* keys this scenario writes>
Pass criteria   <hard rule>
On fail -> file <BUG-NNN candidate or "raise new BUG">
```

### Failure-mode scenario (Round 2)

```
HRD-R2.<n>  <Title>
Endpoint        <method + path>
Auth            <role token from state.users[<key>], or anonymous>
Body            <minimal payload that triggers the failure; uses state.* refs>
Expected status <HTTP code>
Expected code   <error code constant>
Expected data   <validation field names or BusinessException data shape>
Anti-checks     <must NOT echo PHI; must NOT 500; must NOT silently succeed>
On fail -> file <BUG-NNN candidate>
```

### Replay scenario (Round 3)

```
HRD-R3.<finding-id>  <Title from the finding file>
Repro           <copied verbatim from docs/runbooks/findings/bugs/<id>.md>
Expected if fixed <what a passing run looks like now>
Action on outcome
  - confirmed-open  -> update the finding's `last-replayed: <run-date>`
  - now-passing     -> append "Resolved <run-date> via hardening run" to the
                       finding, set status: fixed
  - inconclusive    -> note in run summary, keep finding open
```

---

## Part 3: Round 1 - Happy paths

Run in order. Scenarios depend on prior scenarios' state. If a scenario
fails, document and skip dependents (note the cascade in the finding).

### Phase 0 - Slot generation (driven by Staff Supervisor)

Adrian's directive: NEVER seed the doctor. Every slot used downstream is
created in this phase via the staff-supervisor-driven UI / API path.

```
HRD-P0.1  Generate equal slots for every active AppointmentType x active Location
Role        stafsuper1@gesco.com
Inputs
  - window: today+3 days .. today+60 days (in tenant TZ)
  - slot duration: 30 minutes (default; confirm via SystemParameter)
  - target slots per type: SAME count across types (e.g., 1 slot/day/type/location
    over the window; equal-count is the assertion)
Steps
  1. Log in as stafsuper1@gesco.com.
  2. For each (AppointmentType, Location) pair returned by
     GET /api/app/appointment-types and GET /api/app/locations
     (active rows only):
       a. POST /api/app/doctor-availabilities/generate-preview with
          { doctorId, locationId, appointmentTypeId,
            startDate = today+3 days, endDate = today+60 days,
            dailyStartTime, dailyEndTime, durationMinutes,
            weekdays = [Mon..Fri] }.
       b. POST /api/app/doctor-availabilities/create with the preview payload.
  3. Persist state.slots[<type>].count and state.slots[<type>].earliestDate
     from the verify SQL below.
Expected
  - HTTP 200 on every generate-preview + create call.
  - SQL row count per AppointmentType differs by at most +/- 1 across types.
Verify SQL
  SELECT at.Name, COUNT(*) AS slots
  FROM AppDoctorAvailabilities a
  JOIN AppAppointmentTypes at ON at.Id = a.AppointmentTypeId
  WHERE a.AvailableDate BETWEEN DATEADD(day, 3, GETDATE())
                            AND DATEADD(day, 60, GETDATE())
    AND a.BookingStatusId = 8
  GROUP BY at.Name
  ORDER BY at.Name;
Persist   state.slots.<typeKey> for each row
Pass criteria
  - Every active AppointmentType returns at least 1 slot in the window.
  - max(slots) - min(slots) <= 1.
  - Earliest slot per type is >= today+3 days.
On fail
  - 0 slots for one type -> raise SEED-3 follow-up (still open) and stop.
  - Unequal counts > 1 -> file new OBS about generate-preview not handling
    weekend skips uniformly; continue if every type has >= 1 slot.
  - 5xx from generate-preview -> capture container logs, raise new BUG.
```

### Phase 1 - Registration (3 sub-phases)

Adrian's directive: 2 users via the manual `/Account/Register` form, 2 users
via the Invite External User flow. Adrian verifies each of the 4 verification
or invite-acceptance links manually by clicking them in his real inbox or
via the URL captured in the WAIT block.

The remaining 6 external users (`patient2`, `appatty2`, `defatty2`,
`claimE2`, `stafsuper2`, `clistaff2`) are NOT registered in this run. They
exist in the dictionary for future expansion; the suite passes without
them.

#### Phase 1.A - Manual self-register (2 users)

```
HRD-P1.A.1  Manual self-register: Patient (patient1@gesco.com / Daniel Harper)
Inputs (synthetic; from Part 5 dictionary)
  - email     = patient1@gesco.com
  - password  = 1q2w3E*r
  - firstName = "Daniel"   lastName = "Harper"
  - userType  = 1 (Patient)
Steps
  1. Log out any prior session (clear browser storage).
  2. Navigate to /Account/Register on the Falkinstein subdomain.
  3. Fill the form. Pick role Patient. Accept T&C. Submit.
  4. Wait for the success card. Trigger or wait for the verification email
     send. If the registration POST triggers the send automatically
     (post B-4), proceed; otherwise click "Verify Email" on the success
     card OR call POST /api/public/external-account/resend-email-verification.
  5. Capture the verifyUrl from one of:
       - SMTP inbox (if available); OR
       - Hangfire job arguments:
         SELECT TOP 1 InvocationData FROM HangFire.Job
           WHERE InvocationData LIKE '%patient1@gesco.com%'
           ORDER BY Id DESC
Persist
  - state.users.patient1 = { email, fullName: "Daniel Harper",
      registeredAt: now, registrationFlow: "manual", role: "Patient" }
  - state.verifications.append({ email: patient1@..., url: <captured>,
      capturedAt: now, status: "pending" })
WAIT FOR ADRIAN
  Print the verifyUrl inside a fenced block tagged
    === WAIT FOR ADRIAN: verify patient1@gesco.com ===
    <url>
    ============================================================
  STOP. Resume only after Adrian replies "verified patient1@gesco.com".
  Update state.verifications[].status = "verified" and
  state.users.patient1.verifiedAt = now.
Verify SQL (after WAIT lifts)
  SELECT NormalizedEmail, EmailConfirmed
  FROM AbpUsers
  WHERE NormalizedEmail = 'PATIENT1@GESCO.COM'
Pass criteria: EmailConfirmed = 1.
On fail
  - 404 on verifyUrl -> [[BUG-006]] regressed (URL points at SPA route).
  - 500 on /Account/EmailConfirmation -> [[BUG-014]] regressed.
  - 302 with ?flash=verification-invalid -> token decode failed; check
    DataProtection key sharing.
```

```
HRD-P1.A.2  Manual self-register: Applicant Attorney (appatty1@gesco.com / Marcus Bennett)
Identical shape to P1.A.1, but:
  - userType  = 3 (Applicant Attorney)
  - firmName  = "Bennett & Associates" (required for attorney roles)
  - firstName = "Marcus"   lastName = "Bennett"
WAIT FOR ADRIAN: verify appatty1@gesco.com
Persist:
  - state.users.appatty1 with registrationFlow="manual", role="ApplicantAttorney"
```

#### Phase 1.B - Invite-flow register (2 users)

```
HRD-P1.B.1  Invite-flow register: Defense Attorney (defatty1@gesco.com / Gregory Stone)
Role        stafsuper1@gesco.com (issues the invite)
Inputs
  - inviteEmail = defatty1@gesco.com
  - inviteRole  = "DefenseAttorney" (ExternalUserType enum value)
Steps
  1. Log in as stafsuper1@gesco.com.
  2. Navigate to the staff supervisor UI for "Invite External User"
     (or call POST /api/app/external-signup/invite directly).
  3. Submit { email: defatty1@..., role: DefenseAttorney }.
  4. Capture the invite URL from:
       - SMTP inbox; OR
       - HangFire.Job InvocationData for the InviteExternalUser template
         row matching the email; OR
       - The InvitationManager.IssueAsync return value if calling the API
         directly (raw token is returned exactly once).
Persist
  - state.users.defatty1 = { email, fullName: "Gregory Stone",
      registeredAt: null, role: "DefenseAttorney", registrationFlow: "invite" }
  - state.verifications.append({ email: defatty1@..., url: <captured>,
      capturedAt: now, status: "pending" })
WAIT FOR ADRIAN
  Print the invite URL inside a fenced block:
    === WAIT FOR ADRIAN: invite defatty1@gesco.com ===
    <url>
    ===================================================
  Adrian must:
    1. Open the invite URL.
    2. Confirm the email field is locked + pre-filled to defatty1@gesco.com.
    3. Confirm the role dropdown is locked + pre-filled to DefenseAttorney.
    4. Provide firmName "Stone Defense LLC", firstName "Gregory", lastName "Stone",
       password 1q2w3E*r, submit.
    5. Reply "verified defatty1@gesco.com" when complete.
Verify SQL (after WAIT lifts)
  SELECT u.NormalizedEmail, u.EmailConfirmed, inv.AcceptedAt
  FROM AbpUsers u
  LEFT JOIN AppInvitations inv ON inv.Email = u.Email
  WHERE u.NormalizedEmail = 'DEFATTY1@GESCO.COM'
Pass criteria:
  - AbpUsers row exists, EmailConfirmed = 1.
  - AppInvitations.AcceptedAt IS NOT NULL.
  - Role grant = DefenseAttorney.
On fail
  - 404 / 500 on invite URL -> raise new BUG; cite InvitationManager.AcceptAsync.
  - Email field editable (anti-spec) -> raise new BUG against
    AuthServer Register page's invite-overlay.
  - Token reuse succeeds twice -> raise new BUG; InvitationManager.AcceptAsync
    is not atomic.
```

```
HRD-P1.B.2  Invite-flow register: Claim Examiner (claimE1@gesco.com / Henry Caldwell)
Identical shape to P1.B.1, but:
  - inviteRole = "ClaimExaminer"
  - firstName  = "Henry"   lastName = "Caldwell"
  - No firmName field (CE is not an attorney role)
WAIT FOR ADRIAN: invite claimE1@gesco.com
Persist:
  - state.users.claimE1 with registrationFlow="invite", role="ClaimExaminer"
```

Note: [[OBS-21]] currently flags a "claimE1 verification not recorded"
finding; re-run this scenario to confirm or close.

#### Phase 1.C - Verification edge cases (idempotency + tampered token)

These apply to any of the 4 verified users from 1.A + 1.B. The agent picks
one (preferably patient1 since it goes through the standard SPA register
form, which has the broadest surface).

```
HRD-P1.C.1  Idempotent re-click of same verify URL
Steps
  1. Open state.verifications[patient1].url in a new tab.
Expected
  - 302 -> /Account/Login?flash=email-verified (same result; idempotent)
  - AbpUsers.EmailConfirmed remains 1
Pass criteria: no error, no double-process, generic success flash.
On fail -> EmailConfirmationModel.OnGetAsync is missing the
           already-confirmed early-return; check the PageModel source.
```

```
HRD-P1.C.2  Tampered verify token
Steps
  1. Take state.verifications[patient1].url, mutate one character of the
     confirmationToken query value.
  2. Open the tampered URL.
Expected
  - 302 -> /Account/Login?flash=verification-invalid
  - AbpUsers.EmailConfirmed for the test user remains 1 (already confirmed)
Pass criteria: generic flash; no token-error detail leaked.
On fail -> the PageModel may be exposing the IdentityResult.Errors
           verbatim; check OnGetAsync's failure branch.
```

### Phase 2 - Logins (covered inline, no explicit scenarios)

Logged in as each role at the start of the phase that role drives.
No standalone login scenarios -- a successful login is implicit in
every booking / approval / scope scenario. Auth failure modes are
explicitly covered in Phase 9.

### Phase 3 - Bookings (5 scenarios, all four party-emails populated)

Adrian's directive: each booking MUST have all four emails (Patient, AA,
DA, CE) filled. Test breadth (every booker role) over depth (lots of
same-role bookings).

| ID | Booker | Patient | AA filled | DA filled | CE+Ins filled | Type | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| HRD-P3.1 | state.users.patient1 | self (Daniel Harper) | YES (appatty1) | YES (defatty1 + synthetic) | YES (claimE1 + synthetic Ins) | AME | Patient books for self |
| HRD-P3.2 | state.users.appatty1 | new "Jane Doe (run-prefix)" | YES (self, locked) | YES (defatty1) | YES (claimE1) | QME | AA books for new patient |
| HRD-P3.3 | state.users.defatty1 | new "John Smith (run-prefix)" | YES (appatty1) | YES (self, locked) | YES (claimE1) | Panel QME | DA books for new patient |
| HRD-P3.4 | state.users.claimE1 | new "Mary Brown (run-prefix)" | YES (appatty1) | YES (defatty1) | YES (self, locked) | Deposition | CE books for new patient |
| HRD-P3.5 | state.users.clistaff1 | existing patient1 (Daniel) | YES (appatty1) | YES (defatty1) | YES (claimE1) | Record Review | Internal staff deep-link to /appointments/add |

Per [[OBS-19]], internal staff booking via the click-nav "+ New Appointment
Request" opens an admin CRUD modal that does NOT exercise the booking
pipeline. HRD-P3.5 deep-links to `/appointments/add` directly so the same
external form is exercised. Document any UI gap encountered.

**Per-scenario template (use for every row above):**

```
HRD-P3.<n>
Inputs (synthetic; from Part 5 dictionary + state.*)
  - appointmentTypeId = <resolved by GET /api/app/appointment-types
                       filtered by Name match for the row's Type>
  - locationId        = <first active location from state>
  - doctorAvailabilityId = <pick the next available slot of the matching
                            type from state.slots[<type>], confirmed via
                            GET /api/app/doctor-availabilities filtered
                            by type + location + status=Available>
  - patient            = <from row; if "new", invent
                         "<first> <last> (<run-prefix>)" with
                         dob 1985-06-15, phone 5550001<n>11>
  - applicantAttorney  = state.users.appatty1 (email, full name, firmName)
  - defenseAttorney    = state.users.defatty1 (email, full name, firmName)
  - claimExaminer      = state.users.claimE1 (email, full name)
  - insuranceCompany   = synthetic Insurance block from Part 5 with
                         suffix "<n>"
  - 1 injury, cumulative = No, body parts = "Lower back"
Steps
  1. Log in as <booker>.
  2. Navigate to /appointments/add.
  3. Set type, location, date (the resolved slot's date), time.
  4. Fill patient section (if new patient).
  5. Fill employer section (required).
  6. Toggle AA section on -> fill with state.users.appatty1.
  7. Toggle DA section on -> fill with state.users.defatty1.
  8. Click "Add" on Claim Information section -> modal opens.
  9. In modal: cumulative=No, date of injury, claim number, WCAB office,
     body parts. Toggle Insurance on + fill. Toggle CE on + fill with
     state.users.claimE1.
 10. Click Add -> modal closes, injury row visible in form.
 11. Click "Book an appointment" -> redirect to /.
Expected
  - HTTP 200 POST /api/app/appointments
  - Response body contains requestConfirmationNumber A?????
  - DB row in AppAppointments with the same number
  - ApplicantAttorneyEmail / DefenseAttorneyEmail / ClaimExaminerEmail
    columns populated to the four party emails from state.users
Persist
  - state.appointments["p3.<n>-<role>"] = {
      confirmationNumber: <from response>,
      id: <from response>,
      slotDate: <resolved>,
      approvedAt: null,
      rejectedAt: null
    }
Verify SQL
  SELECT RequestConfirmationNumber, PatientId, CreatorId,
         ApplicantAttorneyEmail, DefenseAttorneyEmail, ClaimExaminerEmail
  FROM AppAppointments
  WHERE Id = <state.appointments.p3.<n>.id>
Pass criteria: row exists; all four email columns non-null;
               three party emails match state.users.*.email.
On fail -> raise new BUG; common candidates [[BUG-022]] horizon math,
[[BUG-007]] CD bug (fixed; reopen if reproduces), [[BUG-009]] lead-time.
```

### Phase 5 - Approvals (5 scenarios)

Preserved structure; appointment IDs come from state.

| ID | Reviewer | Target | Action | ResponsibleUser | Expected status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| HRD-P5.1 | state.users.clistaff1 | state.appointments.p3.1 | Approve | self | 2 (Approved) | basic approve |
| HRD-P5.2 | state.users.clistaff1 | state.appointments.p3.2 | Approve | state.users.stafsuper1 | 2 | assign different responsible |
| HRD-P5.3 | state.users.clistaff1 | state.appointments.p3.3 | Reject | n/a | 3 (Rejected) | reason="Invalid claim number" (5+ chars per BUG-024) |
| HRD-P5.4 | state.users.stafsuper1 | state.appointments.p3.4 | Approve | state.users.clistaff1 | 2 | supervisor approves |
| HRD-P5.5 | state.users.stafsuper1 | state.appointments.p3.5 | Approve | self | 2 | supervisor approves internal-staff-booked |

Persist `state.appointments.<key>.approvedAt` or `.rejectedAt` after each.

Verify SQL after Phase 5:
```sql
SELECT RequestConfirmationNumber, AppointmentStatus, RejectionNotes
FROM AppAppointments
WHERE Id IN (<all 5 ids from state.appointments>)
ORDER BY RequestConfirmationNumber
```

### Phase 6 - Packet generation + recipient routing + partial-failure isolation

Adrian's directive: he reviews packet CONTENT manually. The suite checks:
1. Each packet kind reaches the correct recipient.
2. If one packet kind fails, the other two still generate AND their
   emails still fire (this is a SUSPECTED CURRENT BUG; confirm).

For each Approve in Phase 5, expect 3 packet rows auto-created in
`AppAppointmentPackets`:
- Kind=1 (patient), Status=2 (Completed)
- Kind=2 (doctor), Status=2
- Kind=3 (attorneyclaimexaminer), Status=2

```
HRD-P6.1  Recipient routing per packet kind
For every approved appointment (state.appointments.* where approvedAt != null):
  Step 1. Pull AppAppointmentPackets rows for the appointment.
  Step 2. For Kind=1 (patient): verify SMTP outbox / HangFire.Job
          contains an email send addressed to the patient's email
          (state.appointments.<key>.patientEmail captured during booking).
  Step 3. For Kind=2 (doctor): verify NO email send fires for this kind
          (doctor packet is storage-only per design). Just verify the row
          exists with Status=2.
  Step 4. For Kind=3 (attorneyclaimexaminer): verify SMTP outbox /
          HangFire.Job contains email sends addressed to ALL of
          state.users.appatty1.email, state.users.defatty1.email, AND
          state.users.claimE1.email (the three party emails set on the
          appointment).
Verify SQL
  SELECT a.RequestConfirmationNumber, p.Kind, p.Status, p.BlobName
  FROM AppAppointments a
  LEFT JOIN AppAppointmentPackets p ON p.AppointmentId = a.Id
  WHERE a.Id IN (<approved ids>)
  ORDER BY a.RequestConfirmationNumber, p.Kind;

  -- Plus Hangfire enqueue inspection:
  SELECT TOP 50 InvocationData
  FROM HangFire.Job
  WHERE InvocationData LIKE '%SendAppointmentEmailJob%'
  ORDER BY Id DESC;
Pass criteria
  - 4 approved appointments x 3 kinds = 12 packet rows, all Status=2.
  - Patient packet emails: exactly 1 per approved appointment, addressed
    to the patient.
  - AttyCE packet emails: exactly 3 per approved appointment, one each to
    AA, DA, CE.
  - Doctor packets: present in DB, no email.
On fail (Status=4 or missing rows): file new BUG; check Gotenberg logs.
On fail (wrong recipient): HIGH severity (HIPAA / leakage); file new BUG.
```

```
HRD-P6.2  Partial-failure isolation (SUSPECTED CURRENT BUG - confirm)
Per episodic memory 2026-05-15, a duplicate-key on Kind=3 packet has
blocked Kind=3 email dispatch in the past while Kind=1 and Kind=2 packets
succeeded. The approval-confirmation email DID fire. The user-facing
question is: when a packet kind fails to GENERATE, do the other two kinds
still email their recipients?

Steps
  1. Pick one approved appointment from state.appointments. Ideally one
     where Kind=3 is applicable (PQME / AME).
  2. Force a Kind=3 failure. Options (pick the least invasive that the
     codebase supports):
     a. Re-trigger GenerateAppointmentPacketJob for the appointment with
        a Kind=3 row already present (duplicate-key on the composite
        unique index TenantId+AppointmentId+Kind).
     b. Insert a deliberately corrupted token into the injury data that
        the docx-renderer rejects (e.g., an Insurance Address field with
        more than 8000 chars).
     c. Stop Gotenberg mid-job (docker compose stop gotenberg, then
        regenerate the packet, then docker compose start gotenberg).
        Reversible; use this if a + b are not feasible.
  3. Observe Hangfire job outcomes and AppAppointmentPackets rows.
Expected (if the system honors isolation correctly)
  - Kind=1 (Patient) packet: Status=2, email fires.
  - Kind=2 (Doctor) packet: Status=2.
  - Kind=3 (AttyCE) packet: Status=4 (Failed), NO email fires for Kind=3.
  - No exception bubbles up that prevents Kind=1 + Kind=2 email dispatch.
Observed (per 2026-05-15 memory; confirm in this run)
  - Kind=3 failure may currently abort the per-appointment notification
    loop, blocking Kind=1 email even though its packet generated.
On outcome
  - Matches expected -> isolation works; no bug; close the suspicion.
  - Matches observed -> file new BUG citing 2026-05-15 episodic
    observation as prior art; recommend per-kind try/catch + per-kind
    notification fan-out.
```

### Phase 7 - Document uploads (manual)

Driver limitation [[OBS-20]]: Playwright DataTransfer file injection
does NOT trigger the Angular upload pipeline. Either:
- Run this phase by hand in a real browser, OR
- Skip and mark as "manually verified once" (acceptable if a Patient
  upload was confirmed in an earlier session).

| ID | Uploader | Target | File | Expected |
| --- | --- | --- | --- | --- |
| HRD-P7.1 | state.users.patient1 | state.appointments.p3.1 (approved) | small PNG | row in AppAppointmentDocuments, IsAdHoc=1 |
| HRD-P7.2 | state.users.appatty1 | state.appointments.p3.2 (approved) | PDF | row, IsAdHoc=1 |
| HRD-P7.3 | state.users.clistaff1 | state.appointments.p3.1 | PDF | row, IsAdHoc=0 (internal) |
| HRD-P7.4 | state.users.patient1 | anonymous /upload route via emailed verification code | small PNG | row, IsAdHoc=1, VerificationCode populated |

### Phase 8 - Scope visibility (5 scenarios)

Each scenario: log in as the role, hit `GET /api/app/appointments?MaxResultCount=100`,
count rows + verify membership.

| ID | Role | User (state.users.*) | Expected count | Inclusion rule |
| --- | --- | --- | --- | --- |
| HRD-P8.1 | Patient | patient1 | own only | `PatientEmail = me OR PatientId.IdentityUserId = me` |
| HRD-P8.2 | AA | appatty1 | AA matches | `ApplicantAttorneyEmail = me` |
| HRD-P8.3 | DA | defatty1 | DA matches | `DefenseAttorneyEmail = me` |
| HRD-P8.4 | CE | claimE1 | CE matches | `ClaimExaminerEmail = me OR injury.ClaimExaminerEmail = me` |
| HRD-P8.5 | Clinic Staff | clistaff1 | all in tenant | unrestricted within tenant |

Each scenario must independently verify by cross-checking against
`AppAppointments` columns directly:

```sql
-- Example for AA (HRD-P8.2):
SELECT RequestConfirmationNumber
FROM AppAppointments
WHERE ApplicantAttorneyEmail = '<state.users.appatty1.email>'
ORDER BY RequestConfirmationNumber
```

Compare the SQL result with the API result; they must match exactly.

### Phase 9 - Authentication probes

Adrian's directive: cover lockout, password reset, refresh-token rotation,
concurrent sessions. No MFA today. Multi-session is intentional (locked
2026-05-01) -- the concurrent-session probe asserts the policy, not a
single-session limit.

```
HRD-P9.1  Lockout after N failed logins
Inputs
  - target user: state.users.patient1
  - N = ABP's configured MaxFailedAccessAttempts (resolve from
        AbpUser settings / IdentityOptions; default is 5)
Steps
  1. POST N login attempts against the AuthServer login form with the
     correct username but a wrong password each time.
  2. After the Nth attempt, attempt a login with the CORRECT password.
Expected
  - Attempts 1..N return generic "invalid credentials" (no leak of which
    of email/password is wrong).
  - Attempt N+1 (correct password) is rejected because the account is
    locked. Response is HTTP 401 or 423; flash banner is generic
    (e.g., "Your account is temporarily locked").
  - AbpUsers.AccessFailedCount = N (or >= N, depending on framework).
  - AbpUsers.LockoutEnd > current UTC.
Verify SQL
  SELECT UserName, AccessFailedCount, LockoutEnd
  FROM AbpUsers
  WHERE UserName = '<state.users.patient1.email>'
Anti-checks
  - The response body MUST NOT echo whether the user exists (test
    against a non-existent email; response timing + body should be
    indistinguishable).
On fail
  - Account not locked after N attempts -> raise new BUG; IdentityOptions
    misconfigured or MaxFailedAccessAttempts effectively infinite.
  - User-existence leak via timing -> raise HIGH-severity BUG.
```

```
HRD-P9.2  Password reset round-trip + edge cases
Inputs
  - target user: state.users.patient1 (state.users.patient1.password
    will change; update state after success)

P9.2.a  Happy-path round-trip
Steps
  1. POST /api/public/external-account/forgot-password { email: patient1@... }
  2. Capture the reset URL (Hangfire / SMTP).
  3. WAIT FOR ADRIAN: reset patient1@gesco.com  (Adrian opens link, sets
     new password "2W3e4R*t", confirms).
  4. Attempt login with the new password.
Expected
  - Login succeeds. Old password no longer works (verify with an extra
    login attempt using the old credential).
Persist
  - state.users.patient1.password = "2W3e4R*t"

P9.2.b  Idempotent re-click of consumed reset URL
Steps
  1. Open the SAME reset URL from P9.2.a a second time.
Expected
  - 302 -> /Account/Login?flash=reset-invalid-or-used (or equivalent
    generic flash). EmailConfirmed and password unchanged.

P9.2.c  Tampered reset token
Steps
  1. Mutate one character of the reset URL's token.
  2. Open the URL.
Expected
  - 302 -> /Account/Login?flash=reset-invalid (generic). No leak of token
    error detail.

P9.2.d  Expired reset token
Steps
  1. Issue a fresh reset URL via POST /forgot-password.
  2. Wait until past the configured token TTL (default 1 day; for testing,
    use SQL UPDATE on AbpUserTokens to backdate, OR document as "skipped:
    needs TTL override"). If skipped, file an OBS that the test needs a
    TTL knob.
Expected
  - 302 -> /Account/Login?flash=reset-expired.

P9.2.e  Anti-enumeration
Steps
  1. POST /forgot-password { email: <non-existent>@gesco.com }
Expected
  - Same HTTP status + same response body shape as for an existing
    email. Response timing within 100ms of the existing-email response.
Anti-checks
  - Response MUST NOT echo whether the user exists.
  - Hangfire MUST NOT enqueue a SendAppointmentEmailJob for the
    non-existent email.

P9.2.f  Throttle (5/hour per email)
Steps
  1. POST /forgot-password 6 times within 5 minutes for the same email.
Expected
  - Calls 1..5 return 200 (or whatever the happy-path status is).
  - Call 6 returns 429 with generic body.
On fail -> rate limiter regressed; cite PR #197 family of fixes.
```

```
HRD-P9.3  Refresh-token rotation
Steps
  1. Log in as state.users.patient1 via OpenIddict password grant:
       POST /connect/token grant_type=password
         { username, password, client_id, scope }
     Capture refresh_token_A.
  2. POST /connect/token grant_type=refresh_token { refresh_token: A }
     Capture refresh_token_B.
  3. POST /connect/token grant_type=refresh_token { refresh_token: A }
     (REUSE the OLD token).
  4. POST /connect/token grant_type=refresh_token { refresh_token: B }
     (use the NEW token).
Expected
  - Step 2 returns 200 with refresh_token_B != refresh_token_A
    (rotation actually happened).
  - Step 3 returns 400 with invalid_grant (old token revoked on rotation).
  - Step 4 returns 200 (new token still valid; sliding refresh works).
Anti-checks
  - Step 3's response body MUST be generic (no leak of which token was
    invalid; just invalid_grant).
On fail
  - refresh_token_B == refresh_token_A -> rotation is NOT happening;
    OpenIddict configured with reuse_refresh_tokens. Raise HIGH BUG
    (refresh-token theft replayable).
  - Step 3 returns 200 -> old token NOT revoked on rotation. Same severity.
```

```
HRD-P9.4  Concurrent sessions (multi-session intentional)
Steps
  1. Log in as state.users.patient1 in Playwright session A.
  2. Log in as state.users.patient1 in Playwright session B (separate
     browser context).
  3. From both sessions, hit a protected endpoint (e.g.,
     GET /api/app/appointments). Both should return 200.
  4. Log out from session A.
  5. From session B, hit the protected endpoint again.
Expected
  - Steps 1-3: both sessions return 200.
  - Step 5: session B still returns 200. The intentional multi-session
    policy (per 2026-05-01 decision) is preserved.
Anti-checks
  - If session B fails after session A logout, the system has
    accidentally regressed to single-session enforcement. File a new
    OBS (not a BUG -- it's a policy regression, behavior may be desired
    by future security review).
```

### Phase 10 - Email template language review (rubric)

Adrian's directive: review every notification template for human-like,
non-redundant, logical language. Output a structured table so reviews
are comparable across runs.

The 5-item rubric (each = pass | fail per template):

1. **Subject line discipline**: subject <= 60 chars, no template-code
   leakage (no raw "Apprvd", "Stackholder", "Join", "Apporved"), no
   unresolved token like `{{patient_name}}`.
2. **Single primary CTA**: exactly one prominent call-to-action in the
   body (count `<a>` tags with button-class styling OR phrases like
   "Click here", "Open", "Login", "Verify").
3. **Token substitution resolved**: render the template with a sample
   token dictionary and grep the output for `{{` / `}}` / `[[` / `]]`.
   Zero hits = pass.
4. **Plain-language pass**: body does NOT contain jargon from the
   banned list: `AppService`, `DTO`, `BookingPolicyValidator`,
   `IdentityUser`, `BusinessException`, `Hangfire`, `Mapperly`,
   `OpenIddict`, `IRepository`.
5. **Non-redundancy**: subject and first body paragraph do not duplicate
   more than 70% of tokens (case-insensitive, ignore stopwords).

```
HRD-P10.1  Rubric review per template
Inputs
  - Set: all 59 codes in NotificationTemplateConsts (16 DB-managed +
    43 on-disk HTML, plus the 2 recent additions InviteExternalUser
    and InternalUserCreated, plus the 3 Phase 2.A codes
    AppointmentRequestedOffice / Registered / Unregistered).
  - Sample token dictionary: synthetic values for every documented
    token (patient_name = "Daniel Harper (hrd-MMDD)",
    appointment_date = state.slots.ame.earliestDate,
    request_confirmation_number = "A99999", etc.).
Steps
  1. For DB-managed templates: SELECT Code, Subject, Body
     FROM AppNotificationTemplates WHERE TenantId = <Falkinstein>.
  2. For on-disk HTML templates: read each file under
     src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/.
  3. For each template, evaluate the 5 rubric items.
  4. Render the template with the sample token dictionary (use the
     same Scriban/Razor renderer the runtime uses if reachable from a
     test harness; otherwise compute substitution manually).
  5. Append a row to docs/runbooks/findings/template-review-<run-date>.md:
       | Code | Subject<=60 | One CTA | Tokens resolved | No jargon | Non-redundant | Notes |
       | ---  | --- | --- | --- | --- | --- | --- |
       | UserRegistered | PASS | PASS | PASS | PASS | FAIL | "Subject + first line both contain 'Patient Portal'" |
Pass criteria
  - 0 templates fail >= 2 rubric items.
On fail
  - >= 2 rubric items fail for a single template -> file BUG-NNN
    targeting that template's content. Severity: medium.
  - Plain-text fallback observed (no HTML body) -> file OBS-NNN noting
    the 40-template plain-text backlog from prior research.
```

---

## Part 4: Round 2 - Failure modes

15 major probes, mostly API-direct. Run after Round 1 so the appointment
corpus exists. All probes reference `state.*` for IDs; no hard-coded
confirmation numbers.

### Future work sidebar

These probes are deterministic and API-direct; they are good candidates
for migration to xUnit integration tests under
`test/HealthcareSupport.CaseEvaluation.Application.Tests/` so CI runs them
on every PR. **Not done this session** per Adrian's direction (2026-05-19);
the runbook is the canonical artifact for now. Future plan: extract each
HRD-R2.* probe into a fact in `AppointmentsAppServiceTests` /
`ExternalSignupAppServiceTests` and delete the corresponding runbook
section once parity is achieved.

### Registration validation (R2.1 - R2.3)

```
HRD-R2.1  Duplicate email
Endpoint  POST /api/public/external-signup/register
Body      { email: state.users.patient1.email, password, confirmPassword,
            firstName, lastName, userType: 1 }
Expected status: 400 (per PR #197)
Expected code:   CaseEvaluation:Registration.DuplicateEmail
Expected data:   generic message; MUST NOT echo the email
Anti-checks      response body does not contain the email string;
                 no row added to AbpUsers
On fail -> [[BUG-001]] resurrected (it's currently `status: fixed`)
```

```
HRD-R2.2  ConfirmPassword mismatch
Endpoint  POST /api/public/external-signup/register
Body      { email: new, password: X, confirmPassword: Y (X != Y), ... }
Expected status: 400
Expected code:   CaseEvaluation:Registration.ConfirmPasswordMismatch
On fail (status 403) -> [[BUG-023]] still open as expected
```

```
HRD-R2.3  Attorney without FirmName
Body      userType: 3 (Applicant Attorney), firmName omitted
Expected status: 400
Expected code:   CaseEvaluation:Registration.FirmNameRequiredForAttorney
On fail (status 403) -> [[BUG-023]] still open
```

### Booking validation (R2.4 - R2.5)

```
HRD-R2.4  Empty body
Endpoint  POST /api/app/appointments  (auth: any logged-in user)
Body      {}
Expected status: 400 with validationErrors array
```

```
HRD-R2.5  Date past max horizon
Body      doctorAvailabilityId pointing at a slot > today + 60d (non-AME)
          or > today + 90d (AME)
Expected status: 400 or 403 (per [[BUG-022]] - the rule is intentional;
          status code may be wrong; the math may also be wrong)
Expected code:   CaseEvaluation:Appointment.BookingDatePastMaxHorizon
Expected data:   { maxTimeDays: N }
Anti-checks      response data.maxTimeDays equals the seeded
                 SystemParameter value for the chosen type
```

### Permission + scope rejection (R2.6 - R2.9)

```
HRD-R2.6  Patient deep-links non-owned appointment
Auth      state.users.patient1
Endpoint  GET /api/app/appointments/<id from state.appointments.p3.3>
          (DA-booked; not in patient's scope)
Expected status: 403
Expected code:   CaseEvaluation:Appointment.AccessDenied
Anti-checks      response body does not contain another patient's name
```

```
HRD-R2.7  Patient privilege escalation (approve own)
Auth      state.users.patient1
Endpoint  POST /api/app/appointments/<state.appointments.p3.1.id>/approve
Body      { responsibleUserId: <patient1.id>, notes: "x" }
Expected status: 403
```

```
HRD-R2.8  DA accesses non-scope
Auth      state.users.defatty1
Endpoint  GET /api/app/appointments/<state.appointments.p3.4.id>
          (CE-only scope; DA not on this appointment)
Expected status: 403 AccessDenied
```

```
HRD-R2.9  Anonymous booking attempt
Auth      no Authorization header
Endpoint  POST /api/app/appointments
Body      <minimal valid body>
Expected status: 401
```

### State-machine rejection (R2.10 - R2.11)

```
HRD-R2.10 Re-approve already-Approved appointment
Auth      state.users.clistaff1
Endpoint  POST /api/app/appointments/<state.appointments.p3.1.id>/approve
          (already approved in P5.1)
Expected code: CaseEvaluation:AppointmentInvalidTransition
Expected data: { from: 2, trigger: 1 }
Status:   400 expected; actual 403 (same [[BUG-023]] pattern)
```

```
HRD-R2.11 Approve a Rejected appointment
Auth      state.users.clistaff1
Endpoint  POST /api/app/appointments/<state.appointments.p3.3.id>/approve
          (rejected in P5.3)
Expected code: AppointmentInvalidTransition with from: 3
```

### Rejection + upload validation (R2.12)

```
HRD-R2.12 Reject without reason
Auth      state.users.clistaff1
Endpoint  POST /api/app/appointments/<a pending id>/reject
Body      { rejectionNotes: "" }
Expected status: 400 with "Reason for rejection is required"
Observed (prior run): 200, appointment transitions to Rejected with
                      empty notes
On fail (status 200) -> [[BUG-024]] still open
```

### Email confirmation endpoint (R2.13 - R2.15)

```
HRD-R2.13 Empty params on /Account/EmailConfirmation
Endpoint  GET http://falkinstein.localhost:44368/Account/EmailConfirmation
Auth      anonymous
Expected status: 302
Expected Location: /Account/Login?flash=verification-invalid
Anti-checks      response body has no stack trace, no class names,
                 no userId echo
On fail (status 500) -> [[BUG-014]] / B-2 regressed; full ABP dev page
                        is leaking
```

```
HRD-R2.14 Fake userId + token (anti-enumeration)
Endpoint  GET /Account/EmailConfirmation?userId=11111111-1111-1111-1111-111111111111
          &confirmationToken=abc
Auth      anonymous
Expected status: 302
Expected Location: /Account/Login?flash=verification-invalid
Anti-checks      same redirect target as R2.13 (no leak of whether the
                 userId existed); response timing should be similar
```

```
HRD-R2.15 HEAD request on /Account/EmailConfirmation
Endpoint  HEAD /Account/EmailConfirmation?userId=<real-unverified>
          &confirmationToken=<real>
Auth      anonymous
Expected status: 405 Method Not Allowed (or 200 with empty body)
Expected behavior: EmailConfirmed stays 0 (HEAD does NOT process token)
Anti-checks      AbpUsers.EmailConfirmed for the test user MUST remain 0
                 after the HEAD; the framework v10.4 HEAD-pre-fire bug
                 MUST NOT regress
```

### Upload limits (code review only)

The upload size limit is a code-review check, not a runtime test
(see [[OBS-20]] for the runtime driver limit + [[BUG-025]] for the
finding).

Verify:
```
Grep src/.../AppointmentDocumentsAppService.cs for "MaxFileSize" or
"fileSize >" -> there should be an upper-bound check.
```

If missing: file BUG-025-style finding (currently still open).

---

## Part 4.5: Round 3 - Replay sweep of open findings

Run after Round 1 + Round 2. For every finding under
`docs/runbooks/findings/bugs/` with frontmatter `status: open` or
`open-low`, attempt to reproduce. Skip `resolved-not-a-bug`,
`fixed`, `superseded`, `driver-limitation`, and `stub` entries -- those
are not candidates for replay.

### Round 3 enumeration

The agent MUST regenerate this list at run-time by globbing the bugs
directory:
```
Glob: docs/runbooks/findings/bugs/*.md
For each file: read frontmatter, keep iff status in {open, open-low}.
```

As of 2026-05-19, the open list is:

| ID | One-liner | Severity | Phase to cross-reference |
| --- | --- | --- | --- |
| BUG-012 | firmname-required | medium | Phase 1.A + R2.3 |
| BUG-013 | cors-confirmuser | medium | Phase 1 verify-email |
| BUG-014 | hardcoded-email-urls | medium | Phase 1 verify-email + R2.13 |
| BUG-015 | dynamic-env-unused | low | (config; verify build env honors override) |
| BUG-016 | openiddict-subdomain | medium | Phase 9 (login + refresh) |
| BUG-018 | smtp-misleading-error | medium | Phase 1 + Phase 9.2 forgot-password |
| BUG-020 | smtp-password-decrypt-noise | low | startup logs (Prereq) |
| BUG-021 | ce-cannot-book | open-low (UX) | Phase 3.4 (CE booker) |
| BUG-021 | login-tempdata-success-banner | medium | Phase 2 login (DUPLICATE ID; rename one) |
| BUG-022 | booking-horizon-rejects-within-range | medium | R2.5 |
| BUG-023 | registration-validation-returns-403 | medium | R2.2 + R2.3 + R2.10 + R2.11 |
| BUG-024 | reject-accepts-empty-reason | medium | R2.12 |
| BUG-025 | no-document-upload-size-limit | medium | Phase 7 + code review |
| BUG-026 | duplicate-email-placeholder | medium | (cross-reference R2.1) |
| OBS-22 | docker-watch-misses-bind-mount-edits | observation | (dev workflow; spot check) |
| SEED-3 | non-ame-slots-missing | seed-gap | Phase 0 (should be resolved by P0.1) |

**Known duplicate ID:** two files share `BUG-021` (ce-cannot-book +
login-tempdata-success-banner). Round 3 must flag this and recommend
renaming one (e.g., the login banner one to BUG-027) -- file a tiny meta
finding the first time it's encountered. Do not auto-rename.

**Needs-rehydration findings** (BUG-008, BUG-009, BUG-010, BUG-011,
SEED-2) are not in the active replay list; treat them as
"investigate-when-encountered". If a Phase 1-10 scenario incidentally
reproduces one, append a `last-replayed:` line to its finding and flip
status to `open`.

**Needs-repro finding** (OBS-21 claime1-verification-not-recorded) IS in
scope -- Phase 1.B.2 implicitly retests it. If verification persists,
flip its status to `fixed`.

### Round 3 scenario template

```
HRD-R3.<finding-id>  <Title from finding>
Source            docs/runbooks/findings/bugs/<finding-id>.md
Repro
  <copied verbatim from the finding's Symptom or Repro section>
Expected if fixed
  <what a passing run looks like -- derived from the finding's
   Recommended fix or Expected outcome>
Action on outcome
  - confirmed-open  -> append `last-replayed: <run-date>` to finding
                       frontmatter
  - now-passing     -> append "Resolved <run-date> via hardening run"
                       to the finding body + set `status: fixed`
  - inconclusive    -> note in run summary; do NOT modify the finding
  - duplicate-id    -> file a meta OBS recommending a rename
On fail
  Round 3 itself does not produce new BUGs unless replay surfaces a
  brand-new behavior not described in the existing finding.
```

---

## Part 5: Test data dictionary

All test data MUST be synthetic. Never invent real-looking SSN /
MRN / DOB. The values below are documented and reusable across runs.

### Users (12-user roster; only 2 pre-seeded)

| Role | UserName | Real name | Password | Seeded? | Notes |
| --- | --- | --- | --- | --- | --- |
| Patient | patient1@gesco.com | Daniel Harper | `1q2w3E*r` | NO | Phase 1.A.1 (manual self-register) |
| Patient | patient2@gesco.com | Olivia Turner | `1q2w3E*r` | NO | Spare; not registered this run |
| Applicant Attorney | appatty1@gesco.com | Marcus Bennett | `1q2w3E*r` | NO | Phase 1.A.2 (manual self-register), firmName "Bennett & Associates" |
| Applicant Attorney | appatty2@gesco.com | Tiffany Lawson | `1q2w3E*r` | NO | Spare |
| Defense Attorney | defatty1@gesco.com | Gregory Stone | `1q2w3E*r` | NO | Phase 1.B.1 (invite flow), firmName "Stone Defense LLC" |
| Defense Attorney | defatty2@gesco.com | Alicia Perez | `1q2w3E*r` | NO | Spare |
| Claim Examiner | claimE1@gesco.com | Henry Caldwell | `1q2w3E*r` | NO | Phase 1.B.2 (invite flow) |
| Claim Examiner | claimE2@gesco.com | Jasmine Reid | `1q2w3E*r` | NO | Spare |
| Staff Supervisor | stafsuper1@gesco.com | Patrick O'Neal | `1q2w3E*r` | YES | Phase 0 driver; issues invites in Phase 1.B |
| Staff Supervisor | stafsuper2@gesco.com | Denise Fowler | `1q2w3E*r` | NO | Spare |
| Clinic Staff | clistaff1@gesco.com | Rachel Kim | `1q2w3E*r` | YES | Phase 5 approver |
| Clinic Staff | clistaff2@gesco.com | Luis Mendoza | `1q2w3E*r` | NO | Spare |

After Phase 1 completes, state.users.* is populated for the 4 newly-
registered users; the 6 "Spare" rows remain unused unless a future
expansion of the suite calls them in.

### Patient demographic templates (new-patient bookings in Phase 3)

| Scenario | First | Last | DOB | Synthetic email | Phone |
| --- | --- | --- | --- | --- | --- |
| P3.2 | Jane | Doe (`<run-prefix>`) | 1985-06-15 | jane.doe.`<run-prefix>`@example.test | 5550001011 |
| P3.3 | John | Smith (`<run-prefix>`) | 1980-03-22 | john.smith.`<run-prefix>`@example.test | 5550001013 |
| P3.4 | Mary | Brown (`<run-prefix>`) | 1990-03-10 | mary.brown.`<run-prefix>`@example.test | 5550001014 |

`<run-prefix>` is `hrd-MMDD` from `state.runPrefix`. So multiple runs
do not collide.

### Synthetic AA / DA / CE / Insurance for fill

Pre-defined synthetic block (use verbatim; vary only the scenario
suffix in the FirmName):

```
AA  uses state.users.appatty1 (email, full name, firmName)
    fax: 5550001<n>12   phone: 5550001<n>11
    street: "100 AA St"        city: San Francisco   state: CA   zip: 94101
DA  uses state.users.defatty1 (email, full name, firmName)
    fax: 5550001<n>22   phone: 5550001<n>21
    street: "200 DA Ave"       city: San Diego       state: CA   zip: 92101
CE  uses state.users.claimE1 (email, full name)
    phone: 5550001<n>31        fax: 5550001<n>32
    street: "300 CE Way"       city: Los Angeles     state: CA   zip: 90001
Ins name: "Test Insurance Co <n>"  attn: "Test Adjuster" phone: 5550001<n>41
    street: "700 Ins Blvd"     city: Sacramento      state: CA   zip: 94203
```

### Lookup IDs (Falkinstein test tenant)

These are stable across stack rebuilds because they come from
explicit seeds:

| Entity | Id |
| --- | --- |
| Demo Clinic North | a0a00005-0000-4000-9000-000000000001 |
| AME (Agreed Medical Examination) | a0a00002-0000-4000-9000-000000000003 |
| California state | a0a00001-0000-4000-9000-00000000ca00 |

If you query `/api/app/appointments/location-lookup` and get
different IDs, the seed has drifted and Prereq #3 fails.

For AppointmentType IDs other than AME, the suite resolves at runtime via
`GET /api/app/appointment-types`; do not hard-code them.

---

## Part 6: Lessons learned (bake into every run)

These are insights from prior hardening runs. Treat them as
hard rules, not suggestions.

### Driver / automation

1. **Playwright DataTransfer file injection does NOT trigger Angular
   upload pipelines.** Use real file chooser (`browser_file_upload`
   when an MCP file-chooser dialog is active) or run uploads by hand.
   See [[OBS-20]].

2. **The ngb-datepicker becomes unreliable after multiple open /
   close cycles in one session.** Symptoms: setting month=6 shifts
   visible month to July; clicking a date does not propagate to the
   FormControl. Mitigation: `browser_navigate` to reload
   `/appointments/add` before each booking scenario, or use the
   ngb-datepicker input's typed-date path (`dpInput.value =
   "6/22/2026"` + dispatch input/change events).

3. **The datepicker shows ALL dates as `aria-disabled` while
   `availableDateKeys` is empty during the initial slot fetch.**
   This is not a permission block. Wait 2-3 seconds after selecting
   type + location before opening the picker. See [[BUG-021]]
   (ce-cannot-book -- downgraded to UX).

4. **Direct API testing is 5-10x faster than UI cycling for failure
   modes.** Prefer fetch with Bearer token (from
   `localStorage.access_token`) for Round 2 + Round 3 scenarios.

### Form / business-logic gotchas

5. **The DA section is auto-on and required when a DA logs in to
   book.** Same will be true if the framework grows to support
   AA-as-booker form auto-fill. Don't flag this as a parity question;
   it is intentional design.

6. **CE / Insurance is per-injury, NOT a top-level section on the
   booking form.** Look inside the Claim Information modal. See
   [[OBS-17]] (resolved-not-a-bug).

7. **The "+ New Appointment Request" button on `/appointments` opens
   an ADMIN CRUD modal for internal users, NOT the same form as
   `/appointments/add`.** External users do not see this button.
   Internal-staff booking is a separate workstream. Deep-link to
   `/appointments/add` for internal-staff hardening scenarios
   (HRD-P3.5). See [[OBS-19]].

8. **`/appointments/add` route has only `[authGuard]`, no permission
   gate.** Any authenticated user can deep-link. Server-side checks
   are authoritative. Don't conclude "X blocked from booking" from
   UI evidence alone -- verify with a direct API call. See [[OBS-18]].

9. **`AppDoctorAvailabilities` schema binds each slot to a single
   `AppointmentTypeId`.** Slots are per-type. Phase 0 must generate
   slots for every active type explicitly. See [[SEED-3]].

10. **The max-horizon rule (`BookingDatePastMaxHorizon`) is
    INTENTIONAL OLD parity, not a bug.** The arithmetic in
    `IsSlotWithinMaxTime` may still be wrong; see [[BUG-022]] for the
    open math question. The rule itself stays.

### Test-data hygiene

11. **Always use synthetic emails ending `@gesco.com`,
    `@example.test`, `@falkinstein.test`, or `@evaluators.com`.**
    Never `@gmail.com`, `@protonmail.com`, etc. The HIPAA rule
    (`.claude/rules/hipaa-data.md`) and PHI scanner enforce this.

12. **Never invent real-looking SSN, MRN, or DOB.** Use the
    documented dictionary in Part 5.

13. **The standard test password is `1q2w3E*r` for ALL test users.**
    Documented in this file and in
    `memory/project_2026-05-14-session-state.md`. Do not invent
    per-user passwords (Phase 9.2.a is the only exception -- it
    deliberately changes patient1's password to verify reset works,
    and persists the new value back into state).

### Database / state

14. **The Falkinstein tenant Id is documented in the session-state
    memory.** Confirm it matches the value in `AppSystemParameters`
    before running. If it drifts, the validator may load defaults
    instead of tenant-specific values, and tests will misfire.

15. **Approvals trigger 3 packet generations per appointment.** If
    you only see 1-2 packets, Gotenberg may have failed silently.
    Check `docker logs main-gotenberg-1`. Phase 6.2 specifically
    interrogates whether partial failure breaks the other kinds'
    emails.

16. **`down -v` wipes the test DB, including
    `AppDoctorAvailabilities`.** Re-run Phase 0 before re-running.
    The `DemoDoctorDataSeedContributor` is still missing per
    [[SEED-2]] (needs-rehydration); until it lands, Phase 0's
    staff-supervisor-driven generation is the canonical bootstrap.

### Auth URL routing convention (added 2026-05-18, B-1 fix)

17. **Email-link URLs (verify, reset, etc.) always target the AuthServer
    Razor pages, never the SPA.** Pre-2026-05-18, `BuildEmailConfirmationUrl`
    in `CaseEvaluationAccountEmailer` and `ExternalAccountAppService`
    pointed at the SPA route `/account/email-confirmation` which had
    been deleted, so clicks 404'd. After the B-1 fix, both builders use
    `{AuthServerBaseUrl}/Account/EmailConfirmation` (mirroring the
    existing `BuildResetUrl` pattern for password reset).

    Rule when adding new email flows: the URL the user clicks from an
    inbox MUST resolve to a server-side endpoint (Razor or controller)
    on the AuthServer; never to a SPA route. The SPA's `/account/*`
    routes were deleted 2026-05-15 and `app.config.ts:provideAccountPublicConfig()`
    is currently an orphan provider that does NOT restore them.

### Robustness + run-state (added 2026-05-19, this revision)

18. **No hard-coded `RequestConfirmationNumber` values; no `T+Nd`
    offsets in scenarios.** Capture every confirmation number from
    the POST response into `state.appointments[<scenario_id>]`.
    Resolve slot dates at runtime via
    `GET /api/app/doctor-availabilities`. Brittle inputs caused
    the original 14-scenario Phase 3 to cascade-fail when one slot
    was stolen mid-run; the new template is resilient to that.

19. **WAIT-marker pause/resume protocol.** When the suite needs
    Adrian to verify an email link or invite, print the URL inside
    a fenced block tagged `=== WAIT FOR ADRIAN: ... ===` and STOP.
    Persist progress in `state.verifications[]` so reruns resume
    cleanly without losing prior verifications.

20. **Multi-session is intentional, not a bug.** Phase 9.4 asserts
    the policy. If a future change introduces single-session
    enforcement (logout-anywhere kills all sessions), that is a
    POLICY regression to file as OBS -- not a security BUG, since
    multi-session is the documented design (locked 2026-05-01).

21. **Phase 0 must verify equal slot counts before Phase 3 starts.**
    If one appointment type has 0 slots in the window, Phase 3 will
    silently skip the corresponding booker scenario. Phase 0's verify
    SQL is a HARD GATE.

---

## Part 7: Findings cross-reference

When a scenario fails, file a finding using the structure below.
Cross-link with `[[BUG-NNN]]` syntax to existing tickets.

### Finding file naming

| Prefix | Use for |
| --- | --- |
| `BUG-NNN` | Real defect (functional / security / data integrity) |
| `OBS-N` | Observation worth tracking but not a defect (design question, behavior nuance, driver limit) |
| `SEED-N` | Test-data / seed gap |

Increment N from the highest existing in `docs/runbooks/findings/bugs/`.

### Per-scenario finding map

| Scenario | If it fails | Severity baseline | Pattern |
| --- | --- | --- | --- |
| HRD-P0.* | slot generation gap | HIGH (blocks all downstream) | Phase 0 / SEED-3 |
| HRD-P1.A.* | manual register | medium | Registration / [[BUG-014]] |
| HRD-P1.B.* | invite flow | medium | Invite / InvitationManager |
| HRD-P1.C.* | verify edge cases | medium | EmailConfirmationModel |
| HRD-P3.* | booking pipeline | blocker | Booking form / [[BUG-022]] |
| HRD-P5.* | approval state machine | medium | Approval / state machine |
| HRD-P6.1 | wrong recipient | HIGH (HIPAA) | Packet routing |
| HRD-P6.2 | packet failure cascade | medium-high | Packet partial-failure isolation |
| HRD-P7.* | upload pipeline | medium | Document upload / [[OBS-20]] |
| HRD-P8.* | scope leak | HIGH (HIPAA) | Authorization |
| HRD-P9.1 | lockout absent or leaky | HIGH | Auth |
| HRD-P9.2.* | password reset broken / leak | HIGH | Auth / [[BUG-011]] |
| HRD-P9.3 | refresh-token reuse | HIGH | Auth / OpenIddict |
| HRD-P9.4 | concurrent sessions broken | OBS (policy) | Auth |
| HRD-P10.* | template language fail | medium | Email templates |
| HRD-R2.1 | regression of PR #197 | HIGH | Registration |
| HRD-R2.2/2.3/2.10/2.11 | 403 vs 400 | medium | [[BUG-023]] |
| HRD-R2.5 | booking math | medium | [[BUG-022]] |
| HRD-R2.6/2.8 | scope guard failure | HIGH (HIPAA) | Authorization |
| HRD-R2.12 | empty rejection accepted | medium | [[BUG-024]] |
| HRD-R2.13-15 | email-conf endpoint leak | HIGH | [[BUG-014]] / B-2 |
| HRD-R3.* | replay of open finding | inherits the finding's severity | Regression |

### Finding file template

```markdown
---
id: BUG-NNN
title: <one-line>
severity: <blocker|high|medium|low|observation|seed-gap>
status: open
found: YYYY-MM-DD hardening HRD-<phase>.<n>
flow: <flow-slug>
component: <path/to/file.cs>
last-replayed: <date or omit>
---

# BUG-NNN - <title>

## Symptom
<exact reproduction with HTTP + body or DB row>

## Hypothesis
1. <theory> -- <how to test>
2. <competing theory>
3. <alternative>

## Recommended fix
<single concrete change or code block>

## Functional impact
<who is affected, how>

## Related
- [[BUG-XXX]] - <relation>
```

---

## Part 8: Reporting

After a complete run, post a summary message containing:

1. **Pass/Fail counts** per Phase and Round (e.g., "P0: 1/1, P1.A: 2/2,
   P1.B: 2/2, P1.C: 2/2, P3: 5/5, P5: 5/5, P6: 2/2 with P6.2 confirming
   partial-failure bug, ..., R1 total X/Y, R2: 12/15, R3: <n> replayed").
2. **New findings** filed this run: ID, severity, one-liner.
3. **Verifications** that passed: list scenarios that confirm prior PR
   fixes still hold (e.g., "PR #197 duplicate-email fix re-verified",
   "[[BUG-001]] still fixed").
4. **Round 3 outcomes**: per finding ID, one of `confirmed-open`,
   `now-passing`, `inconclusive`.
5. **Final DB state**: count of appointments by AppointmentStatus, count
   of packets by Kind+Status, count of slots by AppointmentType.
6. **Skipped scenarios**: which and why (e.g., "HRD-P9.2.d expired
   token: needs TTL knob; filed OBS-NN").
7. **Next-run blockers**: anything that requires a fix before re-running
   the same scenario.
8. **Template review summary**: how many of the 59 templates passed all
   5 rubric items; how many failed >= 2; link to
   `docs/runbooks/findings/template-review-<run-date>.md`.

Do not commit the test run; the run produces findings, not code.

---

## Part 9: Maintenance

This suite must stay current. When you change product behavior, update
this suite **in the same PR** to keep the test corpus aligned.

- Adding a new appointment type? Extend Phase 0's loop so slots are
  generated for it. Add a Phase 3 row + decide its max-horizon
  classification.
- Adding a new role? Add a Phase 8 scope scenario + extend the 12-user
  roster in Part 5.
- Changing an error code or HTTP status mapping? Update the relevant
  Round 2 scenario's Expected.
- Changing the test password or test user roster? Update Part 5.
- Adding a new finding type or fixing a finding? Update Round 3's
  enumeration table -- move resolved findings to a "historical" section
  below the active list and append `status: fixed` to the finding file.
- Adding a new email template? Add a row to the rubric review in
  Phase 10.
- Adding a new auth feature (MFA, SSO, etc.)? Add a Phase 9 scenario.

The findings index at `docs/runbooks/findings/2026-05-13-userflow-findings.md`
should be rotated quarterly (`YYYY-MM-DD-userflow-findings.md`).
