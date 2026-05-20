---
title: Slot generation & storage rework — capacity, multi-type, multi-weekday, multi-range
status: draft
approach: mixed (see per-task approach flags)
created: 2026-05-15
audience: implementing session
related:
  - docs/parity/wave-1-parity/_slot-generation-deep-dive.md
  - docs/parity/wave-1-parity/staff-supervisor-doctor-management.md
  - src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md
---

# Slot generation & storage rework

Replace the current single-type / single-seat / contiguous-weekday-range slot
model with a real-life clinic model where:

- A slot has a **capacity** (default 1; can be > 1 for multi-exam-room clinics).
- A slot can be qualified for **multiple appointment types** via a join table;
  an empty set means "accepts any type" (matches OLD's `null = wildcard`).
- Generation lets the supervisor **cherry-pick weekdays** (Mon + Wed + Fri).
- Generation accepts **multiple time ranges per day** (9:00-12:00 + 13:00-17:00
  for lunch-break support).

Per the design decisions locked 2026-05-15:

1. Persisted `Capacity` + derived booked-count; `BookingStatusId` retained as a manual close/open override.
2. M:N join table for slot types, empty set = wildcard.
3. 7-checkbox cherry-pick weekday UI.
4. **In scope**: multiple time ranges per day.

---

## Goal

Land all four mechanical changes (capacity column, type join table,
multi-weekday, multi-range) in a single coherent rework PR, with no regressions
in the existing booking / approval / change-request flows. Existing slots
backfill to `Capacity=1` and migrate their single `AppointmentTypeId` (if any)
into one join row.

## Non-goals

- Rethinking the appointment booking workflow itself.
- Changing how appointments link to slots (`Appointment.DoctorAvailabilityId`
  stays a required `Guid` FK).
- Touching the Doctor or Tenant entities (one-doctor-per-tenant invariant is unchanged).
- Building a calendar drag-and-drop UI (out of scope; the preview-then-confirm
  pattern stays).
- SignalR push of slot availability changes.

## Background — current state (cross-reference)

- Entity: `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs` — single `AppointmentTypeId?` field, no capacity, `BookingStatusId` controls bookability.
- Manager: `DoctorAvailabilityManager` — thin Create/Update; no validation.
- AppService: `DoctorAvailabilitiesAppService.GeneratePreviewAsync` — accepts `List<DoctorAvailabilityGenerateInputDto>`; each item has single-valued `LocationId`, `AppointmentTypeId?`, and date/time range. Frontend fans out a weekday range into N items.
- Frontend generate UI: `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` — two-mode form (`dates` | `weekdays`); weekday mode is contiguous range only.
- Booking-form picker: `GetDoctorAvailabilityLookupAsync` — filters by `BookingStatusId == Available`, optional type match.
- Appointment booking: `AppointmentsAppService.CreateAsync` flips slot to `Booked`. No reverse transition exists today (BUG-class "slot never released").

Existing audit + design docs (read before starting):
- `docs/parity/wave-1-parity/_slot-generation-deep-dive.md`
- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md`

---

## Target model

### Storage

```
DoctorAvailability  (mostly unchanged)
  Id, TenantId, AvailableDate, FromTime, ToTime,
  BookingStatusId,                            // NOW: manual close/open only
  LocationId,
  Capacity : int (NEW; default 1, must be >= 1)
  -- AppointmentTypeId : Guid? (REMOVED)

DoctorAvailabilityAppointmentType  (NEW join entity)
  DoctorAvailabilityId : Guid    (composite PK part 1, FK -> DoctorAvailability, Cascade)
  AppointmentTypeId    : Guid    (composite PK part 2, FK -> AppointmentType, NoAction)
  TenantId             : Guid?   (mirrored for query filter)
```

**Empty set on the join table = wildcard** (accepts any type). Verified by
`!slot.AppointmentTypes.Any() || slot.AppointmentTypes.Any(t => t.AppointmentTypeId == requested)`.

### Bookable check (booking-form picker + booking validator)

```
bookable(slot, requestedType, now) =
     slot.AvailableDate >= now + leadTime
  && slot.BookingStatusId == Available                              // manual override gate
  && (slot.Capacity - activeAppointmentCount(slot.Id)) > 0          // capacity gate
  && (slot.AppointmentTypes is empty OR contains requestedType)     // type-set gate
```

Where `activeAppointmentCount` counts Appointments referencing this slot whose
status is not in the terminal set (`Rejected`, `CancelledNoBill`, `CancelledLate`).

### `BookingStatusId` semantic change

| Status | New meaning |
|---|---|
| `Available` (default) | Slot is open for booking subject to capacity |
| `Booked` | **Deprecated** for capacity > 1 slots; kept for backward-compat queries. Migration sets all existing `Booked` rows to `Available` if no Appointment references them; leaves the FK linkage; capacity check handles bookability going forward |
| `Reserved` | **Manual override**: admin closed this slot. Bookable = false regardless of capacity. (Repurposed from the original "pending office review" intent.) |

Doctor sick-day workflow: admin flips `Reserved` on all slots that day.

### Generation input

```
DoctorAvailabilityGenerateInputDto  (REWORKED)
  LocationId : Guid                        (required, single)
  AppointmentTypeIds : List<Guid>          (multi-select; empty = wildcard slot)
  Capacity : int                           (default 1)
  BookingStatusId : BookingStatus          (default Available)

  // Mode A: explicit date range
  FromDate? : DateTime
  ToDate? : DateTime

  // Mode B: weekdays within a single month
  SelectedDays? : List<int>                (0=Sun..6=Sat; empty/null = all 7 days)
  Year? : int
  Month? : int                             (1..12; null = current month if SelectedDays present)

  // Multi-range: replaces FromTime+ToTime
  TimeRanges : List<TimeRangeDto>          (>= 1; each {FromTime, ToTime})

  AppointmentDurationMinutes : int         (default 15)
```

Mode discriminator is implicit: `SelectedDays != null` ⇒ weekday mode; otherwise
date-range mode. Backend computes the matching dates from
`(Year, Month, SelectedDays)` or `(FromDate, ToDate)`.

---

## Tasks

Each task lists files touched and the `approach` flag per
`~/.claude/rules/rpe-workflow.md`. Tasks within a phase are sequential; phases
are sequential.

### Phase 1 — Schema + entity foundation

Goal: schema is in place; nothing else changes behavior yet.

#### T1.1 — Add `Capacity` to `DoctorAvailability` entity (approach: code)

- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs`:
  - Add `public virtual int Capacity { get; set; } = 1;` with `Check.Positive` in constructor.
- `EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` and
  `CaseEvaluationTenantDbContext.cs`:
  - Add `b.Property(x => x.Capacity).IsRequired().HasDefaultValue(1);`
- DTOs: add `Capacity` to `DoctorAvailabilityDto`, `DoctorAvailabilityCreateDto`, `DoctorAvailabilityUpdateDto`, `DoctorAvailabilityWithNavigationPropertiesDto`, `DoctorAvailabilitySlotPreviewDto`.
- Mapper update (none needed — Mapperly picks up the new field).

#### T1.2 — Create `DoctorAvailabilityAppointmentType` join entity (approach: code)

- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityAppointmentType.cs`:
  ```csharp
  public class DoctorAvailabilityAppointmentType : Entity, IMultiTenant
  {
      public Guid? TenantId { get; set; }
      public Guid DoctorAvailabilityId { get; protected set; }
      public virtual DoctorAvailability DoctorAvailability { get; set; }
      public Guid AppointmentTypeId { get; protected set; }
      public virtual AppointmentType AppointmentType { get; set; }
      public override object[] GetKeys() => new object[] { DoctorAvailabilityId, AppointmentTypeId };
  }
  ```
- `DoctorAvailability` entity: add `public virtual ICollection<DoctorAvailabilityAppointmentType> AppointmentTypes { get; protected set; }` initialised to an empty list, plus mutator methods `AddAppointmentType(Guid)`, `RemoveAppointmentType(Guid)`, `RemoveAllAppointmentTypesExceptGivenIds(List<Guid>)`, `RemoveAllAppointmentTypes()` — same shape as `Doctor.AddAppointmentType`.
- `CaseEvaluationDbContext.cs` and `CaseEvaluationTenantDbContext.cs`: configure the join entity in both contexts (outside `IsHostDatabase()` guard); FK to `DoctorAvailability` Cascade, FK to `AppointmentType` NoAction (matches host scope), composite PK `{DoctorAvailabilityId, AppointmentTypeId}`, query filter on soft-deletes of either side.

#### T1.3 — EF migration `Phase8_Slot_Capacity_And_TypeSet` (approach: code)

- Generate via `dotnet ef migrations add Phase8_Slot_Capacity_And_TypeSet`.
- Up():
  1. `AddColumn(AppEntity_DoctorAvailabilities, Capacity int NOT NULL DEFAULT 1)`.
  2. `CreateTable(AppEntity_DoctorAvailabilityAppointmentTypes, ...)` with composite PK and FKs.
  3. `INSERT INTO AppEntity_DoctorAvailabilityAppointmentTypes(DoctorAvailabilityId, AppointmentTypeId, TenantId) SELECT Id, AppointmentTypeId, TenantId FROM AppEntity_DoctorAvailabilities WHERE AppointmentTypeId IS NOT NULL` — backfill rows that had a non-null type.
  4. `DropColumn(AppEntity_DoctorAvailabilities, AppointmentTypeId)`.
- Down(): reverse — re-add `AppointmentTypeId` (nullable), copy the first join row back per slot, drop the join table, drop `Capacity`.

#### T1.4 — Repository updates (approach: code)

- `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs`:
  - Drop the `appointmentTypeId` parameter; add `appointmentTypeIds : List<Guid>?` to filter methods.
  - Change list method to eager-load `AppointmentTypes` collection.
- `EntityFrameworkCore/DoctorAvailabilities/EfCoreDoctorAvailabilityRepository.cs`:
  - Update queries: `appointmentTypeIds != null && appointmentTypeIds.Count > 0 ⇒ Where(x => !x.AppointmentTypes.Any() || x.AppointmentTypes.Any(t => appointmentTypeIds.Contains(t.AppointmentTypeId)))`.
  - Update `GetCountAsync` similarly.
- `DoctorAvailabilityWithNavigationProperties`: add `public List<AppointmentType> AppointmentTypes { get; set; }`.

#### T1.5 — DTO + Mapper updates (approach: code)

- `DoctorAvailabilityCreateDto` / `UpdateDto`: drop `AppointmentTypeId`; add `AppointmentTypeIds : List<Guid> = new()`.
- `DoctorAvailabilityDto`: drop `AppointmentTypeId`; add `AppointmentTypeIds : List<Guid>`.
- `DoctorAvailabilityWithNavigationPropertiesDto`: drop `AppointmentType : AppointmentTypeDto?`; add `AppointmentTypes : List<AppointmentTypeDto>`.
- `DoctorAvailabilitySlotPreviewDto`: drop `AppointmentTypeId`; add `AppointmentTypeIds : List<Guid>`.
- `GetDoctorAvailabilitiesInput`: drop `AppointmentTypeId?`; add `AppointmentTypeIds : List<Guid>?` (nullable so unset filter is distinguishable from empty filter).
- `CaseEvaluationApplicationMappers.cs`: update mappers to copy the new shape (Mapperly partial-class additions).

**Phase 1 acceptance**: solution builds clean; one new migration; one new entity; existing tests still pass (they don't test the deleted column directly).

### Phase 2 — Domain logic for capacity + type-set

Goal: real bookability semantics live in code, callers still treat the slot
the old way (no new fields surfaced yet).

#### T2.1 — `DoctorAvailabilityManager` rework (approach: tdd)

- `CreateAsync(locationId, appointmentTypeIds, capacity, availableDate, fromTime, toTime, bookingStatusId)`:
  - Validate `capacity >= 1`.
  - Build entity; call `SetAppointmentTypesAsync` to populate the join collection.
- `UpdateAsync` same shape + concurrency stamp.
- `SetAppointmentTypesAsync(slot, ids)`: idempotent replace-all using `RemoveAllAppointmentTypesExceptGivenIds(ids)` + `AddAppointmentType(id)` for any missing.
- Tests (`test/.../Domain.Tests/DoctorAvailabilities/DoctorAvailabilityManagerTests.cs`):
  - `CreateAsync_WithEmptyAppointmentTypeIds_PersistsWildcardSlot`
  - `CreateAsync_WithThreeAppointmentTypes_PersistsThreeJoinRows`
  - `CreateAsync_WithCapacityZero_Throws`
  - `UpdateAsync_RemovesUnsuppliedAppointmentTypes_KeepsSuppliedOnes`
  - `UpdateAsync_PreservesCapacityWhenInputUnchanged`

#### T2.2 — New `IAppointmentRepository.GetActiveCountForSlotAsync(slotId)` (approach: tdd)

- Returns count of Appointments where `DoctorAvailabilityId == slotId` AND `AppointmentStatus NOT IN (Rejected, CancelledNoBill, CancelledLate)`.
- Tests:
  - `Returns0_WhenNoAppointmentsLinked`
  - `Returns2_WhenTwoActiveAppointments`
  - `IgnoresRejectedAppointments`
  - `IgnoresCancelledNoBillAppointments`
  - `IgnoresCancelledLateAppointments`

#### T2.3 — Update slot validators in `AppointmentsAppService` booking validators (approach: tdd)

- `AppointmentBookingValidators.cs` (or wherever the 5-way slot check lives):
  - Remove the `slot.BookingStatusId == Available` check.
  - Replace with: `slot.BookingStatusId != BookingStatus.Reserved` (Reserved = manual closed) AND `slot.Capacity > activeCount`.
  - Remove the `slot.AppointmentTypeId == null || slot.AppointmentTypeId == requestedType` check.
  - Replace with: `!slot.AppointmentTypes.Any() || slot.AppointmentTypes.Any(t => t.AppointmentTypeId == requestedType)`.
- New error codes:
  - `CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull` ("This slot is at capacity.")
  - `CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed` ("This slot has been closed by the office.")
  - `CaseEvaluationDomainErrorCodes.AppointmentBookingSlotTypeMismatch` ("This slot does not accept the selected appointment type.")
- Map all three to HTTP 400 in `CaseEvaluationHttpApiHostModule.cs`.
- Tests:
  - `Book_FillsSlotToCapacity_FourthBookingThrowsSlotFull` (Capacity=3)
  - `Book_AgainstClosedSlot_ThrowsSlotClosed` (BookingStatusId=Reserved)
  - `Book_RequestingPQME_AgainstAMEOnlySlot_ThrowsSlotTypeMismatch`
  - `Book_RequestingPQME_AgainstWildcardSlot_Succeeds`
  - `Book_AgainstSlot_AfterCancellationFreedASeat_Succeeds`

#### T2.4 — Remove the `AppointmentsAppService.CreateAsync` `BookingStatusId = Booked` flip (approach: tdd)

- The flip is no longer the bookability signal; remove it. Slot status only changes when an admin manually closes it.
- Update existing tests that asserted `BookingStatusId == Booked` post-create; they should now assert `activeCount == 1` instead.

#### T2.5 — Update `GetDoctorAvailabilityLookupAsync` (approach: tdd)

- Filter: `slot.BookingStatusId != Reserved AND slot.Capacity > activeCount AND (no types OR contains requestedType)`.
- Add `bookedCount` to the response DTO so the picker can display "2 of 3 seats available".
- Tests:
  - `Lookup_FullCapacitySlot_Excluded`
  - `Lookup_PartiallyBookedSlot_Included_WithRemainingCount`
  - `Lookup_ClosedSlot_Excluded`
  - `Lookup_WildcardSlot_MatchesAnyRequestedType`
  - `Lookup_TypeRestrictedSlot_OnlyMatchesIncludedTypes`

**Phase 2 acceptance**: all booking + lookup tests pass; existing
end-to-end booking scenarios still work; new error codes mapped to 400.

### Phase 3 — Generation API rework

Goal: accept multi-type, multi-weekday, multi-range generation inputs.

#### T3.1 — Update `DoctorAvailabilityGenerateInputDto` (approach: code)

- Add new fields per "Target model" above. Remove `FromTime`/`ToTime` (replaced by `TimeRanges`).
- Backward compat: not preserved at the DTO surface — but the Angular side is the only caller and updates in lockstep.
- Add a small `TimeRangeDto { TimeOnly FromTime; TimeOnly ToTime; }` in the same folder.

#### T3.2 — Rework `GeneratePreviewAsync` (approach: tdd)

- Per-item validation now covers:
  - `LocationId != Empty`
  - `Capacity >= 1`
  - `AppointmentDurationMinutes > 0`
  - `TimeRanges.Count >= 1`; each range `ToTime > FromTime`
  - Date mode: `ToDate >= FromDate`
  - Weekday mode: `Year, Month` valid; `SelectedDays.Count > 0` (else wildcard = all 7 days, which is fine)
- Date resolution:
  - Date-range mode: iterate `FromDate..ToDate` inclusive.
  - Weekday mode: iterate every day in `(Year, Month)`; keep days where `(int)date.DayOfWeek` is in `SelectedDays`.
- For each resolved date, for each `TimeRange`, walk fixed-duration sub-slots from `FromTime` to `ToTime`.
- Conflict detection: scoped by `LocationId + date + time-overlap` — unchanged from today.
- Output: one `DoctorAvailabilitySlotPreviewDto` per generated sub-slot, carrying `AppointmentTypeIds`, `Capacity`, `LocationId`, etc.
- Tests:
  - Multi-range single day: `[(09:00,12:00),(13:00,17:00)]`, 60-min duration → 3 + 4 = 7 slots
  - Cherry-pick weekdays: `SelectedDays=[1,3,5]` (Mon/Wed/Fri) in June 2026 → exactly the M/W/F dates
  - Multi-type slot: `AppointmentTypeIds=[pqme,ame]` → preview row has both IDs
  - Empty `AppointmentTypeIds` → wildcard slot (empty list in preview)
  - Capacity 5 → preview row has `Capacity=5`
  - Conflict detection still flags overlapping existing slots at same location

#### T3.3 — Update `CreateAsync` to persist multi-type (approach: code)

- The Angular preview-then-submit loop today calls `CreateAsync` per slot; update `DoctorAvailabilityCreateDto` parameters per T1.5 and have `CreateAsync` pass `AppointmentTypeIds` to `DoctorAvailabilityManager.CreateAsync`.

**Phase 3 acceptance**: full preview-then-create round-trip works end-to-end via
direct API calls (curl / Playwright); preview returns correct shape; create
persists slot + join rows in one save.

### Phase 4 — Angular generation UI

Goal: surface the new fields to the supervisor; preserve preview-then-confirm UX.

#### T4.1 — `appointmentTypeIds` multi-select (approach: test-after)

- `doctor-availability-generate.component.ts`:
  - Replace `appointmentTypeId` form control with `appointmentTypeIds : Guid[]`.
  - Use ABP's `LookupSelectComponent` with `multiple: true` (verify the component supports multi; if not, fall back to a separate multi-select control like `ng-select`).
- `.html`: update binding + display.

#### T4.2 — Cherry-pick weekday checkboxes (approach: test-after)

- Replace `fromDay : Guid` + `toDay : Guid` with `selectedDays : number[]`.
- 7 checkboxes labelled Sun–Sat. Default unchecked. Validation: if `slotMode == 'weekdays'`, at least 1 checkbox required.
- Remove `isWeekdayInRange` (no longer needed); date resolution is `selectedDays.includes(dayNum)`.

#### T4.3 — Multi-range time inputs (approach: test-after)

- Replace `fromTime` + `toTime` with `timeRanges : FormArray` of `{ fromTime, toTime }` pairs.
- Initial: one row. "Add another time range" button appends a row. Each row has a remove button (disabled when only one row remains).
- Validation: each row independently — `toTime > fromTime`.
- Template: render the FormArray as a stack of two-column rows.

#### T4.4 — Capacity input (approach: test-after)

- New numeric `capacity` form control, default 1, min 1, max 50 (sanity cap).
- Add inline help: "Number of patients this slot can serve simultaneously."

#### T4.5 — Submit pipeline update (approach: test-after)

- `submit()` no longer iterates `forkJoin` over single slots — backend will accept the bulk shape. Alternative: keep `forkJoin` and just pass `appointmentTypeIds` and `capacity` per slot. Lower diff, same behavior. **Recommended: keep forkJoin**.
- Preview rendering: show "Capacity: N" and "Types: PQME, AME" badges on each preview row.
- "Booked seats / Capacity" display on the slot-list page (`doctor-availability.component.html` / `abstract.component.ts`): show `2 / 3` for partially-filled slots.

**Phase 4 acceptance**: supervisor can generate from the UI with multi-type +
cherry-pick weekdays + multi-range + capacity; preview matches what the
backend produces; submit creates the slots with full multi-type association.

### Phase 5 — Booking-form picker UI

Goal: patient-side booking honors capacity + type-set.

#### T5.1 — Slot picker displays remaining capacity (approach: test-after)

- `angular/src/app/appointments/appointment-add-schedule.component.ts` (or wherever the picker renders):
  - Show `Available (3 of 5)` or just `Available` for capacity 1.
  - Hide capacity-display for `Capacity == 1` slots so existing UX is unchanged.

#### T5.2 — Slot picker filters by type-set (approach: test-after)

- Backend already filters in `GetDoctorAvailabilityLookupAsync` per Phase 2. Frontend just consumes the filtered list.
- No-op for the Angular side if the lookup endpoint is the only entrypoint.

#### T5.3 — Error rendering for new error codes (approach: test-after)

- `appointment-add.component.ts` error handler: map the three new codes
  (`AppointmentBookingSlotFull`, `AppointmentBookingSlotClosed`,
  `AppointmentBookingSlotTypeMismatch`) to friendly messages. Default error
  handler should fall through but a custom toast text is nicer.

**Phase 5 acceptance**: patient sees real remaining-seat counts; booking a
full slot returns a clean 400 with a clear message.

### Phase 6 — Tests + verification

#### T6.1 — Domain + Application unit tests (covered in Phase 2 + 3 task acceptance)

Each task in Phase 2 and 3 already lists `[Fact]` tests. Re-confirm coverage:

- Capacity math: 8+ tests
- Multi-type matching: 6+ tests
- Cherry-pick weekday resolution: 5+ tests
- Multi-range slot math: 6+ tests
- Booking error codes: 3 tests (one per code)

#### T6.2 — Integration tests for booking under capacity (approach: tdd)

- `test/.../EntityFrameworkCore.Tests/Appointments/CapacityBookingFlowTests.cs`:
  - `Capacity3Slot_AcceptsFirst3Bookings_RejectsFourth`
  - `WildcardSlot_AcceptsAnyType`
  - `TypeRestrictedSlot_RejectsMismatchedType`
  - `RejectedAppointment_FreesSeat_NextBookingAccepts`

#### T6.3 — HARDENING-TEST-SUITE additions (approach: code)

Append to `docs/runbooks/HARDENING-TEST-SUITE.md`. Suggested IDs `HRD-R1.12.{1..6}` + `HRD-R2.10.{1..3}`.

- **HRD-R1.12.1** — Generate a wildcard multi-day slot via UI; verify DB rows have empty join table.
- **HRD-R1.12.2** — Generate slots for Mon+Wed+Fri in current month; verify DB has exactly the matching dates and no others.
- **HRD-R1.12.3** — Generate with two time ranges (9-12, 13-17), 60-min duration; verify 3 + 4 = 7 slots per chosen day.
- **HRD-R1.12.4** — Generate a Capacity=3 slot; verify slot picker shows "3 seats available".
- **HRD-R1.12.5** — Book three patients into the Capacity=3 slot; verify slot disappears from picker on the 4th attempt with 400 / `SlotFull`.
- **HRD-R1.12.6** — Cancel one of the three bookings; verify slot reappears.
- **HRD-R2.10.1** — Direct API POST with `AppointmentTypeIds=[]`; verify wildcard slot created.
- **HRD-R2.10.2** — Direct API POST booking a `Reserved` (manually closed) slot returns 400 / `SlotClosed`.
- **HRD-R2.10.3** — Direct API generation with `Capacity=0` returns 400.

**Phase 6 acceptance**: all unit + integration + Playwright scenarios pass on
a fresh `docker compose up` of the main stack.

---

## Migration order (single PR vs phased)

**Recommendation**: ship as **one PR** with the 6 phases internally sequenced.
The schema migration in T1.3 cannot land separately from the code that
consumes it — the column rename / drop has to happen atomically with the
manager + AppService updates. Rolling out in pieces creates a window where
the entity has `Capacity=1` but no caller reads it, or worse, the
`AppointmentTypeId` is dropped before the join table exists.

If you want to split, the only safe split point is:
- **PR-1**: Phase 1 schema + Phase 2 domain logic. `AppointmentTypeIds` defaults to a single-element list from the existing single column at the DTO layer. Backend behavior unchanged for callers.
- **PR-2**: Phase 3-5 generation + UI + booking-flow surface.

I'd default to a single PR unless review burden becomes the bottleneck.

---

## Risks

| Risk | Mitigation |
|---|---|
| The `BookingStatusId = Booked` flip in `AppointmentsAppService.CreateAsync` is used by an unexpected caller (a job, a test, the audit log) | Grep before T2.4. If found, decide per case; the audit log is fine to keep but we should not gate behavior on it. |
| Migration fails on a tenant with existing slots that have null `AppointmentTypeId` AND null `IsDeleted` | The backfill `INSERT ... WHERE AppointmentTypeId IS NOT NULL` already excludes nulls — those become wildcard slots, which is correct. |
| Booking under capacity has a race condition: two callers see `count = 2` against `Capacity = 3` and both insert, ending at `count = 3` and a third caller fails — but if `Capacity = 2`, both succeed → overbook | Wrap the count + insert in a serializable transaction (or a unique index `{DoctorAvailabilityId, SeatNumber}` if we want strict guarantees). MVP: serializable transaction. |
| Angular multi-select for AppointmentTypes — ABP's stock `LookupSelectComponent` may not support multi-select | Plan B: use `ng-select` (already in some patterns) or a custom checkbox list. Spike in T4.1 first. |
| Existing tests asserting `slot.BookingStatusId == Booked` post-create will break | Update them in T2.4 as part of the same task. |
| Multi-range UX is heavier than expected | Acceptable per design decision 4; budget 4-6 hours for the FormArray + template. |

---

## Open questions (flag if blocking; do not stall)

1. **`Reserved` semantic repurpose** — is the manual-close-override usage acceptable, or should we introduce a new `Closed` enum value and leave `Reserved` for its original "pending office review" intent? My recommendation: repurpose `Reserved` to mean "admin manually closed" since the original semantic was never wired. Document in `_parity-flags.md`.
2. **Booking with a specific seat number** — the user requested "each slot can take multiple patients" but did not specify whether the patient picks a seat (1, 2, 3) or just "any seat in this window". My read: any seat. If seat numbers matter (different exam rooms with different equipment), open a follow-up.
3. **Wildcard slots and the booking-form type dropdown** — when a patient selects "AME" and the slot list returns both wildcard slots and AME-specific slots, do we visually mark wildcards differently? Recommendation: no, treat them identically.
4. **Cancellation race**: T6.2 includes `RejectedAppointment_FreesSeat_NextBookingAccepts`. Should we add an `IsRescheduled` exclusion to the active-count query? Reschedule today creates a new Appointment row with `OriginalAppointmentId` and (per audit) sets the old row to a transitional status. Verify this doesn't double-count.

---

## Verification procedure

```bash
# 1. Build + migrate
docker compose -f docker-compose.yml -f docker-compose.testing.yml build api angular
docker compose up -d
docker exec main-api-1 dotnet run --project /workspace/src/HealthcareSupport.CaseEvaluation.DbMigrator

# 2. Run all tests
docker exec main-api-1 dotnet test \
  /workspace/test/HealthcareSupport.CaseEvaluation.Application.Tests \
  --filter "FullyQualifiedName~DoctorAvailabilities|FullyQualifiedName~CapacityBookingFlowTests"

# 3. Drive HARDENING-TEST-SUITE Phase 12 scenarios via Playwright MCP
#    (HRD-R1.12.{1..6} + HRD-R2.10.{1..3})
```

---

## Files touched (summary)

**Backend** (~10 files modified, 2 new):

1. `src/.../Domain/DoctorAvailabilities/DoctorAvailability.cs` — add Capacity + AppointmentTypes collection + mutators
2. `src/.../Domain/DoctorAvailabilities/DoctorAvailabilityAppointmentType.cs` — **NEW**
3. `src/.../Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs` — rework Create/Update
4. `src/.../Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs` + EfCore impl — multi-type filter
5. `src/.../Domain.Shared/CaseEvaluationDomainErrorCodes.cs` — 3 new codes
6. `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` + `CaseEvaluationTenantDbContext.cs` — entity config
7. `src/.../EntityFrameworkCore/Migrations/{date}_Phase8_Slot_Capacity_And_TypeSet.cs` — **NEW**
8. `src/.../Application.Contracts/DoctorAvailabilities/*` — DTO updates (10 files)
9. `src/.../Application.Contracts/DoctorAvailabilities/TimeRangeDto.cs` — **NEW**
10. `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` — rework GeneratePreview + lookup + Create
11. `src/.../Application/CaseEvaluationApplicationMappers.cs` — Mapperly updates
12. `src/.../Application/Appointments/AppointmentBookingValidators.cs` — capacity + type-set check
13. `src/.../Application/Appointments/AppointmentsAppService.cs` — drop the `BookingStatusId = Booked` flip
14. `src/.../HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` — map 3 new error codes to 400
15. `src/.../Domain.Shared/Localization/CaseEvaluation/en.json` — 3 new error messages

**Frontend** (~5 files modified):

16. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` + `.html`
17. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability.abstract.component.ts` + `.html` — capacity display in list
18. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-detail.component.ts` + `.html` — single-slot detail modal capacity + multi-type
19. `angular/src/app/appointments/appointment-add-schedule.component.ts` + `.html` — "N of M seats" display
20. `angular/src/app/proxy/doctor-availabilities/models.ts` + `index.ts` — **regenerated** via `abp generate-proxy`

**Tests** (~5 files modified or new):

21. `test/.../Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs` — extend for multi-type, multi-range, capacity, cherry-pick weekdays
22. `test/.../Domain.Tests/DoctorAvailabilities/DoctorAvailabilityManagerTests.cs` — **NEW**
23. `test/.../EntityFrameworkCore.Tests/Appointments/CapacityBookingFlowTests.cs` — **NEW**

**Docs** (~3 files modified):

24. `docs/parity/wave-1-parity/_slot-generation-deep-dive.md` — note the rework superseded the OLD-parity model
25. `docs/parity/wave-1-parity/_parity-flags.md` — add `PARITY-FLAG-NEW-007` for the slot-model rework (parity-plus: NEW exceeds OLD with capacity + multi-type + multi-range)
26. `docs/runbooks/HARDENING-TEST-SUITE.md` — Phase 12 scenarios

---

## Estimated effort

| Phase | Hours |
|---|---|
| Phase 1 (schema) | 4-6 |
| Phase 2 (domain logic + tests) | 8-10 |
| Phase 3 (generation API) | 6-8 |
| Phase 4 (Angular UI) | 8-12 |
| Phase 5 (booking-form UI) | 3-4 |
| Phase 6 (integration tests + hardening) | 4-6 |
| **Total** | **~35-45 hours** |

End of plan.
