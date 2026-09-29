# Doctor Availabilities -- slot management, bulk generation and the schedule view

## What Lives Here

- `doctor-availability/internal-availabilities.component.ts` -- the slot grid (list, close, delete);
  grid helpers in `avail-grid.util.ts`.
- `doctor-availability/internal-generate-slots.component.ts` -- the bulk generate form; its pure
  helpers (payload, slot estimate, preview grid) are in `gen-slots.util.ts`.
- `schedule/internal-schedule.component.ts` -- the read-only Schedule time grid (FullCalendar) of a
  location's slots with appointment chips; helpers in `schedule-calendar.util.ts`.
- `doctor-availability/providers/` -- the ABP menu registration (`/doctor-management/doctor-availabilities`,
  policy `CaseEvaluation.DoctorAvailabilities`).

The slot model, the capacity rule and the server's generation rules are in
docs/business-domain/DOCTOR-AVAILABILITY.md.

## Routes

| Path                                                | Component                         | Declared in                                         |
| --------------------------------------------------- | --------------------------------- | --------------------------------------------------- |
| `/doctor-management/doctor-availabilities`          | `InternalAvailabilitiesComponent` | `doctor-availability-routes.ts` (`''`)              |
| `/doctor-management/doctor-availabilities/generate` | `InternalGenerateSlotsComponent`  | `app.routes.ts` and `doctor-availability-routes.ts` |
| `/doctor-management/doctor-availabilities/add`      | `InternalGenerateSlotsComponent`  | `app.routes.ts` and `doctor-availability-routes.ts` |
| `/doctor-management/schedule`                       | `InternalScheduleComponent`       | `app.routes.ts`                                     |

`generate` and `add` load the same form on purpose; keep both. The three availability routes carry
no `data.requiredPolicy`: ABP's `permissionGuard` takes the policy from the menu registration in
`providers/` (see docs/frontend/ROUTING-AND-NAVIGATION.md). Add `requiredPolicy` to route data if
you touch them.

## Conventions

### Build the generate payload only through `buildGenerateInput()`

`gen-slots.util.ts` `buildGenerateInput(state)` turns the form state into
`DoctorAvailabilityGenerateInputDto`:

- **Two modes:** `range` sends `fromDate` / `toDate` plus `selectedDays`; `pick` sends a sorted
  `selectedDates` list instead.
- **Weekdays:** `selectedDays` is every checked index (0 = Sunday .. 6 = Saturday). The server treats
  an empty list as every day and otherwise uses exactly the listed days, so all seven checked and
  none checked mean the same thing. There is no special sentinel.
- **Times:** `"HH:mm"` from `<input type="time">` is padded to `"HH:mm:ss"` (`toTimeOnly`).
- A time range's own duration is sent only when it is a positive override; otherwise the form's
  default duration applies.
- New slots are always generated `Available`.

### Preview, then create -- conflicts are skipped by the server

**Preview** calls `generatePreview()`. **Create** is enabled once a preview exists with at least one
non-conflicting slot (`canSubmit`), and calls `createRange()`. The server re-runs the preview itself,
inserts only the non-conflicting slots and returns the inserted and skipped counts, which the page
reports. The page never has to remove conflicts by hand, and the button is not what stops a
double-booked slot -- the server is.

The client estimates the slot count (`estimateSlotCount`) and warns before the server's 5000-slot
limit (`GENERATION_SLOT_LIMIT`, mirroring `GenerationSlotLimit`).

### Capacity default

Default capacity is 3 (`capacity = signal(3)`). This was a locked decision on 2026-05-27; do not
change it without explicit approval.

## Gotchas

- `removeRange()` refuses to drop the last time range: at least one range is enforced in code, not
  just in the template.
- The earliest date offered is today plus the office's lead time, fetched at load; until it arrives
  (or if the fetch fails) no minimum is applied on the client. The server refuses past dates on its own.
- `InternalScheduleComponent` uses `ViewEncapsulation.None` because FullCalendar builds its DOM
  imperatively; keep every style rule nested under `.sched-page` so it cannot leak.

## Related

- angular/src/app/doctor-availabilities/doctor-availability/doctor-availability-routes.ts
- angular/src/app/doctor-availabilities/doctor-availability/gen-slots.util.ts
- docs/business-domain/DOCTOR-AVAILABILITY.md
- docs/frontend/ROUTING-AND-NAVIGATION.md
