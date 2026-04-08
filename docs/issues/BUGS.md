[Home](../INDEX.md) > [Issues](./) > Confirmed Bugs

# Confirmed Bugs

Ten confirmed bugs were identified during the codebase audit and E2E testing. These are not missing features or design concerns -- they are cases where the code produces incorrect results relative to either its own documented intent, the existing business-domain documentation, or basic expected behaviour.

> **Test Status (2026-04-02)**: BUG-02 confirmed via B11.1.1, BUG-09 confirmed via exploratory test E1, BUG-10 confirmed via B7.4.2 and E7. See [TEST-EVIDENCE.md](TEST-EVIDENCE.md).

---

## BUG-01: Slot Conflict Detection Logic Is Inverted

**Severity:** High
**Status:** Open

### Description

`DoctorAvailabilitiesAppService.GeneratePreviewAsync` contains an inverted condition in its conflict detection logic. The `||` operator causes available slots at different locations to be incorrectly flagged as conflicts, while the actual conflict scenario (an `Available` slot at the same location that would overlap) is handled incorrectly.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 280--284:

```csharp
// Current (incorrect)
if (isSameLocation || overlap.BookingStatusId == BookingStatus.Available)
{
    timeSlot.IsConflict = true;
    isAlreadyExist = true;
}
```

The intent, based on the [Doctor Availability](../business-domain/DOCTOR-AVAILABILITY.md) documentation, is:
- Flag a conflict if an overlapping slot at the **same location** already exists (regardless of its booking status).
- Flag a conflict if any overlapping slot is already **booked** (regardless of location, since the doctor cannot be in two places).

The `||` with `BookingStatus.Available` means: "flag as conflict if same location, OR if the overlap happens to be available." An available slot at a *different* location is not a conflict. The condition should instead be:

```csharp
// Correct
if (isSameLocation || overlap.BookingStatusId != BookingStatus.Available)
{
    timeSlot.IsConflict = true;
    isAlreadyExist = true;
}
```

### Impact

- Generating slots for Location B will mark slots as conflicts if Location A has available (unbooked) slots at the same time -- preventing valid multi-location scheduling.
- Doctors who work at multiple locations on the same day cannot generate their availability without false conflict warnings.

---

## BUG-02: Appointment Status Changes Are Never Persisted

**Severity:** High
**Status:** Open

### Description

`AppointmentUpdateDto` does not contain an `AppointmentStatus` property. When `AppointmentViewComponent.save()` sends an update, the status field is silently dropped during deserialization. `AppointmentManager.UpdateAsync` also does not accept a status parameter.

### Affected Files

- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentUpdateDto.cs` -- missing `AppointmentStatus` property
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` lines 39--58 -- no `appointmentStatus` parameter
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts` line 295 -- sends `appointmentStatus` that is ignored

### Sequence of Failure

1. User opens an appointment in `AppointmentViewComponent` and changes the status dropdown.
2. `save()` builds an update payload including `appointmentStatus: selected.appointmentStatus`.
3. The HTTP request hits `PUT /api/app/appointments/{id}`.
4. `AppointmentUpdateDto` has no `AppointmentStatus` property -- the value is discarded by the deserializer.
5. `AppointmentsAppService.UpdateAsync` calls `AppointmentManager.UpdateAsync` with no status parameter.
6. The appointment's status remains unchanged in the database.
7. The Angular component reloads the appointment and displays the unchanged status, making the user's change appear to have reverted.

### Impact

The status field is frozen at the value set during creation. No status changes made through the UI are ever saved. This renders the status field effectively read-only after creation, even though the UI presents it as editable.

### Recommended Fix

This bug cannot be fully fixed in isolation without first designing the appointment status workflow ([FEAT-01](INCOMPLETE-FEATURES.md#feat-01-appointment-status-workflow-has-no-implementation)). However, the immediate fix is:

1. Add `AppointmentStatusType AppointmentStatus { get; set; }` to `AppointmentUpdateDto`.
2. Add `AppointmentStatusType appointmentStatus` parameter to `AppointmentManager.UpdateAsync`.
3. Set `appointment.AppointmentStatus = appointmentStatus` inside the manager.
4. Add transition validation (only valid transitions from the current status should be allowed).

---

## BUG-03: GetDoctorAvailabilityLookupAsync Filter Condition Is Always False

**Severity:** Medium
**Status:** Open

### Description

`AppointmentsAppService.GetDoctorAvailabilityLookupAsync` applies a `WhereIf` filter with a condition that can never be true, causing the filter to never execute and returning unfiltered results.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` lines 143--153:

```csharp
var query = await _doctorAvailabilityRepository.GetQueryableAsync();
query = query
    .WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
        x => x.FromTime != null   // FromTime is TimeOnly -- a value type -- it is NEVER null
    );
```

`TimeOnly` is a non-nullable value type in .NET. The expression `x.FromTime != null` is always `true` (or always `false` depending on compiler interpretation), meaning the `WhereIf` inner predicate never actually filters by the user's search input. All availability records are returned for every lookup call.

### Impact

The availability lookup dropdown in the appointment forms does not filter by user input, returning all records regardless of what the user types. For a doctor with many availability slots this results in an unmanageable dropdown.

### Recommended Fix

Determine what the filter was intended to do and replace the condition. A typical availability lookup filter would match on date or location name:

```csharp
.WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
    x => x.AvailableDate.ToString().Contains(input.Filter)
)
```

---

## BUG-04: Slot Preview Uses Only the First Input's Location Label

**Severity:** Medium
**Status:** Open

### Description

`DoctorAvailabilitiesAppService.GeneratePreviewAsync` uses `input.First().LocationId` to look up the location name for the preview result header. When a preview is generated for multiple locations in a single call, only the first item's location name is used for all results.

### Affected File

`src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` line 233:

```csharp
var locationName = (await _locationRepository.GetAsync(input.First().LocationId)).Name;
// locationName is reused for every slot in the result, even for different LocationIds
```

### Impact

If a batch preview is requested for multiple locations (e.g., Location A and Location B), every slot in the preview result shows Location A's name. The user cannot visually distinguish which slots belong to which location.

### Recommended Fix

Look up the location name per item, or build a dictionary keyed by `LocationId` before the loop:

```csharp
var locationIds = input.Select(x => x.LocationId).Distinct().ToList();
var locations = (await _locationRepository.GetListAsync(x => locationIds.Contains(x.Id)))
    .ToDictionary(x => x.Id, x => x.Name);

// Then inside the loop:
var locationName = locations[item.LocationId];
```

---

## BUG-05: Slot Save Fires N+1 Individual HTTP POSTs

**Severity:** Medium
**Status:** Open

### Description

`DoctorAvailabilityGenerateComponent.submit()` saves generated slots by calling `this.service.create(slot)` individually for each slot and aggregating them with `forkJoin`. There is no bulk-insert API endpoint; each slot results in a separate HTTP `POST /api/app/doctor-availabilities` request.

### Affected File

`angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` lines 225--253:

```typescript
submit(): void {
    const saveObservables = this.previewSlots.map(slot =>
        this.service.create({ ...slot })
    );
    forkJoin(saveObservables).subscribe(...);
}
```

For a typical day of 15-minute slots across an 8-hour workday, this generates 32 parallel HTTP requests. For a week of availability, this is 160 requests.

### Impact

- High latency for bulk slot creation -- users wait for all requests to complete before the UI updates.
- The server handles 32+ parallel EF Core `INSERT` operations, each in its own HTTP pipeline. Under load, this saturates the thread pool.
- If any individual request fails (timeout, transient error), some slots are saved and some are not, leaving the availability in a partial state with no indication of which slots failed.

### Recommended Fix

Add a bulk-create endpoint to `DoctorAvailabilitiesAppService` and `DoctorAvailabilityController`:

```csharp
// New application service method
public async Task<List<DoctorAvailabilityDto>> CreateManyAsync(List<DoctorAvailabilityCreateDto> inputs)
{
    var results = new List<DoctorAvailabilityDto>();
    foreach (var input in inputs)
        results.Add(await CreateAsync(input));
    return results;
}
```

The Angular component then makes a single HTTP call with the full list of slots.

---

## BUG-06: goBack() Always Navigates to Root Regardless of Origin

**Severity:** Low
**Status:** Open

### Description

`AppointmentViewComponent.goBack()` and `AppointmentAddComponent`'s back navigation both call `this.router.navigateByUrl('/')` unconditionally, dropping the user at the home page regardless of where they navigated from.

### Affected Files

- `angular/src/app/appointments/appointment/components/appointment-view.component.ts` line 209
- `angular/src/app/appointments/appointment-add.component.ts` line 383

```typescript
goBack(): void {
    this.router.navigateByUrl('/');  // always goes home
}
```

### Impact

An admin who navigates to an appointment detail from `/appointments` (the appointment list) and clicks Back is taken to `/` (the home page) instead of `/appointments`. They must navigate manually back to where they were. This is a minor UX issue but affects every use of the back button in the two most-used views.

### Recommended Fix

Use Angular's `Location` service to go back in browser history, or capture the previous URL in the route state:

```typescript
constructor(private location: Location) {}

goBack(): void {
    this.location.back();
}
```

---

## BUG-07: onSubmit() Error in save() Is Silently Swallowed

**Severity:** Low
**Status:** Open

### Description

`AppointmentAddComponent.save()` calls `this.onSubmit()` without `await`. Because `onSubmit` is an `async` function, this call creates a `Promise` that is never awaited and never caught. Any exception thrown inside `onSubmit` -- including errors from `createEmployerDetailsIfProvided`, `upsertApplicantAttorneyForAppointmentIfProvided`, or the main `create` API call -- becomes an unhandled Promise rejection.

### Affected File

`angular/src/app/appointments/appointment-add.component.ts` line 373:

```typescript
save(): void {
    this.onSubmit();  // async function called without await -- exception is lost
}
```

The `onSubmit` method has a `try/finally` block that resets `isSaving = false` in `finally`, but there is no `catch` block to display an error to the user.

### Impact

If the appointment creation API call fails (e.g., server error, validation error, network timeout), the user sees the "Save" button re-enable but receives no error message. The appointment is silently not created. The user has no indication that anything went wrong.

### Recommended Fix

Make `save()` async and await `onSubmit`, or add explicit error handling:

```typescript
async save(): Promise<void> {
    try {
        await this.onSubmit();
    } catch (err) {
        this.toastr.error(this.localizationService.instant('::AppointmentSaveFailed'));
    }
}
```

---

## BUG-08: QUICK-START.md Instructs ng serve Which Silently Breaks the App

**Severity:** High
**Status:** Fixed -- Fixed in `docs/QUICK-START.md` on 2026-04-03. Correct `npx ng build --configuration development` + `npx serve` sequence now documented.

### Description

`docs/QUICK-START.md` Step 4 instructs developers to run `ng serve` to start the Angular application. This command silently produces a broken application due to an Angular 20 + Vite pre-bundling incompatibility.

### Root Cause

Angular 20 uses Vite as its dev-server bundler. Vite pre-bundles `@abp/ng.core` into two separate JavaScript chunks. When both chunks are loaded in the browser, two instances of `InjectionToken("CORE_OPTIONS")` exist simultaneously. Angular's dependency injection system treats them as different tokens, causing all `@abp/ng.core` services to fail silently.

The application loads, the login page appears, but authentication, route guards, and all ABP services are non-functional. There is no visible error message.

### Affected File

`docs/QUICK-START.md` Step 4

### Workaround (Already Documented in LOCAL_DEV_SETUP.md)

```bash
npx ng build --configuration development
npx serve -s dist/CaseEvaluation/browser -p 4200
```

### Recommended Fix

Update `QUICK-START.md` Step 4 to replace the `ng serve` instruction with the working static-serve approach. Add a note explaining why `ng serve` cannot be used until the upstream ABP/Vite issue is resolved.

The existing `LOCAL_DEV_SETUP.md` already documents the correct steps and should be referenced as the authoritative setup guide.

---

## BUG-09: Past-Date Appointments Accepted Without Validation

**Severity:** Medium
**Status:** Open -- **Confirmed via E2E testing (2026-04-02, test E1)**

### Description

`AppointmentsAppService.CreateAsync` performs no validation on whether `appointmentDate` is in the past. A user can create an appointment for a date that has already passed, which has no business meaning in a scheduling system.

### Test Evidence

```
Exploratory test E1:
  Created availability slot for 2026-01-15 (past date)
  POST /api/app/appointments with appointmentDate=2026-01-15T09:30:00
  Result: 200 OK - appointment created successfully
```

### Affected Files

- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` -- `CreateAsync` method lacks date validation

### Impact

- Appointments can be backdated, corrupting scheduling data
- Reports and analytics may include impossible past appointments
- No audit trail distinguishing legitimate past appointments from erroneously created ones

### Recommended Fix

Add a validation check in `CreateAsync` before the FK validation chain:

```csharp
if (input.AppointmentDate < Clock.Now.Date)
    throw new BusinessException("CaseEvaluation:AppointmentDateInPast");
```

---

## BUG-10: fromTime > toTime Accepted on Slot Creation

**Severity:** Medium
**Status:** Open -- **Confirmed via E2E testing (2026-04-02, tests B7.4.2 and E7)**

### Description

`DoctorAvailabilitiesAppService.CreateAsync` does not validate that `fromTime < toTime`. A slot can be created with `fromTime=15:00, toTime=14:00`, which represents a negative-duration time range. The `GeneratePreviewAsync` method validates this, but direct creation via `CreateAsync` does not.

### Test Evidence

```
B7.4.2: POST /api/app/doctor-availabilities
  Body: { fromTime: "15:00:00", toTime: "14:00:00", ... }
  Result: 200 OK - slot created with invalid time range

E7 (exploratory): Same test, same result.
```

### Affected Files

- `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` -- `CreateAsync` method

### Impact

- Invalid slots appear in scheduling UI
- Booking validation may fail in unexpected ways when time range is inverted
- Inconsistent validation between Preview (validates) and Create (does not)

### Recommended Fix

Add the same validation from `GeneratePreviewAsync` to `CreateAsync`:

```csharp
if (input.FromTime >= input.ToTime)
    throw new BusinessException("CaseEvaluation:InvalidTimeRange");
```

---

## Related Documentation

- [Issues Overview](OVERVIEW.md) -- All issues by category and severity
- [Test Evidence](TEST-EVIDENCE.md) -- Full E2E test results and evidence
- [Data Integrity Issues](DATA-INTEGRITY.md) -- Race conditions related to slot booking
- [Incomplete Features](INCOMPLETE-FEATURES.md) -- Status workflow gap underlying BUG-02
- [Doctor Availability](../business-domain/DOCTOR-AVAILABILITY.md) -- Intended slot conflict rules
- [Appointment Booking Flow](../frontend/APPOINTMENT-BOOKING-FLOW.md) -- Booking component detail
- [Development Setup](../devops/DEVELOPMENT-SETUP.md) -- Local environment setup
