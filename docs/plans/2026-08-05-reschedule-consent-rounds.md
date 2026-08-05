---
feature: Reschedule consent rounds (epic phase 4c)
date: 2026-08-05
status: done
base-branch: main
related-issues: []
---

# Phase 4c -- reschedule consent rounds

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Follows phase 4b (`docs/plans/2026-08-04-staff-picks-reschedule-date.md`), which stopped issuing
reschedule consent at submit. **4b and 4c deploy to the server TOGETHER.**

## Goal

Staff confirm a chosen reschedule date with an explicit button, which issues one consent round to
both sides; the appointment moves only after both sides agree and staff finalize; every date ever
proposed -- with who declined it -- is queryable; and throughout, the appointment reads as
"Reschedule Requested" rather than the misleading "Rescheduled".

## Context & decisions

Completes source item R2 ("staff pick the new date, not the requestor, and BOTH sides then
consent"). 4b delivered the staff pick; 4c delivers the consent half.

Why now: 4b deliberately left reschedule consent unissued, so today a supervisor can finalize a
reschedule with no consent recorded at all (`AreAllRequiredSidesGranted()` returns true when both
sides are `NotRequired`). That gap is only acceptable because 4c follows immediately and ships in
the same release.

### Resolved decisions

- Decision: the staff flow is PICK -> CONFIRM -> FINALIZE, three distinct steps, because Adrian:
  "what if the staff selects a date and then changes it immediately, in that case 2 different
  emails will go out. I want a button where the staff can click to confirm the date so we know
  that the staff meant select this date". Picking a date has NO server effect; the Confirm button
  is what commits it and sends.
- Decision: consent emails send ONLY on Confirm -- never automatically on date selection --
  for the same reason. There is therefore no state where tokens exist but nothing was sent.
- Decision: staff finalize explicitly (not auto-finalize on the last consent), because the
  billing outcome is chosen at finalize and staff keep the last look.
- Decision: each confirmed date is a NEW child entity row (`ChangeRequestConsentRound`) rather
  than resetting columns, because Adrian wants "the full audit trail of every date proposed and
  who declined it" -- that has to be queryable, not reconstructed from a log.
- Decision: the round entity DISSOLVES the blocker the roadmap recorded. `IssueSideConsent` is
  only valid from `NotRequired` (`AppointmentChangeRequest.cs:192-199`); a fresh round row starts
  at `NotRequired`, so no reset hack is needed.
- Decision: RESCHEDULE ONLY. Cancellation keeps its existing at-submit consent on the parent's
  flat columns, because a cancellation has no proposed date to re-propose and nobody asked to
  change a working flow.
- Decision (Adrian, 2026-08-05, post-build review): a RESCHEDULE submit sends NO stakeholder
  email. "Once the staff selects a date, both parties get consent emails that include and tell
  them that reschedule was requested, this extra email is not required." Implemented as an early
  return in `ChangeRequestSubmittedEmailHandler`, scoped to the EMAIL only -- staff still get
  their in-app notification and the cancel-side clinical-staff email is untouched.
  Found while implementing: that email was a THIRD stale reader of the proposed slot. It
  rendered `NewAppointmentDate` / `NewAppointmentFromTime` from `NewDoctorAvailabilityId`, which
  4b leaves null on the external path, so it was already going out with BLANK date and time --
  the only one of the three stale readers reaching real inboxes. Cancellation keeps its email:
  its consent is issued at submit, so there is no later message to fold the notice into.
  Consequence to accept: a party who is neither side's representative now hears nothing until
  the approval email at finalize.
- Decision: confirming again WITHOUT changing the date RESENDS within the current round
  (`SendAttempts + 1`, same `RoundNumber`); changing the date creates a new round.
- Decision (Adrian, 2026-08-05, build time -- SUPERSEDES "same tokens"): a resend MINTS A FRESH
  TOKEN for each still-`Pending` side and replaces that side's stored hash and expiry. The
  original decision said the resend must reuse the same tokens so a link a party already holds
  stays valid. That is not implementable: only the SHA256 HASH is persisted
  (`ChangeRequestConsentManager.cs:116`) and the raw token is returned exactly once at issuance,
  so a resend cannot rebuild the URL. Rejected alternative: store the token reversibly
  (encrypted) -- it would keep every old link alive but leaves a decryptable consent credential
  at rest in a PHI-adjacent system. Consequence to accept: clicking the link from a SUPERSEDED
  send returns the invalid-token page; the recipient must use the newest email. Sides already
  Approved / Rejected / Expired are NOT re-tokened -- only still-`Pending` sides are resent.
- Decision: the appointment status needs no new value -- it stays
  `AppointmentStatusType.RescheduleRequested` (12) while a round awaits consent, and
  `RequestStatus` stays `Pending` until finalize.
- Decision: one phase, including the expiry sweep job (Adrian, 2026-08-05), rather than splitting.
- Decision (Adrian, 2026-08-05): an in-flight request must READ as in-flight. Today
  `appointmentStatusToPill` buckets `RescheduleRequested` into the `Rescheduled` pill
  (`appointment-status.util.ts:38-41`), so the appointment shows "Rescheduled" and the external
  banner claims "This appointment has been rescheduled -- the new date and time are shown above"
  while nothing has moved. Adrian: "that is misleading". 4c adds distinct
  `RescheduleRequested` + `CancellationRequested` pills. This is a PRE-EXISTING bug on `main`, not
  introduced by 4c -- it is folded in because 4c lengthens the in-flight window considerably.
- Decision: fix BOTH sides, because `CancellationRequested` -> `Cancelled` is the identical defect
  ("This appointment was cancelled" on a request that is merely pending) and leaving one half
  wrong would be incoherent.
- Decision: the new pills filter under the EXISTING chips (`RescheduleRequested` -> `rescheduled`,
  `CancellationRequested` -> `cancelled`), so no seventh chip appears and chip counts do not move.
  The pill TEXT is what becomes honest.
- Decision: an appointment with a request in flight offers NO detail-page Reschedule/Cancel
  actions. This matches what cancel-in-flight already does (`CancellationRequested` maps to
  `Cancelled` -> `detailActions` default `[]`) and prevents stacking a second reschedule on one
  awaiting consent. NOTE: this REMOVES two buttons that currently render on a reschedule-requested
  appointment -- an intentional behaviour change, not a regression.

## All needed context

### The three-step flow this replaces

Post-4b, `ApproveRescheduleAsync` picks the date AND moves the appointment in one call
(`AppointmentChangeRequestsAppService.Approval.cs:218` resolve, `:246-256` move). 4c splits that:
confirm creates the round and sends; approve becomes finalize and reads the slot from the round.

### Two stale readers of the proposed slot -- MUST fix (they are consent-facing)

4b moved the staff slot to `AdminOverrideSlotId`, leaving `NewDoctorAvailabilityId` null on the
external path. One reader was fixed in 4b; these two were not, and both go live the moment 4c
re-enables consent:

| Reader        | Anchor                                             | Symptom                                                                                                        |
| ------------- | -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Consent email | `ChangeRequestConsentRequestEmailHandler.cs:83-85` | `BuildDetailsBlock` omits the date line                                                                        |
| Consent page  | `PublicChangeRequestConsentAppService.cs:76-78`    | `RequestedNewDateTime` null; template hides it via `*ngIf` (`public-change-request-consent.component.html:18`) |

Both must read the CURRENT ROUND's `ProposedDoctorAvailabilityId`. Net effect today would be
asking a party to approve a reschedule with no date shown anywhere.

### The outbox silently swallows duplicate sends -- THE blocker for rounds AND resend

- Key = `SHA256(tenantId | recipientEmail | contextTag | packetKind)`
  (`SendAppointmentEmailArgs.cs:111-121`).
- `NotificationOutboxManager.EnqueueAsync` returns the EXISTING row on a key match --
  no throw, no log (`:59-64`), backed by a unique index `(TenantId, IdempotencyKey)` filtered on
  `IsDeleted = 0` (`CaseEvaluationDbContext.cs:309`).
- Today's consent contextTag is `ChangeRequestConsent/{changeRequestId}`
  (`ChangeRequestConsentRequestEmailHandler.cs:117`) -- no round, no attempt.

So round 2's email to the same recipient, and every resend, would be silently dropped. The
contextTag MUST become round- and attempt-specific.

### No consent-expiry job exists

Expiry is evaluated LAZILY, only inside `RecordDecisionAsync` when a party clicks
(`ChangeRequestConsentManager.cs:90-95`). A token nobody clicks stays `Pending` past its 7-day TTL
(`AppointmentChangeRequestConsts.ConsentDefaultTtlDays`) forever and blocks finalize with no staff
signal.

Pattern to mirror: `CancellationRescheduleReminderJob`
(`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Notifications/Jobs/CancellationRescheduleReminderJob.cs`)
-- `ITransientDependency`, `public const string RecurringJobId` + `CronExpression`, `[UnitOfWork]`,
`_tenantWorkRunner.ForEachOfficeAsync(...)` for db-per-office iteration. Registered via
`Hangfire.RecurringJob.AddOrUpdate<T>(T.RecurringJobId, x => x.ExecuteAsync(), T.CronExpression)`
at `CaseEvaluationHttpApiHostModule.cs:1349-1352`.

### Integration harness EXISTS (corrects earlier research)

The roadmap (line 261) says 4c needs a harness built. It does not.
`test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/` provides
`CaseEvaluationMultiOfficeTestBase`, `MultiOfficeTestDatabase`, and `MultiOfficeSeeder` (seeds
appointment type, location, doctor availability AND an appointment per office --
`SeededOffice` record). `MultiOfficeAppointmentsAppServiceTests:39-43` resolves real app services
via `GetRequiredService<...>()` and uses `ICurrentPrincipalAccessor` to act as a role. Change-request
consent tests belong here. No change-request test source exists yet.

### Patterns to mirror

- Child entity: `AppointmentChangeRequestDocument` (`AppointmentChangeRequestDocument.cs:16-65`)
  -- sibling `FullAuditedAggregateRoot<Guid>` + `IMultiTenant` + `[Audited]` with an
  `AppointmentChangeRequestId` FK and a `protected` parameterless ctor.
- Consent state machine + token crypto: the existing region on
  `AppointmentChangeRequest.cs:168-289` and `ChangeRequestConsentManager.cs:106-127`
  (`GenerateRawToken` / `ComputeTokenHash`) -- MOVE the per-side logic to the round, keep the
  crypto in the manager.
- Both-sides resolution already exists: `ChangeRequestSideResolver.ResolveBothSidesAsync`
  (`:78-92`).
- Controller: `AppointmentChangeRequestApprovalController` (`[Area("app")]`,
  `[ControllerName]`, `[Route("api/app/appointment-change-request-approvals")]`, one
  `[HttpPost] [Route("{id}/...")]` per method). ABP `[RemoteService(IsEnabled = false)]` +
  manual controller is REQUIRED here per `AppointmentChangeRequests/CLAUDE.md`.
- Permissions: `CaseEvaluationPermissions.AppointmentChangeRequests.Approve` (`:296`).

### Gotchas

- `AppointmentChangeRequest` is mapped in BOTH DbContexts (`CaseEvaluationDbContext.cs:688`,
  `CaseEvaluationTenantDbContext.cs:624`), so the new table needs a migration in BOTH sets:
  `dotnet ef migrations add <Name> -c CaseEvaluationDbContext -o Migrations` AND
  `-c CaseEvaluationTenantDbContext -o TenantMigrations` (`docs/database/MIGRATION-GUIDE.md:68-74`).
- `ChangeRequestConsentManager.ResolveByRawTokenAsync` queries the REQUEST's two hash columns
  (`:62-63`); with rounds this must query the round table. Both hash columns are indexed today
  (`CaseEvaluationDbContext.cs:719+`) -- keep indexes on the round's two hash columns.
- `ConsentGatingEnabled` is a compile-time `const bool = true`
  (`AppointmentChangeRequestConsts.cs:35`). Leave it; the gate still honours it.
- The finalize path must keep setting `changeRequest.AdminOverrideSlotId` (4b's approval-email
  reader depends on it -- `ChangeRequestApprovedEmailHandler.cs:144-146` via
  `ResolveScheduledSlotId`). Copy the round's slot onto it at finalize.
- Do NOT bind a getter that CONSTRUCTS an object to an ABP signal input -- it loops change
  detection and hangs the tab silently in a production build (4b, ~2h lost). Frozen constants.
- `ng lint` is broken locally; use `npx eslint`. Run `npx prettier --check` before committing.
- The full backend suite can OOM the stack (`main-sql-server-1` exit 137). Check `docker ps`
  before a live gate; `docker compose up -d` restores it.

## Tasks

### T1 -- consent round entity

- what: CREATE `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentChangeRequests/ChangeRequestConsentRound.cs`
  -- `ChangeRequestConsentRound : FullAuditedAggregateRoot<Guid>, IMultiTenant`, `[Audited]`.
  Fields: `TenantId`, `AppointmentChangeRequestId`, `RoundNumber` (int), `ProposedDoctorAvailabilityId`
  (Guid), `ProposedByUserId` (Guid?), `ProposedReason` (string?), `SendAttempts` (int, starts 1),
  `SupersededAt` (DateTime?),
  and per side `SideAConsentStatus/TokenHash/ExpiresAt/RespondedAt/RespondedByEmail` (+ SideB).
  Methods ported from the parent's consent region: `IssueSideConsent`, `RecordSideDecision`,
  `MarkSideExpired`, `IsSideExpired`, `SideConsentStatus`, `SideConsentTokenHash`,
  `AreAllRequiredSidesGranted`, plus `ReissueSideConsent()`, `RegisterResend()` (increments
  `SendAttempts`) and `Supersede(DateTime nowUtc)`.
- **CORRECTION (2026-08-05, build time).** Two fields this task's original list omitted, both
  required by T8: `ProposedReason` (T8 says finalize copies "the round's reason" onto
  `AdminReScheduleReason`, but no reason field was defined -- and holding it per round is what
  keeps "we proposed Aug 27 because X, they declined" queryable, which is the stated point of
  rounds), and `ReissueSideConsent(side, tokenHash, expiresAt)`, valid only from `Pending`, which
  the corrected resend decision above needs. `IssueSideConsent` stays restricted to
  `NotRequired`, so re-soliciting a DECIDED side still requires a new round.
- pattern: `AppointmentChangeRequestDocument.cs:16-65` for the shape;
  `AppointmentChangeRequest.cs:168-289` for the per-side transitions.
- approach: tdd
- acceptance (EARS): WHEN a round is constructed, THE SYSTEM SHALL set `RoundNumber` >= 1,
  `SendAttempts` = 1, both sides `NotRequired`, and `SupersededAt` null. WHEN `RecordSideDecision`
  is called on a side that is not `Pending`, THE SYSTEM SHALL throw
  `ChangeRequestConsentAlreadyResponded`. WHEN both required sides are `Approved`,
  `AreAllRequiredSidesGranted` SHALL return true. WHEN `RegisterResend` is called, THE SYSTEM
  SHALL increment `SendAttempts` and SHALL NOT change either side's token hash or status.

### T2 -- round repository contract + EF implementation

- what: CREATE `.../Domain/AppointmentChangeRequests/IChangeRequestConsentRoundRepository.cs` with
  `FindByTokenHashAsync(string tokenHash)` and `GetCurrentAsync(Guid changeRequestId)` (highest
  `RoundNumber` with `SupersededAt == null`); CREATE the EF implementation in
  `.../EntityFrameworkCore/AppointmentChangeRequests/EfCoreChangeRequestConsentRoundRepository.cs`.
- pattern: `EfCoreAppointmentChangeRequestRepository.cs`.
- approach: tdd
- acceptance (EARS): WHEN a token hash matches either side of a round, `FindByTokenHashAsync`
  SHALL return that round, else null. WHEN several rounds exist for one request,
  `GetCurrentAsync` SHALL return the highest-numbered non-superseded round.

### T3 -- EF mapping + DbSet in BOTH contexts

- what: MODIFY `CaseEvaluationDbContext.cs` (after the `AppointmentChangeRequest` block at `:688`)
  and `CaseEvaluationTenantDbContext.cs` (`:624`) -- add `DbSet<ChangeRequestConsentRound>` and an
  `Entity<ChangeRequestConsentRound>` block: table
  `CaseEvaluationConsts.DbTablePrefix + "ChangeRequestConsentRounds"`, `ConfigureByConvention()`,
  hash columns `HasMaxLength(ConsentTokenHashLength)`, email columns
  `HasMaxLength(ConsentRespondedByEmailMaxLength)`, indexes on both hash columns, and a UNIQUE
  index on `(TenantId, AppointmentChangeRequestId, RoundNumber)` filtered `[IsDeleted] = 0`.
- pattern: the `AppointmentChangeRequest` mapping block (`:688-720`) and the filtered unique index
  at `:309`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose `ChangeRequestConsentRounds` on both contexts, and
  `dotnet build` SHALL succeed.

### T4 -- migrations in BOTH sets

- what: RUN `dotnet ef migrations add Added_ChangeRequestConsentRounds -c CaseEvaluationDbContext -o Migrations`
  and `dotnet ef migrations add Added_ChangeRequestConsentRounds -c CaseEvaluationTenantDbContext -o TenantMigrations`
  from `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore`.
- pattern: `docs/database/MIGRATION-GUIDE.md:68-74`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL contain one new migration in EACH of `Migrations/` and
  `TenantMigrations/`, and `dotnet ef migrations has-pending-model-changes` SHALL report none for
  BOTH contexts.

### T5 -- relocate token resolution to rounds

- what: MODIFY `.../Domain/AppointmentChangeRequests/ChangeRequestConsentManager.cs` -- inject
  `IChangeRequestConsentRoundRepository`; ADD an `IssueSideConsent(round, side)` overload;
  `ResolveByRawTokenAsync` looks the hash up via `FindByTokenHashAsync` FIRST, then falls back to
  the parent's two hash columns; `RecordDecisionAsync` records on whichever matched. Change
  `ChangeRequestConsentMatch` to
  `(AppointmentChangeRequest Request, ChangeRequestConsentRound? Round, ChangeRequestSide Side)`.
  Keep `GenerateRawToken` / `ComputeTokenHash` unchanged.
- **CORRECTION (2026-08-05, build time).** This task originally said the round-based
  `IssueSideConsent` REPLACES the request-based one and that `Match.Round` is non-nullable. Both
  are wrong and would have broken cancellation consent, contradicting this plan's own
  "RESCHEDULE ONLY" decision and risk 3. Verified: `IssueConsentAndNotifyAsync` is CANCEL ONLY
  since 4b (`AppointmentChangeRequestsAppService.cs:225`, `:263`, `:279`, `:284`) and issues onto
  the PARENT's flat columns. A cancel token therefore has NO round, so: the request-based
  overload STAYS, and `Round` is NULLABLE. Token resolution must cover both stores -- rounds for
  reschedule, parent columns for cancel and for legacy pre-4b reschedule rows.
- pattern: the existing method bodies (`:37-100`).
- approach: tdd
- acceptance (EARS): WHEN a raw token matches a round side, THE SYSTEM SHALL return that round and
  side. WHEN a raw token matches only the PARENT's hash columns (cancel / legacy), THE SYSTEM
  SHALL return that request with a null round and SHALL record the decision on the parent. WHEN
  the token is unknown or over `ConsentEncodedTokenMaxLength`, THE SYSTEM SHALL throw
  `ChangeRequestConsentTokenInvalid`. WHEN the matched side's token has expired, THE SYSTEM SHALL
  mark it `Expired` and throw `ChangeRequestConsentExpired`.

### T6 -- finalize gate reads the current round

- what: CREATE `.../Application/AppointmentChangeRequests/RescheduleConsentGate.cs` --
  `internal static` `EnsureRoundConsentGranted(ChangeRequestConsentRound? currentRound, bool gatingEnabled)`:
  no-op when gating is off; throws `ChangeRequestConsentNotGranted` when the round is null (no date
  confirmed yet) or its required sides are not all `Approved`.
  Leave `OpposingConsentValidator` untouched -- it still serves CANCEL.
- pattern: `OpposingConsentValidator.cs:21-42`.
- approach: tdd
- acceptance (EARS): WHEN no round exists, THE SYSTEM SHALL throw
  `ChangeRequestConsentNotGranted`. WHEN the current round has any required side not `Approved`,
  THE SYSTEM SHALL throw the same code. WHEN every required side is `Approved`, THE SYSTEM SHALL
  return without throwing. WHERE gating is disabled, THE SYSTEM SHALL return without throwing.

### T7 -- confirm-date input + resend contracts

- what: CREATE `.../Application.Contracts/AppointmentChangeRequests/ConfirmRescheduleDateInput.cs`
  -- `[Required] Guid DoctorAvailabilityId`, `[StringLength(ReasonMaxLength)] string? AdminReScheduleReason`,
  `string? ConcurrencyStamp`. MODIFY `ApproveRescheduleInput.cs` -- REMOVE `OverrideSlotId` and
  `AdminReScheduleReason` (the slot now comes from the confirmed round; keeping them would create
  two sources of truth), keep `RescheduleOutcome` + `ConcurrencyStamp`.
- pattern: `ApproveCancellationInput.cs`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL reject a `ConfirmRescheduleDateInput` with an empty
  `DoctorAvailabilityId` via model validation.

### T8 -- confirm + resend + finalize on the approval app service

- what: MODIFY `.../Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs`:
  - ADD `ConfirmRescheduleDateAsync(Guid changeRequestId, ConfirmRescheduleDateInput input)`
    `[Authorize(...AppointmentChangeRequests.Approve)]`: load + `EnsurePending`; load the slot;
    run `_bookingPolicyValidator.ValidateAsync(slot.AvailableDate, appt.AppointmentTypeId, isInternalCaller: true)`;
    guard `RescheduleRequestValidators.IsSlotAvailable`; if a current round exists with the SAME
    `ProposedDoctorAvailabilityId`, call `RegisterResend()` and re-publish only; otherwise
    `Supersede()` the current round, insert round `N+1`, resolve both reps via
    `ResolveBothSidesAsync`, `IssueSideConsent` per side that HAS a rep, and publish one
    `ChangeRequestConsentRequestedEto` per rep.
  - ADD `ResendConsentRequestAsync(Guid changeRequestId)` -- `RegisterResend()` on the current
    round and re-publish per side still `Pending`; throws `ChangeRequestNewSlotRequired` when no
    round exists.
  - MODIFY `ApproveRescheduleAsync` -> FINALIZE: replace
    `ResolveNewSlotAndEnsureAdminReason` + `OpposingConsentValidator` (`:215-227`) with
    `GetCurrentAsync` + `RescheduleConsentGate.EnsureRoundConsentGranted`; take the slot from the
    round; copy it onto `changeRequest.AdminOverrideSlotId` and the round's reason onto
    `AdminReScheduleReason`; keep the rest of the move (`:246-256`) and the Eto publishing.
  - ADD `IChangeRequestConsentRoundRepository`, `ChangeRequestConsentManager`,
    `ChangeRequestSideResolver`, `IAccountUrlBuilder` to the ctor.
- pattern: the submit-side issuance block `AppointmentChangeRequestsAppService.cs:257-310`
  (staff-initiated both-sides tokening + `PublishConsentRequestedAsync`).
- approach: tdd
- acceptance (EARS): WHEN staff confirm a date, THE SYSTEM SHALL create exactly one round, issue a
  token per side that has a representative, and publish one consent event per issued side. WHEN
  staff confirm the SAME date again, THE SYSTEM SHALL NOT create a second round and SHALL NOT
  change either token. WHEN staff confirm a DIFFERENT date, THE SYSTEM SHALL supersede the current
  round and create the next `RoundNumber`. WHEN finalize is called with no round, or with a round
  whose required sides are not all `Approved`, THE SYSTEM SHALL throw
  `ChangeRequestConsentNotGranted` and SHALL NOT move the appointment. WHEN finalize succeeds, THE
  SYSTEM SHALL move the appointment to the round's slot and SHALL set `AdminOverrideSlotId` to it.
  If the confirmed slot is inside the lead time or beyond the 90-day horizon, then THE SYSTEM SHALL
  reject the confirm.

### T9 -- interface + controller routes

- what: MODIFY `.../Application.Contracts/AppointmentChangeRequests/IAppointmentChangeRequestsApprovalAppService.cs`
  -- add `ConfirmRescheduleDateAsync` + `ResendConsentRequestAsync`; update the
  `ApproveRescheduleAsync` doc to say finalize. MODIFY
  `.../HttpApi/Controllers/AppointmentChangeRequests/AppointmentChangeRequestApprovalController.cs`
  -- add `[HttpPost] [Route("{id}/confirm-reschedule-date")]` and
  `[HttpPost] [Route("{id}/resend-consent-request")]`.
- pattern: the existing `{id}/approve-reschedule` action in the same controller.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose both routes under
  `/api/app/appointment-change-request-approvals`, and both SHALL require the `Approve` permission.

### T10 -- consent round exposed on the read DTO

- what: MODIFY `.../Application.Contracts/AppointmentChangeRequests/AppointmentChangeRequestDto.cs`
  -- add `int? CurrentConsentRoundNumber`, `Guid? CurrentRoundProposedSlotId`,
  `DateTime? CurrentRoundProposedDate`, `string? CurrentRoundProposedFromTime`,
  `ChangeRequestConsentStatus? CurrentRoundSideAStatus`, `...SideBStatus`, `int? CurrentRoundSendAttempts`.
  MODIFY `CaseEvaluationApplicationMappers.AppointmentChangeRequests.cs` -- `[MapperIgnoreTarget]`
  for each. MODIFY `ChangeRequestQueueContext.cs` + `PopulateAppointmentContextAsync` to fill them
  set-based from the rounds of the paged requests (one extra query).
- pattern: `ChangeRequestQueueContext.Apply` + `PopulateAppointmentContextAsync` (added in 4b).
- approach: tdd
- acceptance (EARS): WHEN the pending queue is fetched, THE SYSTEM SHALL populate the current-round
  fields for rows that have a round and leave them null for rows that do not, using at most one
  additional query.

### T11 -- fix the two stale consent readers

- what: MODIFY `.../Application/Notifications/Handlers/ChangeRequestConsentRequestEmailHandler.cs`
  (`:83-85`) and `.../Application/AppointmentChangeRequests/PublicChangeRequestConsentAppService.cs`
  (`:76-78`) -- resolve the date from the CURRENT ROUND's `ProposedDoctorAvailabilityId` (inject
  `IChangeRequestConsentRoundRepository`), falling back to `NewDoctorAvailabilityId` for legacy
  rows. Add `RoundNumber` to `ChangeRequestConsentRequestedEto` and use the round's slot directly
  in the handler.
- pattern: 4b's fix in `ChangeRequestApprovedEmailHandler.cs:144-146`.
- approach: tdd
- acceptance (EARS): WHEN a consent email is dispatched for a confirmed round, THE SYSTEM SHALL
  include that round's date and time in the body. WHEN the public consent page is loaded for a
  confirmed round, THE SYSTEM SHALL return that round's date in `RequestedNewDateTime` and SHALL
  NOT return null.

### T12 -- round-and-attempt-specific contextTag

- what: MODIFY `.../Application/Notifications/Handlers/ChangeRequestConsentRequestEmailHandler.cs:117`
  -- contextTag becomes
  `$"ChangeRequestConsent/{eventData.ChangeRequestId}/r{eventData.RoundNumber}/a{eventData.SendAttempt}"`;
  add `SendAttempt` to `ChangeRequestConsentRequestedEto`.
- pattern: the packet handlers' contextTags, which already include a discriminator.
- approach: tdd
- acceptance (EARS): WHEN two different rounds dispatch consent to the SAME recipient, THE SYSTEM
  SHALL produce two distinct idempotency keys and SHALL write two outbox rows. WHEN the same round
  and attempt is dispatched twice, THE SYSTEM SHALL write exactly one row.

### T13 -- consent expiry sweep job

- what: CREATE `.../Domain/AppointmentChangeRequests/Jobs/ChangeRequestConsentExpirySweepJob.cs`
  -- `ITransientDependency`, `RecurringJobId = "change-request-consent-expiry-sweep"`,
  `CronExpression = "30 * * * *"`, `[UnitOfWork] ExecuteAsync()` iterating
  `_tenantWorkRunner.ForEachOfficeAsync`; for each non-superseded round with a `Pending` side past
  its expiry, call `MarkSideExpired` and log a count. MODIFY
  `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` (beside
  `:1349`) to register it.
- pattern: `CancellationRescheduleReminderJob.cs:63-82`.
- approach: tdd
- acceptance (EARS): WHEN a round side is `Pending` and its `ExpiresAt` has passed, THE SYSTEM
  SHALL set it `Expired` and record `RespondedAt`. WHEN a side is already decided or the round is
  superseded, THE SYSTEM SHALL leave it unchanged. THE SYSTEM SHALL process every office in one run.

### T14 -- regenerate Angular proxies

- what: RUN `abp generate-proxy -t ng -u http://localhost:44327 --module app` (the CLI is the
  DOTNET GLOBAL TOOL at `~/.dotnet/tools/abp`; `npx abp` fails). Keep ONLY
  `angular/src/app/proxy/appointment-change-requests/**` + `generate-proxy.json`; revert the rest.
- pattern: 4b's proxy step.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose `confirmRescheduleDate` and `resendConsentRequest` on
  the approval proxy service, the new DTO fields, and an `ApproveRescheduleInput` without
  `overrideSlotId`.

### T15 -- inbox: three-stage approve modal

- what: MODIFY `angular/src/app/appointments/change-requests/internal-change-request-inbox.component.{ts,html}`
  -- the reschedule approve modal renders by round state:
  (a) no round, or current round declined/expired -> calendar + "Confirm date & request consent";
  (b) round awaiting -> the confirmed date, per-side consent status, "Resend consent request", and
  a DISABLED Finalize; (c) round granted -> billing outcome + "Finalize reschedule".
  Wire `confirmRescheduleDate` / `resendConsentRequest`; `approveReschedule` now sends only the
  outcome. Keep exactly ONE `<app-availability-calendar>` rendered at a time.
- pattern: the 4b modal in the same file; `cr-approve.util.ts` for pure gating.
- approach: test-after
- acceptance (EARS): WHILE no round is confirmed, THE SYSTEM SHALL show the calendar and SHALL
  disable Finalize. WHILE a round awaits consent, THE SYSTEM SHALL show that round's date and both
  sides' status, SHALL offer Resend, and SHALL keep Finalize disabled. WHILE the current round is
  granted, THE SYSTEM SHALL enable Finalize once a billing outcome is chosen. THE SYSTEM SHALL
  render at most one availability calendar at a time.

### T16 -- pure util + specs for the round-state gating

- what: MODIFY `angular/src/app/appointments/change-requests/cr-approve.util.ts` -- add
  `type ConsentRoundStage = 'needs-date' | 'awaiting-consent' | 'granted'` and
  `rescheduleStage(row)` deriving it from the DTO's current-round fields, plus
  `canFinalizeReschedule({stage, outcome})` and `canConfirmDate({slotId, time})`. Have the
  component delegate. MODIFY `cr-approve.util.spec.ts` to cover all three stages.
- pattern: the 4b utils in the same files.
- approach: tdd
- acceptance (EARS): THE SYSTEM SHALL map a row with no round to `needs-date`, a round with any
  side `Pending` to `awaiting-consent`, a round with all required sides `Approved` to `granted`,
  and a round with a `Rejected`/`Expired` side back to `needs-date`.

### T17 -- integration tests on the MultiOffice harness

- what: CREATE `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/MultiOfficeRescheduleConsentTests.cs`
  -- resolve `IAppointmentChangeRequestsApprovalAppService`; seed via `MultiOfficeSeeder`; cover:
  confirm creates a round + outbox rows; same-date confirm resends without a new round;
  different-date confirm supersedes and creates round 2 WITH a second outbox row (the contextTag
  regression); finalize blocked until both sides Approved; finalize moves the appointment and sets
  `AdminOverrideSlotId`. Set consent state directly on the round (the tokenised email click cannot
  be driven from a test -- see tracker `:380-384`).
- pattern: `MultiOfficeAppointmentsAppServiceTests.cs:29-43`.
- approach: tdd
- acceptance (EARS): THE SYSTEM SHALL pass all listed cases, and the round-2 case SHALL fail if the
  contextTag omits the round.

### T18 -- honest in-flight status pills

- what: MODIFY `angular/src/app/shared/ui/status-pill/status-pill.component.ts` -- add
  `'RescheduleRequested'` and `'CancellationRequested'` to `AppointmentPillStatus` (`:4-10`) and to
  `PILL_META` (`:22-28`) with labels `'Reschedule Requested'` / `'Cancellation Requested'` and tone
  `'pending'` (amber reads as in-progress; `'info'` blue currently reads as done).
  MODIFY `angular/src/app/shared/ui/status-pill/appointment-status.util.ts` -- map
  `RescheduleRequested` and `CancellationRequested` to their own pills (remove them from the
  `Rescheduled` / `Cancelled` cases at `:35` and `:40`) and add both to `PILL_TO_SEGMENT` (`:60-67`)
  pointing at `'rescheduled'` / `'cancelled'` so the existing chips keep finding them. Update the
  doc comment at `:17-22`.
- pattern: the existing `InfoRequested` pill, which is already a distinct pill with its own tone.
- approach: tdd
- acceptance (EARS): WHEN the status is `RescheduleRequested`, THE SYSTEM SHALL return the
  `RescheduleRequested` pill with label "Reschedule Requested" and SHALL map it to the
  `rescheduled` filter segment. WHEN the status is `CancellationRequested`, THE SYSTEM SHALL return
  the `CancellationRequested` pill with label "Cancellation Requested" and the `cancelled` segment.
  WHEN the status is `RescheduledNoBill` or `RescheduledLate`, THE SYSTEM SHALL still return
  `Rescheduled`. THE SYSTEM SHALL keep every existing segment chip count unchanged for a given set
  of statuses.

### T19 -- honest in-flight banners, labels and actions

- what: MODIFY `angular/src/app/appointments/appointment/components/internal-detail.util.ts`:
  `bannerVariant` (`:65-67`) returns `'reschedule-requested'` / `'cancellation-requested'` for the
  new pills (kebab-case, mirroring the `InfoRequested` special case); `statusLabel` (`:70-72`)
  returns `'Reschedule requested'` / `'Cancellation requested'`; `detailActions` (`:26-37`) leaves
  both new pills on the `default: []` branch (no office actions in flight) and KEEPS
  `['reschedule','cancel']` for `Approved` / `Rescheduled`.
  MODIFY `angular/src/app/appointments/appointment/components/external-appointment-detail.component.ts`
  -- add `CALLOUTS['reschedule-requested']` (icon `refresh`, title "Reschedule requested", body
  "We received your reschedule request. Our staff will propose a new date and time, and all parties
  must agree before it takes effect.") and `CALLOUTS['cancellation-requested']` (icon `x`, title
  "Cancellation requested", body "We received your cancellation request. It is awaiting review;
  the appointment is still scheduled until then."); reuse the shared `bannerVariant` / `statusLabel`
  helpers instead of its local duplicates (`:164-169`); exclude both new variants from
  `showOutcomeNote` (`:174`) since no outcome exists yet.
- pattern: the `info-requested` callout + `InfoRequested` special-casing already present in both
  files.
- approach: test-after
- acceptance (EARS): WHILE an appointment is `RescheduleRequested`, THE SYSTEM SHALL show the pill
  "Reschedule Requested", SHALL show the reschedule-requested banner, SHALL NOT claim the
  appointment has been rescheduled, SHALL NOT show an outcome note, and SHALL offer no
  detail-page Reschedule or Cancel action. WHILE an appointment is `CancellationRequested`, THE
  SYSTEM SHALL show "Cancellation Requested" and SHALL NOT claim the appointment was cancelled.

### T20 -- localization + tracker

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
  -- keys for the confirm / resend / finalize labels and the awaiting-consent notice. MODIFY
  `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md` -- 4c row to DONE with the
  PR/sha, correct the "4c needs a harness" note at `:261`, record the outbox-suppression and
  stale-reader findings, append 4c learnings. Flip BOTH this plan and the 4b plan to `status: done`
  (4b's was left `in-progress`).
- approach: code
- acceptance (EARS): THE SYSTEM SHALL resolve every `abpLocalization` key used by the modified
  template, and the tracker SHALL NOT still claim 4c needs a harness built.

## Validation loop

Backend:

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

Migrations (BOTH contexts, from `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore`):

```
dotnet ef migrations has-pending-model-changes -c CaseEvaluationDbContext
dotnet ef migrations has-pending-model-changes -c CaseEvaluationTenantDbContext
```

Frontend:

```
export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe"
npx prettier --check <changed frontend files>
npx eslint <changed frontend files>
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

Live gate (check `docker ps` first; `docker compose up -d` if the stack is down; then
`docker restart main-api-1 main-angular-1` and wait for health + "Accepting connections"):

1. Internal `clistaff1@gesco.com` at `admin.localhost:4200` -> Enter practice -> Change requests.
   On a pending reschedule: pick a date, CHANGE it twice, confirm NO email was sent
   (`AppNotificationOutbox` row count unchanged).
2. Click "Confirm date & request consent" -> exactly TWO outbox rows appear (one per side) and the
   modal switches to awaiting-consent. Verify Finalize is disabled.
3. Click "Resend consent request" -> two MORE rows (distinct idempotency keys), same token hashes.
4. Confirm a DIFFERENT date -> round 2 created, round 1 `SupersededAt` set, two more rows.
5. SQL-set both sides of round 2 to `Approved` (2), reload, Finalize with an outcome -> appointment
   moves to the round-2 slot, `AdminOverrideSlotId` set, `RequestStatus` `Accepted` (26).
6. Open the public consent link for a live token at `falkinstein.localhost:4200/public/change-request-consent/<token>`
   and confirm the DATE IS SHOWN (the stale-reader regression).
7. STATUS HONESTY (Adrian's requirement). Throughout steps 1-4, on BOTH the internal detail
   (`admin.localhost:4200`) and the external detail (`falkinstein.localhost:4200`) for the
   in-flight appointment:
   - the pill reads **"Reschedule Requested"**, never "Rescheduled";
   - the external banner does NOT say "This appointment has been rescheduled";
   - no Reschedule/Cancel action renders on the detail page;
   - the appointment still appears under the existing "Rescheduled" filter chip and the chip
     counts are unchanged.
     Then repeat on a pending CANCELLATION request and confirm it reads "Cancellation Requested",
     not "Cancelled". Only after finalize (step 5) may the pill read "Rescheduled".

Note: `internal-detail.util.spec.ts`, `internal-appointments.util.spec.ts` and
`schedule-calendar.util.spec.ts` pin current pill/label values and are EXPECTED to fail on T18/T19
until updated -- that is the signal working, not a regression.

SQL verification queries against `CaseEvaluation_falkinstein`:
`AppChangeRequestConsentRounds` (RoundNumber, ProposedDoctorAvailabilityId, SendAttempts,
SupersededAt, both side statuses) and `AppNotificationOutbox` (IdempotencyKey, Context) and
`AppAppointments` (AppointmentDate, AppointmentStatus).

## Risk / rollback

Blast radius: the reschedule APPROVAL path and the consent machinery. Cancellation CONSENT is
untouched (still on the parent's flat columns via `OpposingConsentValidator`). Booking, submit and
the 4b request modal are untouched.

The status-pill work (T18/T19) is WIDER than the consent work: `appointmentStatusToPill` feeds the
internal list chips, the internal detail, the external detail and the external home segments. It is
type-guarded -- `PILL_META` and `PILL_TO_SEGMENT` are total `Record`s over `AppointmentPillStatus`,
so a missed mapping is a compile error rather than a runtime surprise -- but it touches every
surface that renders a status, and it is the one part of 4c that changes CANCEL-side display.

Highest risks:

1. `ApproveRescheduleInput` loses `OverrideSlotId` -- a BREAKING wire change. Safe only because 4b
   is unreleased and this repo ships both together; the Angular caller is updated in T15.
2. Two migrations must land together. A partial apply leaves one context without the table;
   `has-pending-model-changes` on both is the gate.
3. Token resolution moves table. Any legacy row with consent on the PARENT columns (cancellations,
   and pre-4b reschedules) must keep working -- the manager keeps the parent path for cancel and
   the readers fall back to `NewDoctorAvailabilityId`.
4. The expiry sweep mutates rows on a schedule; it is guarded to `Pending` sides on
   non-superseded rounds only, and logs a count per run.
5. T19 REMOVES the detail-page Reschedule/Cancel buttons for a reschedule-in-flight appointment.
   Intended and decided, but it is the change most likely to read as "something disappeared" --
   call it out in the PR.

Rollback: `git revert` the squash-merge. The new table is additive and unreferenced by other
entities, so leaving it in place after a revert is harmless; drop it only if desired.
