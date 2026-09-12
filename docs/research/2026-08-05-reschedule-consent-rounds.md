# Reschedule Consent Rounds -- Research (epic phase 4c)

Research-only output. No code edits in this pass. Produced 2026-08-05 against `main` at
`326f08a9` (immediately after phase 4b merged as PR #423).

Companion plan: `docs/plans/2026-08-05-reschedule-consent-rounds.md`.
Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.

Every claim below was verified by reading the current code at the cited `file:line`, not from
memory or a prior summary. Confidence is HIGH unless stated otherwise. Line numbers drift --
re-verify before editing (the roadmap's own recorded anchors had already moved by 11 lines when
this pass started).

---

## 1. Where 4b left the system

Phase 4b made staff the date-picker and DELIBERATELY stopped issuing reschedule consent at submit
(`AppointmentChangeRequestsAppService.cs`, `RequestRescheduleAsync` -- the
`IssueConsentAndNotifyAsync` call is skipped for `ChangeRequestType.Reschedule`).

Consequence to be aware of: `AppointmentChangeRequest.AreAllRequiredSidesGranted()` returns TRUE
when both sides are `NotRequired`, so `OpposingConsentValidator.EnsureConsentGranted` is currently
a NO-OP for reschedules. A supervisor can finalize a reschedule today with zero consent recorded.
That is the temporary gap 4c closes, and the reason 4b and 4c must deploy together.

Post-4b, `ApproveRescheduleAsync` does two jobs in one call:

- `AppointmentChangeRequestsAppService.Approval.cs:215` -- consent gate
- `:218` -- `ResolveNewSlotAndEnsureAdminReason` (slot resolution)
- `:238` -- `BookingPolicyValidator.ValidateAsync` (added in 4b)
- `:246-256` -- moves the appointment in place

4c splits this into confirm (creates a round, sends) and finalize (moves the appointment).

## 2. Consent as it exists today

### Storage: flat columns on the aggregate

`AppointmentChangeRequest` carries twelve consent columns -- per side:
`Side{A|B}ConsentStatus`, `...TokenHash`, `...ExpiresAt`, `...RespondedAt`,
`...RespondedByEmail`, plus `RequestingSide` and `SubmittedByUserId`
(`AppointmentChangeRequest.cs:91-130`). Mapped identically in BOTH DbContexts
(`CaseEvaluationDbContext.cs:688-720`, `CaseEvaluationTenantDbContext.cs:624+`) with an index on
each token-hash column.

### The state machine

Pure domain transitions on the aggregate (`AppointmentChangeRequest.cs:168-289`):
`InitiateConsent`, `AutoGrantSide`, `IssueSideConsent`, `RecordSideDecision`, `MarkSideExpired`,
`IsSideExpired`, `SideConsentStatus`, `SideConsentTokenHash`, `AreAllRequiredSidesGranted`.

`ChangeRequestConsentStatus`: `NotRequired = 0`, `Pending = 1`, `Approved = 2`, `Rejected = 3`,
`Expired = 4`.

### Token crypto

`ChangeRequestConsentManager` (`Domain/AppointmentChangeRequests/ChangeRequestConsentManager.cs`):
32 random bytes -> URL-safe base64 (~43 chars), SHA256 hex stored (`:106-127`). Raw token returned
ONCE for the email link. TTL 7 days (`AppointmentChangeRequestConsts.ConsentDefaultTtlDays`).
`ResolveByRawTokenAsync` (`:53-73`) queries the REQUEST's two hash columns.
`RecordDecisionAsync` (`:82-100`) resolves, checks expiry, records.

### Both-sides resolution already exists

`ChangeRequestSideResolver.ResolveBothSidesAsync` (`:78-92`) returns Side A rep (Applicant
Attorney else Patient) and Side B rep (Defense Attorney else Claim Examiner); either may be null,
in which case the caller leaves that side `NotRequired` (auto-satisfied). This is exactly what
"token both sides after the staff pick" needs -- do not write a new resolver.

Side A = Patient + Applicant Attorney. Side B = Defense Attorney + Claim Examiner.

### The public response path

`PublicChangeRequestConsentAppService` (`[AllowAnonymous]`, tenant resolved from subdomain):
`GetConsentInfoAsync(token)` and `SubmitDecisionAsync(token, dto)`. Idempotent by design -- a
replay or expiry returns current state rather than erroring (`:58-67`). Angular landing page:
`angular/src/app/public-change-request-consent/` (203-line component, 46-line template), routed at
`app.routes.ts:373` `public/change-request-consent/:token`.

### Feature flag

`AppointmentChangeRequestConsts.ConsentGatingEnabled` is a compile-time `const bool = true`
(`:35`). Documented as "promote to a per-tenant ISettingProvider setting when multi-tenant
toggling is needed" -- still a const.

---

## 3. Verified defects and blockers

### 3.1 TWO stale readers of the proposed slot (HIGH -- both consent-facing)

4b moved the staff-chosen slot to `AdminOverrideSlotId`, leaving `NewDoctorAvailabilityId` NULL on
the external path. 4b fixed one reader (`ChangeRequestApprovedEmailHandler.cs:144-146`, now via
`ChangeRequestApprovalValidator.ResolveScheduledSlotId`). Two were missed:

| #   | Reader        | Anchor                                                                                | Symptom                                                                                                              |
| --- | ------------- | ------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| 1   | Consent EMAIL | `Application/Notifications/Handlers/ChangeRequestConsentRequestEmailHandler.cs:83-85` | reads only `NewDoctorAvailabilityId`; `BuildDetailsBlock` (`:129-142`) omits the date line entirely                  |
| 2   | Consent PAGE  | `Application/AppointmentChangeRequests/PublicChangeRequestConsentAppService.cs:76-78` | `RequestedNewDateTime` stays null; template hides it via `*ngIf` (`public-change-request-consent.component.html:18`) |

Net effect once 4c re-enables consent: a party is asked to approve a reschedule with **no date
shown anywhere**. Both are inert TODAY only because 4b suppressed issuance.

This is the same failure class 4b hit -- fix a column's writer, then find every reader. Three
readers of "the proposed slot" existed; only one was known at the time.

### 3.2 The notification outbox SILENTLY swallows duplicate sends (HIGH -- the key blocker)

- Idempotency key = `SHA256(tenantId | recipientEmail | contextTag | packetKind)`
  (`Domain.Shared/Appointments/SendAppointmentEmailArgs.cs:111-121`).
- `NotificationOutboxManager.EnqueueAsync` returns the EXISTING row on a key match -- no throw, no
  log (`Domain/Notifications/Outbox/NotificationOutboxManager.cs:59-64`).
- Enforced by a unique index `(TenantId, IdempotencyKey)` filtered `[IsDeleted] = 0`
  (`CaseEvaluationDbContext.cs:309`; same in the Tenant context ~`:214`).
- The consent contextTag today is `ChangeRequestConsent/{changeRequestId}`
  (`ChangeRequestConsentRequestEmailHandler.cs:117`) -- **no round, no attempt discriminator**.

Therefore: round 2's consent email to the same recipient, and every resend, would be silently
dropped. No error surfaces anywhere. The contextTag MUST carry round + send attempt.

This single fact is why a resend button cannot be bolted on without touching the contextTag.

### 3.3 No consent-expiry job exists (HIGH)

Expiry is evaluated LAZILY -- only inside `RecordDecisionAsync` when a party actually clicks
(`ChangeRequestConsentManager.cs:90-95`). Nothing sweeps. A token nobody clicks stays `Pending`
past its 7-day TTL forever, so the finalize gate blocks indefinitely with no staff signal. The
tracker (`:199-200`) also notes the consent email's "referred to our clinic staff" promise has no
implementation.

Pattern to mirror for a sweep:
`Domain/Appointments/Notifications/Jobs/CancellationRescheduleReminderJob.cs`
-- `ITransientDependency`, `public const string RecurringJobId`, `public const string CronExpression`,
`[UnitOfWork] ExecuteAsync()`, and `_tenantWorkRunner.ForEachOfficeAsync(...)` for db-per-office
iteration (`:63-82`). Registered with
`Hangfire.RecurringJob.AddOrUpdate<T>(T.RecurringJobId, x => x.ExecuteAsync(), T.CronExpression)`
at `HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:1349-1352`.
Other job precedents: `DraftCleanupJob`, `ApprovalReconciliationJob`,
`CaseTrackerReconciliationJob`, `CaseTrackerCompletenessSweepJob`.

### 3.4 Ordering inversion -- still present, anchors had drifted

Consent is checked BEFORE the slot is resolved:
`OpposingConsentValidator.EnsureConsentGranted` at `Approval.cs:215`, then
`ResolveNewSlotAndEnsureAdminReason` at `:218`. The roadmap recorded these as `:204-205` vs `:207`
-- an 11-line drift in one phase. 4c needs slot-first-then-consent, which the confirm/finalize
split delivers naturally.

### 3.5 Misleading in-flight status (HIGH -- pre-existing on `main`, raised by Adrian)

`appointmentStatusToPill` (`angular/src/app/shared/ui/status-pill/appointment-status.util.ts:38-41`)
buckets `RescheduleRequested` into the `Rescheduled` pill, by deliberate design of the 6-pill model
(documented at `:11-23`). Consequences:

- Pill reads "Rescheduled" while nothing has moved (`status-pill.component.ts:22-28`, tone `info`
  blue = looks completed).
- External banner asserts **"This appointment has been rescheduled -- the new date and time are
  shown above"** (`external-appointment-detail.component.ts:74-78`), which is simply false during
  an in-flight request. Observed live during the 4b gate.
- IDENTICAL defect on the cancel side: `CancellationRequested` -> `Cancelled` pill (`:35`), banner
  "This appointment was cancelled" (`:69-73`).

Mechanics that constrain the fix:

- `bannerVariant = pill.toLowerCase()` with an `InfoRequested` special case
  (`internal-detail.util.ts:65-67`; duplicated inline at
  `external-appointment-detail.component.ts:164-166`).
- `CALLOUTS` is `Record<string, CalloutCopy>` with a `?? CALLOUTS['pending']` fallback
  (`external-appointment-detail.component.ts:53`), so an unmapped variant silently falls back.
- `statusLabel` returns the raw pill (`internal-detail.util.ts:70-72`), so a new pill would render
  as one word without a case.
- `detailActions(pill)` grants `['reschedule','cancel']` to `Rescheduled` (`:26-37`), so splitting
  the pill REMOVES those buttons for an in-flight reschedule -- a real behaviour change.
  `CancellationRequested` already gets `[]` today.
- `PILL_META` and `PILL_TO_SEGMENT` are TOTAL `Record`s over `AppointmentPillStatus`, so adding a
  pill member forces every mapping to be handled at compile time. Type-guarded.

Consumers of the mapping: `external-appointment-detail.component.ts:162`,
`internal-appointment-detail.component.ts:120`, `internal-appointments.component.ts:201`,
`internal-appointments.util.ts:44,62,95,102`. `schedule-calendar.util.ts:33` already treats
`RescheduleRequested` as a "requested" status -- consistent with Adrian's intent.

---

## 4. Facts that shape the design

- **No new appointment status is needed.** `AppointmentStatusType.RescheduleRequested = 12`
  already fits "date proposed, awaiting consent"; the appointment only moves on finalize.
  `RequestStatusType` has exactly `Pending = 25`, `Accepted = 26`, `Rejected = 27` -- the ROUND
  carries its own state, so no new value there either.
- **The round entity dissolves the recorded blocker.** `IssueSideConsent` is only valid from
  `NotRequired` (`AppointmentChangeRequest.cs:192-199`), which is why the roadmap flagged that a
  declined side cannot be re-tokened. A fresh round row starts at `NotRequired`, so no reset hack.
- **Dual-context migrations are mandatory.** `AppointmentChangeRequest` is mapped in both
  contexts, so a new child table needs a migration in EACH set (85 host / 11 tenant migrations
  today):
  `dotnet ef migrations add <Name> -c CaseEvaluationDbContext -o Migrations` and
  `-c CaseEvaluationTenantDbContext -o TenantMigrations`
  (`docs/database/MIGRATION-GUIDE.md:68-74`).
- **Child-entity pattern to mirror:** `AppointmentChangeRequestDocument` (`:16-65`) -- a sibling
  `FullAuditedAggregateRoot<Guid>` + `IMultiTenant` + `[Audited]` with an
  `AppointmentChangeRequestId` FK and a `protected` parameterless ctor. NOT an owned collection.
- **Manual controllers are required here.** Both app services carry
  `[RemoteService(IsEnabled = false)]` with paired hand-written controllers
  (`AppointmentChangeRequests/CLAUDE.md`, "Two AppServices, single feature folder").
  `AppointmentChangeRequestApprovalController` is routed at
  `api/app/appointment-change-request-approvals`.
- **Permissions:** `CaseEvaluationPermissions.AppointmentChangeRequests.{Default,Approve,Reject}`
  (`Permissions/CaseEvaluationPermissions.cs:293-298`).
- **App-service DI weight:** the approval service now has 6 injected deps (5 + `BookingPolicyValidator`
  added in 4b); the submit service has 10.

### Integration harness EXISTS -- corrects earlier research

The tracker (`:261`) and 4b's research both state 4c needs a test harness built. **That is wrong.**
`test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/` provides:

- `CaseEvaluationMultiOfficeTestBase`, `MultiOfficeTestModule`, `MultiOfficeTestDatabase`,
  `MultiOfficeCollection`
- `MultiOfficeSeeder.SeedAsync(officeId, label)` returning a `SeededOffice` record with
  `AppointmentTypeId`, `SecondAppointmentTypeId`, `DoctorAvailabilityId`, `AppointmentId`,
  location ids -- everything a change-request test needs
- `MultiOfficeAppointmentsAppServiceTests:39-43` resolving REAL app services via
  `GetRequiredService<IAppointmentsAppService>()`, plus `ICurrentTenant` and
  `ICurrentPrincipalAccessor` (to act as a role)

Existing sibling suites: `MultiOfficeIsolationMatrixTests`, `MultiOfficeImpersonationRoleTests`,
`MultiOfficeCatalogResolutionTests`, `MultiOfficeHostNotificationTemplatesTests`,
`MultiOfficeHarnessSelfValidationTests`.

No change-request test source exists yet (an earlier grep appeared to find some -- it was hitting
compiled DLLs under `bin/`).

Caveat carried from the tracker (`:380-384`): a tokenised email click cannot be driven from a
test, so consent state must be SET DIRECTLY on the row/round in test setup.

### Pure-helper precedent (why extractions are idiomatic here)

`ChangeRequestListFilter`, `ChangeRequestApprovalValidator`, `RescheduleInPlacePolicy`,
`OpposingConsentValidator`, and 4b's `ChangeRequestQueueContext` are all `internal static` pure
classes unit-tested via `InternalsVisibleTo`. When app-service logic needs a `tdd` flag, extract
the decision into one of these rather than downgrading the flag.

---

## 5. Decisions taken (with rationale)

Recorded verbatim in the plan's "Resolved decisions". Summary:

| Decision                                                                    | Rationale                                                                                                                                                                                                                               |
| --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Flow is PICK -> CONFIRM -> FINALIZE                                         | Adrian: "what if the staff selects a date and then changes it immediately, in that case 2 different emails will go out. I want a button where the staff can click to confirm the date so we know that the staff meant select this date" |
| Emails send ONLY on Confirm                                                 | Same reason; also removes any state where tokens exist but nothing was sent                                                                                                                                                             |
| Staff finalize explicitly (no auto-finalize)                                | Billing outcome is chosen at finalize; staff keep the last look                                                                                                                                                                         |
| New child entity per round                                                  | Adrian wants "the full audit trail of every date proposed and who declined it" -- must be queryable, not reconstructed from a log                                                                                                       |
| Reschedule only; cancel consent unchanged                                   | A cancellation has no date to re-propose; nobody asked to change a working flow                                                                                                                                                         |
| Same-date confirm = resend in the current round; different date = new round | A resend must not invalidate a link a party already holds                                                                                                                                                                               |
| One phase, expiry sweep included                                            | Adrian, 2026-08-05                                                                                                                                                                                                                      |
| Distinct `RescheduleRequested` + `CancellationRequested` pills              | Adrian: showing "Rescheduled" mid-flight "is misleading"                                                                                                                                                                                |
| Fix BOTH sides                                                              | `CancellationRequested` -> `Cancelled` is the identical defect                                                                                                                                                                          |
| New pills filter under EXISTING chips                                       | No seventh chip; chip counts unchanged; only the pill TEXT becomes honest                                                                                                                                                               |
| No detail-page Reschedule/Cancel while in flight                            | Matches cancel-in-flight today; prevents stacking a second request. Removes two buttons that currently render -- intentional                                                                                                            |

Rejected alternatives worth recording:

- **Round columns + history table** -- two places hold consent state and can drift.
- **Reset columns in place + rely on the change log** -- audit becomes a log to read rather than
  queryable data; weakest against the stated requirement.
- **Auto-finalize on last consent** -- forces the billing outcome to be chosen up front and
  removes the staff last look.
- **Automatic send on date selection** -- the exact double-email problem Adrian raised.
- **A seventh status chip** -- shifts chip counts, layout, and the external home segments.

---

## 6. Open questions deliberately left for build time

None blocking. One assumption carried into the plan and flagged for correction: confirming the
SAME date again resends within the current round rather than creating a duplicate round, which is
what forces the send-attempt discriminator in the contextTag.
