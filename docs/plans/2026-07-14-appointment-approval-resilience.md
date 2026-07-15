---
feature: appointment-approval-resilience
date: 2026-07-14
status: in-progress
base-branch: main
related-issues: []
---

## Goal

Make appointment-approval packet generation and notification emails survive concurrent load and
container crashes -- jobs wait and finish once services return -- with NO lost packets or emails and
NO duplicate PHI-bearing emails.

## Context

Approving an appointment offloads two kinds of heavy work to SQL-backed Hangfire jobs: PDF packet
generation (via the WeasyPrint `packet-renderer` sidecar) and notification emails (MailKit). This is
the right shape, and the durable pieces already work (`restart: unless-stopped` + Hangfire
`SlidingInvisibilityTimeout=5min` re-run in-flight jobs after a worker dies). But a 5-agent adversarial
verification pass (2026-07-14, all citations file:line) proved the pipeline is currently **neither
no-loss nor no-duplicate**, and one gap is HIPAA-adjacent:

- **Duplicate PHI emails on any retry/crash.** `EnsureGeneratingAsync` flips an already-Generated kind
  back to Generating with no skip guard, and a render timeout on one packet kind re-runs the other
  kinds and **re-fires their emails** (AppointmentPacketManager.cs:45, GenerateAppointmentPacketJob.cs:143).
  No email dedup key exists anywhere. A reconciliation sweep would *amplify* this -- so idempotency must
  land before the sweep.
- **Silent email loss.** `SendAppointmentEmailJob.SendPlainAsync` catches + swallows SMTP failures
  (job marked Succeeded, never retried/dead-lettered) -- contradicting its own docstring
  (SendAppointmentEmailJob.cs:171). Every plain notification is lost on a transient SMTP blip.
- **Emails are not reconcilable by a packet sweep.** BUG-033 dropped ~21 emails while packet rows were
  Generated; a packet-completeness sweep sees "3 Generated" and does nothing. Emails need their own
  durable per-recipient delivery ledger (a hand-rolled outbox; ABP's built-in outbox is distributed-
  event infra and tracks "handed to bus", not "SMTP delivered").
- **Enqueue window.** The packet job is enqueued in `OnCompleted` (after the approval commits, not
  atomically); a crash -- or even a brief DB blip -- in that window loses the job with nothing to
  re-drive it (PacketGenerationOnApprovedHandler.cs:65-94).
- **Topology (confirmed):** Hangfire tables live in the host DB; appointments + packets live in
  per-office DBs; host jobs reach an office via `TenantId` + `ICurrentTenant.Change`. The outbox must
  live in the per-office DB to be atomic with the approval. The office-enumeration pattern
  (`ITenantWorkRunner.ForEachOfficeAsync`) is proven but aborts the whole run if one office throws.
- **Config (confirmed):** the prod compose sets zero memory limits on all 9 services; SQL defaults to
  ~80% of the 16 GB box. Hangfire runs 20 workers (default) against a 2-worker renderer.

Deployment is currently blocked on IT (wildcard DNS + TLS cert), so there is time to do this properly.

## Approach

**Chosen: phased.** Phase 1 stops the bleeding (duplicate PHI sends + silent loss) and stabilizes the
box; Phase 2 adds the durable no-loss guarantee (outbox + reconciliation). Sequencing is deliberate:
idempotency (T1/T2) MUST precede the reconciliation sweep (T11), because the sweep re-enqueues work and
would multiply duplicate emails against a non-idempotent pipeline.

**Rejected -- one big-bang epic:** larger blast radius merged at once on PHI paths; the duplicate-email
and silent-loss fixes are independently valuable and should not wait behind the outbox.

**Rejected -- config-only now, resilience later:** leaves the HIPAA-adjacent duplicate-email gap and the
silent email-loss gap open on a system about to go live.

**Rejected -- transactional Hangfire enqueue (instead of an outbox):** impractical on this stack (Linux,
no MSDTC; ABP's EF UoW is not a `TransactionScope`; Hangfire storage is pinned to the host DB while
appointments live per-office). A per-office outbox row written in the approval UoW is the correct atomic
mechanism (verified).

**Rejected -- reuse ABP's built-in event outbox:** it is distributed-integration-event infrastructure;
its "sent" means "handed to the bus", never "SMTP delivered". Hand-roll is correct (verified).

## Tasks

### Phase 1 -- stop duplicates + silent loss + stabilize

- T1: Idempotent packet generation
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentPacketManager.cs, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs]
  - acceptance: re-running the job for an appointment whose kinds are all Generated performs zero renders
    and publishes zero `PacketGeneratedEto`; `EnsureGeneratingAsync` is an atomic guard (skip-if-Generated,
    no Generated->Generating reset) called INSIDE the per-kind try/catch so a concurrency collision marks
    that kind Failed without rolling back / retry-storming the whole job. Unit tests prove skip + no double
    publish + collision containment.

- T2: Per-kind targeting in `GenerateAppointmentPacketArgs`
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentDocuments/GenerateAppointmentPacketArgs.cs, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs]
  - acceptance: enqueuing args with `Kind = AttorneyClaimExaminer` renders only that kind and publishes
    only its Eto; `Kind = null` preserves the all-three behavior. Lets Regenerate + the Phase 2 sweep be
    surgical (fix one kind without re-emailing the other two).

- T3: Stable email idempotency key on dispatch args
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/SendAppointmentEmailArgs.cs, src/HealthcareSupport.CaseEvaluation.Application/Notifications/NotificationDispatcher.cs]
  - acceptance: every dispatched email carries a deterministic idempotency key (pure function of
    appointment + context + kind + recipient); identical logical emails produce identical keys. Key is
    carried + logged now; ENFORCEMENT lands with the Phase 2 outbox (T10). Pure-function derivation is
    unit-tested.

- T4: Fix the swallowed SMTP failure
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs]
  - acceptance: `SendPlainAsync` propagates `IEmailSender` failures (like the attachment path) so Hangfire
    retries and dead-letters instead of marking Succeeded; the stale class docstring is corrected to match.
    Test asserts the exception propagates on a failing sender.

- T5: Surface per-kind packet failures (depends: T1)
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/Jobs/GenerateAppointmentPacketJob.cs]
  - acceptance: if any kind ends Failed after the loop, the job surfaces it (throws so Hangfire retries /
    dead-letters) instead of reporting Succeeded; because T1 makes re-runs skip already-Generated kinds,
    the retry re-renders only the Failed kind. A permanently broken kind becomes visible on /hangfire.

- T6: Container memory caps + renderer timeout alignment
  - approach: code
  - files-touched: [docker-compose.prod.yml, docker/packet-renderer/Dockerfile, src/HealthcareSupport.CaseEvaluation.Domain/CaseEvaluationDomainModule.cs]
  - acceptance: `MSSQL_MEMORY_LIMIT_MB` (~7168) + per-service `mem_limit` set (SQL ~9-10G, api/authserver/
    renderer bounded); `docker inspect` shows non-zero `HostConfig.Memory`. The .NET render client timeout
    and gunicorn `--timeout` are aligned (client >= server) so the client no longer aborts a render the
    sidecar is still doing.

- T7: Pin Hangfire `WorkerCount`
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs]
  - acceptance: `Configure<AbpHangfireOptions>(o => o.ServerOptions = new BackgroundJobServerOptions { WorkerCount = N })`
    with N ~5-8; the `/hangfire` dashboard shows N workers (not 20). AuthServer stays
    `IsJobExecutionEnabled=false` (only the api runs the processing server).

### Phase 2 -- durable no-loss guarantee

- T8: Notification outbox entity + state machine
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Outbox/NotificationOutboxItem.cs, src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Outbox/NotificationOutboxManager.cs]
  - acceptance: an `IMultiTenant` outbox entity (To/Cc/Subject/Body/Context/PacketRef/IdempotencyKey +
    Status[Pending/Sent/Failed] + AttemptCount/MaxAttempts + NextAttemptAt + LockedUntil + concurrency
    stamp). State methods: `Claim` (lease via conditional update, respects LockedUntil/NextAttemptAt),
    `MarkSent` (idempotent -- no-op if already Sent), `MarkFailed` (increments attempt; terminal Failed at
    MaxAttempts). Unit tests cover the lease race, idempotent mark-sent, and attempt cap.

- T9: Outbox EF mapping + migrations (per-office)
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentPacket.cs, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/*]
  - acceptance: outbox mapped in BOTH DbContexts (dual-context per the AppNotification recipe,
    CaseEvaluationTenantDbContext.cs:89,171-188); `AppointmentPacket.LastAttemptAt` column added for
    staleness; generated EF migrations apply to every office DB via `CaseEvaluationDbMigrationService`
    (per-tenant loop). Migrations are additive.

- T10: Route email dispatch through the outbox (depends: T3, T8, T9)
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/Notifications/NotificationDispatcher.cs, src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/*, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Jobs/SendAppointmentEmailJob.cs]
  - acceptance: the `[UnitOfWork]` email handlers write Pending outbox rows ATOMICALLY in the approval UoW
    (single ledger -- remove the direct Hangfire enqueue); an `OutboxDrainJob` claims due Pending rows,
    sends via `IEmailSender`, marks Sent only on SMTP success, reschedules on failure, marks Failed at cap.
    The T3 idempotency key makes redelivery effectively-once. Tests: crash-before-mark leaves the row
    Pending (redriven, not lost); a duplicate drain of a Sent row does not re-send.

- T11: Host reconciliation recurring job (depends: T2, T8, T9, T10)
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Notifications/Jobs/ApprovalReconciliationJob.cs, src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs]
  - acceptance: a host recurring job iterates offices via `ITenantWorkRunner.ForEachOfficeAsync` WITH a
    per-office try/catch (one failing office does not abort the run); per office it (i) re-enqueues
    per-kind packet generation for Approved appointments whose packets are missing/Failed/stale-Generating
    (staleness thresholded on `LastAttemptAt` >> render time), and (ii) triggers the outbox drain for
    due/Pending rows (threshold > Hangfire's retry window to avoid double-send). Registered in
    `ConfigureHangfireRecurringJobs`. Tests cover the incomplete-detection predicate + per-office isolation.

## Risk / Rollback

- Blast radius: **all notification email** (approval, booking, reminders, digests all share
  `NotificationDispatcher` / `SendAppointmentEmailJob`), packet generation, EF schema in every office DB,
  and the prod compose. These are PHI-bearing paths -- correctness matters more than speed.
- Rollback: Phase 1 tasks are independent, individually revertible commits; T6/T7 are config-only.
  Phase 2 migrations are additive (safe to roll forward); reverting the T10 dispatch rewire is the
  highest-risk step -- gate it behind full tests + a staging soak before prod. Never `docker compose
  down -v` (PHI volumes).
- Sequencing risk: shipping T11 (sweep) before T1/T2 would multiply duplicate PHI emails -- enforce the
  phase order.

## Verification

Run after each phase; full end-to-end once deployed to a real stack:

1. Concurrency + crash: fire N concurrent approvals; kill the api container mid-flight; on restart, confirm
   every packet generates exactly once (no orphan blobs, no duplicate rows) and each recipient receives
   exactly one email.
2. SMTP outage: block the relay; confirm outbox rows go Pending (not lost), the job dead-letters visibly on
   the attachment path, then restore the relay and confirm the drain sends each once (no duplicates).
3. Enqueue-window: kill the api between approval-commit and enqueue; confirm the reconciliation job later
   finds the Approved-with-no-packet appointment and completes it.
4. Broken kind: point one packet template at a failing render; confirm that kind dead-letters visibly on
   `/hangfire` while the other kinds succeed and email once.
5. Memory: under an approval burst, `docker stats` shows SQL bounded near its cap and no OOM-kills;
   `/hangfire` shows the pinned worker count.
