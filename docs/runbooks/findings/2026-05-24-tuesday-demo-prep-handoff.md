---
title: Tuesday demo prep -- Sunday night session hand-off
date: 2026-05-24 (Sun PM)
session: BUG-036 fix + Stage 1-3 merges + post-merge smoke pass
status: paused-for-the-night
---

# Tuesday demo prep -- session hand-off (2026-05-24, ~11:15 PM Pacific)

## What landed on main tonight (4 merges + 1 PR open)

| PR | SHA | Subject |
|---|---|---|
| #247 | `1e3fc50` | fix(packets): close BUG-036 packet-gen soft-delete race (3-layer fix) |
| #248 | `a35e404` | docs(findings): hardening R3 sweep + housekeeping |
| #238 | `8faf2e5` | fix(external-signup): enforce Firm Name client + server (BUG-012) |
| #245 | `8a35a61` | test(appointments): cover new helpers + karma minimatch pin |
| #249 | merged into feat | fix(auth): partition password-reset rate limit by email |
| #250 | `01bfd06` | feat(parity): promote OLD-app parity port to main (~100 commits) |
| #251 | OPEN | fix(docker): copy BannedSymbols.txt into backend build contexts |

Recommend merging #251 with `--admin` after CI greens (likely already
green by morning). Without #251, fresh `docker compose up --build`
fails on `BannedSymbols.txt` missing from build context.

## Verified working on main (smoke pass tonight)

- **Branding:** SPA title is "Appointment Portal" (parity rename).
- **Auth flow:** AuthServer Razor login + register pages render, OIDC redirect works.
- **Registration with firm-name validation (BUG-012):** signed up
  `appatty1@gesco.com` as Applicant Attorney; the Firm Name field
  conditionally appears + is required for attorney roles.
- **Slot generation:** `stafsuper1` generated 84 AME slots at Demo
  Clinic North (06/01-07) and 84 QME slots at Demo Clinic South
  (06/01-07).
- **BUG-036 filtered index** is live on the SQL Server schema:
  `[IsDeleted]=(0) AND [TenantId] IS NOT NULL`.
- **DbMigrator:** all 50 migrations applied cleanly. Falkinstein
  tenant seeded with 7 users (admin, supervisor, staff, adjuster,
  applicant.attorney, defense.attorney, stafsuper1, clistaff1).

## ALSO verified tonight (Sun late session)

- **Phase 3 booking submit (full UI):** appatty1@gesco.com booked A00001
  -- AME at Demo Clinic North, 2026-06-02 10:00 AM. Full booking form
  exercised end-to-end (Patient Demographics, Employer Details,
  Applicant Attorney Details, Claim Information modal). Status=1
  (Pending) on submit.
- **Phase 5 approval:** clistaff1@gesco.com approved A00001.
  Status=2 (Approved) + AppointmentApproveDate populated --
  **BUG-030 fix from #239 verified live.**
- **Phase 6 packet generation + BUG-036 regression:**
  - Initial: 3 packet rows created (Patient + Doctor + AttyCE),
    all Status=2 (Generated).
  - AttyCE retention soft-delete: Kind=3 row flipped to IsDeleted=1.
  - Regenerate clicked WHILE Kind=3 IsDeleted=1 row existed (the
    BUG-036 trigger condition).
  - Fresh Kind=3 row inserted successfully -- no SQL Server 2601
    error, no AbpDbConcurrencyException, no Hangfire retry storm.
  - **All Hangfire jobs (8-17) Succeeded on FIRST attempt -- zero
    retries. BUG-036 3-layer fix verified end-to-end live.**

## EVEN MORE verified tonight (Sunday late-late session)

- **Phase 0 expanded:** 252 slots seeded across AME (Demo Clinic
  North, 06/01-07 09:00-12:00), QME (Demo Clinic South, 06/01-07
  09:00-12:00), Panel QME (Demo Clinic North, 06/01-07 13:00-16:00).
- **Phase 3 second booking:** A00002 QME at Demo Clinic South
  (2026-06-03 10:00 AM) booked via UI by appatty1. Existing-patient
  dropdown auto-populated Alex Patient demographics.
- **Phase 7 document upload:** 9 MB PDF uploaded to A00001.
  AppAppointmentDocuments row created (FileSize=9437184,
  ContentType=application/pdf, IsAdHoc=1, Status=1).
- **Phase 8 scope visibility:**
  - Patient (patient1) sees 2 appointments (own).
  - Applicant Attorney (appatty1) sees 2 appointments (own as AA).
  - Scope filter operative.
- **Invite-external-user flow:** Staff Supervisor sent invite for
  defatty1@gesco.com (Defense Attorney). AppInvitations row created
  with TokenHash + 7-day ExpiresAt + AcceptedAt=NULL. Hangfire email
  dispatched. Accept-step requires real inbox click.
- **BUG-019 dual-partition rate limiter (PR #249):** verified live.
  - Same email (patient1) 7 attempts: 5x 204 -> 2x 429 (per-email
    cap working).
  - Different email (appatty1) same IP: 3x 204 -> no rate limit
    (per-email partitioning, no shared-IP DoS).
- **Phase 10 email-template parity audit:** OBS-36 confirmed live --
  64 active templates, 41 filled, 23 still TODO stubs. Demo-critical
  templates that are STILL STUBS: `AppointmentBooked`,
  `AppointmentApproved`, `AppointmentRejected`. Demo emails for the
  approve flow will render "Stub body for AppointmentApproved" if
  not patched before Tuesday.

## Final DB state for the demo

| Table | Count | Notes |
|---|---|---|
| AppDoctorAvailabilities | 252 | 3 types x 2 locations x 7 days x 12 slots |
| AppAppointments | 2 | A00001 (Approved AME) + A00002 (Pending QME) |
| AppAppointmentDocuments | 1 | 9 MB PDF on A00001 |
| AppAppointmentPackets | 4 total / 2 active | Patient + Doctor active; 2 AttyCE rows both soft-deleted by retention (BUG-036 regenerate cycle verified) |
| AbpUsers (@gesco.com) | 4 | stafsuper1, clistaff1, patient1, appatty1 |
| AppInvitations | 1 | defatty1 pending acceptance |
| HangFire.Job | 34 | All Succeeded, zero retries |

## NOT verified tonight (still saved for Monday)

- R2 failure modes (BookingPolicyValidator rejections, permission
  rejections, state-machine rejections).
- R3 replay sweep of existing open findings.
- Booking flows for Deposition, Record Review, Supplemental Medical
  Report (3 types still untested).
- Multi-tenant scope checks (Falkinstein is the only seeded tenant).
- Defense Attorney role end-to-end (defatty1 invite still pending
  acceptance via inbox click).
- Claim Examiner scope (no CE user registered yet).

## Known issues that may surface during demo

1. **#251 (Docker BannedSymbols.txt) must merge before any rebuild.**
   Current containers were built BEFORE the BannedSymbols.txt commit
   was added to repo root by #240, then survived because the build
   cache had the old layers. A fresh `docker compose up --build` on
   main TODAY will fail until #251 merges.

2. **Slot generation only writes one type x one location per click.**
   To seed all 6 types x 2 locations for the demo, that's 12
   round-trips through the UI. For variety in the demo, consider
   running this 2-3 more times for the actual demo types you'll show.

3. **Email verification goes to real inboxes.** New patient/attorney
   registrations send confirmation emails via real SMTP. For demo
   purposes I SQL-confirmed test accounts:
   ```sql
   UPDATE AbpUsers SET EmailConfirmed=1 WHERE Email LIKE '%@gesco.com';
   ```
   The actual email-click flow works in real life; for demo prep this
   is the fastest path to "registered + ready to use" state.

4. **Patient demographics + employer + attorney sections all need
   manual fill on each booking.** No "use sample data" quick-fill
   exists. If you want multiple demo appointments showing during the
   presentation, either:
   - Pre-fill them via the UI during prep on Monday morning, OR
   - Pre-seed via SQL (see `docs/runbooks/findings/bugs/SEED-3` for
     pattern; user manager seeds the right way to do this).

## Recommended Monday morning sequence

1. **Merge #251** (Docker BannedSymbols.txt fix) with `--admin`. CI
   should already be green by then.
2. **Open #168** (`main -> development`) auto-PR; merge to keep dev
   in sync.
3. **Complete Phase 0:** generate slots for Panel QME, Deposition,
   Record Review, Supplemental Medical Report. Stagger times by
   location per OBS-26 (slot-gen rejects same-location overlap
   across types).
4. **Phase 3 booking submit:** drive one booking end-to-end for each
   appointment type you plan to demo (probably AME + QME + Panel QME).
   ~10 min each.
5. **Phase 5 approval:** sign in as `clistaff1@gesco.com`, approve
   the bookings.
6. **Phase 6 packet generation:** verify packet rows materialize.
   For BUG-036 regression check, wait for AttyCE soft-delete (the
   retention rule), then click Regenerate and confirm it succeeds
   on first attempt (no Hangfire retry storm).
7. **Polish pass:** walk the actual demo flow you'll show Tuesday
   morning, look for any rough edges (missing toasts, ugly error
   text, broken links).

## State of the demo accounts

| Email | Password | Role | Confirmed |
|---|---|---|---|
| stafsuper1@gesco.com | 1q2w3E*r | Staff Supervisor | yes (seeded) |
| clistaff1@gesco.com | 1q2w3E*r | Clinic Staff | yes (seeded) |
| patient1@gesco.com | 1q2w3E*r | Patient (Alex Patient) | yes (SQL) |
| appatty1@gesco.com | 1q2w3E*r | Applicant Attorney (Aria Stone, Stone & Associates) | yes (SQL) |

## Open from-feat-side findings carried in #250 -- worth a look Monday

- BUG-019 (was BUG-035) -- password-reset rate-limit bucketed-by-ip --
  FIXED in this branch via dual-partition limiter.
- BUG-033 -- Kind=3 packet generation cascade failure (related to
  BUG-036 but not the same bug). HIGH severity, OPEN.
- BUG-038 -- /appointments/add route missing permissionGuard. LOW.
- BUG-039 -- internal booker uses generated CRUD modal instead of
  parity-port booking form. LOW.
- BUG-040 (was BUG-036) -- cumulative-trauma flag + ToDateOfInjury
  not persisting on booking submit. MEDIUM.
- BUG-041 (was BUG-037) -- Authorized User picker restricts to 2 roles
  vs OLD free-text. MEDIUM.
- OBS-27 -- invite email greeting empty (main side).
- OBS-35 -- CE scope misses injuryless appointment (feat side).
- OBS-36 (was OBS-27) -- 23 of 64 active templates are TODO stubs.

## How to resume tomorrow

In this worktree (`W:/patient-portal/main`):

1. `git status` should be clean (nothing dirty).
2. `git log origin/main --oneline -10` confirms the merges above.
3. Docker stack is up and seeded: `docker compose ps` shows all 7
   services healthy.
4. SPA at `http://falkinstein.localhost:4200`, AuthServer at
   `http://falkinstein.localhost:44368`, API at
   `http://falkinstein.localhost:44327`, Hangfire dashboard at
   `http://falkinstein.localhost:44327/hangfire`.

Tasks tracked in the session task list (#33 in_progress -- Phase 3
booking; #34-36 pending).
