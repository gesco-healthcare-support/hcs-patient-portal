---
status: draft
issue: slot-rework-phase-2-domain-logic
owner: AdrianG
created: 2026-05-15
approach: tdd (pure domain logic + AppService validators) + code
  (event-handler edits where the test would re-test ABP plumbing)
sequence: 3 of 7 (slot-generation + doctor-invariant series)
depends-on: 2026-05-15-slot-rework-phase-1-schema.md (Capacity +
  join entity must exist in code)
branch: create a new branch off `feat/replicate-old-app`. PR back
  to `feat/replicate-old-app`. Do not merge to `main` until plans
  2 through 7 are merged together.
---

# Slot rework Phase 2: capacity-aware booking domain logic

## Goal

Make the booking flow capacity-aware. Specifically:

1. Add a new repository method
   `IAppointmentRepository.GetActiveCountForSlotAsync(Guid slotId)`
   that returns the count of "active" appointments tied to a
   `DoctorAvailability` (excludes the four terminal-free statuses
   listed below).
2. Replace the existing single-status booking gate in
   `AppointmentsAppService.ValidateDoctorAvailabilityForBooking`
   with the new capacity-aware `bookable` predicate.
3. Add three new error codes for the three rejection arms:
   - `AppointmentBookingSlotFull` -- capacity hit.
   - `AppointmentBookingSlotClosed` -- slot was manually closed
     (BookingStatusId = Reserved per the repurpose).
   - `AppointmentBookingSlotTypeMismatch` -- requested type not
     in the slot's non-empty AppointmentTypes set.
4. Repurpose `BookingStatus.Reserved` as "manually closed --
   never bookable regardless of capacity". Remove every
   automated writer that flips a slot to `Reserved` on
   appointment status transitions.
5. Remove the existing `BookingStatusId = Booked` flip from the
   `SlotCascadeHandler` so a slot does not become unbookable
   just because one appointment landed on it. The slot stays
   `Available`; the `bookable` predicate consults `Capacity`
   minus active count instead.
6. Update `DoctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync`
   to expose `remainingCapacity` per slot so the booking-form
   picker (plan 5) can render "3 remaining" and disable the
   selection when count hits 0.

## Why

Plan 1 (doctor invariant) and plan 2 (Phase 1 schema) move the
data model. This plan moves the behaviour. The slot-generation
rework plan
(`W:\patient-portal\main\docs\plans\2026-05-15-slot-generation-rework.md`)
section "Phase 2 -- Domain logic" prescribes:

> `bookable(slot, requestedType, now) =
>   slot.AvailableDate >= now+leadTime
>   && BookingStatusId == Available
>   && (Capacity - activeAppointmentCount) > 0
>   && (AppointmentTypes empty OR contains requestedType)`

Today the gate is single-row (`BookingStatusId == Available`)
and the `SlotCascadeHandler` flips slots between Available /
Booked / Reserved per a 14-state mapping
(`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Handlers/SlotCascadeHandler.cs:133-158`).
That mapping was designed for "slot ties to exactly one
appointment". With Capacity it stops making sense -- if Capacity
is 3 and one appointment lands, flipping the slot to Booked
prevents the next two patients from booking.

The clean repurpose is: `Reserved` means "manually closed by
the doctor's-admin" (urgent override for emergency holds,
training days, etc.); `Available` means "the predicate is
authoritative"; `Booked` is unused going forward but stays in
the enum for backward compatibility (existing rows in pre-rework
tenants are migrated to `Available` in a data fix, see step 8
below).

The active-count probe is the authoritative measure of "how
many appointments currently tie to this slot". A slot that holds
3 patients and has Capacity 3 is full. The four statuses that
DO NOT count as "active" -- because they have released the slot
already -- are:

- `Rejected` (slot was freed; the patient was turned away)
- `CancelledNoBill` (slot was freed cleanly)
- `CancelledLate` (slot was freed; just billed)
- `RescheduledNoBill` / `RescheduledLate` (slot was freed when
  the patient was moved to a different slot)

Other terminal statuses (`NoShow`, `Billed`) DO count because
they still represent "this slot was used by that patient and
the practice has the row in its books".

## Non-goals

- No UI work (plans 4-6).
- No new DTO surface beyond `RemainingCapacity` (a single new
  property on `DoctorAvailabilityDto`).
- No removal of the `BookingStatus` enum values. `Booked` stays
  so existing data and the `SlotCascadeHandler`'s historic emit
  path remain compilable.
- No change to the lead-time / max-time gates in
  `BookingPolicyValidator` -- those stay as-is and run BEFORE
  the new capacity gate.
- No change to the `DeleteAsync` / `DeleteBySlotAsync` /
  `DeleteByDateAsync` paths from
  `DoctorAvailabilitiesAppService`. They already guard against
  `Reserved` / `Booked` (`HasInFlightStatus` helper). The
  semantics change: `Reserved` now means "manually closed" and
  must continue to block delete; `Booked` is effectively a
  legacy value that the bulk-delete loop will treat the same
  way it always did.

## Decisions locked

1. **Active-count excludes 5 statuses**: `Rejected`,
   `CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`,
   `RescheduledLate`. Everything else counts.

2. **`Reserved` is manual-close-only**. The
   `SlotCascadeHandler` MUST NOT write `Reserved`. The only
   path that produces `Reserved` is an admin's explicit
   `DoctorAvailabilitiesAppService.UpdateAsync` with
   `BookingStatusId = Reserved` (the slot is being closed).

3. **`SlotCascadeHandler` keeps the event subscription** but
   becomes a no-op for status transitions. The handler does NOT
   flip the slot's `BookingStatusId` anymore on Appointment
   status changes; the slot stays `Available` until the
   capacity gate naturally locks it out. The handler still
   handles the **reschedule-swap** case (move an appointment
   from slot A to slot B) by triggering a recount, but that
   recount is computed server-side, not stamped onto the slot.
   Realistically the handler becomes a 5-line log-only stub
   for transitions and a no-op for swaps; the original 14-state
   mapping is deleted entirely.

4. **`RemainingCapacity` is computed on read, not stored**.
   Stored remaining-count would race with concurrent bookings;
   the recount on the booking-form lookup endpoint is cheap
   enough (small N per location per date range). The active-
   count repo method is the single source of truth.

5. **Race condition under concurrent bookings**: closed at the
   AppService boundary by wrapping the capacity check + create
   in a transactional UoW and adding a `SELECT ... WITH
   (UPDLOCK)` row-lock hint on the slot read. See step 5 below.
   Tested in plan 7's HRD-R2.10.x scenarios.

6. **Three new error codes** all map to HTTP 400 via
   `CaseEvaluationHttpApiHostModule`. Localization keys go in
   `en.json`.

## Files touched

### 1. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs`

Add three new const strings:

```csharp
/// <summary>
/// 2026-05-15 -- raised by
/// <c>AppointmentsAppService.ValidateDoctorAvailabilityForBooking</c>
/// when the slot's <c>Capacity - activeAppointmentCount</c> is 0
/// (full). Carries <c>capacity</c> + <c>activeCount</c> as
/// <c>WithData</c>. Localization key
/// <c>Appointment:BookingSlotFull</c>.
/// </summary>
public const string AppointmentBookingSlotFull =
    "CaseEvaluation:Appointment.BookingSlotFull";

/// <summary>
/// 2026-05-15 -- raised by
/// <c>AppointmentsAppService.ValidateDoctorAvailabilityForBooking</c>
/// when the slot's <c>BookingStatusId</c> is <c>Reserved</c>
/// (manually closed by the doctor's-admin). Localization key
/// <c>Appointment:BookingSlotClosed</c>.
/// </summary>
public const string AppointmentBookingSlotClosed =
    "CaseEvaluation:Appointment.BookingSlotClosed";

/// <summary>
/// 2026-05-15 -- raised by
/// <c>AppointmentsAppService.ValidateDoctorAvailabilityForBooking</c>
/// when the slot's <c>AppointmentTypes</c> set is non-empty and
/// the requested <c>AppointmentTypeId</c> is not in it.
/// Carries <c>requested</c> + <c>permitted</c> ids as <c>WithData</c>.
/// Localization key <c>Appointment:BookingSlotTypeMismatch</c>.
/// </summary>
public const string AppointmentBookingSlotTypeMismatch =
    "CaseEvaluation:Appointment.BookingSlotTypeMismatch";
```

### 2. `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`

Add HTTP 400 mappings for the three new codes (same block as
plan 1's mappings).

### 3. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`

Add three new keys:

```jsonc
"Appointment:BookingSlotFull": "This time slot is full ({activeCount} of {capacity} booked). Please pick another time.",
"Appointment:BookingSlotClosed": "This time slot has been closed by the clinic. Please pick another time.",
"Appointment:BookingSlotTypeMismatch": "This time slot is not available for the selected appointment type. Please pick another time."
```

### 4. `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/IAppointmentRepository.cs`

Add the new method:

```csharp
/// <summary>
/// 2026-05-15 -- counts the appointments tied to the given slot
/// that are NOT in a "slot-freed" terminal status. The five
/// freed statuses excluded are: Rejected, CancelledNoBill,
/// CancelledLate, RescheduledNoBill, RescheduledLate. Caller
/// uses this count against the slot's Capacity to determine
/// whether the slot is bookable.
/// </summary>
Task<long> GetActiveCountForSlotAsync(
    Guid doctorAvailabilityId,
    CancellationToken cancellationToken = default);
```

### 5. `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs`

Implement the new method:

```csharp
public virtual async Task<long> GetActiveCountForSlotAsync(
    Guid doctorAvailabilityId,
    CancellationToken cancellationToken = default)
{
    var query = (await GetDbSetAsync())
        .Where(x => x.DoctorAvailabilityId == doctorAvailabilityId)
        .Where(x =>
            x.AppointmentStatus != AppointmentStatusType.Rejected &&
            x.AppointmentStatus != AppointmentStatusType.CancelledNoBill &&
            x.AppointmentStatus != AppointmentStatusType.CancelledLate &&
            x.AppointmentStatus != AppointmentStatusType.RescheduledNoBill &&
            x.AppointmentStatus != AppointmentStatusType.RescheduledLate);

    return await query.LongCountAsync(GetCancellationToken(cancellationToken));
}
```

### 6. `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`

Replace `ValidateDoctorAvailabilityForBooking` (line 808-...).
Inject `IAppointmentRepository` (already injected as
`_appointmentRepository`) -- use the new method directly.

```csharp
private async Task ValidateDoctorAvailabilityForBooking(
    AppointmentCreateDto input,
    DoctorAvailability doctorAvailability)
{
    // 2026-05-15 -- capacity-aware booking gate. The single
    // BookingStatusId == Available gate is replaced by a
    // four-arm predicate: status not closed, capacity not
    // exhausted, type permitted, lead-time + max-time honored
    // (the last two are upstream in BookingPolicyValidator).

    // Arm 1: explicit manual close.
    if (doctorAvailability.BookingStatusId == BookingStatus.Reserved)
    {
        throw new BusinessException(
            CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed);
    }

    // Arm 2: capacity. We load the slot via WithDetailsAsync to
    // pull the AppointmentTypes M2M list for Arm 3, then ask
    // the repo for the active-count.
    var activeCount = await _appointmentRepository
        .GetActiveCountForSlotAsync(doctorAvailability.Id);

    if (activeCount >= doctorAvailability.Capacity)
    {
        throw new BusinessException(
            CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull)
            .WithData("capacity", doctorAvailability.Capacity)
            .WithData("activeCount", activeCount);
    }

    // Arm 3: type membership.
    if (doctorAvailability.AppointmentTypes.Count > 0 &&
        !doctorAvailability.AppointmentTypes.Any(at =>
            at.AppointmentTypeId == input.AppointmentTypeId))
    {
        throw new BusinessException(
            CaseEvaluationDomainErrorCodes.AppointmentBookingSlotTypeMismatch)
            .WithData("requested", input.AppointmentTypeId)
            .WithData("permitted", string.Join(",",
                doctorAvailability.AppointmentTypes.Select(at => at.AppointmentTypeId)));
    }

    // Arms 4 + 5 (location match + date-component match) stay
    // from the OLD code at line 813-828; preserve them verbatim.
    if (doctorAvailability.LocationId != input.LocationId)
    {
        throw new UserFriendlyException(
            L["The selected availability slot does not match the chosen location."]);
    }

    if (doctorAvailability.AvailableDate.Date != input.AppointmentDate.Date)
    {
        throw new UserFriendlyException(
            L["The selected availability slot date does not match the chosen appointment date."]);
    }

    var timeOfDay = TimeOnly.FromTimeSpan(input.AppointmentDate.TimeOfDay);
    if (timeOfDay < doctorAvailability.FromTime ||
        timeOfDay >= doctorAvailability.ToTime)
    {
        throw new UserFriendlyException(
            L["The selected availability slot time does not match the chosen appointment time."]);
    }
}
```

Replace the call site at line 643 in `CreateAsync` -- the new
method is async, so the call becomes `await ValidateDoctorAvailabilityForBooking(...)`.

**Load the slot with `WithDetailsAsync`**. The existing line
637 `var doctorAvailability = await _doctorAvailabilityRepository.FindAsync(...)`
does NOT pull the M2M collection. Replace with:

```csharp
var slotQueryable = await _doctorAvailabilityRepository
    .WithDetailsAsync(x => x.AppointmentTypes);
var doctorAvailability = await AsyncExecuter.FirstOrDefaultAsync(
    slotQueryable.Where(x => x.Id == input.DoctorAvailabilityId));
if (doctorAvailability == null)
{
    throw new UserFriendlyException(L["..."]);
}
```

**Critical race fix**: wrap the validation + the Appointment
insert in a transactional UoW with a row-lock hint. ABP
provides `_unitOfWorkManager` (inject if absent); the lock is
EF-level via `FOR UPDATE` equivalent on SQL Server (`WITH
(UPDLOCK, HOLDLOCK)`). In ABP repos, the idiom is:

```csharp
using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
{
    var slot = await LoadSlotWithLockAsync(input.DoctorAvailabilityId);
    await ValidateDoctorAvailabilityForBooking(input, slot);
    var appointment = await _appointmentManager.CreateAsync(...);
    await uow.CompleteAsync();
    return appointment;
}
```

Where `LoadSlotWithLockAsync` runs an explicit
`FromSqlRaw("SELECT * FROM AppEntity.DoctorAvailabilities WITH (UPDLOCK, HOLDLOCK) WHERE Id = {0}", id)`
+ a second `.Include(x => x.AppointmentTypes).LoadAsync()` to
hydrate the M2M list. The raw SQL is necessary because EF
Core's LINQ does not emit lock hints; FromSqlRaw + tagged
hint is the standard pattern (Microsoft docs:
`docs.microsoft.com/en-us/ef/core/querying/raw-sql`).

### 7. `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Handlers/SlotCascadeHandler.cs`

Reduce to a 6-line no-op-with-logging stub. The capacity gate
on the booking path is now authoritative; the handler should
not stamp the slot anymore. Keep the subscription so future
plans can re-introduce side effects without re-wiring DI.

```csharp
public class SlotCascadeHandler :
    ILocalEventHandler<AppointmentStatusChangedEto>,
    ITransientDependency
{
    private readonly ILogger<SlotCascadeHandler> _logger;

    public SlotCascadeHandler(ILogger<SlotCascadeHandler> logger)
    {
        _logger = logger;
    }

    public virtual Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        // 2026-05-15 (slot rework plan 3) -- the slot is no longer
        // stamped to Booked / Reserved on appointment transitions.
        // Capacity-aware booking treats BookingStatusId as a manual-
        // close override only; the active-count probe is the
        // authoritative source of "is this slot bookable".
        _logger.LogDebug(
            "SlotCascadeHandler: appointment {AppointmentId} transitioned {From} -> {To}; no slot mutation needed under capacity model.",
            eventData.AppointmentId,
            eventData.FromStatus,
            eventData.ToStatus);
        return Task.CompletedTask;
    }
}
```

Remove the injected `_availabilityRepository` and
`_appointmentRepository` fields; remove the helper methods
`ApplySlotStatusAsync` and `MapToSlotStatus`. The class is now
log-only.

### 8. `DoctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync`

Extend to return `remainingCapacity` per slot. The return DTO
adds a new field. Two options:

Option A (chosen): augment `DoctorAvailabilityDto` with a new
optional `int? RemainingCapacity { get; set; }`. The CRUD
endpoints return `null` (they don't compute it); the
booking-form lookup endpoint fills it. Plan 5 reads the field.

```csharp
public virtual async Task<List<DoctorAvailabilityDto>> GetDoctorAvailabilityLookupAsync(
    GetDoctorAvailabilityLookupInput input)
{
    Check.NotNull(input, nameof(input));
    if (input.LocationId == Guid.Empty)
    {
        throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
    }

    var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
    var leadTimeDays = systemParameter?.AppointmentLeadTime ?? 0;
    var minDate = (input.AvailableDateFrom?.Date ?? DateTime.Today).AddDays(leadTimeDays);

    var slotQuery = (await _doctorAvailabilityRepository.WithDetailsAsync(x => x.AppointmentTypes))
        .Where(x =>
            x.LocationId == input.LocationId &&
            x.BookingStatusId == BookingStatus.Available &&
            x.AvailableDate >= minDate);

    if (input.AvailableDateTo.HasValue)
    {
        slotQuery = slotQuery.Where(x => x.AvailableDate <= input.AvailableDateTo.Value.Date);
    }

    if (input.AppointmentTypeId.HasValue)
    {
        var typeId = input.AppointmentTypeId.Value;
        slotQuery = slotQuery.Where(x =>
            !x.AppointmentTypes.Any() ||
            x.AppointmentTypes.Any(at => at.AppointmentTypeId == typeId));
    }

    slotQuery = slotQuery.OrderBy(x => x.AvailableDate).ThenBy(x => x.FromTime);
    var slots = await AsyncExecuter.ToListAsync(slotQuery);

    // Bulk fetch active counts so we don't run N+1 round-trips.
    var slotIds = slots.Select(x => x.Id).ToList();
    var activeCounts = await _appointmentRepository.GetActiveCountsForSlotsAsync(slotIds);
    // GetActiveCountsForSlotsAsync is a sibling method that returns
    // Dictionary<Guid, long>. Implementation mirrors GetActiveCountForSlotAsync
    // but groups by DoctorAvailabilityId and projects to a dict.

    var dtos = slots.Select(s =>
    {
        var dto = ObjectMapper.Map<DoctorAvailability, DoctorAvailabilityDto>(s);
        var active = activeCounts.TryGetValue(s.Id, out var c) ? c : 0;
        dto.RemainingCapacity = Math.Max(0, s.Capacity - (int)active);
        return dto;
    }).ToList();

    // Exclude full slots from the picker -- the booking form
    // should not show 0-remaining rows.
    return dtos.Where(d => d.RemainingCapacity > 0).ToList();
}
```

Wire `GetActiveCountsForSlotsAsync(List<Guid> slotIds)` into
the repository interface and EF impl alongside the single-id
variant. The bulk implementation:

```csharp
public virtual async Task<Dictionary<Guid, long>> GetActiveCountsForSlotsAsync(
    List<Guid> doctorAvailabilityIds,
    CancellationToken cancellationToken = default)
{
    if (doctorAvailabilityIds == null || doctorAvailabilityIds.Count == 0)
    {
        return new Dictionary<Guid, long>();
    }

    var dbSet = await GetDbSetAsync();
    var grouped = await dbSet
        .Where(x => doctorAvailabilityIds.Contains(x.DoctorAvailabilityId))
        .Where(x =>
            x.AppointmentStatus != AppointmentStatusType.Rejected &&
            x.AppointmentStatus != AppointmentStatusType.CancelledNoBill &&
            x.AppointmentStatus != AppointmentStatusType.CancelledLate &&
            x.AppointmentStatus != AppointmentStatusType.RescheduledNoBill &&
            x.AppointmentStatus != AppointmentStatusType.RescheduledLate)
        .GroupBy(x => x.DoctorAvailabilityId)
        .Select(g => new { SlotId = g.Key, Count = (long)g.Count() })
        .ToDictionaryAsync(x => x.SlotId, x => x.Count, GetCancellationToken(cancellationToken));

    return grouped;
}
```

### 9. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityDto.cs`

Add the new property:

```csharp
/// <summary>
/// 2026-05-15 -- the remaining bookable capacity for this slot,
/// computed as Capacity - activeAppointmentCount. Populated by
/// GetDoctorAvailabilityLookupAsync (the booking-form lookup
/// endpoint); null on CRUD reads. Always &gt;= 0.
/// </summary>
public int? RemainingCapacity { get; set; }
```

### 10. `CaseEvaluationApplicationMappers.cs` (the
`DoctorAvailabilityToDoctorAvailabilityDtoMappers` partial)

Add `[MapperIgnoreTarget(nameof(DoctorAvailabilityDto.RemainingCapacity))]`
to the partial Map methods. The field is computed in the
AppService, not mapped from the entity.

### 11. Data migration: backfill `Reserved` -> `Available` in existing slots

The `SlotCascadeHandler` will no longer write `Reserved`. Existing
production data may have slots in `Reserved` that were stamped
on a `Pending` Appointment. We need to migrate those slots back
to `Available` so the new gate doesn't lock them out as
"manually closed".

Add a one-shot SQL block to the migration that drops the
`SlotCascadeHandler` mapping. Generate a NEW migration:

```bash
DOTNET_ENVIRONMENT=Development \
  dotnet ef migrations add Phase21_RepurposeReservedAndBackfill \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host \
  --context CaseEvaluationDbContext
```

Edit the auto-generated migration to include only the data fix
(no schema delta):

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 2026-05-15 -- repurpose BookingStatus.Reserved as
    // "manually closed". Pre-rework data has Reserved on slots
    // tied to Pending appointments. Migrate those slots back
    // to Available; the new capacity-aware gate treats them
    // correctly via the active-count probe.
    //
    // Only flip Reserved -> Available where there exists at
    // least one Appointment row on the slot in an ACTIVE
    // status. Reserved slots with NO active appointments are
    // assumed to be admin-intent ("close this slot") and stay
    // Reserved.
    migrationBuilder.Sql(@"
        UPDATE da
        SET da.[BookingStatusId] = 8  -- BookingStatus.Available
        FROM [AppEntity].[DoctorAvailabilities] da
        WHERE da.[BookingStatusId] = 10  -- BookingStatus.Reserved
          AND EXISTS (
            SELECT 1
            FROM [AppEntity].[Appointments] a
            WHERE a.[DoctorAvailabilityId] = da.[Id]
              AND a.[AppointmentStatus] NOT IN (3, 5, 6, 7, 8)
              -- 3=Rejected, 5=CancelledNoBill, 6=CancelledLate,
              -- 7=RescheduledNoBill, 8=RescheduledLate
          );

        -- Same for Booked: under the new model Booked is unused.
        -- Flip every Booked slot to Available; the active-count
        -- probe gives the same answer.
        UPDATE [AppEntity].[DoctorAvailabilities]
        SET [BookingStatusId] = 8
        WHERE [BookingStatusId] = 9;  -- BookingStatus.Booked
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // No-op down -- the original Reserved/Booked partition is
    // not reconstructible without the SlotCascadeHandler running.
}
```

Note: confirm enum integer values match the SQL literals by
reading `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/`.
`BookingStatus` is `Available=8, Booked=9, Reserved=10`;
`AppointmentStatusType.Rejected=3`, `CancelledNoBill=5`,
`CancelledLate=6`, `RescheduledNoBill=7`, `RescheduledLate=8`.
Cross-check before running.

### 12. Update `DoctorAvailabilitiesAppService.GeneratePreviewAsync` conflict detection

The conflict detection at line 322-349 currently uses
`overlap.BookingStatusId == Booked || overlap.BookingStatusId == Reserved`
to decide whether to mark the new slot's conflict message as
"already booked or reserved" vs "already exists". Under the new
model `Booked` is dead. Update to:

```csharp
if (overlap.BookingStatusId == BookingStatus.Reserved)
{
    timeSlot.IsConflict = true;
    isBookedByUser = true;  // legacy variable name; message stays accurate
}
else
{
    timeSlot.IsConflict = true;
    isAlreadyExist = true;
}
```

This preserves the user-facing message split: "already booked
or reserved" (now only triggered by manual-close Reserved
slots) vs "already exists" (Available slots that overlap).
Plan 4's UI changes consume this verbatim.

## Test plan

### `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/AppointmentsAppServiceTests.cs`

Add 8 new `[Fact]` tests (TDD: write first, watch fail, ship):

| # | Test | Acceptance |
|---|------|------------|
| 1 | `CreateAsync_WhenSlotIsReserved_ThrowsSlotClosed` | Seed slot with `BookingStatusId = Reserved`, `Capacity = 1`. Create throws `AppointmentBookingSlotClosed`. |
| 2 | `CreateAsync_WhenSlotCapacityIsExhausted_ThrowsSlotFull` | Seed slot with `Capacity = 2` + 2 active appointments. Third create throws `AppointmentBookingSlotFull`, with `capacity=2`, `activeCount=2` in WithData. |
| 3 | `CreateAsync_WhenSlotHasFreedAppointments_DoesNotCountThem` | Seed slot `Capacity = 1` + 1 Rejected appointment. New create succeeds; active count is 0. |
| 4 | `CreateAsync_WhenSlotTypesEmpty_AnyTypeWorks` | Seed slot with empty `AppointmentTypes`. Create with any `AppointmentTypeId` succeeds. |
| 5 | `CreateAsync_WhenRequestedTypeNotInSlotTypes_ThrowsTypeMismatch` | Seed slot with `AppointmentTypes = [t1]`. Create with `AppointmentTypeId = t2` throws `AppointmentBookingSlotTypeMismatch`. |
| 6 | `CreateAsync_WhenRequestedTypeInSlotTypes_Succeeds` | Seed slot with `AppointmentTypes = [t1, t2]`. Create with `AppointmentTypeId = t2` succeeds. |
| 7 | `CreateAsync_WhenLeadTimeBlocks_RaisesLeadTimeNotCapacity` | Verify ordering: lead-time check (existing `BookingPolicyValidator`) fires BEFORE the capacity gate. Today's lead-time-fail message text is preserved. |
| 8 | `CreateAsync_ConcurrentCallsAtCapacity1_OneSucceedsOneFails` | Two parallel `Task.Run(() => CreateAsync(...))` against a `Capacity=1` slot. Exactly one succeeds; the other throws `AppointmentBookingSlotFull`. Relies on the row-lock + transactional UoW. |

### `test/HealthcareSupport.CaseEvaluation.Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs`

Add 4 facts:

| # | Test | Acceptance |
|---|------|------------|
| 9 | `GetDoctorAvailabilityLookupAsync_RemainingCapacityComputed` | Seed slot `Capacity=3` + 1 active appointment. Lookup returns the slot with `RemainingCapacity = 2`. |
| 10 | `GetDoctorAvailabilityLookupAsync_FullSlotsExcluded` | Seed slot `Capacity=1` + 1 active appointment. Lookup omits the slot. |
| 11 | `GetDoctorAvailabilityLookupAsync_ReservedSlotsExcluded` | Seed slot `BookingStatusId=Reserved`, `Capacity=10`. Lookup omits the slot. |
| 12 | `GetDoctorAvailabilityLookupAsync_TypeFilterRespected` | Seed slot with `AppointmentTypes=[t1]`. Lookup with `AppointmentTypeId=t2` omits the slot. |

### `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/Appointments/EfCoreAppointmentRepositoryTests.cs`

Add 1 fact:

| # | Test | Acceptance |
|---|------|------------|
| 13 | `GetActiveCountForSlotAsync_ExcludesFiveFreedStatuses` | Seed slot + 6 appointments: one each for Rejected, CancelledNoBill, CancelledLate, RescheduledNoBill, RescheduledLate, plus one Approved. Active count = 1. |

### Manual UI verification

After backend ships:

1. `docker compose down -v && docker compose up -d --build`.
2. Confirm both migrations ran:
   - `Phase20_DoctorAvailabilityCapacityAndTypeSet`
   - `Phase21_RepurposeReservedAndBackfill`
3. Log in as Patient (external user) on tenant subdomain.
4. Navigate to `/appointments/add`.
5. Pick a location + appointment type. The slot picker still
   renders (plan 5 will add the "X remaining" display; today
   no UI change is visible).
6. Pick a slot whose `Capacity = 1` (the seeded default). Book.
   Expect 200 success.
7. As a second Patient (different tenant user), pick the same
   slot. Expect 400 with `Appointment:BookingSlotFull`.
8. As Staff Supervisor, navigate to
   `/doctor-management/doctor-availabilities`. Edit the slot to
   `Capacity = 3`.
9. Second Patient retries. Expect 200 success.

## Risk and rollback

**Blast radius:**
- 1 AppService method body. 1 repo gains 2 methods. 1 event
  handler reduced to a stub. 1 data migration.
- All AppService permission, mapping, and tenant filtering
  unchanged.

**Rollback:**
- Revert the commit. Run `dotnet ef database update <previous>`.
  The `Down()` on `Phase21` is a no-op (the original
  Reserved/Booked partition was not reconstructible after the
  SlotCascadeHandler stopped running). The slot statuses stay
  on `Available`; this is the WORST case for the booking UX --
  every slot looks bookable, and the soft check happens at the
  AppService capacity gate, which is also reverted. Net effect:
  back to pre-rework single-row "Available means bookable"
  semantics.

**Risk: row-lock hint is dialect-specific.** SQL Server is the
only target. The `FromSqlRaw` literal with `WITH (UPDLOCK,
HOLDLOCK)` is documented at
`docs.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table`.
Test the concurrency fact (#8) under the actual SQL Server
container, not LocalDB or SQLite -- the in-process test SQLite
DB does not honour the hint and will produce false positives.

**Risk: missed transition mapping in the SlotCascadeHandler stub
breaks the email / event subscribers downstream**. Mitigated by
keeping the handler subscribed and log-only; downstream
subscribers (email handlers, audit handlers) continue to
receive their events unmodified -- only the slot-status side
effect goes away.

**Risk: pre-rework data has slots in Reserved that admins
genuinely intended to manually close**. Mitigated by the
backfill SQL's predicate: it only flips Reserved -> Available
when there exists at least one ACTIVE appointment on the slot.
Reserved slots with zero active appointments stay Reserved
(admin intent).

## Verification

End-to-end:

1. Fresh DB; migrate; seed data via DbMigrator.
2. Run plan 7's HRD-R1.12.x scenarios manually if plan 7 hasn't
   shipped yet (the test plan above covers the essentials).
3. SQL probes after migration:
   ```sql
   SELECT BookingStatusId, COUNT(*)
   FROM AppEntity.DoctorAvailabilities
   GROUP BY BookingStatusId;
   -- Expected: predominantly 8 (Available), with the admin-
   -- closed slots at 10 (Reserved). Zero rows with 9 (Booked).
   ```

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Merge order: plan 1 -> plan 2 -> plan 3. Plans 4-7 follow.
