---
feature: appointment-go-live-fixes
date: 2026-06-07
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Fix the four go-live blockers (F1-F4) found in the integrated build and reduce selectable
appointment types to AME/IME/PQME, committed directly to local `main` (HEAD a1ecdde) and
verified in the live Falkinstein Docker stack.

## Context

- Base is local `main` = `a1ecdde` (the replicate-old-app parity work merged in; 40 commits
  ahead of `origin/main`). Root causes were re-confirmed against THIS code, not 751e320.
- Optimal-fix directions validated against official docs: EF Core optimistic concurrency
  (delete-of-already-modified row throws by design; resolve by idempotent/bulk delete), ABP
  events publish on UoW completion (handlers can race committed data -> abp#17508), ABP
  background jobs are retried so must be idempotent.
- Delivery: commit to local `main` per task; rebuild + verify in the Docker stack ("live").
- HIPAA: synthetic data only; no PHI in logs/tests/fixtures.

## Approach (decisions locked)

- **F4 (reschedule 500):** the reschedule clone reuses the source `RequestConfirmationNumber`
  (`AppointmentChangeRequestsAppService.Approval.cs:~257` `sameConfirmationNumber: true`).
  Fix = generate a fresh number for the clone. Reason: each reschedule is a NEW row under a
  unique `(TenantId, RequestConfirmationNumber)` index. Rejected: dropping the index (loses
  the dedup guarantee).
- **F3 (duplicate AttyCE emails):** per-recipient email jobs share one `AppointmentPacket`
  and each deletes it in `PacketAttachmentProvider.NotifySendCompletedAsync` -> concurrent
  deletes throw `DbUpdateConcurrencyException` -> Hangfire retries re-send. Fix = idempotent
  prune via a set-based `DeleteDirectAsync(x => x.Id == id)` (no load, no concurrency token,
  0-rows is benign) + never let a post-send failure re-trigger the SMTP send. Rejected:
  swallowing the exception only (leaves the retry-storm window).
- **F1+F2 (auto-approve loses parties/injuries):** the booking is split into create + 5
  client-side post-create attach POSTs that race the create's approval side-effects. Fix =
  make booking ATOMIC server-side: extend the create payload with parties + injuries, persist
  them in the SAME unit of work in `AppointmentsAppService.CreateAsync` BEFORE side-effects,
  then for internal users approve in-flow (gates run on complete data) -- keeping auto-approve.
  Angular submits ONE call; regenerate the proxy. Reason: a booking is one aggregate
  transaction (DDD); this removes the 409 race AND the gate bypass. Rejected: "don't
  auto-approve on create" (smaller, but loses the auto-approve UX the user wants kept).
- **Appointment types:** current seeder already seeds only AME/IME/PQME; the 4 legacy types
  linger only in already-seeded DBs and are referenced by 27 historical appointments (FK
  `NO_ACTION` blocks delete). Fix = a run-once EF migration that reclassifies referencing rows
  (appointments + availability type-sets) from legacy -> canonical, then deletes the 4 legacy
  types. No-op on fresh DBs (0 rows affected). Mapping (synthetic demo data; prod fresh has no
  legacy): QME -> PQME; Deposition/Record Review/Supplemental Medical Report -> IME.

## Tasks (sequenced low-risk/high-value first; commit to main + rebuild + verify each)

- T0: Rebuild the live stack from `a1ecdde` and confirm migration integrity (GATE).
  - approach: code
  - files-touched: []
  - acceptance: `docker compose up -d --build`; all containers healthy; `db-migrator` exits 0
    applying the new migrations on the existing Falkinstein volume (no wipe); the 3 appts
    booked earlier (A00065/66/67) still present. If db-migrator fails -> STOP and report.

- T1: F4 -- regenerate confirmation number on reschedule-approval clone.
  - approach: tdd
  - files-touched: [`src/...Application/Appointments/AppointmentChangeRequestsAppService.Approval.cs`,
    `src/...Domain/Appointments/AppointmentManager.cs` (expose number generation if needed),
    `src/...Domain/Appointments/AppointmentRescheduleCloner.cs`]
  - acceptance: a test asserts the rescheduled appointment gets a NEW confirmation number !=
    the source; approving a reschedule no longer 500s (verified live: reschedule A00066 ->
    Approved, new A##### row, old row terminal).

- T2: F3 -- idempotent packet prune + no re-send on post-send failure.
  - approach: test-after
  - files-touched: [`src/...Domain/AppointmentDocuments/PacketAttachmentProvider.cs`,
    `src/...Domain/Appointments/Jobs/SendAppointmentEmailJob.cs`]
  - acceptance: `NotifySendCompletedAsync` uses a set-based delete that no-ops when the row is
    already gone (no `DbUpdateConcurrencyException`); a test covers "packet already pruned ->
    no throw"; live: approving an appointment sends each AttyCE packet email exactly once (no
    retry-storm, no duplicate sends in logs).

- T3: Appointment-type reduction -- run-once reclassify + delete migration.
  - approach: code
  - files-touched: [`src/...EntityFrameworkCore/Migrations/<new>_Reduce_AppointmentTypes_To_Three.cs`
    (+ Designer + snapshot), verify `AppointmentTypeDataSeedContributor.cs` seeds only 3]
  - acceptance: after migrate, the live DB has exactly AME/IME/PQME; the 27 historical
    appointments now point at canonical types (per mapping); booking dropdown shows only 3;
    migration is a no-op re-run and on a fresh DB.

- T4: F1+F2 -- atomic server-side booking (parties + injuries in the create UoW) + keep
  internal auto-approve; Angular single-call submit + proxy regen.
  - approach: test-after
  - files-touched: [`src/...Application.Contracts/Appointments/AppointmentCreateDto.cs`,
    `src/...Application/Appointments/AppointmentsAppService.cs`,
    `src/...Application/CaseEvaluationApplicationMappers*.cs`,
    `angular/src/app/appointments/appointment-add.component.ts` (+ service),
    `angular/src/app/proxy/**` (regenerate via abp generate-proxy)]
  - acceptance: a booking persists appointment + AA/DA/CE + injuries in ONE transaction (no
    separate client attach calls); internal booking ends Approved WITH all party/injury join
    rows populated and packets generated; external booking ends Pending WITH all joins; live:
    re-book an internal AME -> Approved, AA/DA/CE joins=1/1/1, injuries>=1, no 409 in logs.

- T5: Verify all four fixed in live + final report.
  - approach: code
  - files-touched: [`docs/feedback-research/2026-06-07-go-live-fixes-verification.md`]
  - acceptance: a short report shows each of F1-F4 + type-reduction PASS in the rebuilt live
    stack with evidence (DB rows, log lines, screenshots); working tree clean (all committed).

## Risk / Rollback

- Blast radius: T4 is the largest (create DTO + AppService + mappers + Angular + proxy);
  T1/T2/T3 are contained. T3 is destructive (deletes 4 types) but guarded to the 4 legacy IDs
  and no-ops on fresh DBs. Migrations run on the existing Falkinstein volume in place (never
  `down -v`).
- Rollback: each task is its own commit on `main` -> `git revert <sha>`; T3's deletes are
  irreversible for the legacy rows (acceptable: synthetic demo data; prod has none). The
  email-link logging diagnostic + `docker-compose.override.yml` remain local/uncommitted.
- Concurrency-correctness risk: T4 must preserve the external (Pending) path; covered by tests
  + live re-book of both an internal and an external booking.

## Verification

After all tasks: rebuild stack from the committed `main`; (1) internal AME re-book ->
Approved with full joins + packets, no 409; (2) external IME/PQME -> Pending with full joins;
(3) approve -> single packet email per recipient, no concurrency errors; (4) reschedule
approve -> new confirmation number, no 500; (5) booking dropdown lists exactly AME/IME/PQME
and the DB holds only those 3 types. Report at
`docs/feedback-research/2026-06-07-go-live-fixes-verification.md`.
