---
feature: bug-036-packet-gen-soft-delete-race
date: 2026-05-23
status: in-progress
base-branch: main
related-issues: []
related-findings:
  - docs/runbooks/findings/bugs/BUG-036-packet-generation-silently-fails-for-some-appointments.md
---

## Goal

Fix BUG-036 so `GenerateAppointmentPacketJob` succeeds reliably on first
attempt AND on regenerate after a prior AttyCE delivery, by closing the
3 compounding defects identified during the 2026-05-23 diagnosis.

## Context

The hardening run on 2026-05-23 observed packet jobs failing with
`AbpDbConcurrencyException` ("expected 1 row, actually 0") on first
attempt; the Hangfire retry then "succeeded." Deep diagnosis revealed
the symptom is actually 3 compounding defects:

1. **Unique index `IX_AppAppointmentPackets_TenantId_AppointmentId_Kind`
   has no `IsDeleted` filter**, so soft-deleted AttyCE rows physically
   remain in the table and block reinsert on regenerate (SQL Server
   Msg 2601). Proven by direct SQL INSERT repro.
2. **`PacketGenerationOnApprovedHandler.HandleEventAsync:44` calls
   `IBackgroundJobManager.EnqueueAsync` directly inside `[UnitOfWork]`
   without `CurrentUnitOfWork.OnCompleted` wrapping.** Per ABP framework
   docs, Hangfire's enqueue is immediate (not UoW-deferred), so the
   worker can dequeue before the approve UoW commits. This is the
   first-attempt failure mode for fresh approvals.
3. **EF Core 8+ misclassifies SqlException 2601 on batched INSERT as
   `DbUpdateConcurrencyException`** (acknowledged as won't-fix in
   `dotnet/efcore #20649` and `#35043`), which ABP wraps as
   `AbpDbConcurrencyException`. The catch filter at
   `GenerateAppointmentPacketJob.GenerateKindAsync:216` only catches
   `IOException | InvalidOperationException | ArgumentException`, so
   the concurrency exception escapes, UoW rolls back, the job goes to
   Hangfire retry, and the row state is inconsistent across attempts.

Diagnosis citations + deterministic SQL repro are in the finding file.

Why now: BUG-036 is the only remaining real "medium" severity bug from
the hardening run (the other surfaced concerns - BUG-035 lockout,
BUG-034 refresh leeway, BUG-025 upload cap - all turned out to be
working-as-designed config). Closing it cleanly leaves the packet
pipeline self-healing without retries.

## Approach

**Chosen**: land all 3 fixes as separate tasks, sequenced by blast
radius (lowest first). All 3 changes touch domain logic / invariants,
so all 3 use the `tdd` approach: a failing test per fix, then the
production change, then the test passes.

- T1 (filtered unique index) closes the PRIMARY root cause. SQL Server
  filtered indexes are already used by the project (the existing index
  has `[TenantId] IS NOT NULL` filter), so this is a pure extension of
  an existing pattern.
- T2 (OnCompleted deferral) closes the Hangfire timing race. Same
  pattern already exists in `GenerateAppointmentPacketJob:202-210` for
  `PacketGeneratedEto` publish; apply consistently in the handler.
- T3 (catch widening) is defense-in-depth: even if T1 + T2 land
  cleanly, future concurrency edge cases (e.g., manual SQL writes,
  out-of-band jobs) should fail gracefully via `MarkFailed` rather
  than triggering the Hangfire retry storm.

**Rejected approaches:**

- Hard-delete instead of soft-delete in `NotifySendCompletedAsync` -
  loses audit trail of pruned AttyCE rows. Defer; not needed once T1
  lands.
- Refactor `EnsureGeneratingAsync` to include soft-deleted rows via
  `IIncludeAllSoftDeletedRecords` - reverses ABP's global-filter
  pattern; works but is more invasive. T1 is cleaner.
- Add a custom OpenIddict-style stamp comparison - over-engineering.
- Bypass Hangfire and run packet generation synchronously - regresses
  the documented W2-11 decision to decouple packet wiring from the
  booking domain.

## Tasks

- T1: Filtered unique index on `IsDeleted = 0`
  - approach: tdd
  - files-touched:
    - src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs (line 499)
    - src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs (line 392)
    - src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/2026XXXX_Packet_FilteredUniqueIndex_SoftDelete.cs (new)
    - test/HealthcareSupport.CaseEvaluation.Domain.Tests/AppointmentDocuments/AppointmentPacketIndexTests.cs (new)
  - acceptance:
    1. New xUnit test inserts a (tenant, appt, Kind=3) row, soft-deletes it (`IsDeleted=1`), then inserts a fresh (tenant, appt, Kind=3) row. The second insert SUCCEEDS (verified via `SaveChangesAsync` not throwing).
    2. Same test then inserts a THIRD (tenant, appt, Kind=3) row while the second is still active (IsDeleted=0) - SQL Server Msg 2601 is expected. The test asserts `AbpDbConcurrencyException` (or `DbUpdateConcurrencyException`) is raised, confirming the unique constraint still works for non-deleted rows.
    3. Migration generated via `dotnet ef migrations add Packet_FilteredUniqueIndex_SoftDelete --context CaseEvaluationDbContext` (and again for `CaseEvaluationTenantDbContext`). Up filter on the unique index changes from `([TenantId] IS NOT NULL)` to `([IsDeleted] = CAST(0 AS bit)) AND ([TenantId] IS NOT NULL)`.
    4. `dotnet build` passes; existing tests pass.

- T2: Wrap `EnqueueAsync` in `CurrentUnitOfWork.OnCompleted` in `PacketGenerationOnApprovedHandler`
  - approach: tdd
  - files-touched:
    - src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Handlers/PacketGenerationOnApprovedHandler.cs (lines 24-52: add IUnitOfWorkManager dependency + wrap EnqueueAsync)
    - test/HealthcareSupport.CaseEvaluation.Domain.Tests/AppointmentDocuments/PacketGenerationOnApprovedHandlerTests.cs (new)
  - acceptance:
    1. New xUnit test mocks `IBackgroundJobManager` + `IUnitOfWorkManager`. Sends `AppointmentStatusChangedEto` with `ToStatus = Approved` inside an active UoW. Asserts `_backgroundJobManager.EnqueueAsync` is NOT called synchronously - it is registered via `CurrentUnitOfWork.OnCompleted` and fires only after a simulated UoW commit.
    2. Second test: outside any UoW (UnitOfWorkManager.Current is null), the existing direct-EnqueueAsync fallback fires immediately (mirrors the same fallback in `GenerateAppointmentPacketJob:202-210`).
    3. Third test: when `ToStatus != Approved`, no enqueue happens (existing behavior preserved).
    4. `dotnet build` + tests pass.

- T3: Widen catch filter in `GenerateAppointmentPacketJob.GenerateKindAsync` to include `AbpDbConcurrencyException`
  - approach: tdd
  - files-touched:
    - src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs (line 216: add `|| ex is AbpDbConcurrencyException` to the catch filter)
    - test/HealthcareSupport.CaseEvaluation.Domain.Tests/AppointmentDocuments/GenerateAppointmentPacketJobTests.cs (new or extend existing)
  - acceptance:
    1. New xUnit test injects a stub `AppointmentPacketManager` whose `EnsureGeneratingAsync` throws `AbpDbConcurrencyException`. Run the job. Assert `MarkFailedAsync(packet.Id, errorMessage)` IS called for the failing kind, AND the exception does NOT propagate out of `GenerateKindAsync`, AND the OTHER kinds (Patient + Doctor) STILL generate cleanly.
    2. Existing tests covering the IOException / InvalidOperationException / ArgumentException paths still pass (no regression).
    3. Note in code comment that this is BUG-036 defense-in-depth: even with T1 + T2 landed, future concurrency races (cross-tenant SQL ops, manual janitor scripts) should not retry-storm Hangfire.
    4. `dotnet build` + tests pass.

## Risk / Rollback

- Blast radius:
  - T1: schema change. Both DbContexts get a new migration. Existing soft-deleted packet rows (e.g., A00006 Kind=3) become re-insertable. The migration's `Up` is reversible via `Down`.
  - T2: domain handler change. Behavior shift: packet job no longer races the parent UoW. Same pattern is already in use elsewhere (job's PacketGeneratedEto publish), so risk is low.
  - T3: catch-filter widening. Strictly additive; only changes behavior for exception types that were previously unhandled and would have crashed the job. Worst case: a previously-loud failure becomes a quietly logged + MarkFailed row, which is exactly the intended improvement.
- Rollback:
  - Each task is a separate commit. `git revert <sha>` rolls back T1 / T2 / T3 independently.
  - T1's migration can also be rolled back via `dotnet ef migrations remove` then `dotnet ef database update <prev-migration>`.

## Verification

After all 3 tasks land:

1. Run the new xUnit test suite: `dotnet test test/HealthcareSupport.CaseEvaluation.Domain.Tests/HealthcareSupport.CaseEvaluation.Domain.Tests.csproj --filter "FullyQualifiedName~Packet"`. All new tests pass + existing packet tests pass.
2. Live E2E repro:
   - `docker compose up -d --build api`
   - Login as Applicant Attorney; book an AME appointment.
   - Login as Clinic Staff; approve it. Wait 5 s.
   - `docker exec ... sqlcmd ... "SELECT Kind, [Status], IsDeleted FROM AppAppointmentPackets WHERE AppointmentId = '<approved-id>'"` -> 3 rows (Kind 1/2/3 all Status=Generated, IsDeleted=0).
   - Wait for AttyCE email Hangfire job to complete (`HangFire.Job` shows `SendAppointmentEmailJob` Succeeded for claime1/appatty1/defatty1).
   - Re-query packet rows: Kind=3 is now `IsDeleted=1` (intentional retention rule).
   - Login as IT Admin; POST `/api/app/appointments/<id>/packet/regenerate`.
   - Within 30 s, `HangFire.Job` shows the regenerate's packet job entered `Succeeded` on the FIRST attempt (no retry). `HangFire.State` has no Failed row for that job.
   - Re-query packet rows: Kind=1/2/3 all present and `IsDeleted=0` again. Old soft-deleted Kind=3 row is either hard-deleted by the cleanup logic OR remains soft-deleted next to the new active row (the filtered index permits this).
3. Run the full hardening suite Phase 6 + the new BUG-036 replay step. The "0 packets for A00006" symptom does not recur on any approve or regenerate.
4. No regression in `Hangfire.Job` Failed-then-Scheduled-then-Succeeded retry pattern for packet jobs. New approvals produce a single Succeeded packet job per appointment (no retries).
5. Update the finding file: `last-replayed: 2026-05-23 (fixed)` + `status: fixed`.
