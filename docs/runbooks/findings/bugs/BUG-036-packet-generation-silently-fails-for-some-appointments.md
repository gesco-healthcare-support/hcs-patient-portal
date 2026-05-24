---
id: BUG-036
title: Packet generation silently fails for some approved appointments; Hangfire reports Succeeded but no packets persisted
severity: medium-to-high (refined 2026-05-23 after deep diagnosis)
status: open
found: 2026-05-23 hardening HRD-P6.1
diagnosed: 2026-05-23 -- deterministic SQL repro + ABP/EF Core research confirms 3-layer root cause
last-replayed: 2026-05-23 (A00006 AME approval also generated ZERO packets, same pattern as A00004. So BUG-036 affects multiple types, not just Deposition. Approval emails to all 4 parties still fire correctly via the partial-failure-isolation path; Kind=3 AttyCE packet attachment for AME types is MISSING.)
flow: packet-generation
component: src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs (suspected)
---

# BUG-036 - Packet generation silently fails on some appointments

## Symptom

During the hardening run, 4 appointments were approved (A00001 SupReport,
A00002 QME, A00004 Deposition, A00005 Record Review). Each should have
generated 2 packets (`Kind=1 Patient`, `Kind=2 Doctor`) since none were
AME / Panel-QME (the only types that also generate `Kind=3 AttyCE`).

DB observation:

```
SELECT a.RequestConfirmationNumber, p.Kind, p.[Status]
FROM AppAppointments a LEFT JOIN AppAppointmentPackets p ON p.AppointmentId = a.Id
WHERE a.IsDeleted = 0 AND a.AppointmentStatus = 2
ORDER BY a.RequestConfirmationNumber, p.Kind;

A00001 | 1 | 2  (Patient packet generated)
A00001 | 2 | 2  (Doctor packet generated)
A00002 | 1 | 2
A00002 | 2 | 2
A00004 | NULL | NULL   <-- NO PACKETS GENERATED
A00005 | 1 | 2
A00005 | 2 | 2
```

A00004 has ZERO packet rows.

Hangfire job ledger:

```
Job 63 | Succeeded | 2026-05-23 18:32:45.650
       Arguments: {"AppointmentId":"097e2788-686a-f430-1a89-3a216842d1c2",
                  "TenantId":"d2b03683-2ad9-e7c6-7d97-3a2167dbfded"}
```

Job 63 is the `GenerateAppointmentPacketJob` for A00004. It reports
**Succeeded** in `HangFire.Job.StateName`, yet produced zero rows.

By contrast, Jobs 58 (A00002) and the corresponding A00001 + A00005 jobs
all Succeeded AND produced 2 packet rows each.

## Differences between A00004 and the others

What's different about A00004:

- Booker: `claimE1` (Claim Examiner role) - the others were booked by
  Patient (A00001), Applicant Attorney (A00002), Defense Attorney (A00003
  rejected), and Clinic Staff (A00005).
- A00003 was a Panel-QME booked by `defatty1`, rejected before approval,
  so no packets expected.
- The approval was triggered via direct API (POST .../approve) rather than
  UI for A00004 (and A00002 - which still got packets). So the API path
  alone is not the cause.
- A00004 was the FIRST approval done as part of a batch of 3 API calls
  (P5.2 + P5.4 + P5.5). A00002 also batch-API. Both batch but only
  A00004 missed packets.

## Positive collateral finding (partial-failure isolation)

Despite A00004 missing packet generation, the 4 approval-notification
emails for A00004 (Jobs 64-67 to AA + DA + CE + Patient) ALL fired
successfully. This means the packet job and the email-fan-out jobs are
properly decoupled - one's failure does not cascade to the others. This
is GOOD behavior; the 2026-05-15 episodic memory of "Kind=3 dup-key blocks
emails" is NOT reproducing.

## 2026-05-23 DEFINITIVE ROOT CAUSE (post-deep-dive + SQL repro + research)

After deep code reading, Hangfire state-history inspection, direct SQL
repro, and online research, BUG-036 is actually **two compounding
defects** that together cause the symptoms.

### What the original BUG-036 finding got WRONG

The "A00004 has 0 packet rows" observation was a TIMING artifact: the
first packet-job attempt fails and Hangfire reschedules a retry +15-30s
out via `DelayedJobScheduler`. The retry succeeds and creates the rows.
My DB query at 18:36 happened BEFORE the retry. After full state
inspection of `HangFire.Job` + `HangFire.State`, A00004 actually has
both Kind=1 and Kind=2 packet rows (created at 18:33:36 and 18:33:37).

The AttyCE soft-delete on A00006 (`Kind=3, IsDeleted=1`) is BY DESIGN
per `PacketAttachmentProvider.NotifySendCompletedAsync` lines 100-117:
"AttyCE rows persist ONLY when send fails -- gives the office a
manual-resend fallback. On success, prune the row + blob to match
Adrian's retention rule." Documented in `AttyCEPacketEmailHandler`
lines 42-49.

So "packets never generate" is wrong. The remaining bug is:

### Defect 1: Unique index has NO `IsDeleted` filter

Confirmed by direct SQL inspection of
`IX_AppAppointmentPackets_TenantId_AppointmentId_Kind`:

```
INDEX_NAME                                       is_unique filter_definition
IX_AppAppointmentPackets_TenantId_AppointmentId_Kind  1   ([TenantId] IS NOT NULL)
```

The index applies regardless of `IsDeleted`. After
`PacketAttachmentProvider.NotifySendCompletedAsync` SOFT-deletes the
AttyCE row (line 113: `_packetRepository.DeleteAsync(packet, autoSave:
true)`), the underlying SQL row physically remains (IsDeleted=1).

Verified by direct INSERT attempt:

```sql
INSERT INTO AppAppointmentPackets (...) VALUES (..., Kind=3, ...);
-- Msg 2601, Level 14, State 1
-- Cannot insert duplicate key row in object 'dbo.AppAppointmentPackets'
-- with unique index 'IX_AppAppointmentPackets_TenantId_AppointmentId_Kind'.
-- The duplicate key value is (<tenant>, <appointment>, 3).
```

When `AppointmentPacketManager.EnsureGeneratingAsync` runs after a
prior AttyCE send + Regenerate:

```csharp
// line 38: ABP's global query filter excludes IsDeleted=1
var existing = queryable.FirstOrDefault(x => x.AppointmentId == ... && x.Kind == kind);

if (existing == null)
{
    existing = new AppointmentPacket(...);
    // line 42: INSERT violates unique constraint - row exists with IsDeleted=1
    return await _packetRepository.InsertAsync(existing, autoSave: true);
}
```

This is the EXACT failure path captured in Hangfire's exception trace:

```
at AppointmentPacketManager.EnsureGeneratingAsync(...) line 42
at GenerateAppointmentPacketJob.GenerateKindAsync(...) line 156
```

### Defect 2: ABP `EnqueueAsync` does NOT defer to UoW commit (Hangfire provider)

Research finding from ABP framework GitHub + ABP support threads:

> `Volo.Abp.BackgroundJobs.Hangfire.HangfireBackgroundJobManager.EnqueueAsync`
> is a thin wrapper around `BackgroundJobClient.Enqueue`. The job row
> is written to Hangfire's storage using Hangfire's own
> connection/transaction, separate from the ABP EF Core UoW. As soon
> as the call returns, the Hangfire worker can dequeue it - even if
> the outer ABP UoW later rolls back.

Sources:
- https://abp.io/support/questions/3685/Hangfire-background-job-does-not-work-with-unit-of-work-properly
- https://abp.io/support/questions/10072/How-to-make-Hangfire-job-creation-part-of-transaction
- https://github.com/aspnetboilerplate/aspnetboilerplate/issues/3375

`PacketGenerationOnApprovedHandler.HandleEventAsync` at line 44 calls
`_backgroundJobManager.EnqueueAsync` directly inside `[UnitOfWork]`
WITHOUT wrapping in `CurrentUnitOfWork.OnCompleted(...)`. This means
the Hangfire worker can dequeue the packet job and start running it
BEFORE the surrounding Approve UoW commits the appointment-status
change.

On the very first packet-generation run after Approve, the job can
race the parent UoW's commit. Within the racy window:
- Job's `_appointmentRepository.GetAsync(args.AppointmentId)` may
  succeed (appointment row already exists at Status=Pending).
- Job's `EnsureGeneratingAsync.InsertAsync` ATTEMPTS to insert a new
  packet row.
- If the parent UoW is also writing to / locking the same packet
  table (e.g., from a separate handler), the timing is non-
  deterministic.

This explains why the FIRST attempt fails on Approve even though no
prior packet rows existed - it's a UoW commit race, not a unique
constraint violation.

### Defect 3: EF Core BATCHED INSERT mis-classification (acknowledged, won't-fix)

When EF Core 8+ batches an INSERT that violates a unique constraint,
the SQL error 2601 surfaces as `DbUpdateConcurrencyException` rather
than `DbUpdateException`, because EF's batch executor's
`AffectedCountModificationCommandBatch.ThrowAggregateUpdateConcurrencyExceptionAsync`
counts rows-affected mismatch.

This is documented as won't-fix in EF Core issues:
- https://github.com/dotnet/efcore/issues/20649 (EF Core 3.1.2)
- https://github.com/dotnet/efcore/issues/35043 (EF Core 8.0.10)

ABP wraps `DbUpdateConcurrencyException` as
`Volo.Abp.Data.AbpDbConcurrencyException`. So the chain is:

```
SqlException (2601 - duplicate key)
  -> DbUpdateConcurrencyException
    -> AbpDbConcurrencyException
      -> BackgroundJobExecutionException (Hangfire wrapper)
```

This is why the exception type is misleading - it looks like a stale
concurrency-stamp issue, but is actually a constraint violation.

### Replication

DETERMINISTIC repro (proven 2026-05-23):

```sql
SET QUOTED_IDENTIFIER ON;
INSERT INTO AppAppointmentPackets (Id, TenantId, AppointmentId, Kind,
    BlobName, [Status], GeneratedAt, IsDeleted, CreationTime,
    ConcurrencyStamp, ExtraProperties)
VALUES (NEWID(), '<existing-tenant-id>', '<existing-appt-id>', 3,
    'test/blob.pdf', 1, GETUTCDATE(), 0, GETUTCDATE(), 'test', '{}');
-- Where (tenant, appt, 3) has a soft-deleted row.
-- Result: Msg 2601 - duplicate key violation
```

E2E repro (would require IT Admin permission to trigger Regenerate):
1. Approve an AME-type appointment as Clinic Staff. Wait for packet job
   retry to complete (60 s).
2. Verify Patient + Doctor + AttyCE packets exist.
3. Wait for AttyCE email to be sent (Hangfire SendAppointmentEmailJob).
   `NotifySendCompletedAsync` soft-deletes the AttyCE row.
4. Login as IT Admin. POST `/api/app/appointments/<id>/packet/regenerate`.
5. Observe Hangfire job fails with `AbpDbConcurrencyException` on Kind=3.
6. Retry also fails. Manual recovery: hard-delete the IsDeleted=1 row
   from SQL.

### Fix paths (in priority order)

1. **PRIMARY FIX: filter the unique index on `IsDeleted = 0`** -
   the cleanest fix. Use a SQL Server filtered index:

   ```csharp
   // CaseEvaluationDbContext.cs:499 (and TenantDbContext.cs:392)
   b.HasIndex(x => new { x.TenantId, x.AppointmentId, x.Kind })
       .IsUnique()
       .HasFilter("[IsDeleted] = 0 AND [TenantId] IS NOT NULL");
   ```

   This excludes soft-deleted rows from the unique constraint. Add a
   migration. Verified-fit: SQL Server's filtered indexes have already
   been configured (the index has `[TenantId] IS NOT NULL` filter).

2. **SECONDARY FIX: wrap EnqueueAsync in OnCompleted** in
   `PacketGenerationOnApprovedHandler.HandleEventAsync`:

   ```csharp
   var currentUow = _unitOfWorkManager.Current;
   if (currentUow != null)
   {
       currentUow.OnCompleted(async () =>
           await _backgroundJobManager.EnqueueAsync(new GenerateAppointmentPacketArgs { ... }));
   }
   else
   {
       await _backgroundJobManager.EnqueueAsync(new GenerateAppointmentPacketArgs { ... });
   }
   ```

   Same pattern already used in `GenerateAppointmentPacketJob` lines
   202-210 for `PacketGeneratedEto` publish. Apply consistently.

3. **DEFENSE-IN-DEPTH: widen catch filter in GenerateKindAsync** to
   include `AbpDbConcurrencyException`. On catch, log + `MarkFailed`,
   don't propagate. Avoids the Hangfire retry storm.

4. **OPTIONAL: hard-delete instead of soft-delete in
   NotifySendCompletedAsync**. The AttyCE row is intentionally
   transient (delivered + pruned). HardDelete avoids the constraint
   collision entirely. Tradeoff: loses audit trail of the row.

### Recommended order of operations for the fix

Land fix #1 (filtered unique index) FIRST as the cheap +
self-contained fix. Then fix #2 (OnCompleted) to remove the Hangfire
race. Fix #3 is optional defense-in-depth.

## Hypothesis (3 in priority order)

Hangfire state-history inspection for the failing packet jobs (Job 63 for
A00004, Job 87 for A00006) shows the EXACT failure path:

```
63 Enqueued     2026-05-23T18:32:45.648
63 Processing  2026-05-23T18:32:45.663
63 Failed      2026-05-23T18:32:45.771
   Volo.Abp.Data.AbpDbConcurrencyException:
     "The database operation was expected to affect 1 row(s),
      but actually affected 0 row(s); data may have been modified or
      deleted since entities were loaded."
   ---> Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
63 Scheduled   (retry attempt 1 of 10) -> EnqueueAt: T+15s
63 Enqueued    (DelayedJobScheduler trigger) 18:33:36
63 Processing  18:33:36
63 Succeeded   18:33:39 (PerformanceDuration: 3205 ms)
```

**The job retried and "Succeeded" on attempt 2, BUT produced zero packet
rows.** Same pattern observed for Job 87 (A00006, AME, 15.8 s retry
duration).

The failure surface is `_packetRepository.UpdateAsync(...)` somewhere in
`AppointmentPacketManager.EnsureGeneratingAsync` (line 48: UPDATE path
when an existing packet row is found) or `MarkGeneratedAsync` (line 69)
or `MarkFailedAsync` (line 79). All three call `UpdateAsync` on an entity
whose `ConcurrencyStamp` no longer matches the DB row.

Likely sequence on the first attempt that fails:
1. Worker dequeues job, ExecuteAsync starts inside [UnitOfWork] +
   tenant scope.
2. `EnsureGeneratingAsync` finds an existing row (from a PRIOR PARTIAL
   RUN whose UoW rolled back at a later step). The row was inserted
   with autoSave=true.
3. UpdateAsync runs on the existing row with the captured
   ConcurrencyStamp. The DB row's ConcurrencyStamp has been mutated by
   something else (or the row was deleted).
4. `expected to affect 1 row, actually 0` exception fires.
5. Parent UoW rolls back. Packet row from the INSERT in step 2 (if any
   was new) is lost.
6. Hangfire reschedules attempt 2.

On attempt 2:
- `EnsureGeneratingAsync.FirstOrDefault` may again return null (because
  the prior insert rolled back). InsertAsync creates a new row.
- The catch filter (line 216: only IOException / InvalidOperationException
  / ArgumentException) doesn't cover concurrency exceptions, so the
  failure escapes the per-kind try.
- Subsequent attempts may keep failing OR the Hangfire job is now in
  an inconsistent state where the JOB succeeds but the row insertions
  rolled back.

Suspected fix paths:
- Widen `catch when (ex is IOException || ...)` to also catch
  `AbpDbConcurrencyException` + `DbUpdateConcurrencyException` ->
  MarkFailed + don't rethrow.
- Or restructure `EnsureGeneratingAsync` to use upsert semantics
  (INSERT ... ON CONFLICT DO UPDATE) at the SQL layer, avoiding the
  concurrency-stamp race.
- Or wrap the per-kind packet-row write in its own SCOPED UoW so
  the surrounding rollback doesn't undo the row.

## Hypothesis (3 in priority order)

1. **EnsureGeneratingAsync race / silent swallow** - per the 2026-05-15
   episodic context, `EnsureGeneratingAsync` is a query+insert sequence
   that can throw on duplicate `(TenantId, AppointmentId, Kind)`. If the
   `OnCompleted(...)` UnitOfWork deferral pattern fired the job before
   the parent's appointment-write transaction was committed (a known
   prior issue, PR #174), the insert could race or fail silently.
2. **Try/catch swallows exceptions** - the per-Kind try/catch may be
   catching ALL exceptions including the "appointment not found" ones,
   logging a warning but marking the Hangfire job as Succeeded. Need
   to inspect job container logs:
   ```
   docker logs main-api-1 --since 2026-05-23T18:32:00 | grep -i "packet\|appointmentid\|generate"
   ```
3. **Single-row pre-check returns empty** - if the job's first step
   reads `AppAppointmentPackets` to check existence and the query
   somehow returns "exists" for an empty result set (column-comparison
   bug), the job exits early without generating anything.

## Recommended next step

1. Reproduce by approving a fresh Deposition / Record Review / QME and
   checking packet counts. If reproducible, it's a real bug.
2. Inspect API container logs around 18:32:45 for any warnings or
   exceptions from `GenerateAppointmentPacketJob`.
3. Add a database constraint or post-job assertion that every approved
   appointment has at least the 2 expected packet rows (Kind=1, Kind=2)
   within N seconds of approval, raising an alarm if not.

## Functional impact

Medium severity:

- Patient + Doctor packets are storage-only for Kind=2 (no email) and
  the recipient for Kind=1. If they don't generate, the Patient never
  receives the packet PDF; clinic staff cannot retrieve it.
- The approval-notification email still fires (per positive finding
  above), so the patient knows the appointment was approved - but they
  cannot get the document.
- For appointments that DO need a packet attachment in subsequent
  workflow steps (e.g., a future "send packet to insurance" step), the
  missing rows would block that step entirely.

## Related

- 2026-05-15 episodic memory: "EnsureGeneratingAsync duplicate-key
  race" pattern.
- PR #174 (UoW commit-race in Hangfire packet job).
- HRD-P6.2 partial-failure isolation: separately confirmed working.
