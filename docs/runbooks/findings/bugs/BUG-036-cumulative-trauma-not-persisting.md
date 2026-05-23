---
id: BUG-036
title: Cumulative trauma flag and ToDateOfInjury not persisting on booking submit
severity: medium
status: open
found: 2026-05-14 hardening Phase 3.6
promoted-from: OBS-15 (2026-05-22)
flow: booking-claim-information-modal
component: angular/src/app/appointments/sections/appointment-add-claim-information.component.ts
---

# BUG-036 — Cumulative trauma flag + ToDateOfInjury not persisting

> **Promoted from OBS-15 on 2026-05-22.** Originally filed as an observation under the "driver-artifact most likely" theory. Promoting to a tracked bug so a session-A/B pass picks it up. The fix path is trivial if confirmed real; the cost of finding out via live repro is small.
>
> **Verification 2026-05-22 (code-side, partial):** the suspicious serializer line cited below exists exactly as described -- `src/.../appointment-add-claim-information.component.ts:282` reads `isCumulativeInjury: v.injuryCumulative === true`. The strict-equality on a value that may arrive as the string `"true"` (HTML radio default) instead of boolean `true` is a known Angular hazard. Whether the FormControl actually carries a string or a boolean depends on `[value]` binding semantics in this exact reactive-forms setup, which I haven't traced end-to-end. Live repro by a human is still the deciding test.
>
> **Suggested fix shape if confirmed:** loosen line 282 to `Boolean(v.injuryCumulative)` or `v.injuryCumulative == true` (==, not ===). Apply the same coercion at the matching `isActive: v.injuryInsuranceEnabled === true` line (~290) which has the identical pattern.

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
