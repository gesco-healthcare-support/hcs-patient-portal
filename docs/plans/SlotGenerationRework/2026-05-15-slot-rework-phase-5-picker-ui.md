---
status: draft
issue: slot-rework-phase-5-picker-ui
owner: AdrianG
created: 2026-05-15
approach: test-after (booking-form picker is visual; assert
  through the browser, not Jasmine)
sequence: 6 of 7 (slot-generation + doctor-invariant series)
depends-on: 2026-05-15-slot-rework-phase-4-generation-ui.md
branch: create a new branch off `feat/replicate-old-app`. PR back
  to `feat/replicate-old-app`. Do not merge to `main` until plans
  2 through 7 are merged together.
---

# Slot rework Phase 5: booking-form picker UI

## Locked decisions -- 2026-05-27 (round 2; Adrian)

These supersede any conflicting text below.

- **External booking picker shows BINARY availability only.** A slot is
  selectable when remaining capacity >= 1, otherwise shown as unavailable.
  **Never display the capacity number or remaining count to external users.**
  This simplifies the plan: do NOT render `remainingCapacity` as a number; just
  include/enable bookable slots and exclude/disable full ones. The server-side
  capacity gate (phase-2) does the enforcement; phase-3 supplies the computed
  "bookable" signal on the slot data this picker consumes.
- **Reschedule is DESCOPED from this wave.** There is no reschedule slot-picker
  UI in the codebase (confirmed in the re-verification below). This plan covers
  the BOOKING flow only. Drop the files-touched item for the reschedule /
  appointment-view path. Reschedule capacity-awareness becomes a separate later
  ticket. The "verify BOTH booking and reschedule" mandate is reduced to
  booking only.
- The three new booking error codes still drive the refetch cascade -- their
  literals must BYTE-MATCH the `CaseEvaluationDomainErrorCodes` constants
  defined in phase-2.

## Re-verified 2026-05-27 (HEAD ad07947) -- NOTE: no prior readiness check existed; first re-verification

Verified against current source. Plan intent is sound but several
facts are stale and one referenced component (reschedule UI) does
not exist. Evidence below; confidence HIGH unless noted.

**Confirmed correct (still valid):**
- `appointment-add-schedule.component.ts` / `.html` exist, are
  standalone, OnPush. The time `<select>` uses `@for ... track
  slot.value` with `<option [value]="slot.value">{{ slot.label }}</option>`
  (`.html`:103-112). Plan's @for/@control-flow style matches repo.
- Parent `appointment-add.component.ts` owns `appointmentTimeOptions`
  and the cascade subs (`appointment-add.component.ts`:139-140,
  411-426). `loadAvailableDatesBySelection()` exists (:2136) and is
  wired to locationId + appointmentTypeId valueChanges (:412-414).
- ABP `withHttpErrorConfig` screen-intercepts ONLY [401,403,404,500]
  (`app.config.ts`:126-132). A 400 (the booking error codes) is NOT
  screened, so a component `subscribe error:` / try-catch handles it.
  This is the correct hook for the cascade. (HIGH)
- `ToasterService` from `@abp/ng.theme.shared` is the repo's toast
  pattern (`appointment-documents.component.ts`:14,92;
  `doctor-availability.abstract.service.ts`:60). Error shape accessor
  `err?.error?.error?.code` / `.message` is the repo convention
  (`invite-external-user.component.ts`:116). (HIGH)

**STALE / BROKEN -- corrected inline below:**
1. **Slot source is NOT the lookup endpoint.** The picker currently
   fetches via `GET /api/app/doctor-availabilities` (paged list,
   filtered `bookingStatusId=Available`) in `fetchAllAvailableSlots()`
   (`appointment-add.component.ts`:2345-2384), reading
   `item.doctorAvailability` (the WithNavigationProperties shape).
   The plan assumes `GetDoctorAvailabilityLookupAsync`. A
   `getDoctorAvailabilityLookup` proxy method DOES exist
   (`doctor-availability.service.ts`:76-82, returns
   `DoctorAvailabilityDto[]` at `/lookup`) but the picker does not use
   it. DEPENDENCY: Phase 3 must either add capacity to the list
   endpoint OR this plan must first switch the picker to the lookup
   endpoint. Flagged as a blocking question. (HIGH)
2. **`appointmentTimeOptions` type has 3 fields, not 5.** Current:
   `{ value; label; doctorAvailabilityId }` (`.ts`:139-140 parent;
   `schedule.component.ts`:76-80 child Input). No `capacity` /
   `remainingCapacity`. These come from Phases 1+3 (DTO not built yet).
3. **No `requestRefetch` @Output exists** on the schedule component
   (`schedule.component.ts`:92-93 has only `locationSelected` +
   `appointmentDateCleared`). Plan adds it -- correct, but it is NEW.
4. **`DoctorAvailabilityDto` has NO capacity/remainingCapacity**
   (`doctor-availabilities/models.ts`:32-40). Added by Phases 1+3.
   Treat all `s.capacity` / `s.remainingCapacity` reads as
   "verify after Phases 1-3".
5. **The 3 booking error codes DO NOT EXIST yet.** Not in
   `CaseEvaluationDomainErrorCodes.cs` (checked full file). Phase 2
   defines them. CRITICAL NAMING MISMATCH (see Files-touched #4).
6. **`appointment-view.component.ts` is NOT a reschedule picker.** Its
   `save()` (:574-) is the appointment EDIT/update flow; it reads the
   EXISTING `appointment.appointment.doctorAvailabilityId` directly
   (:583, :665) with NO slot picker, NO `appointmentTimeOptions`, and
   surfaces results via inline `errorMessage`/`successMessage`
   strings, NOT a toaster. Lines are ~574-700, not the plan's
   ~700-800. (HIGH)
7. **A reschedule slot-picker UI DOES NOT EXIST anywhere in the SPA.**
   Repo-wide search for reschedule/change-request UI found only the
   auto-generated proxy (`appointment-change-requests/*`) and a
   dashboard label. There is no component to "apply the same changes"
   to. Files-touched #6 cannot be executed as written. (HIGH)
8. **The parent does NOT use `ToasterService` today** and the booking
   POST uses `firstValueFrom(restService.request(...))` inside a
   `try/finally` with NO `.catch` (`appointment-add.component.ts`:
   970-991). Plan's `appointmentService.create(...).subscribe({next,
   error})` does not match the current submit shape. Corrected #4.
9. **No localization keys exist** for the 3 error messages or
   `Appointment:NoSlotsRemaining` (checked en.json). Phase 2 adds the
   error messages; this plan adds the client-only key.

**Angular 20 version-specific finding:** No surprises. Error in a 400
arrives in the `subscribe` error callback / awaited-promise catch
(the Observable contract: a stream errors at most once); try/catch
around `firstValueFrom` DOES catch it (unlike a bare `.subscribe`).
The repo already standardizes on the subscribe `error:` callback.
`httpResource()`/signals were considered and rejected: the repo uses
imperative `restService.request().subscribe()` everywhere -- stay
consistent. (Angular HTTP error-handling guidance, MEDIUM community
sources -- principle is stable across versions.)

## Goal

Update the appointment-booking-form slot picker to:

1. Render `remainingCapacity` next to each slot
   ("3 remaining of 5") so the patient sees how packed each
   slot is.
2. Disable selection (and add a "full" label) when the slot's
   `remainingCapacity` reaches 0 in the picker's local state
   (e.g., after another tab booked the same slot in the
   background and the picker refetches).
3. Surface friendly inline error messages on the three new
   booking-time error codes shipped in plan 3:
   - `Appointment:BookingSlotFull` -- show the per-slot
     "full now" message and refresh the slot list.
   - `Appointment:BookingSlotClosed` -- show "This slot is no
     longer available" and refresh.
   - `Appointment:BookingSlotTypeMismatch` -- show "This slot
     is no longer compatible with the chosen appointment type."
     Refresh, then keep the patient on the form (do NOT
     navigate away).
4. Re-query the picker when the form's `appointmentTypeId`
   changes (today's behavior) AND when the form's
   `locationId` changes (today's behavior), AND when the
   server returns one of the three new error codes (re-fetch
   so the patient sees the current state).
5. Keep the existing ngbDatepicker `[markDisabled]` integration
   wired -- it now reads from a filtered cache that excludes
   full slots.

## Why

Plan 3's `GetDoctorAvailabilityLookupAsync` returns
`DoctorAvailabilityDto.RemainingCapacity` and excludes the
full and reserved slots from the result set. Today's picker
(`angular/src/app/appointments/sections/appointment-add-schedule.component.ts`
+ html) renders a flat dropdown of times with no
capacity hint.

The picker upgrade lets patients see "3 remaining" before they
commit, and rules out clicks on slots that are already full.

The three new error codes (plan 3) replace the OLD's single
"slot no longer available" generic message. Friendly inline
errors with the new wording reduce repeat help-desk traffic.

## Non-goals

- No change to the appointment-booking-form's other sections
  (patient demographics, employer details, attorney, etc.).
  This plan is scoped to the schedule section.
- No new permission gates -- the lookup endpoint stays
  `[Authorize]` only (any authenticated user).
- No client-side capacity tracking. The server is authoritative;
  the picker re-queries the lookup endpoint on every relevant
  cascade.

## Decisions locked

1. **Display format**: `"{ToTime} -- {RemainingCapacity} of
   {Capacity} remaining"`. When `RemainingCapacity ==
   Capacity` (no bookings yet), suppress the "of X" suffix and
   show plain `"{ToTime}"`.

2. **Full slots are NOT rendered**. Plan 3's
   `GetDoctorAvailabilityLookupAsync` already filters them out
   server-side. If the picker holds a slot in local state when
   the server's most recent response no longer contains it,
   the picker invalidates the form's `appointmentTime` and
   shows the inline message.

3. **Disabled state visual cue**: drop the row entirely. Do
   NOT show "full" rows greyed out. Reason: a full row that
   appears in the dropdown invites refreshing-to-see-if-anyone-
   cancelled hunting, which races the booking-rate gate.

4. **Error-driven refetch**: when the booking POST returns one
   of the three error codes, the picker triggers a refetch and
   reseeds the cache. The user keeps their selected
   AppointmentType and Location but the time dropdown clears.

5. **No animation on capacity transitions**. Patients are not
   gaming the system.

## Files touched

### 1. `angular/src/app/appointments/sections/appointment-add-schedule.component.ts`

> RE-VERIFY 2026-05-27: confirmed file + standalone + OnPush. Current
> `appointmentTimeOptions` Input is `{ value; label;
> doctorAvailabilityId }` (no capacity fields) at `schedule.component.ts`
> :76-80. Current outputs are only `locationSelected` +
> `appointmentDateCleared` (:92-93). The two NEW fields below depend on
> Phases 1+3 landing capacity on the DTO; `requestRefetch` is net-new.

Extend the existing `@Input appointmentTimeOptions` with the two new
display fields, and add a new `@Output` to drive the parent's refetch:

```typescript
// EXTEND the existing required Input (do not duplicate it):
@Input({ required: true }) appointmentTimeOptions: Array<{
  value: string;
  label: string;
  doctorAvailabilityId: string;
  capacity: number;          // verify after Phases 1-3 (DTO field)
  remainingCapacity: number; // verify after Phases 1-3 (DTO field)
}> = [];

@Output() requestRefetch = new EventEmitter<void>();
```

### 2. `angular/src/app/appointments/sections/appointment-add-schedule.component.html`

> RE-VERIFY 2026-05-27: current markup at `.html`:103-112 matches the
> pre-change shape below (same `formControlName`, `@for ... track
> slot.value`, `<option [value]="slot.value">{{ slot.label }}</option>`).
> NOTE the existing empty-state literal at `.html`:113-119 ("No
> available time slots for selected date.") -- see Files-touched #8,
> swap to the localized key there, not a new block.

Update the time `<option>` rendering to use the new display
format:

```html
<select class="form-select" formControlName="appointmentTime"
        [class.is-invalid]="isFieldInvalid('appointmentTime')">
  <option [ngValue]="null">SELECT</option>
  @for (slot of appointmentTimeOptions; track slot.value) {
    <option [value]="slot.value">
      {{ slot.label }}
      @if (slot.remainingCapacity < slot.capacity) {
        -- {{ slot.remainingCapacity }} of {{ slot.capacity }} remaining
      }
    </option>
  }
</select>
```

### 3. `angular/src/app/appointments/appointment-add.component.ts`

The parent owns `appointmentTimeOptions`.

> RE-VERIFY 2026-05-27: there is NO `rebuildAppointmentTimeOptions`
> method. The actual cache builder is `populateTimeSlotsForDate()`
> (`appointment-add.component.ts`:2251-2271), which maps from the
> parent-owned `availableSlotsByDate` Map (NOT directly from the HTTP
> response). The Map is keyed by date and holds `{ time;
> doctorAvailabilityId }` (:132-135), populated by
> `loadAvailableDatesBySelection` (:2155-2185) from
> `fetchAllAvailableSlots` (:2345-2384). There is no
> `serializeAppointmentDateTime` helper -- the value is `slot.time`
> and the date/time are joined at submit via
> `combineAppointmentDateAndTime` (:2303-2317).
>
> To carry capacity, you must thread `capacity` + `remainingCapacity`
> through BOTH the `availableSlotsByDate` Map entry type (:132-135)
> AND `populateTimeSlotsForDate` (:2251-2271), reading them from the
> slot source in `loadAvailableDatesBySelection` (:2163-2185). The
> current shape below is the REAL target, not the plan's invented
> `slots.map(s => ...)`:

```typescript
// In populateTimeSlotsForDate (current, capacity-less):
this.appointmentTimeOptions = slots.map((slot) => ({
  value: slot.time,
  label: this.toTimeLabel(slot.time),
  doctorAvailabilityId: slot.doctorAvailabilityId,
  // ADD (after Map entry carries them):
  // capacity: slot.capacity ?? 1,
  // remainingCapacity: slot.remainingCapacity ?? 1,
}));
```

DEPENDENCY (verify after Phases 1-3): `capacity` and
`remainingCapacity` must appear on the slot source. As of HEAD
ad07947 the picker reads from `GET /api/app/doctor-availabilities`
(WithNavigationProperties list), NOT the `/lookup` endpoint, and
`DoctorAvailabilityDto` carries no capacity fields. Phase 3 must
either (a) add capacity to whatever endpoint the picker reads, or
(b) this plan must first migrate `fetchAllAvailableSlots` to call
`getDoctorAvailabilityLookup` (`doctor-availability.service.ts`:76).
This is a blocking sequencing decision -- see top changelog Q1.

### 4. Error handler in `appointment-add.component.ts` submit path

> RE-VERIFY 2026-05-27 (multiple corrections):
>
> a. **The submit path is NOT `appointmentService.create(...).subscribe`.**
>    `onSubmit()` (:887-991) awaits
>    `firstValueFrom(this.restService.request({ method:'POST',
>    url:'/api/app/appointments', body: payload }))` (:970-979) inside
>    a `try { ... } finally { this.isSaving = false; }`. There is NO
>    `.catch` today -- a 400 currently propagates unhandled. Add a
>    `catch (err: any)` block; do NOT restructure into `.subscribe`.
>
> b. **The parent does not inject `ToasterService` today.** Add
>    `private readonly toaster = inject(ToasterService);` (import from
>    `@abp/ng.theme.shared`). Repo convention calls it with a single
>    message arg: `this.toaster.warn(message)` /
>    `this.toaster.error(message)` (see
>    `appointment-documents.component.ts`,
>    `doctor-availability.abstract.service.ts`:60). The plan's
>    3-arg `('', { life: 5000 })` form is ABP-valid
>    (`warn(message, title?, options?)`) but UNVERIFIED in this repo --
>    prefer the single-arg form for consistency. (MEDIUM)
>
> c. **ERROR-CODE NAMING MISMATCH -- BLOCKING.** The codes below
>    (`...BookingSlotFull` / `...BookingSlotClosed` /
>    `...BookingSlotTypeMismatch`) DO NOT EXIST in
>    `CaseEvaluationDomainErrorCodes.cs` as of HEAD ad07947. They are
>    Phase 2's deliverable. The existing booking-error naming
>    convention in that file is `CaseEvaluation:Appointment.<Pascal>`
>    (e.g. `Appointment.BookingDateInsideLeadTime`,
>    `Appointment.AmeRequiresAttorneyRole`). The string literals below
>    MUST be confirmed byte-for-byte against Phase 2's actual
>    `public const string` values before this plan is built. If Phase 2
>    names them differently (e.g. `SlotFull` vs `BookingSlotFull`),
>    update here. Do NOT hardcode unverified literals.

Add a `catch` block to the existing `onSubmit()` try/finally:

```typescript
// inside onSubmit(), wrapping the existing POST + cascade:
try {
  // ... existing payload build + POST + downstream cascade ...
  this.router.navigateByUrl('/');
} catch (err: any) {
  const code = err?.error?.error?.code as string | undefined;
  // VERIFY these 3 literals against Phase 2's DomainErrorCodes:
  if (code === 'CaseEvaluation:Appointment.BookingSlotFull'
   || code === 'CaseEvaluation:Appointment.BookingSlotClosed'
   || code === 'CaseEvaluation:Appointment.BookingSlotTypeMismatch') {
    this.toaster.warn(err?.error?.error?.message ?? 'Slot no longer available.');
    this.scheduleSection?.requestRefetch.emit();
    this.form.get('appointmentTime')?.setValue(null, { emitEvent: false });
    this.form.get('appointmentTime')?.markAsUntouched();
    return;
  }
  this.toaster.error(err?.error?.error?.message ?? 'Booking failed.');
} finally {
  this.isSaving = false;
}
```

> NOTE: ABP's `withHttpErrorConfig` (`app.config.ts`:126-132) screens
> only [401,403,404,500]; a 400 from these codes reaches this catch
> uninterrupted. (HIGH)

`scheduleSection` is a `@ViewChild(AppointmentAddScheduleComponent)`.
The parent does not declare one today -- add it.

### 5. Re-wire the refetch cascade

Subscribe to the schedule component's `requestRefetch`:

```html
<app-appointment-add-schedule
  [form]="form"
  [appointmentTimeOptions]="appointmentTimeOptions"
  ...
  (requestRefetch)="loadAvailableDatesBySelection()">
</app-appointment-add-schedule>
```

`loadAvailableDatesBySelection` already exists; it re-runs the
lookup using the current form state.

### 6. `angular/src/app/appointments/appointment/components/appointment-view.component.ts`

> RE-VERIFY 2026-05-27 -- THIS SECTION IS BROKEN AS WRITTEN. STOP and
> resolve before building. (HIGH)
>
> - `appointment-view.component.ts` is NOT a reschedule slot picker.
>   Its `save()` (:574-) is the appointment EDIT/update flow. It reads
>   the EXISTING slot id directly
>   (`selected.doctorAvailabilityId`, :583, :665), has NO
>   `appointmentTimeOptions`, NO slot dropdown, and surfaces results
>   via inline `errorMessage` / `successMessage` strings (NOT a
>   toaster). Lines are ~574-700, not ~700-800. There is "similar slot
>   picker" to mirror here.
> - **A reschedule slot-picker UI does not exist anywhere in the SPA.**
>   Repo-wide search found only the auto-generated proxy
>   (`proxy/appointment-change-requests/*`:
>   `appointment-change-request.service.ts`,
>   `appointment-change-request-approval.service.ts`, `models.ts`) and
>   a dashboard status label. No component consumes the
>   `SubmitRescheduleAsync` / `NewDoctorAvailabilityId` backend
>   surface yet.
> - Backend reschedule error codes already exist and differ from the 3
>   booking codes:
>   `AppointmentChangeRequest.NewSlotNotAvailable`,
>   `.NewSlotRequired`, `.RescheduleReasonRequired`
>   (`CaseEvaluationDomainErrorCodes.cs`:503-533).
>
> CONSEQUENCE: the "verify BOTH booking AND reschedule" mandate cannot
> be satisfied by editing an existing reschedule picker -- there isn't
> one. Options for Adrian (top changelog Q2): (a) descope reschedule
> from this plan and gate it behind a future "reschedule UI" plan;
> (b) this plan first BUILDS a reschedule picker (large -- out of the
> stated blast radius); or (c) confirm whether some later phase in
> this series owns the reschedule UI. Do not silently invent a
> reschedule component.

### 7. CSS niceties

`angular/src/app/appointments/sections/appointment-add-schedule.component.scss`
(or styles inline)

Add a small style to make the time dropdown wider so the new
"remaining" suffix fits:

```scss
:host ::ng-deep select[formControlName="appointmentTime"] {
  min-width: 14rem;
}
```

(Inline-only if no .scss exists in the section folder.)

### 8. Localization keys

Server-side `en.json` already has the booking error messages
(plan 2 added them). The picker passes through the server's
already-localized message. Add ONE new client-only key for the
empty-state when full-only slots existed (rare):

```jsonc
"Appointment:NoSlotsRemaining": "No slots are available for the selected date. Please pick another date."
```

Rendered in the picker when `appointmentTimeOptions.length === 0`
AND `appointmentDate` has a value (the existing "No available
time slots" template literal -- swap to the localized key).

> RE-VERIFY 2026-05-27: the existing literal is "No available time
> slots for selected date." at `schedule.component.html`:113-119
> (guarded by `form.get('appointmentDate')?.value &&
> !isAvailableDatesLoading && appointmentTimeOptions.length === 0`).
> Swap that literal for `{{ 'Appointment:NoSlotsRemaining' |
> abpLocalization }}`. Confirmed: no `Appointment:NoSlotsRemaining`
> key exists in en.json yet -- this plan adds it. (HIGH)

## Test plan (test-after)

### Browser verification (primary)

1. Pre-seed: as Staff Supervisor, create 1 slot with
   `Capacity = 2` for tomorrow 09:00-10:00 at Location L1 with
   `AppointmentTypeIds = [t1]`.
2. As Patient A (external user), navigate
   `/appointments/add`. Pick Location L1, AppointmentType t1.
   Expect the time picker to show "09:00 - 10:00" (no
   remaining suffix -- capacity = remaining).
3. Book the slot. Confirm 200 success.
4. As Patient B, repeat step 2. Expect the time picker to show
   "09:00 - 10:00 -- 1 of 2 remaining".
5. Book the slot. Confirm 200 success.
6. As Patient C, repeat step 2. Expect the time picker to show
   "SELECT" only -- slot is full and filtered out.
7. As Staff Supervisor, edit the slot to `Capacity = 3`. As
   Patient C, refresh the picker (re-pick location or
   AppointmentType). Slot shows "09:00 - 10:00 -- 1 of 3
   remaining".
8. As Staff Supervisor, mark the slot manually closed
   (`BookingStatusId = Reserved`). As Patient C, refresh.
   Slot is gone.

### Concurrent-booking failure path

1. Pre-seed: 1 slot `Capacity = 1`, no bookings.
2. Open two browser tabs as Patient A and Patient B (different
   accounts).
3. Both pick the same slot. A submits first -> 200. B submits
   -> 400 with `Appointment:BookingSlotFull`.
4. Expect Patient B's UI to:
   - Show a warning toast with the slot-full message.
   - Clear the `appointmentTime` field.
   - Refetch and update the dropdown (slot now absent).

### Type-mismatch failure path

1. Pre-seed: 1 slot `Capacity = 1`, `AppointmentTypes = [t1]`.
2. As Patient, pick that slot, then change AppointmentType to
   t2 BEFORE clicking save.
3. Submit. Expect 400 with
   `Appointment:BookingSlotTypeMismatch`. Toast + refetch +
   field clear.

### Unit tests (small, optional)

`angular/src/app/appointments/sections/appointment-add-schedule.component.spec.ts`

| # | Test | Acceptance |
|---|------|------------|
| 1 | `dropdown_RendersRemainingSuffix_WhenLessThanCapacity` | Bind a slot with capacity=3, remaining=2. The option text contains "2 of 3 remaining". |
| 2 | `dropdown_SuppressesRemainingSuffix_WhenAtCapacity` | Bind a slot with capacity=3, remaining=3. The option text does NOT contain "remaining". |

## Risk and rollback

**Blast radius:**
- 2 component template/TS pairs (appointment-add-schedule,
  appointment-view) + the parent appointment-add.component.ts.
- No new shared components.
- No backend changes.

> RE-VERIFY 2026-05-27: blast radius is wrong for appointment-view --
> there is no reschedule picker there to touch (see Files-touched #6).
> Realistic radius if reschedule is descoped (Q2 option a): the
> schedule section pair + the parent only. Also adds a NEW
> `ToasterService` injection to the parent (Files-touched #4b).

**Rollback:**
- Revert the commit. The picker reverts to the plain dropdown.
  The backend still emits the new error codes; the UI shows
  the raw localized message via the default toast.

**Risk: parent's `appointmentTimeOptions` shape change breaks
ngModel binding.** Mitigated: the type-extension is additive;
existing readers (`slot.value`, `slot.label`) work unchanged.

**Risk: the refetch emit loops infinitely if the lookup keeps
returning the same full slot.** Mitigated: the slot is
filtered server-side; if the server returns an empty list, the
emit cascade does not re-fire.

## Verification

After ship:

1. Build + serve.
2. Run the browser scenarios above.
3. Smoke-test the appointment view-page reschedule with the
   same matrix.

> RE-VERIFY 2026-05-27: step 3 is not executable -- no reschedule
> picker exists (Files-touched #6). Drop it if reschedule is descoped,
> or replace with the reschedule-UI plan's own verification once that
> exists.

## Dependencies (added 2026-05-27)

- **Phase 1** -- adds `capacity` to the slot DTO/entity. Not yet
  implemented (no `capacity` on `DoctorAvailabilityDto`).
- **Phase 2** -- defines the 3 booking error codes + their en.json
  messages. Not yet implemented (absent from
  `CaseEvaluationDomainErrorCodes.cs` and en.json). The error-code
  literals in Files-touched #4 are unconfirmed until Phase 2 lands.
- **Phase 3** -- adds `remainingCapacity` + (per plan) the
  capacity-aware `GetDoctorAvailabilityLookupAsync`. The proxy
  `getDoctorAvailabilityLookup` already exists but returns the
  capacity-less `DoctorAvailabilityDto[]` and the picker does not call
  it yet. This plan must not start until Phases 1-3 are merged, or its
  capacity reads will all be `?? 1` fallbacks (silent no-op).

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Plan 7 (tests + hardening) is the last in the series.
