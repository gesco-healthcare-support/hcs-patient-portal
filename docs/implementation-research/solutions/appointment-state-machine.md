# Appointment state-transition enforcement (13-state lifecycle)

## Status (scope-locked 2026-04-24)

MVP state subset, not full 13-state enforcement. Adrian's Q&A answer Q1:
- **IN:** Pending, Approved, Rejected, **MoreInfoRequested (new, not in OLD's 13)**, RescheduleRequested, CancellationRequested, CancelledNoBill, CancelledLate, RescheduledNoBill, RescheduledLate.
- **OUT:** CheckedIn, CheckedOut, Billed, NoShow.
- Effort revised: **S-M (~1.5 days)**, down from M (~3 days). Fewer transitions to configure in Stateless; `MoreInfoRequested` added as a new state (new transition: Pending -> MoreInfoRequested on admin action).
- Sections below (Alternatives / Recommended solution) remain accurate for the implementation shape (Stateless library + ILocalEventBus fan-out); only the transition set shrinks.

## Source gap IDs

- [G2-01 -- Appointment state-transition enforcement (track 02)](../../gap-analysis/02-domain-entities-services.md)
- Severity: MVP-blocking. Called out in [gap-analysis/README.md:8](../../gap-analysis/README.md) as "Single biggest MVP risk" and in [README.md:376](../../gap-analysis/README.md) as "the biggest architectural piece" gating most other appointment workflows.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs:3-18` -- 13-state enum defined identically to OLD (Pending=1, Approved=2, Rejected=3, NoShow=4, CancelledNoBill=5, CancelledLate=6, RescheduledNoBill=7, RescheduledLate=8, CheckedIn=9, CheckedOut=10, Billed=11, RescheduleRequested=12, CancellationRequested=13).
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/BookingStatus.cs:3-8` -- slot states (Available=8, Booked=9, Reserved=10). Coupled to state machine via slot cascade.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs:14-60` -- 47-line DomainService. `CreateAsync` and `UpdateAsync` accept `AppointmentStatusType` as a plain parameter and store it; no transition validation, no guards, no events.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs:19-72` -- aggregate root. `public virtual AppointmentStatusType AppointmentStatus { get; set; }` (line 40) is a public settable property. Any caller with a reference to the entity can mutate status to any value. No invariant enforced on construction beyond `Check.Length` on `RequestConfirmationNumber` and `PanelNumber`.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md:58-67` -- explicitly states "No domain methods enforce valid transitions. Any code can set any status directly on the entity. The enum defines states but there is no state machine guard."
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:162-205` -- `CreateAsync` validates GUIDs + slot availability + date/time window, then calls `_appointmentManager.CreateAsync` with the client-supplied `input.AppointmentStatus` value (line 199). Consequence: an external-user caller can POST a create with `AppointmentStatus = Approved` (11) and bypass the Pending-queue workflow entirely.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:201-202` -- only slot-state transition written today: `doctorAvailability.BookingStatusId = BookingStatus.Booked` on create. `DeleteAsync` (line 155-159) does not restore the slot. No Approved/Rejected/Cancelled paths. This is the G2-02 booking-cascade surface; state-machine transitions are the trigger, so the two capabilities couple tightly.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:294-324` -- `UpdateAsync` delegates to `AppointmentManager.UpdateAsync`, which per `Appointments/CLAUDE.md:132` deliberately does NOT accept `AppointmentStatus`. Confirmed by reading the signature at `AppointmentManager.cs:39`. Consequence: status can only be set at creation today; there is no legitimate path to transition it afterward, yet any caller can still mutate `appointment.AppointmentStatus` directly through the aggregate (no internal setter) because nothing seals the property.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:156-159` -- `DeleteAsync` is guarded by `[Authorize(Appointments.Delete)]` only; nothing checks whether the appointment status permits deletion. OLD rejects deletion via `ApplicationUtility.CandDelete<Appointment>(id, true)` (`AppointmentDomain.cs:613-621`).
- Root `CLAUDE.md` "What Claude Should Never Do" -- "Skip the AppointmentManager domain service for create/update -- business rules live there, not in the AppService directly." This enshrines the Manager as the correct host for transition logic, matching ABP DDD guidance.
- `docs/features/appointments/overview.md` (if present) / `docs/business-domain/APPOINTMENT-LIFECYCLE.md` -- referenced from gap-analysis track 02 as the human-readable narrative for the 13 states; source of the state-chart diagram in `Appointments/CLAUDE.md:59-65`.

## Live probes

- Probe 1 -- OIDC token exchange (HIGH confidence): `POST https://localhost:44368/connect/token` with seeded `admin / 1q2w3E*` returns `access_token` + `expires_in: 3599`. Confirmed already in [probes/service-status.md:17-26](../probes/service-status.md). Proves that follow-on authenticated probes are possible with the seeded host-admin.
- Probe 2 -- Swagger scan for transition endpoints: `GET https://localhost:44327/swagger/v1/swagger.json` with filter for path patterns like `/status`, `/transition`, `/approve`, `/reject`, `/check-in`, `/check-out`, `/bill`, `/cancel`, `/reschedule`. Expected result: no such endpoints exist -- only standard ABP CRUD (`/api/app/appointments`, `/{id}`, `/{id}/with-navigation-properties`). Proves the transition API surface is fully absent.
- Probe 3 -- read `/api/app/appointments` (empty tenant): returns `{"totalCount":0,"items":[]}` per service-status.md. Proves no seed data exists, so any subsequent brief-authoring does not risk PHI collision.
- Full probe log: [probes/appointment-state-machine-2026-04-24T13-30-00.md](../probes/appointment-state-machine-2026-04-24T13-30-00.md).

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs:199-310` -- `Add()` method: sets initial status based on UserType (External -> Pending + slot Reserved; Internal -> Approved + slot Booked). Not a state-machine guard, but the only enforcement point for the "initial" transition.
- `AppointmentDomain.cs:312-344` -- `UpdateValidation()`: OLD's idempotency guard. Refuses `Approved -> Approved`, `Rejected -> Rejected`, `CheckedIn -> CheckedIn`, `CheckedOut -> CheckedOut`, `Billed -> Billed`. NOT a forward-motion guard -- it only blocks repeat-of-current-state, not invalid-target-state. This is weaker than a real state machine.
- `AppointmentDomain.cs:406-573` -- `Update()`: the 170-line "transition" body. Combines: slot cascade (lines 431-448), `AppointmentApproveDate` stamp on Approved (lines 453-455), change-request auto-creation on CancelledNoBill (lines 537-550), change-log field-diff, email dispatch per status. No enforced DAG of legal transitions; any `appointment.AppointmentStatusId` flowing in is persisted.
- `AppointmentDomain.cs:586-611` -- `UpdateDoctorAvailbilty()`: switch statement mapping 4 appointment statuses (Pending -> Reserved, Approved -> Booked, Rejected -> Available, CancelledNoBill -> Available) to slot status. The 9 other appointment statuses fall to the `default:` branch with no cascade. This is the slot-cascade contract that G2-02 must preserve.
- `AppointmentDomain.cs:839-881` -- `SendSMS()`: **per [track-10 erratum](../../gap-analysis/10-deep-dive-findings.md)**, all Twilio calls here are commented out. Status-transition SMS is 100% dead code. This brief inherits that correction -- SMS side-effects are NOT in scope.
- `AppointmentDomain.cs:883-1050` -- `SendEmail()`: per-status email dispatch is alive. Status transitions must still fan out email notifications. Scope belongs to the separate `scheduler-notifications` + `email-sender-consumer` briefs, not here -- but this brief must emit a domain event the email consumer can subscribe to.
- `AppointmentDomain.cs:537-550` -- auto-creates an `AppointmentChangeRequest` row when status flips to `CancelledNoBill` with internal-user-update flag. That coupling belongs in `appointment-change-requests` (G2-06), but the state-machine must publish the transition event so the change-request handler can subscribe.
- Full 29-transition matrix: [02-domain-entities-services.md:246-270](../../gap-analysis/02-domain-entities-services.md). OLD implements 29 of 30 documented transitions; NEW implements 0.
- Track-10 errata applied: SMS entirely out of scope for transition side-effects; PDF server-side generation is not a port requirement (this brief does not touch it).

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. Row-level `IMultiTenant` (ADR-004); `Appointment` implements `IMultiTenant` per `Appointment.cs:19`, so transition logic inherits the automatic tenant filter.
- [ADR-001](../../decisions/001-mapperly-over-automapper.md) -- new DTOs for transition requests (e.g. `ApproveAppointmentInput`) use Riok.Mapperly, not AutoMapper.
- [ADR-002](../../decisions/002-manual-controllers-not-auto.md) -- new endpoints (`POST /api/app/appointments/{id}/approve`, etc.) are manual controllers in `HttpApi/Controllers/Appointments/AppointmentController.cs`; every new AppService method carries `[RemoteService(IsEnabled = false)]` implicitly through the class attribute.
- [ADR-003](../../decisions/003-dual-dbcontext-host-tenant.md) -- `Appointment` is tenant-scoped; transition reads/writes automatically flow to `CaseEvaluationTenantDbContext`.
- [ADR-005](../../decisions/005-no-ng-serve-vite-workaround.md) -- Angular must continue `ng build` + `npx serve`; no `ng serve`.
- Root `CLAUDE.md` (Appointments reference-pattern) -- every new entry point extends `CaseEvaluationAppService`, carries `[RemoteService(IsEnabled = false)]`, and is wired through a manual controller.
- `Appointments/CLAUDE.md` (Business Rules) -- `AppointmentManager` is the enforcement host; `AppointmentsAppService` orchestrates and delegates.
- HIPAA: transition-audit rows record `AppointmentId + UserId + FromStatus + ToStatus + Timestamp + Reason`. No PHI (no patient name, DOB, SSN). Reason is free-text and MUST be treated as potentially-PHI-containing -- covered by encryption-at-rest + ABP AuditLogging redaction, but this brief simply forbids embedding name/DOB/SSN in the column.
- Capability-specific constraints:
  - Must not reverse the `AppointmentStatus` property to `internal` setter without a migration story for existing seed callers in `AppointmentManager.CreateAsync` (line 35 passes status as constructor arg -- already compatible). Do seal the property in the aggregate to force transitions through the domain service.
  - Must publish a transition event on success so downstream consumers (slot cascade / change-request / change-log audit / notifications / email / SMS) can subscribe without this brief needing to know them.
  - Must not depend on `scheduler-notifications` (G2-11), `email-sender-consumer` (CC-01), or `background-jobs-infrastructure` (CC-03) to compile or run. Those consume the transition events; this brief only publishes them.
  - Must preserve idempotent-retry semantics that OLD provides: calling `Approve` twice on the same appointment must not double-log, double-stamp `AppointmentApproveDate`, or double-cascade slot state.

## Research sources consulted

All accessed 2026-04-24.

- Stateless library GitHub README + NuGet page (`https://github.com/dotnet-state-machine/stateless`, `https://www.nuget.org/packages/Stateless`). HIGH confidence. Current stable 5.20.1, Apache-2.0, 30M total downloads, TFM .NET 8/9/10 + .NET Standard 2.0. Supports generic state + trigger types, guard clauses via `PermitIf` / `PermitIfAsync`, entry/exit actions via `OnEntry` / `OnExit`, async via `FireAsync` / `OnEntryAsync`, parameterized triggers, DOT + Mermaid export.
- ABP Local Event Bus docs (`https://abp.io/docs/latest/framework/infrastructure/event-bus/local`). HIGH confidence. Two publish styles: inject `ILocalEventBus` and call `PublishAsync(event)`, OR call `AddLocalEvent` on the aggregate so the event fires on DB save. Transactional -- handlers execute inside the same UoW. Auto-discovery of `ILocalEventHandler<T>` via ABP's conventional DI.
- ABP Domain Services docs (`https://abp.io/docs/latest/framework/architecture/domain-driven-design/domain-services`). HIGH confidence. Domain services host cross-entity or dependency-bearing business rules; aggregate setters should be `internal` to force callers through the service; example is `IssueManager` gating `IssueAssignedToUser`.
- ABP "Implementing Domain Driven Design" free ebook (chapter: domain services + aggregate roots). HIGH confidence. Reference for why OLD's "any field anywhere" pattern breaks invariants, and why NEW should move to method-based state changes (`appointment.Approve(approverId)` style), not property setters.
- Martin Fowler -- "Domain-Driven Design: State and Behaviour" articles + the classic "Replacing Throwing Exceptions with Notification" (`https://martinfowler.com/articles/replaceThrowWithNotification.html`). MEDIUM confidence. Motivates treating invalid-transition attempts as `UserFriendlyException` (ABP's convention) rather than silent no-ops.
- GitHub search -- ABP community repos referencing `Stateless` in aggregates. Found 3 public examples using `Stateless.StateMachine<TState, TTrigger>` injected into a DomainService, each wiring `_eventBus.PublishAsync(...)` from `OnEntry` callbacks. LOW-MEDIUM confidence (community code, not ABP-official, but the pattern is coherent with ABP DomainService idioms).

## Alternatives considered

### A. Stateless library inside `AppointmentManager` + `ILocalEventBus` for side-effect fan-out -- chosen.

Add `Stateless 5.20.1` (Apache-2.0, ABP-compatible, .NET 10 TFM). Build the 13-state graph once, statically, inside a private field on `AppointmentManager`. One new public method per transition: `ApproveAsync`, `RejectAsync`, `CheckInAsync`, `CheckOutAsync`, `BillAsync`, `RequestRescheduleAsync`, `RequestCancellationAsync`, `ConfirmRescheduleAsync`, `ConfirmCancellationAsync`, `MarkNoShowAsync`, `MarkLateCancelAsync`. Each call: (1) load aggregate, (2) construct/hydrate a `StateMachine<AppointmentStatusType, Trigger>` bound to the aggregate's current `AppointmentStatus`, (3) call `Fire(trigger, params)`, (4) Stateless throws `InvalidOperationException` on illegal transition -- translate to `UserFriendlyException` via a thin wrapper, (5) on success, `OnEntry` of the target state mutates the aggregate (set status, stamp `AppointmentApproveDate`, capture `RejectedById` / `CancelledById` / reason), (6) publish an `AppointmentStatusChangedEto` via `ILocalEventBus.PublishAsync` inside the same UoW so slot-cascade / change-request / change-log / notification handlers can subscribe.

### B. Hand-rolled switch-based transition table -- rejected.

A `Dictionary<(AppointmentStatusType from, Trigger t), AppointmentStatusType to>` in static data, checked in an `if` block before `appointment.AppointmentStatus = newStatus`. Works for 13 states but: no compile-time graph, no declarative visualisation (Stateless exports DOT + Mermaid), no typed entry/exit hook, duplication between the dictionary and the "what to stamp on entry" logic. Every new state explodes the table's maintenance cost. Rejected because Stateless is a single 130KB NuGet on Apache-2.0 that already solves it.

### C. ABP first-party state machine -- rejected (no such package).

ABP does not ship a state-machine primitive. Nearest adjacent is `Volo.Abp.StateChange` in the audit logging module, which merely records state diffs; it does not enforce transitions. Rejected as not-a-viable-alternative.

### D. Defer enforcement; leave the 13-state lifecycle advisory (current NEW state) -- rejected pending Q5.

gap-analysis Q5 (README:234) asks "Is MVP meant to enforce the 13-state lifecycle or leave it advisory (current NEW state)?" The README's executive summary (line 8) tags this as "Single biggest MVP risk" and line 376 flags it as "biggest architectural piece" gating change-requests, reschedule, cancel, check-in, bill. Recommended answer: enforce. If Adrian answers "advisory" at Q5, this capability reduces to documentation only + the slot-cascade fix (G2-02). Brief assumes enforce until Q5 resolves.

### E. MassTransit State Machines + Saga framework -- rejected.

MassTransit is a message-transport library; its saga state machine works over distributed message bus + saga persistence. Overkill for an in-process aggregate transition; introduces RabbitMQ/Azure Service Bus operational surface that MVP explicitly does not have per gap-analysis README line 9 and ADR-003. Rejected.

## Recommended solution for this MVP

Add `Stateless 5.20.1` to `src/HealthcareSupport.CaseEvaluation.Domain/HealthcareSupport.CaseEvaluation.Domain.csproj` as a single NuGet reference.

In `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/`:

1. Create `AppointmentTransitionTrigger.cs` -- public enum `AppointmentTransitionTrigger` with members `Approve`, `Reject`, `CheckIn`, `CheckOut`, `Bill`, `MarkNoShow`, `RequestReschedule`, `RequestCancellation`, `ConfirmReschedule`, `ConfirmCancellation`, `AutoCancelLate`. 11 triggers map onto the 13-state graph; `Pending` is initial-only (from create), `RescheduledNoBill/Late` and `CancelledNoBill/Late` are entry states reached only through confirmation triggers.
2. Extend `AppointmentManager.cs`:
   - Inject `ILocalEventBus` + `ICurrentUser` via constructor (`ICurrentUser` already used in ABP patterns for `Id`).
   - Seal the aggregate by narrowing `Appointment.AppointmentStatus`'s setter to `internal` (or `private set`) -- this needs a one-line audit of callers (`CreateAsync` uses constructor, not setter, so safe) and a Mapperly re-map for any DTO projection.
   - Private method `BuildMachine(Appointment appt)` returns a configured `StateMachine<AppointmentStatusType, AppointmentTransitionTrigger>`. Configure all 29 transitions inline, matching the [track-02 matrix rows 246-270](../../gap-analysis/02-domain-entities-services.md). Use `PermitIf` where business rules apply (e.g., only internal roles can call `Bill`; only Approved can reach `CheckedIn`).
   - Public method `ChangeStatusAsync(Guid appointmentId, AppointmentTransitionTrigger trigger, string? reason = null)`: loads aggregate, captures `fromStatus`, fires trigger; on failure rethrows as `BusinessException` with code `CaseEvaluation:AppointmentInvalidTransition`; on success `OnEntry` mutates status + stamps audit fields; publishes `AppointmentStatusChangedEto(AppointmentId, FromStatus, ToStatus, ActingUserId, Reason, OccurredAt)`.
3. Thin convenience methods (`ApproveAsync`, `RejectAsync`, etc.) on `AppointmentManager` call `ChangeStatusAsync` with the right trigger; reduce AppService boilerplate.

In `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/`:

4. `AppointmentStatusChangedEto.cs` -- record type: `(Guid AppointmentId, AppointmentStatusType FromStatus, AppointmentStatusType ToStatus, Guid? ActingUserId, string? Reason, DateTime OccurredAt)`. Lives in `Domain.Shared` so subscribers in other projects (Email notification, slot cascade) can reference it without creating a layering violation.

In `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/`:

5. One input DTO per transition operation: `ApproveAppointmentInput`, `RejectAppointmentInput { Reason }`, `CheckInAppointmentInput`, etc. Generic `ChangeAppointmentStatusInput { Trigger, Reason }` also possible but per-operation DTOs keep Swagger clean.
6. Extend `IAppointmentsAppService.cs` with the 11 methods. Each decorated with permission class-level stays but methods get `[Authorize(CaseEvaluationPermissions.Appointments.Edit)]` minimum; `Bill` gets a future `.Bill` permission (tracked as a follow-on, not in this brief).

In `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`:

7. Add one handler method per transition (~3 lines each) that delegates to `_appointmentManager.ChangeStatusAsync`. No business logic here, matching the existing pattern from `CreateAsync` -> `_appointmentManager.CreateAsync`.

In `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentController.cs`:

8. Per ADR-002, add 11 manual endpoints: `POST /api/app/appointments/{id}/approve`, `/reject`, `/check-in`, `/check-out`, `/bill`, `/request-reschedule`, `/request-cancellation`, `/confirm-reschedule`, `/confirm-cancellation`, `/mark-no-show`, `/mark-late-cancel`. Each delegates to the AppService method with the same name + suffix `Async`.

In `angular/src/app/proxy/`:

9. Re-run `abp generate-proxy` AFTER the backend ships. Never hand-edit. Proxy produces new TS service methods automatically.

In `angular/src/app/appointments/appointment/components/`:

10. Replace the status dropdown in `appointment-detail.component.ts` with a transition-action menu that calls the right proxy method per trigger. Post-MVP polish; initial MVP can bind status-change buttons in `appointment-view.component.ts`.

In `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/`:

11. No migration required for the machine itself (status column already exists). Follow-on briefs `appointment-full-field-snapshot` (G2-10) will add the CancelledById/RejectedById/reason columns. This brief can land without those fields -- the events still carry the data; the columns fill in when G2-10 lands.

Reference implementation of a DomainService-using-ILocalEventBus: `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` (for the event-publish pattern), and `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorManager.cs:16` (for the DomainService-mutation-then-persist pattern).

## Why this solution beats the alternatives

- Compile-time graph -- Stateless declares transitions; adding a 14th state or a new trigger is one line in `BuildMachine`. A hand-rolled switch requires changes in N places. Honours the "resolve all errors/warnings" rule by moving legality checks from runtime to declaration.
- Declarative diagram -- `StateMachine.ToDotGraph()` and `ToMermaid()` emit a graph that reviewers and stakeholders can read; satisfies the "frame architectural decisions so Adrian can explain them to non-technical stakeholders" communication rule.
- Domain-events decouple side effects -- slot cascade (G2-02), change-request creation (G2-06), change-log audit (G2-13), notifications (G2-11) each subscribe to `AppointmentStatusChangedEto` independently, so this brief ships without waiting on any of them; and they ship without changing `AppointmentManager` again. Matches ABP DDD guidance (ILocalEventBus transactional handlers).
- Matches ABP convention -- aggregate setters go `internal`/`private`; DomainService holds the rule; AppService orchestrates; manual controller routes. No pattern break.

## Effort (sanity-check vs inventory estimate)

Inventory says M (2-3 days) at [02-domain-entities-services.md:185](../../gap-analysis/02-domain-entities-services.md). Analysis confirms M: 0.5 day to add NuGet + scaffold `AppointmentTransitionTrigger` enum + `AppointmentStatusChangedEto`; 1 day to build the 13-state graph with 29 transitions + `OnEntry` mutations + event publish; 0.5 day to add 11 AppService methods + controller endpoints + permissions; 1 day for xUnit tests covering each legal transition + each illegal-transition-rejection path. Total ~3 days. Does not include slot-cascade handler (G2-02, separate brief, estimated M) or the handler for each other downstream capability. Post-merge follow-on: re-run `abp generate-proxy` + Angular UI binding (0.5 day).

## Dependencies

- Blocks:
  - [appointment-booking-cascade](appointment-booking-cascade.md) (G2-02) -- the slot-cascade handler subscribes to `AppointmentStatusChangedEto` to flip `DoctorAvailability.BookingStatusId` per OLD's switch at `AppointmentDomain.cs:586-611`.
  - [appointment-change-requests](appointment-change-requests.md) (G2-06) -- the change-request auto-creation handler subscribes to the same event filtered to `CancelledNoBill | CancellationRequested | RescheduleRequested`.
  - [appointment-change-log-audit](appointment-change-log-audit.md) (G2-13) -- the change-log writer subscribes to every event to persist an audit row. HIPAA-relevant.
  - [scheduler-notifications](scheduler-notifications.md) (G2-11) -- status-driven reminder jobs subscribe to filter-relevant transitions.
  - [email-sender-consumer](email-sender-consumer.md) (CC-01) -- email dispatch subscribes to events that map to template codes per OLD's per-status switch.
  - [appointment-full-field-snapshot](appointment-full-field-snapshot.md) (G2-10) -- adds the `CancelledById` / `RejectedById` / `ReScheduledById` / reason columns the event carries; when that brief lands, `OnEntry` callbacks can stamp those columns directly on the aggregate in addition to emitting the event.
- Blocked by: none strictly. Can compile and run with all 6 downstream handlers absent (events publish into a no-op fan-out).
- Blocked by open question: **verbatim Q5 from [gap-analysis/README.md:234](../../gap-analysis/README.md) -- "Is MVP meant to enforce the 13-state lifecycle or leave it advisory (current NEW state)? (Tracks 2, 3)"** If Adrian answers "advisory", collapse this brief to "documentation only + seal the setter" (~0.5 day) and route the full Stateless implementation to post-MVP.

## Risk and rollback

- Blast radius: medium-high. The change seals `Appointment.AppointmentStatus` to `internal` setter and adds 11 new endpoints. Any external caller that currently POSTs an update with a status field will break -- confirmed none exist today because `AppointmentUpdateDto` per `AppointmentManager.UpdateAsync` does NOT accept status anyway. Multi-tenant isolation preserved: transitions obey the ABP tenant filter automatically (Appointment is `IMultiTenant`).
- Rollback: revert the PR. The feature is additive (new endpoints + new enum + new NuGet). Reverting leaves the aggregate untouched except for the setter-visibility change, which one commit unwinds. No migration to roll back. If transitions misfire in production, a feature-flag at the AppService layer (`if (!_settings.GetOrNull("EnableStateMachine")) { fall through to old behavior; }`) protects the envelope -- tracked as a follow-on option, not required for the initial land.

## Open sub-questions surfaced by research

- Should late-cancel vs no-show be hard-timed -- e.g., a recurring job that auto-transitions past-start Approved -> NoShow? Belongs to `scheduler-notifications` (G2-11) once timed triggers are in. This brief's `MarkNoShowAsync` is a manual action only.
- Role-based transition guards: should `Bill` require a finance role, not just `Appointments.Edit`? OLD lacks the rule; TBD with Adrian. Default: require `Appointments.Edit` for all transitions until permission split is approved (new child permission `CaseEvaluation.Appointments.Bill`).
- Who can fire `RequestReschedule` / `RequestCancellation` -- external patient via the self-service route, or only internal staff? OLD allows external user via a separate `/api/appointment/request-cancel` route (see `AppointmentChangeRequestDomain`). Route belongs to the `appointment-change-requests` brief; trigger just has to exist in the machine.
- Should `OnEntry` callbacks persist the aggregate immediately or leave the Manager's `UpdateAsync` the single persistence point? ABP convention (per docs): handlers run inside the same UoW, so a single `UpdateAsync` after `Fire` captures everything. Decide in implementation; not a blocker.
- Stateless does not natively emit compile-time guarantees about every source-state-trigger-pair being defined. Need a unit test that enumerates `(from, trigger)` pairs and asserts either a permitted transition or an explicit refusal. Adds ~half a day.
