# Test Plan -- MAIN post-merge seed + verify (2026-06-24)

Senior-QA pass on `main` after merge #322 (frontend-rework epic). Goal: (1) seed 15-20 COMPLETE
appointments mirroring real Gesco usage, (2) verify the merged rework UI + the fixes that landed.
Synthetic data only (HIPAA). UI-only navigation. Decisions via the AskUserQuestion modal. Commit
only when asked, by pathspec, no Claude attribution. Capture email links from logs; do NOT disable
outgoing email.

## Environment (resolved in Phase 0; LIVE values verified against the running stack)

| Item | Value | How resolved |
| --- | --- | --- |
| Checkout | `C:\src\patient-portal\main` | branch `main` @ 74e91563, merge #322 present |
| Compose project | `main` (containers `main-<svc>-1`) | docker-compose.override.yml naming |
| Angular SPA | http://localhost:4200 (tenant: http://<sub>.localhost:4200) | compose default NG_PORT |
| AuthServer | http://localhost:44368 | compose default AUTH_PORT |
| API | http://localhost:44327 | compose default API_PORT |
| SQL host port | 127.0.0.1:1434 | compose default SQL_HOST_PORT |
| MinIO | api 9000 / console 9001 | compose defaults |
| Packet renderer | 3001 | compose default |
| DB name | `CaseEvaluation` (default) | TODO confirm `SELECT name FROM sys.databases` |
| Tenant | TBD | TODO confirm `SELECT Name, Id FROM AbpTenants` (create if fresh seed has none) |
| In-app today | TBD | TODO confirm (dev clock) |
| Booking lead time | 3 days (expected) | TODO confirm via slot lookup |
| Email-link log | ON (`Notifications__LogLinks=true`) | docker-compose.override.yml |

sqlcmd (never echoes pw):
`MSYS_NO_PATHCONV=1 docker compose -p main exec -T sql-server bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d CaseEvaluation -h -1 -W -i /dev/stdin' <<'SQL' ... SQL`
For DELETE/UPDATE on party tables prepend `SET QUOTED_IDENTIFIER ON;`.

## Accounts (password `1q2w3E*r` for all; @gesco.com = monitored test inboxes)

Internal (expected seeded): stafsuper1@gesco.com (Staff Supervisor), clistaff1@gesco.com (Intake).
External (register fresh on main, BEFORE booking that names them, to avoid F-019 dup masters):
- Patients: patient1 (Daniel Harper), patient2 (Olivia Turner)
- Applicant Attorneys: appatty1 (Marcus Bennett / Bennett Lawson Law), appatty2 (Tiffany Lawson / same),
  appatty3 (Jesse Rogers / Rogers Jones Law)
- Defense Attorneys: defatty1 (Gregory Stone / Stone & Perez Defense LLP), defatty2 (Alicia Perez / same),
  defatty3 (Darla Norris / Norris Barret Defense LLP)
- Claim Examiners: claimE1 (Henry Caldwell), claimE2 (Jasmine Reid)

## Phases + checklist

### Phase 0 -- Orient (DONE for path/ports; DB-dependent items pending build)
- [x] Find main checkout + confirm merge
- [x] Resolve compose project + canonical ports from compose/.env.example
- [x] Confirm build prereqs (ABP token, secrets file)
- [x] Kick off from-scratch build + bring-up (fresh volumes)
- [ ] Verify LIVE ports via `docker compose ps`; DB name; tenant; in-app date; lead time

### Phase 1 -- Setup
- [ ] Confirm internal seeds (stafsuper1, clistaff1); note any missing (F-003)
- [ ] Confirm/create tenant; resolve tenant GUID + subdomain
- [ ] Register 2 patients, 3 AA, 3 DA, 2 CE (synthetic); email-confirm each
- [ ] Generate doctor availabilities (Demo Clinic North); South stays empty (expected)
- [ ] Verify masters 1-per-email; slots bookable

### Phase 2 -- Seed 15-20 COMPLETE appointments
- [ ] 3-4 via reworked UI wizard (AME/IME; clinics incl South empty; injury variations)
- [ ] Remainder via faithful 10-step API recipe (create+injury+activeCE+AAlink+DAlink+employer+insurance)
- [ ] Proportions: DA ~60%, AA ~20%, Patient ~10-15%, CE ~5-10%; ~90% paralegal-booked
- [ ] Approve a subset through REAL gates; leave a few Pending
- [ ] AUDIT: 0 appointments missing any of the 6 child records; masters 1-per-email

### Phase 3 -- Lifecycle + verify merged fixes (evidence: DB + UI + EMAIL-LINKS)
- [ ] F-013/F-014: named non-booker DA reschedule/cancel -> 200 + opposing consent; patient-initiated too
- [ ] F-017: reschedule keeps slot TIME (child appt non-midnight, OriginalAppointmentId set, source Rescheduled)
- [ ] F-018: resubmit blocked (403) while flagged Documents unsatisfied; succeeds after valid-PDF upload
- [ ] F-006/F-019: AA/DA/Patient masters 1-per-email w/ name+firm+email; detail shows full party info
- [ ] Also: reschedule reject, cancel reject, direct staff cancel, re-evaluation, approve/reject/send-back at volume

### Phase 4 -- Per-screen QA lens
- [ ] Console errors; 403/500; role-scoped data (HIPAA); business sense; modern UX
- [ ] Watch F-019 deeper layer (register-after-booking still makes a 2nd profile row) -- registration-first avoids it

## Stop points
- Phase 0: if env cannot be determined -> ask via modal (currently determined; verifying live)
- On a meaningful bug -> surface severity + fix-size + recommendation via modal; do NOT fix mid-run unless chosen
- Before any commit -> ask

## Findings
Logged to `FINDINGS.md` in this folder (created in Phase 1+).
