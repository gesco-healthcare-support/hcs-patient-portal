# Doctor Availabilities -- bulk slot generation for a doctor's schedule

## What Lives Here

`doctor-availability/` -- list, generate, and add routes plus supporting services.

Key file: `doctor-availability-generate.component.ts` -- the bulk generate form.
Spec file: `doctor-availability-generate.component.spec.ts` -- covers `buildPayload` only.

## Routes

Three routes registered in `doctor-availability-routes.ts`:

| Path       | Component                           | Notes                          |
| ---------- | ----------------------------------- | ------------------------------ |
| `''`       | DoctorAvailabilityComponent (list)  | abstract+concrete pair         |
| `generate` | DoctorAvailabilityGenerateComponent | bulk generate entry point      |
| `add`      | DoctorAvailabilityGenerateComponent | intentional alias -- same form |

Both `generate` and `add` load the same component. This is a deliberate dual entry point:
the list page links to `add`; the toolbar action links to `generate`. Do not split them.

## Conventions

### LookupTypeaheadMtmComponent -- always call toIdArray() before the API

`LookupTypeaheadMtmComponent` (`abp-lookup-typeahead-mtm`) writes `{ id, name }` objects
into the form control, NOT bare Guid strings. The backend DTO expects `Guid[]`.

IMPORTANT: always collapse via `toIdArray()` before building the payload. Passing raw
objects causes a server 400 -- the error will NOT appear at compile time.

```typescript
// in buildPayload():
appointmentTypeIds: this.toIdArray(value.appointmentTypeIds),
```

`toIdArray()` handles both the object shape and the raw-string shape safely.

### selectedDays sentinel -- empty array means "all days"

The backend interprets `selectedDays: []` as "run on every day of the week."
When all 7 checkboxes are checked, `buildPayload()` sends `[]`, NOT `[0,1,2,3,4,5,6]`.

```typescript
const selectedDays = checkedDays.length === 7 ? [] : checkedDays;
```

Never pass a full 7-element array; the server treats it as a no-day filter (undefined behavior).

### Time normalization -- normalizeTime()

`<input type="time">` may return `"HH:mm"` (2-part) or `"HH:mm:ss"` (3-part). The backend
expects `"HH:mm:ss"`. Call `normalizeTime()` on every `fromTime`/`toTime` value before
sending; it pads 2-part values and strips sub-second fractions.

### Conflict preview -- generate before submit

`generate()` calls `service.generatePreview()` and populates `preview[]`. `submit()` calls
`service.createRange()`. The "Submit" button is disabled until `canSubmit = true`, which
requires at least one slot in the preview AND no unresolved conflicts (`hasConflicts = false`).
Do not short-circuit this gate -- it protects against double-booking.

### Capacity default

Default capacity is 3 (set in the `FormGroup` constructor). This was a locked decision on
2026-05-27; do not change it without explicit approval.

## Gotchas

- `removeTimeRange()` refuses to drop the last row -- minimum one time range is enforced in
  code, not just in the template. The spec covers this case.
- `reset()` manually rebuilds the `timeRanges` FormArray back to one empty row because
  `FormGroup.reset()` does not truncate arrays.
- The spec instantiates the component class directly via `TestBed.runInInjectionContext` to
  avoid compiling the LeptonX template tree. Keep the template free of logic so this remains
  viable.

## Related

- angular/src/app/doctor-availabilities/doctor-availability/doctor-availability-routes.ts
- angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts
- docs/frontend/ROUTING-AND-NAVIGATION.md
- docs/frontend/COMPONENT-PATTERNS.md
