---
feature: One storage slot for the patient Unit number
date: 2026-08-13
status: approved
base-branch: main
related-issues: []
---

# Every "Unit #" box writes to the same column

Found by the phase 6 live gate (2026-08-13). Reproduced as a staff member would: typed a unit into
the box labelled "Unit #" on the internal appointment detail, saved, confirmed the handler queued a
fresh Case Tracker push, and read the payload it built. `unit` came back null.

## The defect

"Unit #" is one label over TWO columns, and which one you fill depends on the screen:

| Surface                                    | "Unit #" writes to   |
| ------------------------------------------ | -------------------- |
| Booking wizard (patient demographics step) | `Patient.Address`    |
| Send-back / request-info correction        | `Patient.Address`    |
| Internal appointment detail                | `Patient.ApptNumber` |
| Appointment view                           | `Patient.ApptNumber` |
| Patient profile                            | `Patient.ApptNumber` |

`PartyResolver` sends `Patient.Address`. So a unit typed at BOOKING reaches the Case Tracker and a
unit typed or corrected by STAFF never does. Worse than missing: if a patient books with a unit and
staff later correct it, the Case Tracker keeps receiving the stale booking-time value forever.

This is NOT a phase 6 regression. The two columns long predate it; phase 6 was simply the first time
the address was ever sent, so it was the first time the split mattered. Levon needs the unit for
proof-of-service, which is why it matters now.

## Decisions (Adrian, 2026-08-13, via modal)

- **Full cleanup, not the two-line read patch.** Every screen writes ONE column: `ApptNumber`.
- **No backfill. New data only.** Existing rows are left exactly as they are -- this data is a legal
  record and a heuristic "is this a unit or a street line?" migration would be approximation. The
  observed data justifies the caution: some rows hold a street line duplicated from `Street`.
- **Therefore reads MUST fall back.** `ApptNumber ?? Address` everywhere the unit is read. Without
  the fallback, every patient who ever booked with a unit would silently stop having it sent -- new
  data fixed by breaking existing data.
- Deploy waits for this; it ships with phase 6 and the JDF work.

## Gotchas

- **Do NOT rename the wizard's `address` form control.** It is threaded through prefill, the address
  autocomplete (`suite: 'address'`), the review step, `step-errors.util.ts` and specs. Renaming is a
  wide, risky change to the highest-traffic flow for zero behaviour gain. Map at the SERVER BOUNDARY
  instead: the control keeps its name, its value goes into `apptNumber` on the DTO.
- The send-back registry key stays `address` too. It is persisted on historic info-request records;
  changing the key would orphan them. Only the column it reads/writes changes.
- `AppointmentDemographicsPdfDocument` composes `Street ?? Address` -- Address as a STREET fallback,
  a third meaning. Deliberately NOT touched: after this change new bookings leave `Address` empty so
  the expression resolves to `Street` on its own, and historic rows keep today's output. Changing a
  report's output is its own risk and is not needed to fix the wire.

## Tasks

### T1 -- the booking wizard writes ApptNumber

- what: MODIFY `angular/src/app/appointments/appointment-add.component.ts`. SUBMIT sites (~2553 create,
  ~3279 update): send the `address` control's value as `apptNumber`, and stop sending `address`. The
  update path currently sends `apptNumber: existing.apptNumber` (~3288) -- that must become the form
  value or the edit silently reverts. PREFILL sites (~1701, ~2515, ~2632, ~2765): read
  `patient.apptNumber ?? patient.address` so an existing booking-time unit still populates the box.
- approach: code
- acceptance (EARS): WHEN a unit is entered in the booking wizard, THE SYSTEM SHALL persist it to
  `ApptNumber` and SHALL NOT write `Address`. WHEN an existing patient holds a unit in either column,
  THE SYSTEM SHALL prefill the box from it.

### T2 -- send-back corrections write ApptNumber

- what: MODIFY `InfoRequestFields.cs` (~113): the `address` spec reads `ApptNumber ?? Address` and
  writes `ApptNumber`. Update its comment and the matching comment in
  `angular/src/app/appointments/appointment/send-back-fields.ts` so neither still claims
  `-> Patient.Address`.
- approach: code
- acceptance (EARS): WHEN staff correct the unit through a send-back, THE SYSTEM SHALL write
  `ApptNumber`. WHERE only the legacy column holds a value, THE SYSTEM SHALL still display it.

### T3 -- the Case Tracker payload prefers ApptNumber

- what: MODIFY `Payload/PartyResolver.cs` (~68): `Unit = patient.ApptNumber ?? patient.Address`, with
  a comment stating WHY the fallback exists (no backfill; historic units live in the legacy column)
  and that it can be dropped once those rows are gone.
- approach: tdd
- acceptance (EARS): WHEN `ApptNumber` is set, THE SYSTEM SHALL send it as `unit`. WHEN only
  `Address` is set, THE SYSTEM SHALL send that. WHEN both are set, THE SYSTEM SHALL prefer
  `ApptNumber`, because it is the one staff corrections land in.

### T4 -- contract wording

- what: MODIFY `docs/integration/case-tracker-api-contract.md` section A: `unit` is the unit/suite
  number; note that it may be absent on older records and that a correction always wins.
- approach: code

## Validation loop

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

Frontend (T1 touches Angular):

```
export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe"
npx prettier --check <changed> && npx eslint <changed>
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

Mutation checks (required):

- Make `PartyResolver` read `Address` first; confirm the precedence test fails.
- Drop the `?? patient.Address` fallback; confirm the legacy-value test fails.

## Live gate

Book through the wizard with a unit, confirm it lands in `ApptNumber` and reaches the wire. Then
correct it on the internal detail and confirm the NEW value is what gets pushed -- that is the exact
case that is broken today.

## Risk / rollback

Blast radius: the booking submit mapping, one send-back field, one payload line.

1. **The booking path is the product's highest-traffic flow.** The mitigation is not renaming the
   control: the diff stays at the DTO boundary, so the form, autocomplete and validation are untouched.
2. **The update path's `apptNumber: existing.apptNumber`** is the trap. Miss it and a staff edit in
   the wizard silently reverts the unit to its stored value.
3. No migration, so rollback is a revert with no data to unwind.
