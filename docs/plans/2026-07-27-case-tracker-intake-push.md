---
feature: Case Tracker intake push (integration Part 1 of 5)
date: 2026-07-27
status: complete
base-branch: main
related-issues: []
---

## Goal

When staff approve an appointment, the portal reliably POSTs a complete intake payload to the Case
Tracker via a durable per-office outbox, with a manual re-push action for recovery.

## Context & decisions

Why now: Case Tracker's receiving endpoints are built and verified against
`docs/integration/case-tracker-api-contract.md`; the portal has no outbound integration at all. This
part delivers the smallest end-to-end slice (an approved appointment reaches Case Tracker). Parts 2-5
(document-update feed, delete + retention, lifecycle re-push, reconcile GET, admin dead-letter screen)
are OUT OF SCOPE here.

Constraints: payload is ePHI; live testing is blocked until Case Tracker deploys, so this ships behind
a per-office toggle defaulting OFF. CI gate requires SonarCloud new_coverage >= 80%.

Resolved decisions:
- Decision: payload builder is a decomposed facade in Domain (`IIntakePayloadBuilder` over focused
  resolvers) because Part 4's reconcile GET reuses it and a single ~10-repo service would breach the
  400-line / 50-line / 7-DI-param thresholds (`PacketTokenResolver`'s 20 ctor params is an outlier, not
  a licence).
- Decision: base URL + token live in appsettings/env (`CaseTracker__BaseUrl`,
  `CaseTracker__IntakeToken`) and the on/off switch is an ABP Setting, because secrets belong in a
  secret store (never the Settings table) while a toggle must be per-office and flippable without a
  redeploy - mirroring SMTP credentials + `NotificationsPolicy.EmailEnabled`.
- Decision: retry reuses the existing flat-backoff mechanism with `MaxAttempts = 3` and
  `RetryBackoffSeconds = 300` (dead-letters ~10 min after first failure) because it lands inside the
  agreed ~20 min window with zero new machinery.
- Decision: a new `Appointments.PushToCaseTracker` permission gates the manual push, because
  re-sending PHI to an external system is a distinct capability from approving an appointment.
- Decision: `Appointment.EvaluationKind` is NOT NULL with the migration defaulting existing rows to the
  Evaluation value, because production has 0 re-evaluations (verified: 0 of 3 appointments carry
  `OriginalAppointmentId`) so the backfill is exact.
- Decision: all of Part 1 ships as one PR, because splitting leaves an outbox with nothing to send.

Two scoping notes recorded so the builder does not write dead code:
- `IntegrationMessageType` ships with only `Intake = 1`. The contract's intake-before-doc-updates
  ORDERING GATE is deliberately NOT implemented here: there are no doc-update messages until Part 2, so
  the gate would be unreachable code. The discriminator column exists so Part 2 adds the gate without a
  migration.
- `EvaluationKind` is stored as an int enum per codebase convention (`AppointmentStatus` precedent), so
  the migration default is the numeric `Evaluation` value; the wire format `"EVAL"` / `"RE_EVAL"` is
  produced by an explicit mapping in the payload builder. This preserves decision 5's intent exactly.

## All needed context

Wire contract (authoritative, do not re-derive field names): `docs/integration/case-tracker-api-contract.md`
sections A (intake payload), B (documents[] entry), I (status matrix + auth).

Pattern to mirror - `Domain/Notifications/Outbox/` is a near-exact template:

| Piece | Anchor |
|---|---|
| Entity + state machine | `NotificationOutboxItem.cs` - `TryClaim:140`, `MarkSent:162`, `MarkFailed:179` |
| Manager | `NotificationOutboxManager.cs` - `EnqueueAsync:43-70`, `ClaimDueBatchAsync:78-112`, `SaveAsync:115` |
| Drain service | `OutboxDrainService.cs` - `DrainDueAsync:48-95`, setting gate `:55`, per-row try/catch that never rethrows `:72-92` |
| Drain job | `OutboxDrainJob.cs:34-47` - `[UnitOfWork]` + `_currentTenant.Change(args.TenantId)`; args `:51-55` |
| Repo interface | `INotificationOutboxRepository.cs:26-30` |
| Repo impl | `EfCoreNotificationOutboxRepository.cs:26-48` - atomic `ExecuteUpdateAsync` lease |
| Consts | `Domain.Shared/Notifications/Outbox/NotificationOutboxConsts.cs` - `:19,:26,:29,:32` |
| Status enum | `Domain.Shared/Notifications/Outbox/NotificationOutboxStatus.cs` |
| Recurring sweep | `Notifications/Jobs/ApprovalReconciliationJob.cs` - `RecurringJobId:53`, `CronExpression:56`, `ForEachOfficeAsync` + per-office try/catch `:69-86` |
| Approval handler | `AppointmentDocuments/Handlers/PacketGenerationOnApprovedHandler.cs:39-102` - `CurrentUnitOfWork.OnCompleted` enqueue |
| Typed HttpClient | `CaseEvaluationDomainModule.cs:97-103` - `AddHttpClient<,>` + config-driven `BaseAddress` + `Timeout` |
| Setting define | `CaseEvaluationSettingDefinitionProvider.cs:76` (`EmailEnabled`, default "true") |
| Manual endpoint | `HttpApi/Controllers/Appointments/AppointmentApprovalController.cs:21-46`; app service `AppointmentsAppService.Approval.cs:63-146` |
| Permissions | `CaseEvaluationPermissions.cs:108-121` (Appointments block); provider `CaseEvaluationPermissionDefinitionProvider.cs:21` (AppointmentsAndRequests) |
| Multi-repo assembler | `AppointmentDocuments/Templates/PacketTokenResolver.cs` - `ResolveAsync:129-144` |

EF + migrations:
- Host: `CaseEvaluationDbContext.cs:94` (DbSet), `:283-307` (config; filtered unique index `:305`, query index `:306`).
- Tenant: `CaseEvaluationTenantDbContext.cs:92` (DbSet), `:199+` (config).
- `CaseEvaluationConsts.DbTablePrefix = "App"`, `DbSchema = null` (`CaseEvaluationConsts.cs:7-8`).
- Two migrations required (`docs/database/MIGRATION-GUIDE.md:68,74`); applied via DbMigrator, never
  `dotnet ef database update` (`:102,:201`).

Payload field sources (all verified): `Appointment.cs:21,26,28,33,40,42,44,48,50,52,146` ·
`Patient.cs:29,32,35,38,42,45,60,62` · `Doctor.cs:19,22` · `Location.cs:23,26,29,32,38-39` ·
`DoctorAvailability.cs:18,20,22` · `AppointmentType.cs:21` ·
`AppointmentDocument.cs:35,39,43,46,48,107,115,202` · `AppointmentPacket.cs:36,43,47,50,53`.
Blob keys built at `GenerateAppointmentPacketJob.cs:157` and `AppointmentDocumentsAppService.cs:333`;
ABP prefixes `tenants/{tenantId:D}/` (verified against ABP v10 `DefaultMinioBlobNameCalculator` and the
live bucket). Bucket `case-evaluation-documents`.

Booking insertion points: `AppointmentsAppService.cs:649-652` (create), `:676-689` (reval), shared
pipeline `CreateAppointmentInternalAsync:720+`, `OriginalAppointmentId` written `:882-884`.
`ITenantWorkRunner.ForEachOfficeAsync(Func<Guid, Task>)` - `MultiTenancy/ITenantWorkRunner.cs:25`.

Gotchas (all verified this session):
1. Dual-context: the entity must be configured in BOTH DbContexts and get TWO migrations, or office DBs
   silently lack the table.
2. The custom EF repo binds `CaseEvaluationDbContext` even for per-tenant rows
   (`EfCoreNotificationOutboxRepository.cs:19-21`) - ABP resolves the office connection at runtime.
3. `IBackgroundJobManager.EnqueueAsync` is NOT UoW-deferred - wrap in `CurrentUnitOfWork.OnCompleted`
   or the Hangfire worker races the approve commit (BUG-036).
4. Hangfire workers have no ambient tenant - `_currentTenant.Change(args.TenantId)` is mandatory.
5. `ForEachOfficeAsync` aborts the whole run if the delegate throws - per-office try/catch required.
6. Complexity limits: file 400, function 50, DI ctor params 7, cyclomatic 10, cognitive 15.

## Tasks (implementation blueprint)

### T1 - Shared enums + consts
- what: CREATE `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Integration/CaseTracker/IntegrationOutboxStatus.cs`
  (`Pending = 1, Sent = 2, Failed = 3`), `IntegrationMessageType.cs` (`Intake = 1`),
  `IntegrationOutboxConsts.cs` (`MaxAttempts = 3`, `RetryBackoffSeconds = 300`,
  `LeaseDurationSeconds = 120`, `DrainBatchSize = 50`, `TargetPathMaxLength = 256`,
  `IdempotencyKeyMaxLength = 128`, `LastErrorMaxLength = 500`); CREATE
  `Domain.Shared/Appointments/EvaluationKind.cs` (`Evaluation = 1, ReEvaluation = 2`).
- pattern: `Domain.Shared/Notifications/Outbox/NotificationOutboxConsts.cs`, `NotificationOutboxStatus.cs`
- approach: code
- acceptance: The system shall expose `IntegrationOutboxConsts.MaxAttempts == 3` and
  `IntegrationOutboxConsts.RetryBackoffSeconds == 300`.

### T2 - Outbox entity with a fatal path
- what: CREATE `Domain/Integration/CaseTracker/IntegrationOutboxItem.cs` -
  `FullAuditedAggregateRoot<Guid>, IMultiTenant`; properties `TenantId`, `MessageType`, `TargetPath`,
  `AppointmentId`, `Payload` (nvarchar(max) JSON), `IdempotencyKey`, `Status`, `AttemptCount`,
  `MaxAttempts`, `NextAttemptAt`, `LockedUntil`, `SentAt`, `LastError`; methods `TryClaim`, `MarkSent`,
  `MarkFailed(nowUtc, error, retryBackoff)`, and NEW `MarkFatal(nowUtc, error)` which goes terminal
  `Failed` immediately regardless of `AttemptCount`.
- pattern: `NotificationOutboxItem.cs:140` (TryClaim), `:162` (MarkSent idempotent), `:179` (MarkFailed)
- approach: tdd
- acceptance: WHEN `MarkFatal` is called on a Pending row, THE SYSTEM SHALL set `Status = Failed` and
  clear `LockedUntil` without incrementing beyond one attempt. WHEN `MarkFailed` is called and
  `AttemptCount` reaches `MaxAttempts`, THE SYSTEM SHALL set `Status = Failed` and `NextAttemptAt = null`.
  WHEN `MarkSent` is called on an already-Sent row, THE SYSTEM SHALL leave `SentAt` unchanged.

### T3 - Push-result classifier (the status matrix)
- what: CREATE `Domain/Integration/CaseTracker/CaseTrackerPushResult.cs` - a pure classifier mapping an
  HTTP status / transport exception to `Success | Retryable | Fatal`: 2xx -> Success; 401, 400, 415 ->
  Fatal; 404 -> Retryable; 5xx, timeout, connection failure -> Retryable.
- pattern: none (new pure function); keep it static + side-effect free so it is unit-testable
- approach: tdd
- acceptance: WHEN classified with 401, 400 or 415, THE SYSTEM SHALL return Fatal. WHEN classified with
  404, 500, 503 or a transport exception, THE SYSTEM SHALL return Retryable. WHEN classified with 200 or
  204, THE SYSTEM SHALL return Success.

### T4 - Object-key builder
- what: CREATE `Domain/Integration/CaseTracker/Payload/ObjectKeyBuilder.cs` - pure static
  `BuildFullyQualifiedKey(Guid? tenantId, string blobName)` returning `tenants/{tenantId:D}/{blobName}`
  when a tenant is in scope, else `host/{blobName}`.
- pattern: prefix semantics verified against ABP v10 `DefaultMinioBlobNameCalculator` and the live bucket
  (`host/` + `tenants/` at root)
- approach: tdd
- acceptance: WHEN given a tenant id and a packet blob name, THE SYSTEM SHALL return the blob name
  prefixed with `tenants/<dashed-guid>/`. WHEN given a null tenant id, THE SYSTEM SHALL prefix with `host/`.

### T5 - Payload DTOs
- what: CREATE `Domain/Integration/CaseTracker/Payload/IntakePayload.cs` plus nested types
  `TenantSection`, `LocationSection`, `AppointmentTypeSection`, `PatientSection`, `DoctorSection`,
  `StorageSection`, `DocumentEntry` - property names matching contract sections A and B exactly
  (`appointmentId`, `confirmationNumber`, `status`, `approvedAtUtc`, `submittedAtUtc`, `updatedAt`,
  `evaluationKind`, `previousAppointmentId`, `previousConfirmationNumber`, `tenant`, `location`,
  `appointmentType`, `panelNumber`, `appointmentDateLocal`, `appointmentTimeLocal`, `timeZone`,
  `durationMinutes`, `patient` incl. `dateOfBirth`, `doctor`, `storage.bucket`, `documents`).
- pattern: `AppointmentDocuments/Templates/PacketTokenContext.cs` (a plain DTO of final render values)
- approach: code
- acceptance: The system shall serialize `IntakePayload` to camelCase JSON with ISO-8601 UTC `Z`
  timestamps and enum values as name strings.

### T6 - Payload resolvers + facade
- what: CREATE under `Domain/Integration/CaseTracker/Payload/`: `AppointmentCoreResolver.cs` (appointment
  scalars, schedule from `DoctorAvailability`, derived `durationMinutes = ToTime - FromTime`, constant
  `timeZone = "America/Los_Angeles"`, `evaluationKind` mapped to `"EVAL"`/`"RE_EVAL"`,
  `previousAppointmentId` from `OriginalAppointmentId`, `previousConfirmationNumber` from the source
  appointment); `PartyResolver.cs` (patient incl. `dateOfBirth` as `yyyy-MM-dd`, `phoneNumberType` name,
  and the office's single `Doctor` row); `TenantLocationResolver.cs` (tenant name via `ITenantStore`,
  `facilityId` + location fields from `Appointment.LocationId`); `DocumentListResolver.cs` (union of
  `AppointmentDocument` + `AppointmentPacket`, OMITTING `Pending` documents whose `BlobName` is
  `(pending-upload)` and packets whose `Status != Generated`, packets always `contentType`
  `application/pdf` with null `fileSize`, `objectKey` via T4); `IIntakePayloadBuilder.cs` +
  `IntakePayloadBuilder.cs` composing the four resolvers.
- pattern: `PacketTokenResolver.cs:129-144` for multi-repo assembly; keep each resolver <= 7 ctor params
- approach: tdd
- acceptance: WHEN building the payload for an approved appointment, THE SYSTEM SHALL populate every
  contract section-A field and SHALL exclude any document with no fetchable object. WHEN the appointment
  is a re-evaluation, THE SYSTEM SHALL set `evaluationKind` to `"RE_EVAL"` and populate
  `previousAppointmentId`. WHEN the appointment is a first evaluation, THE SYSTEM SHALL set
  `evaluationKind` to `"EVAL"` and leave both `previous*` fields null. THE SYSTEM SHALL NOT include any
  patient identifier field.

### T7 - Typed Case Tracker HTTP client
- what: CREATE `Domain/Integration/CaseTracker/ICaseTrackerClient.cs` + `CaseTrackerClient.cs` -
  `PostIntakeAsync(string payloadJson, CancellationToken)` posting to `TargetPath` with
  `Content-Type: application/json` and the raw `X-Intake-Token` header, returning a
  `CaseTrackerPushResult` via T3. Register in `CaseEvaluationDomainModule.ConfigureServices` with
  `BaseAddress` from `CaseTracker:BaseUrl` and an explicit `Timeout`.
- pattern: `CaseEvaluationDomainModule.cs:97-103` (packet-renderer typed client)
- approach: test-after
- acceptance: WHEN posting, THE SYSTEM SHALL send the `X-Intake-Token` header and
  `Content-Type: application/json`. THE SYSTEM SHALL NOT log the token or the payload body.

### T8 - Setting + config
- what: MODIFY `Domain/Settings/CaseEvaluationSettings.cs` adding
  `IntegrationPolicy.CaseTrackerPushEnabled`; MODIFY `Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs`
  defining it with `defaultValue: "false"`; MODIFY `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.json`
  and `env.prod.example` + `docker-compose.prod.yml` adding `CaseTracker__BaseUrl` and
  `CaseTracker__IntakeToken` (placeholder `REPLACE_BEFORE_DEPLOY`, never a real token).
- pattern: `CaseEvaluationSettingDefinitionProvider.cs:76`; env style `docker-compose.prod.yml:147-152`
- approach: code
- acceptance: The system shall default `CaseTrackerPushEnabled` to false. WHERE an office overrides the
  setting, THE SYSTEM SHALL honour the office value over the host default.

### T9 - Outbox repository (interface + EF impl)
- what: CREATE `Domain/Integration/CaseTracker/IIntegrationOutboxRepository.cs` with
  `TryLeaseAsync(Guid id, DateTime nowUtc, DateTime leaseUntil, CancellationToken)`; CREATE
  `EntityFrameworkCore/Integration/CaseTracker/EfCoreIntegrationOutboxRepository.cs` binding
  `CaseEvaluationDbContext` and implementing the lease as a single status-gated `ExecuteUpdateAsync`.
- pattern: `INotificationOutboxRepository.cs:26-30`, `EfCoreNotificationOutboxRepository.cs:26-48`
- approach: test-after
- acceptance: WHEN two callers race `TryLeaseAsync` for the same row, THE SYSTEM SHALL return true to
  exactly one and false to the other without throwing.

### T10 - Outbox manager
- what: CREATE `Domain/Integration/CaseTracker/IntegrationOutboxManager.cs` - `EnqueueAsync` (idempotent
  on `IdempotencyKey`, inserts Pending), `ClaimDueBatchAsync(nowUtc, leaseDuration, batchSize)`,
  `SaveAsync`. Idempotency key = SHA-256 of `$"{messageType}|{appointmentId}|{updatedAt:O}"`.
- pattern: `NotificationOutboxManager.cs:43-70`, `:78-112`, `:115`
- approach: tdd
- acceptance: WHEN `EnqueueAsync` is called twice with the same idempotency key in one office, THE SYSTEM
  SHALL insert one row and return the existing row on the second call.

### T11 - Drain service
- what: CREATE `Domain/Integration/CaseTracker/IntegrationOutboxDrainService.cs` - gate on
  `CaseTrackerPushEnabled` (claim nothing when false), claim a batch, POST each via `ICaseTrackerClient`,
  then `MarkSent` on Success, `MarkFailed` on Retryable, `MarkFatal` on Fatal; never rethrow per row.
- pattern: `OutboxDrainService.cs:48-95` (gate `:55`, per-row try/catch `:72-92`)
- approach: tdd
- acceptance: WHILE `CaseTrackerPushEnabled` is false, THE SYSTEM SHALL claim no rows and leave them
  Pending. WHEN a push returns Fatal, THE SYSTEM SHALL mark the row terminally Failed without further
  attempts. WHEN a push returns Retryable, THE SYSTEM SHALL schedule the next attempt 300 seconds later
  until `MaxAttempts` is reached. IF one row throws, THEN THE SYSTEM SHALL still process the remaining
  claimed rows.

### T12 - Drain job + reconciliation sweep
- what: CREATE `Domain/Integration/CaseTracker/Jobs/IntegrationOutboxDrainJob.cs` (+ `IntegrationOutboxDrainArgs`
  with `TenantId`) using `[UnitOfWork]` and `_currentTenant.Change(args.TenantId)`; CREATE
  `Domain/Integration/CaseTracker/Jobs/CaseTrackerReconciliationJob.cs` with
  `RecurringJobId = "case-tracker-reconciliation"`, `CronExpression = "*/15 * * * *"`, iterating
  `ForEachOfficeAsync` with a per-office try/catch and enqueuing a drain per office; MODIFY
  `CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs` to register it.
- pattern: `OutboxDrainJob.cs:34-47`, `ApprovalReconciliationJob.cs:53,56,69-86,99`,
  `CaseEvaluationHttpApiHostModule.cs:1332`
- approach: test-after
- acceptance: WHEN the sweep runs and one office throws, THE SYSTEM SHALL log that office and continue
  with the remaining offices. WHEN the drain job executes, THE SYSTEM SHALL operate inside the office
  tenant scope from its args.

### T13 - Appointment.EvaluationKind + booking wiring
- what: MODIFY `Domain/Appointments/Appointment.cs` adding
  `public virtual EvaluationKind EvaluationKind { get; set; }`; MODIFY
  `Application/Appointments/AppointmentsAppService.cs` in `CreateAppointmentInternalAsync` (beside the
  `OriginalAppointmentId` write at `:882-884`) to set `EvaluationKind = ReEvaluation` when
  `lifecycleFlow == AppointmentLifecycleFlow.Reval`, else `Evaluation`.
- pattern: `AppointmentsAppService.cs:882-884`
- approach: tdd
- acceptance: WHEN an appointment is created via `CreateRevalAsync`, THE SYSTEM SHALL persist
  `EvaluationKind = ReEvaluation`. WHEN created via `CreateAsync` or `ReSubmitAsync`, THE SYSTEM SHALL
  persist `EvaluationKind = Evaluation`.

### T14 - EF configuration in BOTH contexts
- what: MODIFY `EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` and
  `CaseEvaluationTenantDbContext.cs`: add `DbSet<IntegrationOutboxItem> IntegrationOutboxItems`, table
  `AppIntegrationOutboxItems`, all property mappings with max lengths, a filtered unique index on
  `(TenantId, IdempotencyKey)` and a query index on `(TenantId, Status, NextAttemptAt)`; add the
  `Appointment.EvaluationKind` property mapping in both.
- pattern: `CaseEvaluationDbContext.cs:283-307`; tenant mirror `CaseEvaluationTenantDbContext.cs:199+`
- approach: code
- acceptance: The system shall configure `IntegrationOutboxItem` identically in both contexts so the
  table is column-identical across host and office databases.

### T15 - Two migrations
- what: CREATE migrations named `Added_CaseTrackerIntegrationOutbox`:
  `dotnet ef migrations add Added_CaseTrackerIntegrationOutbox -c CaseEvaluationDbContext -o Migrations`
  and `... -c CaseEvaluationTenantDbContext -o TenantMigrations`, run from
  `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore`. The `EvaluationKind` column must be NOT
  NULL with `defaultValue: 1` so existing rows backfill to Evaluation.
- pattern: `docs/database/MIGRATION-GUIDE.md:68,74`
- approach: code
- acceptance: WHEN DbMigrator runs against an office database, THE SYSTEM SHALL create
  `AppIntegrationOutboxItems` and add a non-null `EvaluationKind` column defaulted to 1 on
  `AppAppointments`.

### T16 - Intake trigger on approval
- DEVIATION (found during build, 2026-07-27): the plan placed this handler in Domain, which is
  IMPOSSIBLE -- `AppointmentApprovedEto` is declared in Application.Contracts
  (`Application.Contracts/Appointments/Events/AppointmentApprovedEto.cs`, namespace
  `HealthcareSupport.CaseEvaluation.Appointments.Events`) and Domain cannot reference upward. The
  handler now lives at `Application/Integration/CaseTracker/Handlers/CaseTrackerIntakeOnApprovedHandler.cs`,
  matching `PackageDocumentQueueHandler`, which subscribes to a sibling Application.Contracts event
  for exactly the same reason. The reusable work was extracted to
  `Domain/Integration/CaseTracker/CaseTrackerIntakeQueue.cs` so the manual push (T17) shares one code
  path with the automatic trigger.
- what: CREATE `Application/Integration/CaseTracker/Handlers/CaseTrackerIntakeOnApprovedHandler.cs` -
  `ILocalEventHandler<AppointmentApprovedEto>`; delegates to `CaseTrackerIntakeQueue`, which builds the
  payload, enqueues an outbox row, and enqueues the drain job inside `CurrentUnitOfWork.OnCompleted`.
- pattern: `PacketGenerationOnApprovedHandler.cs:39-102` (OnCompleted enqueue + `ObjectDisposedException`
  guard)
- approach: test-after
- acceptance: WHEN an appointment is approved, THE SYSTEM SHALL write exactly one Pending outbox row in
  the same transaction as the approval and SHALL enqueue the drain only after that transaction commits.
  WHEN the same approval event is redelivered, THE SYSTEM SHALL NOT create a second outbox row.

### T17 - Manual push endpoint + permission
- what: MODIFY `Application.Contracts/Permissions/CaseEvaluationPermissions.cs` adding
  `PushToCaseTracker = Default + ".PushToCaseTracker"` to the `Appointments` block (`:108-121`); MODIFY
  `CaseEvaluationPermissionDefinitionProvider.cs` defining it under `AppointmentsAndRequests`; CREATE
  `Application.Contracts/Integration/CaseTracker/ICaseTrackerPushAppService.cs` +
  `Application/Integration/CaseTracker/CaseTrackerPushAppService.cs` with
  `PushAppointmentAsync(Guid appointmentId)`; CREATE
  `HttpApi/Controllers/Integration/CaseTrackerPushController.cs` exposing
  `POST api/app/case-tracker/appointments/{id}/push`.
- pattern: `AppointmentApprovalController.cs:21-46`; app service `AppointmentsAppService.Approval.cs:63-146`
- approach: test-after
- acceptance: WHEN a caller without `Appointments.PushToCaseTracker` invokes the endpoint, THE SYSTEM
  SHALL return 403. WHEN an authorised caller invokes it for an approved appointment, THE SYSTEM SHALL
  enqueue an outbox row and return 200.

### T18 - Tests
- what: CREATE `test/HealthcareSupport.CaseEvaluation.Domain.Tests/Integration/CaseTracker/`:
  `IntegrationOutboxItemTests.cs`, `IntegrationOutboxManagerTests.cs`, `CaseTrackerPushResultTests.cs`,
  `ObjectKeyBuilderTests.cs`, `IntakePayloadBuilderTests.cs`, `IntegrationOutboxDrainServiceTests.cs`;
  CREATE `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/Integration/EfCoreIntegrationOutboxRepositoryTests.cs`;
  CREATE `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/EvaluationKindOnBookingTests.cs`.
  All fixtures use SYNTHETIC patient data only.
- pattern: `Domain.Tests/Notifications/Outbox/*Tests.cs`, `EfCoreNotificationOutboxRepositoryTests.cs`
- approach: tdd
- acceptance: The system shall cover the outbox state machine (incl. `MarkFatal`), the status matrix, the
  object-key builder, payload construction for both evaluation kinds, drain behaviour with the setting
  off, the atomic lease, and `EvaluationKind` on both booking paths. Coverage on new code shall be >= 80%.

### T19 - Commit the contract docs
- what: `git add docs/integration/` - the three currently-untracked contract documents
  (`case-tracker-api-contract.md`, `case-tracker-document-sharing.md`, `case-tracker-intake-handoff.md`)
  plus this plan.
- pattern: n/a
- approach: code
- acceptance: The system shall include the integration contract documents in this PR.

## Validation loop

Run from the repo root (`C:\src\patient-portal\main`), in order:

```bash
dotnet format HealthcareSupport.CaseEvaluation.slnx --verify-no-changes
```
```bash
dotnet build HealthcareSupport.CaseEvaluation.slnx -c Release -warnaserror
```
```bash
dotnet test HealthcareSupport.CaseEvaluation.slnx
```

Migration sanity (does not touch a live DB):
```bash
dotnet ef migrations list -c CaseEvaluationDbContext -o Migrations
```
```bash
dotnet ef migrations list -c CaseEvaluationTenantDbContext -o TenantMigrations
```

Done-bar: all three primary commands green, both migration lists show
`Added_CaseTrackerIntegrationOutbox`, and no test uses real patient data.

## Risk / rollback

Blast radius: additive. One new table, one new non-null column with a default, one new local event
handler, one new recurring Hangfire job, one new endpoint. The push itself cannot fire because
`CaseTrackerPushEnabled` defaults to false, so merging this cannot send PHI anywhere.

Specific risks:
- Forgetting the tenant migration would leave office DBs without the table - caught by the migration-list
  check and by running DbMigrator against the office DB before enabling.
- The new event handler runs inside the approval UoW; a bug there could fail approvals. Mitigated by
  enqueue-only work (no HTTP in the handler) and the `OnCompleted` deferral.
- Rollback: revert the PR and run DbMigrator; the dropped column/table carry no other dependencies.
  Because the feature ships disabled, rollback needs no coordination with Case Tracker.

## Pre-enablement checklist (ops, NOT merge-blocking)

These are environment/data actions, deliberately separated from the task list so they do not sit in this
PR's done-bar. They are required before `CaseTrackerPushEnabled` is switched on, not before merge. Each
touches live infrastructure, so each needs Adrian's explicit go before execution.

- **O1 - Populate `Location.FacilityId` on both production clinics.** Verified 2026-07-27: the column
  exists in the deployed office DB (`AppLocations.FacilityId`, shipped in #379) but is EMPTY on 2 of 2
  live clinics, and it is Case Tracker's facility routing key. Pure data task - `LocationManager` +
  `[Required]` DTOs already enforce a non-empty unique value on the create/edit path, so these are legacy
  rows; fill them via the locations screen. Acceptance: WHEN the intake payload is built for either
  production clinic, THE SYSTEM SHALL emit a non-empty `tenant.facilityId`.
- **O2 - Create the `case-tracker-documents` bucket and mint the scoped MinIO key.** On the portal MinIO
  (container `hcs-patient-portal-minio-1`, docker network `hcs-patient-portal_default`, bucket root
  `case-evaluation-documents`): create the bucket, then create a non-root user with a policy granting
  read-only on `case-evaluation-documents` and read/write/delete on `case-tracker-documents`. Verified the
  path is available: `mc admin` reaches the instance and the built-in `readonly`/`readwrite` policies
  exist, with only the root user present today. Deliver the key out of band (never email/chat/repo).
  Acceptance: WHEN Case Tracker authenticates with the scoped key, THE SYSTEM SHALL permit reads of
  `case-evaluation-documents` and reads/writes/deletes of `case-tracker-documents`, and SHALL deny writes
  to `case-evaluation-documents`.

DEFERRED (explicitly out of scope for this plan, no action here): the internal DNS name + TLS certificate
for the MinIO endpoint. A certificate cannot be issued for a bare IP, so `https://192.168.101.37:9000` is
not viable, and Case Tracker hit the same constraint for their own HTTPS base. This needs a concrete
options analysis (internal CA vs self-signed plus a trust-store entry on the Case Tracker box vs an
internal DNS record with a proper cert) and an IT conversation before it can become a task. It blocks
enabling document sharing (Part 2 onward), not this PR.
