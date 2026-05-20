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

Add a new `@Input` for the new display data, and a new
`@Output` to drive the parent's refetch:

```typescript
@Input({ required: true }) appointmentTimeOptions: Array<{
  value: string;
  label: string;
  doctorAvailabilityId: string;
  capacity: number;
  remainingCapacity: number;
}> = [];

@Output() requestRefetch = new EventEmitter<void>();
```

### 2. `angular/src/app/appointments/sections/appointment-add-schedule.component.html`

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

The parent owns `appointmentTimeOptions`. Update the cache
builder (search for `rebuildAppointmentTimeOptions` or
similar) to project `remainingCapacity` from the lookup
response:

```typescript
this.appointmentTimeOptions = slots.map(s => ({
  value: this.serializeAppointmentDateTime(s.availableDate, s.fromTime),
  label: `${s.fromTime} - ${s.toTime}`,
  doctorAvailabilityId: s.id,
  capacity: s.capacity ?? 1,
  remainingCapacity: s.remainingCapacity ?? 1,
}));
```

The lookup response now carries `capacity` and
`remainingCapacity` on each `DoctorAvailabilityDto`. Plan 1
added `capacity`; plan 3 added `remainingCapacity`. The
auto-regenerated proxy from plan 3 surfaces both fields.

### 4. Error handler in `appointment-add.component.ts` submit path

The component's `onSubmit()` (or whatever calls
`this.appointmentService.create(...)`) catches errors. Add:

```typescript
this.appointmentService.create(payload).subscribe({
  next: result => { /* existing success path */ },
  error: err => {
    const code = err?.error?.error?.code as string | undefined;
    if (code === 'CaseEvaluation:Appointment.BookingSlotFull'
     || code === 'CaseEvaluation:Appointment.BookingSlotClosed'
     || code === 'CaseEvaluation:Appointment.BookingSlotTypeMismatch') {
      this.toaster.warn(err.error.error.message, '', { life: 5000 });
      this.scheduleSection.requestRefetch.emit();
      this.form.get('appointmentTime')?.setValue(null);
      this.form.get('appointmentTime')?.markAsUntouched();
      return;
    }
    // existing fallback
    this.toaster.error(err?.error?.error?.message ?? 'Booking failed.', '', { life: 5000 });
  },
});
```

`scheduleSection` is a `@ViewChild(AppointmentAddScheduleComponent)`.
Wire it up at the top of the parent class if not present.

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

The view-page reschedule flow uses a similar slot picker (lines
~700-800 -- find via Grep on `appointmentTime` /
`doctorAvailabilityId`). Apply the same changes: display
`remainingCapacity`, handle the three error codes on save.

Reschedule is plan 6's primary booking-time gate test surface;
the picker change here is symmetric with the booking form.

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

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Plan 7 (tests + hardening) is the last in the series.
