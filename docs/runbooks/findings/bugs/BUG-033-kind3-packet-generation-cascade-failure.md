---
id: BUG-033
title: AbpDbConcurrencyException on first AttyCE-packet send-completion cascades; 3 of 4 subsequent Kind=3 packets never generated
severity: high
status: open
found: 2026-05-21 hardening HRD-P6.1 + HRD-P6.2
last-corroborated: 2026-06-02 (UI-seed T6; still OPEN, reproduces at scale -- see corroboration section)
flow: appointment-packet-generation
component: src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/PacketAttachmentProvider.cs:113 (NotifySendCompletedAsync) + AppointmentDocuments/Jobs/GenerateAppointmentPacketArgs handler
---

# BUG-033 - Kind=3 (AttorneyClaimExaminer) packet generation cascades to failure

## Symptom

Phase 5 approved 4 appointments (A00001, A00002, A00004, A00005). Phase 6 expected 3 packet rows per appointment (Kind=1 patient, Kind=2 doctor, Kind=3 attorneyclaimexaminer) -> 12 rows total. Actual DB state at +15 min after Phase 5:

```
A00001 -> 1, 2, 3   (all three; Status=2 each)
A00002 -> 1, 2      (Kind=3 missing)
A00004 -> 1, 2      (Kind=3 missing)
A00005 -> 1, 2      (Kind=3 missing)
```

Only the FIRST approved appointment's Kind=3 packet materialized. Three subsequent appointments are missing Kind=3 entirely.

API container log at the moment of A00001's Kind=3 send-completion:
```
[18:23:08 WRN] There is an entry which is not saved due to concurrency exception:
AppointmentPacket {Id: 024c71a8-7af5-62a9-f890-3a215df3bbef} Modified FK {AppointmentId: 2b07609a-432f-3dc3-8637-3a215dda046f}
[18:23:08 WRN] SendAppointmentEmailJob: NotifySendCompletedAsync threw for packet 024c71a8-...
              (kind=AttorneyClaimExaminer); attachment lifecycle may need manual cleanup.
Volo.Abp.Data.AbpDbConcurrencyException: The database operation was expected to affect 1 row(s),
  but actually affected 0 row(s); data may have been modified or deleted since entities were loaded.
  at PacketAttachmentProvider.NotifySendCompletedAsync(...) line 113
  at SendAppointmentEmailJob.SendWithAttachmentAsync(...) line 166
[18:23:08 WRN] Failed to process the job '76': an exception occurred. Retry attempt 1 of 10
              will be performed in 00:00:22.
```

And immediately after, the suspended state cascades:
```
[18:23:32 WRN] SendAppointmentEmailJob: packet 024c71a8-... (kind=AttorneyClaimExaminer) is
              not Generated; skipping packet email (AttyCEPacket/2b07609a-...) to appatty1@gesco.com.
[18:23:32 WRN] SendAppointmentEmailJob: packet 024c71a8-... (kind=AttorneyClaimExaminer) is
              not Generated; skipping packet email (AttyCEPacket/2b07609a-...) to defatty1@gesco.com.
```

The packet row still shows `Status = 2 (Completed)` in DB. So generation completed but the send-job's view of the row is stale -- another transaction modified the row between load and save in `NotifySendCompletedAsync`.

For A00002/A00004/A00005, the Kind=3 packet row was never inserted at all. The concurrent transaction that broke A00001's send-completion likely held a lock or version row that prevented subsequent appointments' Kind=3 entity from being inserted within the timeout window of the orchestrating job.

## 2026-06-02 corroboration at scale (UI-seed T6) -- STILL OPEN on current code

Reproduced at much larger scale during the UI-seed lifecycle run. Driving the
appointment-view Approve action over the live SPA, **46 appointments were
approved back-to-back as `supervisor@falkinstein.test` in ~3 minutes** (a far
heavier burst than the original 4-in-60s repro). The synchronous Approve calls
all returned 200 and every status badge flipped to Approved (appointment status
itself is correct); the damage is entirely downstream in the async pipeline:

- **~180 `Volo.Abp.Data.AbpDbConcurrencyException`** across **~41 distinct
  Hangfire jobs**, each "Retry attempt N of 10 ... in 00:0X:XX". The queue
  drains over several minutes with exponential backoff; most succeed on retry.
- **16x** `SendAppointmentEmailJob: NotifySendCompletedAsync threw ... attachment
  lifecycle may need manual cleanup` -- same `PacketAttachmentProvider.cs:113` /
  `SendAppointmentEmailJob.cs:166` path as the original finding.
- **~21x** `SendAppointmentEmailJob: packet <id> (kind=AttorneyClaimExaminer) is
  not Generated; skipping packet email (AttyCEPacket/<appt>) to <recipient>` --
  the email job outran packet generation and the skip is NOT re-queued, so those
  AttyCE packet emails were permanently dropped (mix of synthetic + real inboxes).

Confirms the exact log fingerprints from the original finding, on current code
(2026-06-02). NOTE: these were FIRST-TIME approvals of DISTINCT appointments
(no Regenerate / no prior soft-deleted AttyCE row), so this run corroborates
hypotheses (1)+(3) below and BUG-036 Defect 2 (Hangfire UoW commit race) rather
than BUG-036 Defect 1 (the soft-delete + unfiltered-unique-index collision).
The skipped-email count scales with approval burst size -> a busy clinic morning
(many approvals in a short window) would silently drop a proportional number of
attorney/CE packet emails. Sequential single-appointment approvals (spaced out)
did NOT trigger the storm -- so throughput/serialization of the post-approve job
fan-out is the lever. (Originally logged as UI-seed findings F-H / F-I / F-J;
folded here rather than filed as duplicate bug IDs.)

## Hypothesis

1. **Optimistic concurrency token on shared resource.** Two jobs both load the same `AppointmentPacket` row (or a parent aggregate like `Appointment`), one updates first, the other fails on the version check. The "other" job aborts; its sibling Kind=3 insert never runs. Fix: scope the unit-of-work per kind so a Kind=3 insert isn't transactionally bundled with a Kind=1 send-completion update.

2. **Stale `ConcurrencyStamp` on the parent appointment.** When the packet send-completion update fires `UPDATE AppointmentPackets SET ... WHERE Id = ... AND ConcurrencyStamp = <X>`, the row already has stamp `<Y>` because the appointment-status-update transaction touched it. The 0-rows-affected blow up. Fix: detach the packet stamp from the appointment's stamp; or use a more permissive locking strategy on the packet update.

3. **Hangfire job interleaving + missing transactional guard.** Multiple appointments' approve calls happen near-simultaneously (within ~40s of each other in this run). The packet-generation worker dispatches them concurrently. Each Kind=3 insert competes for the same template-cache row or the same `IRepository<AppointmentPacket>` instance. The first wins; the rest's Kind=3 row writes get rolled back as part of the concurrency abort. Fix: serialize Kind=3 generation per tenant, OR ensure each appointment's Kind=3 generation runs in an isolated UoW.

Most likely (1)+(3) combined: the packet ConcurrencyStamp is shared across send/generate paths, and concurrent appointments' generators interleave such that the first send's stamp invalidates all subsequent packet inserts.

## Reproduction

1. Bootstrap fresh stack, run Phase 0 (slot generation), Phase 1 (register patient1 + appatty1 + defatty1 + claimE1), Phase 3 (5 bookings).
2. Approve at least 4 bookings within ~60 seconds (Phase 5.1, 5.2, 5.4, plus one more).
3. Wait 3 minutes.
4. SQL:
   ```sql
   SELECT a.RequestConfirmationNumber, p.Kind, p.Status
   FROM AppAppointments a
   LEFT JOIN AppAppointmentPackets p ON p.AppointmentId = a.Id
   WHERE a.AppointmentStatus = 2
   ORDER BY a.RequestConfirmationNumber, p.Kind
   ```
5. Observe: only the first approved appointment has 3 packet rows; the rest have 2 each (missing Kind=3).
6. Tail api container logs: `docker logs --since 5m main-api-1 2>&1 | grep -iE 'AbpDbConcurrency|NotifySendCompleted|kind=AttorneyClaimExaminer'` -> shows the cascade.

## Recommended fix

Step 1: Inspect `PacketAttachmentProvider.NotifySendCompletedAsync` at line 113 and the surrounding flow. Identify what entity it loads and updates.

Step 2: Audit `GenerateAppointmentPacketArgs` job handler. Look for any spot where the per-kind generation shares state with the per-kind send-completion. Common pattern: a single `await UnitOfWorkManager.CurrentOrCreateAsync()` wrapping both -- that wrapper is the bug.

Step 3: Refactor to per-kind try/catch with per-kind isolated UoW:
```csharp
foreach (var kind in new[] { Patient, Doctor, AttorneyClaimExaminer })
{
    try
    {
        using var uow = UnitOfWorkManager.Begin(requiresNew: true);
        await GenerateKindAsync(appointmentId, kind);
        await uow.CompleteAsync();
    }
    catch (AbpDbConcurrencyException ex)
    {
        Logger.LogWarning(ex, "Kind {Kind} generation hit concurrency; will retry independently", kind);
        // Enqueue ONE-kind retry job, not the whole 3-kind job
    }
}
```

Step 4: Also fix the send-completion path so a concurrency exception on a SINGLE send doesn't cause the orchestrator to skip subsequent appointments. Each `SendAppointmentEmailJob` should be standalone.

Step 5: Add integration test that approves 5 appointments in rapid succession and asserts all 15 packet rows exist within 60 seconds.

## Functional impact

**HIGH severity** -- this is a data-loss bug for the attorneys + claim examiners on the appointment:
- The 3 party emails (AA, DA, CE) on A00002/A00004/A00005 will NOT receive their packet PDF.
- Audit log shows the appointments were approved but the corresponding party-notification was never sent.
- Adrian's directive in the suite says "AttyCE packet emails: exactly 3 per approved appointment, one each to AA, DA, CE". The system silently delivered 0 per appointment for 3 of 4.
- HIPAA implication: the patient/doctor packets DID generate. Loss of the AttyCE packet means the workflow gap is invisible to internal staff but visible to external attorneys who don't get the document they expected.

## Related

- [[OBS-12]] -- reschedule/cancel UI gap. Different flow, same theme (downstream side effects of state transitions).
- [[BUG-024]] / [[BUG-032]] -- pattern of "validation passes but persistence holes". This is a sibling at the persistence layer.
- Phase 6.2 of the hardening suite (HARDENING-TEST-SUITE.md line 604) explicitly suspected this; this finding confirms.
