# DoctorAvailability booking cascade (reschedule / cancel / delete)

## Source gap IDs

- G2-02 (track 02) -- `../gap-analysis/02-domain-entities-services.md` line 186: "DoctorAvailability booking cascade on reschedule/cancel/delete". Inventory effort M (1-2 days).

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs:16-45` -- aggregate root. `BookingStatusId` is `BookingStatus` enum (Available=8, Booked=9, Reserved=10, per `Domain.Shared/Enums/BookingStatus.cs`). Slot carries `LocationId`, `AppointmentTypeId?`, `AvailableDate`, `FromTime`, `ToTime`, `TenantId?`. `FullAuditedAggregateRoot<Guid>` gives it a `ConcurrencyStamp`.
- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs:14-47` -- DomainService with only `CreateAsync` and `UpdateAsync`. Both accept `bookingStatusId` as a parameter; no state-machine method (e.g. `MarkBooked`, `ReleaseBooking`). `UpdateAsync` at line 43 unconditionally overwrites `BookingStatusId` -- any caller can set any value with no guard, and the CLAUDE.md at `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md:110` confirms "No validation prevents changing a Booked slot back to Available (even if an appointment references it)".
- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md:51-57` -- explicit warning: "Slots transition from Available to Booked when an Appointment is created, but deleting the Appointment does NOT release the slot back to Available. There is no reverse transition." This is the behaviour G2-02 mandates fixing.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:162-205` -- `CreateAsync`. Line 201-202 mutates slot directly: `doctorAvailability.BookingStatusId = BookingStatus.Booked; await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);`. Bypasses `DoctorAvailabilityManager.UpdateAsync`, so no concurrency stamp increment path. Validation at `:235-262` refuses to re-book if `BookingStatusId != Available`, so two concurrent creates fighting for the same slot produce one success and one `UserFriendlyException` -- but the two SELECTs can overlap between the validation read and the UPDATE write.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:156-159` -- `DeleteAsync` is literally `await _appointmentRepository.DeleteAsync(id);` with no slot handling. Confirms G2-02 delete path gap.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:294-324` -- `UpdateAsync` calls `_appointmentManager.UpdateAsync(...DoctorAvailabilityId...)` but neither side rebalances `BookingStatusId` when the slot pointer changes. A reschedule-by-update leaks the old slot as Booked and does not reserve the new one.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs:41-55` (per `Appointments/CLAUDE.md:42-55`) -- `AppointmentStatus` is the `AppointmentStatusType` enum (13 states, Pending=1 .. Billed=11 .. CancellationRequested=13). Per `Appointments/CLAUDE.md:58-67`, "No domain methods enforce valid transitions" -- the state-machine capability (G2-01) is the companion gap that publishes the events this cascade handler will subscribe to.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` (60 lines, per `02-domain-entities-services.md:126`) -- slim manager doing null/length validation only. No `IDistributedEventBus` / `ILocalEventBus` publication today, so the companion state-machine work must add it before this cascade can subscribe.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:5-6,53,226-232` -- `Medallion.Threading.Redis` and `AbpDistributedLockingModule` already wired. `IDistributedLockProvider` is registered as a singleton backed by Redis when `Redis:Configuration` is set; a dev fallback is needed because the provider registration `return` short-circuits when Redis config is absent (line 226-227). HIGH confidence the plumbing exists; solution can rely on `IAbpDistributedLock` from `Volo.Abp.DistributedLocking` without adding packages.
- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs` -- custom repo (per `DoctorAvailabilities/CLAUDE.md:14`). Existing methods cover nav-prop queries; a cascade handler can call `IRepository<DoctorAvailability,Guid>.GetAsync(id)` directly and does not require new repo methods.
- No `ILocalEventHandler<AppointmentStatusChangedEto>` or `AppointmentStatusChangedEto` type exists today (grep returned zero hits for the ETO type in `src/`). Both are new artefacts delivered by G2-01 and consumed here.

## Live probes

Three read-only probes planned; no POST/PUT/DELETE against running services.

- Probe 1 -- `curl -sk -H "Authorization: Bearer <REDACTED>" "https://localhost:44327/api/app/doctor-availabilities?MaxResultCount=5"`. Purpose: observe live `bookingStatusId` field shape on `DoctorAvailabilityWithNavigationPropertiesDto` and confirm the enum is serialized as `int` (8 / 9 / 10). If the tenant database has zero slots, the probe still proves the endpoint exists and returns `{ totalCount, items:[] }` (consistent with service-status.md note that appointments table is empty).
- Probe 2 -- Swagger scan. `curl -sk https://localhost:44327/swagger/v1/swagger.json | grep -i "doctor-availabilities"`. Purpose: enumerate delete-by-date and delete-by-slot endpoints under `/api/app/doctor-availabilities` (documented in CLAUDE.md at line 104 as three delete modes) so the cascade design can confirm there is no existing `ReleaseBooking` verb it must piggyback.
- Probe 3 -- OIDC metadata re-check (reuses service-status.md) -- no new call, just pin the Bearer-token source so the probe log is reproducible.
- Full log at `../probes/appointment-booking-cascade-2026-04-24T13-00-00.md`.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs:199-239` (`Add` booking cascade): external user booking drives slot to `Reserved` + appointment `Pending`; internal user booking drives slot to `Booked` + appointment `Approved`. Source of the rule that OLD's cascade is external-vs-internal-aware, not pure status-aware.
- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs:431-448` (`Update` reschedule cascade): if `oldAppointment.DoctorAvailabilityId != appointment.DoctorAvailabilityId`, new slot goes Booked AND old slot goes Available. This is the swap-cascade behaviour the NEW reschedule path must preserve.
- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs:586-611` (`UpdateDoctorAvailbilty` -- typo is OLD's): explicit status->slot mapping. `Pending => Reserved`, `Approved => Booked`, `Rejected => Available`, `CancelledNoBill => Available`, else no-op. This is the canonical cascade table the NEW handler implements.
- OLD has no cascade for `CheckedIn`, `CheckedOut`, `Billed`, `NoShow`, `CancelledLate`, `RescheduledNoBill`, `RescheduledLate`, `RescheduleRequested`, `CancellationRequested` in `UpdateDoctorAvailbilty`. This is a deliberate gap in OLD (post-Approved terminal lifecycle reuses the slot-as-Booked state); document it so the NEW table does not accidentally widen behaviour.
- Not listed in the 4 track-10 errata (PDF server-side, SMS disabled, scheduler hardcoded-1, CustomField fixed-type) -- G2-02 is unmodified by `docs/gap-analysis/10-deep-dive-findings.md`. Track-10 erratum on SMS/scheduler is orthogonal.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, EF Core. Riok.Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003), doctor-per-tenant (ADR-004). None reversed by this brief.
- Row-level `IMultiTenant` on `DoctorAvailability` and `Appointment` (per `DoctorAvailabilities/CLAUDE.md:68-75`). ABP's automatic tenant filter resolves scoping; the cascade must not disable the filter.
- **Coupled with appointment-state-machine (G2-01).** The cascade is an event handler; it requires G2-01 to emit a status-change event. Without G2-01, the cascade has nothing to subscribe to and the only integration point is inside `AppointmentsAppService` (which violates ABP's preference for loose coupling through the event bus). The brief therefore delivers both: (a) the event type (delivered by G2-01) and (b) the cascade handler (this capability).
- **Race safety.** Two booking requests targeting the same slot must serialize. The handler must acquire an `IAbpDistributedLock` keyed on `slot:{doctorAvailabilityId}` before reading and writing slot state. ABP's distributed-lock module is already in the composition at `CaseEvaluationHttpApiHostModule.cs:53`; the Redis provider is used in non-dev (line 229-232), with ABP's in-memory fallback for development. No new packages.
- **Concurrency stamp.** `DoctorAvailabilityManager.UpdateAsync` already increments the stamp through `SetConcurrencyStampIfNotNull`. The cascade must use the manager (not the raw repository `UpdateAsync`) to keep stamps monotone and to make an unrelated concurrent edit from the admin UI fail cleanly.
- HIPAA applicability: slot state is appointment-metadata, not PHI (no patient name, DOB, claim number, or diagnosis). Audit via ABP's `FullAudited*` is sufficient; no bespoke PHI audit required from this capability.
- **No writes outside `docs/implementation-research/`** on this branch (per research protocol). Implementation sits behind the gate of this plan.

## Research sources consulted (accessed 2026-04-24)

Minimum-3 rule -- 6 URLs, mixed authoritative sources.

1. ABP Docs -- Local Event Bus. `https://abp.io/docs/latest/framework/infrastructure/event-bus/local`. HIGH confidence source. Covers `ILocalEventBus`, `ILocalEventHandler<TEto>`, auto-registration via `ITransientDependency`, and Unit-of-Work delivery semantics (handlers fire inside the same UoW as the publisher by default; use `TransactionBehavior` to defer until commit).
2. ABP Docs -- Distributed Event Bus. `https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed`. HIGH confidence. Explains when to use distributed (cross-service) vs local. Since this handler lives in-process alongside the publisher (both inside HttpApi.Host), local event bus is sufficient and avoids configuring RabbitMQ/Kafka.
3. ABP Docs -- Distributed Locking. `https://abp.io/docs/latest/framework/infrastructure/distributed-locking`. HIGH confidence. Documents `IAbpDistributedLock.TryAcquireAsync(string name, TimeSpan timeout)` returning `IAbpDistributedLockHandle?`. Medallion provider in non-dev; in-memory for dev. Pattern: `await using var handle = await _distributedLock.TryAcquireAsync("slot:" + id);`.
4. ABP Docs -- `IRepository` and Unit of Work. `https://abp.io/docs/latest/framework/architecture/domain-driven-design/repositories`. HIGH confidence. Confirms `IRepository<DoctorAvailability,Guid>.GetAsync` already participates in the ambient UoW; no manual transaction wrapping needed.
5. Microsoft Learn -- EF Core concurrency tokens. `https://learn.microsoft.com/en-us/ef/core/saving/concurrency`. HIGH confidence. Matches ABP's `ConcurrencyStamp` behaviour (optimistic concurrency with `DbUpdateConcurrencyException`).
6. Medallion.Threading (GitHub README). `https://github.com/madelson/DistributedLock`. HIGH confidence. Confirms the Redis provider behaviour the ABP module wraps. Not strictly needed unless the cascade needs to configure per-call retry; ABP's `IAbpDistributedLock` wrapper already abstracts it.

No dead-end searches required; all claims verified against at least one canonical source.

## Alternatives considered

1. **Event handler on `AppointmentStatusChangedEto` published by the state-machine (G2-01).** Chosen. One handler class, `DoctorAvailabilityBookingCascadeHandler : ILocalEventHandler<AppointmentStatusChangedEto>`, lives under `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/EventHandlers/`. Subscribes to each status transition and applies the OLD cascade table (`Pending => Reserved`, `Approved => Booked`, `Rejected | CancelledNoBill | CancelledLate | RescheduledNoBill | RescheduledLate | RescheduleRequested | CancellationRequested => Available`). Loose coupling: the state machine does not import the DoctorAvailability assembly. Testable in isolation (mock the bus, assert the handler side-effects). Cascade on delete handled the same way -- the state-machine publishes a `Deleted` event (or we emit `AppointmentStatusChangedEto` with `NewStatus = null` in the delete path) and the handler frees the slot.
2. **Direct in-line call from `AppointmentsAppService.CreateAsync`/`DeleteAsync`/`UpdateAsync` into `DoctorAvailabilityManager`.** Rejected. Couples application-service code to the cascade semantics; breaks the intent of G2-01 (a single source of truth for transitions); duplicates the status-mapping logic in every AppService that mutates `AppointmentStatus`.
3. **Database trigger on `Appointments` writing through to `DoctorAvailabilities`.** Rejected. Takes business logic outside the app (breaks ABP's DDD discipline), bypasses the multi-tenant filter, and defeats optimistic concurrency (triggers do not update `ConcurrencyStamp`).
4. **Scheduled reconciliation job (nightly sweep that recomputes slot state from appointments).** Rejected as primary mechanism. Good as a safety-net but unacceptable as the MVP solution because users expect immediate slot availability after a cancel. Can be added later as a defence-in-depth job if an audit finds drift.
5. **Entity-change audit handler on `Appointment`.** Rejected. ABP's `EntityUpdatedEventData<Appointment>` fires on every update, including fields that are not status (e.g., PanelNumber edits). Forces the handler to inspect `ChangedProperties`, which is brittle and couples the cascade to EF change-tracking details.

## Recommended solution for this MVP

Add `DoctorAvailabilityBookingCascadeHandler : ILocalEventHandler<AppointmentStatusChangedEto>, ITransientDependency` under `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/EventHandlers/`. The handler injects `DoctorAvailabilityManager` (for stamp-aware updates), `IRepository<DoctorAvailability,Guid>` (for `GetAsync(id)`), and `IAbpDistributedLock`. On `HandleEventAsync(AppointmentStatusChangedEto eto)`:

1. Acquire `IAbpDistributedLock.TryAcquireAsync("slot:" + eto.DoctorAvailabilityId, TimeSpan.FromSeconds(5))`. If null, throw `BusinessException` so ABP's UoW rolls back and the caller retries.
2. Load `slot = await _repo.GetAsync(eto.DoctorAvailabilityId)` (tenant filter already scoped because the handler runs inside the publisher's UoW).
3. Map `eto.NewStatus -> BookingStatus` per the OLD table: `Pending => Reserved`, `Approved => Booked`, `Rejected | CancelledNoBill | CancelledLate | RescheduledNoBill | RescheduledLate | RescheduleRequested | CancellationRequested => Available`, else no-op (CheckedIn / CheckedOut / NoShow / Billed preserve existing value -- slot stays Booked for the appointment's lifetime).
4. If `eto.OldDoctorAvailabilityId.HasValue && eto.OldDoctorAvailabilityId != eto.DoctorAvailabilityId` (reschedule to a different slot), acquire the second slot lock in a stable Guid-order to avoid deadlock, load the old slot, set it to `Available`, update via manager.
5. Call `await _manager.UpdateAsync(slot.Id, slot.LocationId, slot.AppointmentTypeId, slot.AvailableDate, slot.FromTime, slot.ToTime, newBookingStatus, slot.ConcurrencyStamp)` so the stamp increments and a contending admin-UI edit sees `DbUpdateConcurrencyException`.
6. Delete path: `AppointmentsAppService.DeleteAsync` is rewritten (by G2-01, delivered together) to publish `AppointmentStatusChangedEto { NewStatus = null, DoctorAvailabilityId = <snapshot before delete>, OldDoctorAvailabilityId = null }`. The handler treats `null` newStatus as release: slot goes `Available`.

Shape summary: entity (existing) -> domain event ETO (new, in Domain.Shared) -> handler (new) -> manager update (existing) -> no new repository method, no new AppService endpoint, no new DTO. Also updates `AppointmentsAppService.CreateAsync` (line 201-202) to emit the same ETO instead of mutating the slot directly, so all slot state changes flow through the handler (single source of truth for cascade behaviour). Migration-free.

## Why this solution beats the alternatives

- **Loose coupling.** The state-machine module does not reference `DoctorAvailabilities`; the handler does not reference the Appointment AppService. Matches ABP's canonical pattern for cross-aggregate reactions.
- **Race-safe.** Distributed lock keyed on slot ID serializes concurrent booking attempts. Optimistic concurrency stamp catches unrelated admin-UI writes.
- **ABP-native.** Reuses `ILocalEventHandler`, `DoctorAvailabilityManager`, `IAbpDistributedLock`, and the already-registered `AbpDistributedLockingModule`. No new infrastructure.
- **Easy to test.** The handler is a 40-line class testable with a mocked event bus, in-memory repo, and in-memory distributed lock provider -- no end-to-end scaffolding required.

## Effort (sanity-check vs inventory estimate)

Inventory (`02-domain-entities-services.md:186`): M (1-2 days).

Confirm M -- 1 day for the handler class, the ETO type (in `Domain.Shared`), the change in `AppointmentsAppService.CreateAsync`/`DeleteAsync`/`UpdateAsync` to publish instead of mutate, plus two unit tests (single-status cascade and reschedule swap). Extra half-day for race-condition test (two concurrent `Task`s against an in-memory lock) and for wiring the event publication from the G2-01 state machine.

## Dependencies

- **Blocks:** `appointment-change-requests` (reschedule / cancel change-request workflow commits a status transition that this handler translates into slot moves). Without the cascade, reschedule/cancel leak slot state. Also blocks `appointment-search-listview` only in the cosmetic sense that the listview relies on accurate `bookingStatusId` values.
- **Blocked by:** `appointment-state-machine` (G2-01) -- the state machine must publish `AppointmentStatusChangedEto` (name / payload co-designed) before this handler has anything to subscribe to.
- **Blocked by open question:** none. Q5 ("Is the 13-state lifecycle meant to be enforced, or advisory?") gates the state-machine decision itself (G2-01), not this cascade. If Q5 lands on "advisory only" and G2-01 is descoped, this cascade can still be implemented as a direct call from `AppointmentsAppService.DeleteAsync`/`UpdateAsync` into `DoctorAvailabilityManager` (alternative 2), at the cost of losing the loose coupling. Flag in Q5 follow-up.

## Risk and rollback

- **Blast radius.** Scope is two files under `Domain/DoctorAvailabilities/EventHandlers/` + one ETO in `Domain.Shared/Appointments/` + the three mutation paths in `AppointmentsAppService`. If the handler throws unexpectedly, the UoW aborts, the appointment status change rolls back, and slot state is unchanged. No database migration.
- **Concurrency failure mode.** If the Redis lock provider is unavailable in production, `IAbpDistributedLock` falls back to ABP's local in-memory lock. In a multi-node deployment (not current MVP topology) this would silently drop the cross-node guarantee. Mitigate by adding a health check on Redis connection and a config flag that refuses to start without a distributed lock backend in `Production`.
- **Rollback.** Remove the handler class registration (delete the file) and revert the `AppointmentsAppService` mutation paths to the current direct-update code. No schema change to undo. Slot state drift that accumulated while the handler was live can be reconciled with a one-off SQL `UPDATE DoctorAvailabilities SET BookingStatusId = 8 WHERE Id NOT IN (SELECT DoctorAvailabilityId FROM Appointments WHERE AppointmentStatus IN (2,9,10,11)) AND BookingStatusId = 9`. Gate the rollback on G2-01 being reverted in the same commit (coupled capabilities).

## Open sub-questions surfaced by research

- Should `BookingStatus.Reserved` (10) be used at all? OLD uses it only for external-user bookings in `Pending` state. If G2-01 enforces the 13-state lifecycle, then Pending is a legitimate state for internal bookings too -- do we want internal `Pending` slots Reserved or Booked? Recommend Reserved for both (OLD parity on the external path, behaviour change on the internal path), but surface to Adrian.
- What about `Blocked` (admin override, e.g., doctor takes the slot off-market)? OLD had no such state; NEW's `BookingStatus` enum also lacks it. Post-MVP: either add `Blocked=11` to the enum or repurpose `Reserved`. Not in MVP scope.
- If an appointment is re-approved after being `CancelledNoBill` and a different appointment has meanwhile booked into the same slot, the cascade must refuse the re-approval (second Appointment.BookingStatusId validation at line 237-240 would have already failed on the create path, but a direct status update from the state machine would need an equivalent guard in the handler). Recommend the handler re-run the slot-is-Available check before flipping back to Booked and throw `BusinessException` if the slot has been taken. Flag to G2-01 author.
