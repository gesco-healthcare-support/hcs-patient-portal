---
id: BUG-007
title: appointment-add dropdowns render empty due to abp-lookup-select OnPush CD bug
severity: blocker
status: fixed
fixed-in: PR #198
found: 2026-05-13
flow: external-user-appointment-request (also internal booking flow)
component: angular/src/app/appointments/sections/* (5 section components + appointment-view)
---

# BUG-007 — Appointment-add dropdowns empty (lookup-select CD)

## Severity
blocker

## Status
**FIXED in PR #198** — `AppLookupSelectComponent` wrapper introduced; calls `cdRef.markForCheck()` after async lookup resolves. All 7 dropdowns populate on every page load.

## Affected role
Patient (also AA, DA, Clinic Staff, admin — everyone who opens `/appointments/add`)

## Steps to reproduce
1. Sign in as Patient at `falkinstein.localhost:44368`.
2. From `/home` click "Book Appointment" → lands on `/appointments/add?type=1`.
3. Inspect Appointment Type dropdown.

## Expected
- GET `/api/app/appointments/appointment-type-lookup` → 200 with 6 items → dropdown shows 6 options.
- GET `/api/app/appointments/location-lookup` → 200 with 2 items → 2 options.
- AA + DA "State *" dropdowns populate from `/api/app/patients/state-lookup` (50 US states).

## Actual (pre-fix)
- All lookup APIs returned 200 with data.
- DOM rendered only `<option value="0: undefined">-</option>` — no items, no console errors.
- Patient Demographics State dropdown DID render — so `abp-lookup-select` not universally broken; only Schedule + Attorney-section instances dead.
- Booking impossible (Appointment Type + Location required).

## Root cause
`@volo/abp.commercial.ng.ui` 10.0.2 `LookupSelectComponent` assigns `this.datas = items` after async `getFn` resolves, but does NOT call `cdRef.markForCheck()`. Parent sections are `ChangeDetectionStrategy.OnPush` (introduced PR #121's 7-section decomposition). With no CD trigger, Angular doesn't re-render the `@for` loop and the options stay empty.

Patient Demographics worked because something else in that section triggered CD (probably the patient-list `(change)` event handler binding).

## Recommended fix (applied in PR #198)
Wrapper component `angular/src/app/shared/components/app-lookup-select.component.ts`:
```typescript
@Component({
  selector: 'app-lookup-select',
  standalone: true,
  template: `<select [(ngModel)]="value" [disabled]="disabled">
    <option [ngValue]="emptyOption.value">{{ emptyOption.label | abpLocalization }}</option>
    @for (data of datas; track $index) {
      <option [ngValue]="data[lookupIdProp]">{{ data[lookupNameProp] }}</option>
    }
  </select>`,
})
export class AppLookupSelectComponent extends LookupSelectComponent {
  override get() {
    this.getFn(this.pageQuery).subscribe(({ items }) => {
      this.datas = items ?? [];
      this.cdRef.markForCheck();  // <-- the fix
    });
  }
}
```
5 component files swap `<abp-lookup-select>` → `<app-lookup-select>`:
- `appointment-add-schedule.component.{ts,html}`
- `appointment-add-attorney-section.component.{ts,html}`
- `appointment-add-employer-details.component.{ts,html}`
- `appointment-add-patient-demographics.component.{ts,html}`
- `appointment-view.component.{ts,html}`

## Evidence
- innerHTML of the broken select:
  `<select id="appointment-appointment-type-id"><option value="0: undefined">-</option><!----></select>`
- No console errors — silent template-binding failure.

## OLD source
`P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.html` — OLD populated AppointmentType + Location from same lookups successfully.

## NEW source (suspect was PR #121)
The 7-section decomposition (`feat/121-appointment-add-section-decomposition`) introduced `OnPush` on the new section components, which exposed the latent CD bug in `abp-lookup-select`.

## Parity doc
`docs/parity/wave-1-parity/external-user-appointment-request.md`
