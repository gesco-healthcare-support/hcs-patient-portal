[Home](../../INDEX.md) > [Issues](../) > Research > DAT-03

# DAT-03: Reschedule Does Not Release Old Slot -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` lines 285-314, 156-159
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` lines 39-59

---

## Current state (verified 2026-04-17)

`AppointmentManager.UpdateAsync`:

```csharp
var appointment = await _appointmentRepository.GetAsync(id);
appointment.PatientId = patientId;
// ...
appointment.DoctorAvailabilityId = doctorAvailabilityId;   // new slot
// ...never touches either the OLD or the NEW DoctorAvailability entity
return await _appointmentRepository.UpdateAsync(appointment);
```

`AppointmentsAppService.DeleteAsync`: calls `_appointmentRepository.DeleteAsync(id)` (soft delete), does NOT release the slot.

Facts:

- Rescheduling from slot A to slot B: old slot stays `Booked`, new slot stays `Available` (can be double-booked).
- Cancellation via delete: slot stays `Booked` forever.
- E2E test E6 and B11.2.1 already confirmed this.
- Contradicts the documented intent in `docs/business-domain/APPOINTMENT-LIFECYCLE.md` and the feature CLAUDE.md at `src/.../Domain/Appointments/CLAUDE.md`.

---

## Official documentation

- [ABP Domain Services](https://abp.io/docs/latest/framework/architecture/domain-driven-design/domain-services) -- "The logic you need to implement is related to more than one aggregate/entity, so it doesn't properly fit in any of the aggregates". Exactly the reschedule/cancel situation.
- [ABP Entities & Aggregate Roots](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities) -- aggregates referenced by Id only, single transaction boundary, work with sub-entities through the aggregate root.
- [ABP Local Event Bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/local) -- `AddLocalEvent` on aggregates; "Event handlers are always executed in the same unit of work scope, that means in the same database transaction with the code that published the event. If an event handler throws an exception, the unit of work (database transaction) is rolled back." Events fire just before UoW completion.
- [ABP Unit of Work (DDD)](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) -- HTTP GET doesn't start transactional UoW; other HTTP verbs do. AppService methods are transactional by default for POST/PUT/DELETE.
- [Microsoft -- Domain events: Design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) -- reference implementation for coordinating aggregates via events saved in the same transaction.

## Community findings

- [The Reformed Programmer -- Event-driven architecture for EF Core](https://www.thereformedprogrammer.net/a-robust-event-driven-architecture-for-using-with-entity-framework-core/) -- running event handlers inside the original `SaveChanges` transaction; directly applicable to a `ReleaseSlotOnReschedule` handler.
- [Chris Frenzel -- Domain Events with EFCore and MediatR](https://cfrenzel.com/domain-events-efcore-mediatr/) -- concrete code pattern for raising a domain event on an aggregate mutation, handling atomically.
- [stevsharp DEV.to -- Domain Events and the Outbox Pattern with EF Core Interceptors](https://dev.to/stevsharp/reliable-messaging-in-net-domain-events-and-the-outbox-pattern-with-ef-core-interceptors-pjp) -- when slot-release needs to reach an external system, not just another aggregate.

## Recommended approach

Two acceptable shapes; pick one and commit:

### Option A -- Dedicated use-case methods on `AppointmentManager` (preferred)

Add `RescheduleAsync(appointmentId, newSlotId, concurrencyStamp)` and `CancelAsync(appointmentId)` to `AppointmentManager`:

1. Load the old `DoctorAvailability`, set `BookingStatusId = Available`.
2. Load the new `DoctorAvailability`, validate `BookingStatusId == Available`, set to `Booked`.
3. Update `appointment.DoctorAvailabilityId`.
4. Single UoW, atomic commit.
5. Backed by the same per-slot `IAbpDistributedLock` as [DAT-01](DAT-01.md) to prevent races with concurrent bookings of the same old/new slot.

AppService methods become thin orchestrators:
- `AppointmentsAppService.UpdateAsync` calls `_appointmentManager.UpdateAsync` if slot unchanged, else `_appointmentManager.RescheduleAsync`.
- `AppointmentsAppService.DeleteAsync` calls `_appointmentManager.CancelAsync(id)` before `_appointmentRepository.DeleteAsync(id)`.

### Option B -- Domain event on `Appointment`

`appointment.Reschedule(newSlotId)` raises `AppointmentRescheduled` via `AddLocalEvent`; a `LocalEventHandler<AppointmentRescheduled>` updates the two slots. Runs in same UoW transaction per ABP docs, so atomic. Trade-off: more indirection, better decoupling.

Option A is simpler for a 13-state healthcare workflow; Option B is better if you anticipate multiple handlers (email, SMS, audit).

## Gotchas / blockers

- **Soft delete ordering**: ABP's data filter hides soft-deleted appointments, so "find appointments referencing this slot" after soft-delete returns nothing. Release the slot BEFORE (or in the same UoW as) the soft-delete.
- `IDataFilter.Disable<ISoftDelete>()` is needed if you ever have to find the soft-deleted appointment that held the slot.
- If a domain-event handler fails, the entire UoW rolls back INCLUDING the appointment update. That's usually desired for reschedule, but document it.
- `ConcurrencyStamp` on `Appointment` protects the Appointment aggregate only; a concurrency token on `DoctorAvailability` (per DAT-01) is still required or two reschedulers racing to the same new slot can collide.
- ABP's automatic transaction applies to POST/PUT/DELETE only; if this ever moves to a GET endpoint it becomes non-transactional by default.

## Open questions

- **Product decision**: when rescheduling, should the old slot become `Available` or an intermediate state like `Released` to support audit/billing trails?
- **Product decision**: which `AppointmentStatus` transitions should also release the slot? (Cancelled*, NoShow, etc.) -- needs a state-transition table from the business. Overlaps heavily with [FEAT-01](FEAT-01.md).
- Is there any external integration that watches slot state changes? If so, `IDistributedEventBus` is correct over `ILocalEventBus`.

## Related

- [DAT-01](DAT-01.md) -- same per-slot lock serves this path
- [FEAT-01](FEAT-01.md) -- the status workflow dictates which transitions release slots
- [docs/issues/DATA-INTEGRITY.md#dat-03](../DATA-INTEGRITY.md#dat-03-reschedule-does-not-release-the-old-slot)
