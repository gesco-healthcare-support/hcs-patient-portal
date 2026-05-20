---
id: OBS-15
title: Cumulative-trauma "Yes" radio click via JS doesn't persist IsCumulativeInjury or ToDateOfInjury
severity: observation
found: 2026-05-14 hardening Phase 3.6
flow: booking-claim-information-modal
---

# OBS-15 — Cumulative trauma flag possibly not saving

## Symptom
In Phase 3.6 (cumulative-trauma single-injury booking), the Playwright driver:
1. Opened the Claim Information modal.
2. Clicked the `injuryCumulative` radio with `[value]="true"` via `radio.click()`.
3. Waited 200ms for the `@if (injuryForm.get('injuryCumulative')?.value)` block to render the To-Date input.
4. Filled `injuryDateOfInjury = 2025-06-01` and `injuryToDateOfInjury = 2025-12-10` via `el.value = ...; dispatchEvent('input')`.
5. Clicked Add → modal closed and the injury row appeared in the table.
6. Submitted the appointment.

DB inspection of the resulting injury row:
```
IsCumulativeInjury | DateOfInjury | ToDateOfInjury
0                  | 2025-06-01   | NULL
```

Expected `IsCumulativeInjury=1` and `ToDateOfInjury=2025-12-10`.

## Possible causes
1. **Driver issue (most likely)**: native `radio.click()` doesn't fire the change event in a way `RadioControlValueAccessor` recognises in this Angular build. Without setting the FormControl directly, the value stays at default `false`. The `injuryToDateOfInjury` setter wouldn't persist either since the conditional input may not have rendered before fill.
2. **Form serialiser bug**: `appointment-add-claim-information.component.ts:282`:
   ```typescript
   isCumulativeInjury: v.injuryCumulative === true,
   ```
   If the radio value is the string `"true"` (HTML form value) rather than boolean `true` (Angular property binding), `=== true` evaluates to false. The serialiser would discard the flag silently.
3. **Component template bug**: `[value]="true"` should set the input's value to the boolean, but if Angular interprets it as a string in this template, the FormControl ends up with a non-boolean truthy value that the serialiser then rejects.

## To do (manual verification)
A human should:
1. Open the booking form in a browser.
2. Click "Add" in Claim Information to open the modal.
3. Manually click the Cumulative "Yes" radio.
4. Verify the "To Date" input appears.
5. Fill From + To dates + the rest, click Add, submit appointment.
6. Inspect DB: is `IsCumulativeInjury = 1`? Is `ToDateOfInjury` saved?

If manual reproduction shows the same problem → real bug, escalate to BUG. If manual works → my driver issue, no bug.

## Related
- `angular/src/app/appointments/sections/appointment-add-claim-information.component.ts:282` (serialiser line under suspicion)
- `angular/src/app/appointments/sections/appointment-add-claim-information.component.html:113-127` (radio HTML)
