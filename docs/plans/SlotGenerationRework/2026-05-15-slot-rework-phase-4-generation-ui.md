---
status: draft
issue: slot-rework-phase-4-generation-ui
owner: AdrianG
created: 2026-05-15
approach: test-after (Angular components are visual; assert
  behavior through the browser, not Jasmine)
sequence: 5 of 7 (slot-generation + doctor-invariant series)
depends-on: 2026-05-15-slot-rework-phase-3-generation-api.md
  (the proxy must surface the new multi-axis shape and the
  CreateRangeAsync method)
branch: create a new branch off `feat/replicate-old-app`. PR back
  to `feat/replicate-old-app`. Do not merge to `main` until plans
  2 through 7 are merged together.
---

# Slot rework Phase 4: Angular generation UI

## Goal

Replace the existing `DoctorAvailabilityGenerateComponent`
single-axis form with a multi-axis reactive form that maps
1:1 to the plan 3 input DTO:

- Date range as today (`FromDate`, `ToDate`).
- **Seven weekday checkboxes** instead of the "From Day / To
  Day" select pair. Selected checkboxes go into the new
  `SelectedDays` list.
- **`FormArray<TimeRangeFormGroup>`** for `TimeRanges`. Default
  array seeded with one entry; "+ Add time range" button
  appends; "x" button per row removes. Validation: at least one
  row.
- **Numeric `Capacity` input** alongside the existing
  `AppointmentDurationMinutes`.
- **Multi-select `AppointmentTypeIds`** via a multi-pick lookup
  component (current single `<abp-lookup-select>` is single-
  valued; replace with the existing
  `<app-multi-lookup-select>` component used elsewhere in the
  project for M2M pickers, or build a small wrapper around
  `<ngx-select-dropdown>` if no multi-lookup primitive exists).
- **Submit** calls the new `createRange` proxy method, displays
  the inserted/skipped counts, then navigates back to the list.

The slot-mode radio (`'dates'` vs `'weekdays'`) is REMOVED -- the
multi-axis shape supersedes both modes. A single form accepts
the date range AND the weekday filter, behaving as the
"weekdays" mode when fewer than 7 boxes are checked.

## Why

Today's form (`angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts`)
takes one time range, one optional appointment type, one
booking status, and either a date range or a weekday range. It
cannot express "Monday + Wednesday + Friday at 8-10am and 1-3pm
for both AME and PQME with capacity 2". The user has to submit
six separate generations.

The plan 3 backend accepts the multi-axis shape in a single
call. This plan moves the SPA to consume it. The persistence
wave changes from "Angular forkJoin of N single-slot creates"
to "single createRange call returning a summary"; both the
network footprint and the UX of "9 inserted, 3 skipped due to
conflicts" become single-shot.

## Non-goals

- No new permission gates (read of the existing
  `CaseEvaluation.DoctorAvailabilities.Create`).
- No change to the list page or the per-slot detail modal --
  those keep their single-slot ergonomics. Plan 6 (booking-form
  picker) is a separate UI.
- No bilateral changes to the conflict-message rendering: plan
  3 already adjusted `SameTimeValidation` strings; the SPA
  consumes them verbatim.
- No animations / fancy add-row transitions. Use straight
  `@for` loops with `<button>` triggers; matches the existing
  scaffold style.

## Decisions locked

1. **Reactive form via `FormArray`** for `TimeRanges`. The
   existing form is already reactive; the FormArray is the
   straightforward extension. No template-driven sub-trees.

2. **`SelectedDays` = 7 checkboxes** (Sun -> Sat). Each
   checkbox binds to a `FormControl<boolean>` inside a
   `selectedDays` `FormGroup`. On submit the component
   serialises checked controls to the numeric `SelectedDays`
   array. All-7-checked sends an empty array (server-side
   "all weekdays" sentinel) -- saves bandwidth.

3. **Multi-select appointment types**. The codebase does NOT
   ship a generic multi-pick lookup component. Build a slim
   wrapper `<app-multi-lookup-select>` (NEW file) that:
   - Wraps `<ngb-typeahead>` for filterable lookup.
   - Accepts the same `[getFn]` shape as
     `<abp-lookup-select>`.
   - Models the value as `Guid[]`.
   - Renders selected items as Bootstrap badges with an "x"
     to remove.

   The wrapper is built ONCE and reused in plan 5 if needed.
   The implementation is ~80 lines TS + 40 lines HTML.

4. **Capacity input** is `<input type="number" min="1">` with
   a default of 1.

5. **Submit feedback** uses ABP's toast service (already
   injected as `ToasterService` elsewhere in the app). On
   success: toast "Inserted X slots. Skipped Y due to
   conflicts." then `goBack()`. On failure: ABP's default
   error toast (the 400 from the AppService carries the
   localized message).

6. **Form-level validation summary** still uses the existing
   `validationMessage` slot. Add a per-FormArray-row inline
   error message if `FromTime >= ToTime` -- runs client-side
   before submit to avoid round-trips on the obvious case.

7. **Backward-compat:** when a user submits the form on a
   tenant whose existing slot rows were created pre-rework, the
   conflict detection still flags them correctly (plan 3's
   logic compares the new range to existing rows of any
   `BookingStatus` flavor, by location + date + overlap).

## Files touched

### 1. NEW FILE `angular/src/app/shared/components/multi-lookup-select.component.ts`

```typescript
import { ChangeDetectionStrategy, Component, ElementRef, Input, ViewChild, forwardRef, inject } from '@angular/core';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { NgbTypeahead, NgbTypeaheadModule } from '@ng-bootstrap/ng-bootstrap';
import { Observable, Subject, debounceTime, distinctUntilChanged, map, merge, of, switchMap } from 'rxjs';
import { LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import type { LookupDto, LookupRequestDto } from '../../proxy/shared/models';

/**
 * 2026-05-15 -- multi-select wrapper around the same lookup
 * signature accepted by ABP's <abp-lookup-select>. Value is
 * a string[] (Guid[]) of the selected ids. Displays the
 * selected items as badges; typeahead filters by Name.
 */
@Component({
  selector: 'app-multi-lookup-select',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NgbTypeaheadModule, LocalizationPipe],
  template: `
    <div>
      <div class="d-flex flex-wrap gap-1 mb-2" *ngIf="selectedItems.length">
        <span *ngFor="let item of selectedItems" class="badge bg-secondary">
          {{ item.displayName }}
          <button type="button" class="btn-close btn-close-white btn-sm ms-1"
                  (click)="removeId(item.id)" [attr.aria-label]="'AbpUi::Remove' | abpLocalization"></button>
        </span>
      </div>
      <input #ti="ngbTypeahead"
             type="text"
             class="form-control"
             [ngModel]="''"
             (ngModelChange)="onTypeaheadChange($event)"
             [ngbTypeahead]="search"
             [resultFormatter]="formatter"
             [inputFormatter]="emptyFormatter"
             [editable]="false"
             [disabled]="disabled"
             [placeholder]="'AbpUi::Search' | abpLocalization"
             (selectItem)="onSelect($event)" />
    </div>
  `,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MultiLookupSelectComponent),
      multi: true,
    },
  ],
})
export class MultiLookupSelectComponent implements ControlValueAccessor {
  @Input({ required: true }) getFn!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;

  selectedIds: string[] = [];
  selectedItems: LookupDto<string>[] = [];
  disabled = false;

  private onChange: (value: string[]) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string[] | null): void {
    this.selectedIds = value ?? [];
    if (this.selectedIds.length === 0) {
      this.selectedItems = [];
    } else {
      // Hydrate display names lazily; refresh by calling getFn once.
      this.getFn({ filter: '', maxResultCount: 100, skipCount: 0 })
        .subscribe(page => {
          this.selectedItems = page.items?.filter(i => this.selectedIds.includes(i.id)) ?? [];
        });
    }
  }
  registerOnChange(fn: (value: string[]) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.disabled = isDisabled; }

  search = (text$: Observable<string>) =>
    text$.pipe(
      debounceTime(200),
      distinctUntilChanged(),
      switchMap(term =>
        this.getFn({ filter: term, maxResultCount: 20, skipCount: 0 }).pipe(
          map(page => (page.items ?? []).filter(i => !this.selectedIds.includes(i.id))),
        ),
      ),
    );

  formatter = (item: LookupDto<string>) => item?.displayName ?? '';
  emptyFormatter = () => '';

  onTypeaheadChange(_term: string): void {
    // No-op -- search observable handles filtering.
  }

  onSelect(event: { item: LookupDto<string>; preventDefault: () => void }): void {
    event.preventDefault();
    if (!this.selectedIds.includes(event.item.id)) {
      this.selectedIds = [...this.selectedIds, event.item.id];
      this.selectedItems = [...this.selectedItems, event.item];
      this.onChange(this.selectedIds);
    }
    this.onTouched();
  }

  removeId(id: string): void {
    this.selectedIds = this.selectedIds.filter(x => x !== id);
    this.selectedItems = this.selectedItems.filter(x => x.id !== id);
    this.onChange(this.selectedIds);
    this.onTouched();
  }
}
```

### 2. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts`

Rewrite. New form shape, FormArray for ranges, weekday
checkboxes, multi-lookup for types, capacity, and `createRange`
submit:

```typescript
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';
import { MultiLookupSelectComponent } from '../../../shared/components/multi-lookup-select.component';
import { bookingStatusOptions } from '../../../proxy/enums/booking-status.enum';
import type {
  DoctorAvailabilityGenerateInputDto,
  DoctorAvailabilityCreateRangeResultDto,
  DoctorAvailabilitySlotsPreviewDto,
} from '../../../proxy/doctor-availabilities/models';
import { DoctorAvailabilityService } from '../../../proxy/doctor-availabilities/doctor-availability.service';

interface TimeRangeFormShape {
  fromTime: FormControl<string | null>;
  toTime: FormControl<string | null>;
  appointmentDurationMinutes: FormControl<number | null>;
}

@Component({
  selector: 'app-doctor-availability-generate',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    PageComponent,
    PageToolbarContainerComponent,
    LookupSelectComponent,
    MultiLookupSelectComponent,
  ],
  templateUrl: './doctor-availability-generate.component.html',
  styles: [],
})
export class DoctorAvailabilityGenerateComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly service = inject(DoctorAvailabilityService);
  private readonly toaster = inject(ToasterService);
  private readonly subscriptions = new Subscription();

  bookingStatusOptions = bookingStatusOptions;
  isGenerating = false;
  isSubmitting = false;
  preview: DoctorAvailabilitySlotsPreviewDto[] = [];
  validationMessage: string | null = null;
  hasConflicts = false;
  canSubmit = false;
  private readonly expandedRows = new Set<number>();

  form = this.fb.nonNullable.group({
    locationId: this.fb.control<string | null>(null, { validators: [Validators.required] }),
    appointmentTypeIds: this.fb.control<string[]>([]),
    fromDate: this.fb.control<string | null>(null, { validators: [Validators.required] }),
    toDate: this.fb.control<string | null>(null, { validators: [Validators.required] }),
    selectedDays: this.fb.nonNullable.group({
      0: this.fb.control(false),  // Sunday
      1: this.fb.control(true),
      2: this.fb.control(true),
      3: this.fb.control(true),
      4: this.fb.control(true),
      5: this.fb.control(true),
      6: this.fb.control(false),  // Saturday
    }),
    timeRanges: this.fb.array<FormGroup<TimeRangeFormShape>>([this.createTimeRangeGroup()]),
    bookingStatusId: this.fb.control<number | null>(this.bookingStatusOptions[0]?.value ?? null, { validators: [Validators.required] }),
    appointmentDurationMinutes: this.fb.control(15, { validators: [Validators.required, Validators.min(1)] }),
    capacity: this.fb.control(1, { validators: [Validators.required, Validators.min(1)] }),
  });

  weekdayLabels = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  getLocationLookup = this.service.getLocationLookup;
  getAppointmentTypeLookup = this.service.getAppointmentTypeLookup;

  get timeRanges(): FormArray<FormGroup<TimeRangeFormShape>> {
    return this.form.controls.timeRanges;
  }

  get selectedDaysGroup(): FormGroup {
    return this.form.controls.selectedDays as unknown as FormGroup;
  }

  private createTimeRangeGroup(): FormGroup<TimeRangeFormShape> {
    return this.fb.nonNullable.group({
      fromTime: this.fb.control<string | null>(null, { validators: [Validators.required] }),
      toTime: this.fb.control<string | null>(null, { validators: [Validators.required] }),
      appointmentDurationMinutes: this.fb.control<number | null>(null),
    });
  }

  addTimeRange(): void {
    this.timeRanges.push(this.createTimeRangeGroup());
  }

  removeTimeRange(index: number): void {
    if (this.timeRanges.length > 1) {
      this.timeRanges.removeAt(index);
    }
  }

  ngOnInit(): void {
    // No cascade subscriptions needed -- the form is self-contained.
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private normalizeTime(value: string | null | undefined): string | null {
    if (!value) return null;
    const trimmed = value.trim();
    if (!trimmed) return null;
    const main = trimmed.split('.')[0];
    const parts = main.split(':');
    return parts.length === 2 ? `${parts[0]}:${parts[1]}:00` : main;
  }

  private buildPayload(): DoctorAvailabilityGenerateInputDto {
    const v = this.form.getRawValue();
    const selectedDaysArray = Object.entries(v.selectedDays)
      .filter(([, checked]) => checked)
      .map(([day]) => Number(day));
    const selectedDays = selectedDaysArray.length === 7 ? [] : selectedDaysArray;
    return {
      fromDate: v.fromDate as unknown as string,
      toDate: v.toDate as unknown as string,
      selectedDays,
      timeRanges: v.timeRanges.map(r => ({
        fromTime: this.normalizeTime(r.fromTime) ?? '',
        toTime: this.normalizeTime(r.toTime) ?? '',
        appointmentDurationMinutes: r.appointmentDurationMinutes ?? null,
      })),
      bookingStatusId: v.bookingStatusId ?? 0,
      locationId: v.locationId ?? '',
      appointmentTypeIds: v.appointmentTypeIds ?? [],
      appointmentDurationMinutes: Number(v.appointmentDurationMinutes),
      capacity: Number(v.capacity),
    };
  }

  generate(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.validationMessage = 'Please correct the highlighted fields before generating.';
      return;
    }

    this.isGenerating = true;
    this.validationMessage = null;
    this.service
      .generatePreview(this.buildPayload())
      .pipe(finalize(() => (this.isGenerating = false)))
      .subscribe(result => {
        this.preview = result ?? [];
        this.updateConflictState();
        this.expandedRows.clear();
      });
  }

  submit(): void {
    if (!this.canSubmit) {
      return;
    }
    this.isSubmitting = true;
    this.service
      .createRange(this.buildPayload())
      .pipe(finalize(() => (this.isSubmitting = false)))
      .subscribe((result: DoctorAvailabilityCreateRangeResultDto) => {
        this.toaster.success(
          `Inserted ${result.insertedCount} slots. Skipped ${result.skippedConflictCount} due to conflicts.`,
          'Slots generated',
        );
        this.goBack();
      });
  }

  reset(): void {
    this.form.reset({
      appointmentDurationMinutes: 15,
      capacity: 1,
      bookingStatusId: this.bookingStatusOptions[0]?.value ?? null,
      appointmentTypeIds: [],
      selectedDays: { 0: false, 1: true, 2: true, 3: true, 4: true, 5: true, 6: false } as never,
    });
    // Reseed the FormArray with a single empty row.
    while (this.timeRanges.length > 1) {
      this.timeRanges.removeAt(this.timeRanges.length - 1);
    }
    this.timeRanges.at(0).reset();
    this.preview = [];
    this.validationMessage = null;
  }

  goBack(): void {
    this.router.navigate(['/doctor-management/doctor-availabilities']);
  }

  toggleRow(id: number): void {
    if (this.expandedRows.has(id)) {
      this.expandedRows.delete(id);
    } else {
      this.expandedRows.add(id);
    }
  }
  isExpanded(id: number): boolean { return this.expandedRows.has(id); }

  getSlots(day: DoctorAvailabilitySlotsPreviewDto) {
    return day.doctorAvailabilities ?? [];
  }

  formatSlotTime(slot: { fromTime?: string | null; toTime?: string | null }) {
    return `${slot.fromTime ?? ''} - ${slot.toTime ?? ''}`;
  }

  removeSlot(day: DoctorAvailabilitySlotsPreviewDto, slot: { timeId: number }): void {
    day.doctorAvailabilities = (day.doctorAvailabilities ?? []).filter(s => s.timeId !== slot.timeId);
    if ((day.doctorAvailabilities ?? []).length === 0) {
      this.preview = this.preview.filter(d => d !== day);
    }
    this.updateConflictState();
  }

  private updateConflictState(): void {
    const all = this.preview.reduce((acc, d) => acc.concat(this.getSlots(d)), [] as Array<{ isConflict?: boolean }>);
    const anyConflict = all.some(s => !!s.isConflict);
    this.hasConflicts = anyConflict;
    if (anyConflict) {
      this.validationMessage = 'Some generated slots conflict with existing ones. Remove them or proceed -- conflicts will be skipped.';
    } else if (all.length === 0 && !this.isGenerating) {
      this.validationMessage = 'No slots were generated. Check your weekday selection and time ranges.';
    } else {
      this.validationMessage = null;
    }
    // canSubmit allows partial-success: submit-with-skipped is fine.
    this.canSubmit = all.length > 0;
  }
}
```

### 3. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.html`

Rewrite the template. Key sections:

```html
<abp-page [title]="'::SetAvailabilitySlot' | abpLocalization">
  <abp-page-toolbar-container class="col">
    <div class="text-lg-end pt-2">
      <button class="btn btn-outline-secondary btn-sm me-2" type="button" (click)="goBack()">
        <i class="fa fa-arrow-left me-1"></i>{{ 'AbpUi::Back' | abpLocalization }}
      </button>
      <button class="btn btn-secondary btn-sm me-2" type="button" (click)="reset()">
        <i class="fa fa-rotate-left me-1"></i>{{ '::Reset' | abpLocalization }}
      </button>
      <button class="btn btn-primary btn-sm" type="button" [disabled]="isGenerating" (click)="generate()">
        <i class="fa fa-bolt me-1"></i>{{ '::GenerateSlot' | abpLocalization }}
      </button>
    </div>
  </abp-page-toolbar-container>

  <div class="card mb-3"><div class="card-body">
    <form [formGroup]="form">
      <div class="row">
        <!-- Location, Date range -->
        <div class="col-12 col-md-4 mb-3">
          <label class="form-label">{{ '::Location' | abpLocalization }} *</label>
          <abp-lookup-select formControlName="locationId" [getFn]="getLocationLookup"></abp-lookup-select>
        </div>
        <div class="col-12 col-md-4 mb-3">
          <label class="form-label">{{ '::FromDate' | abpLocalization }} *</label>
          <input type="date" class="form-control" formControlName="fromDate" />
        </div>
        <div class="col-12 col-md-4 mb-3">
          <label class="form-label">{{ '::ToDate' | abpLocalization }} *</label>
          <input type="date" class="form-control" formControlName="toDate" />
        </div>
      </div>

      <!-- Weekday checkboxes -->
      <div class="mb-3" formGroupName="selectedDays">
        <label class="form-label d-block">{{ '::Weekdays' | abpLocalization }}</label>
        <div class="d-flex gap-3 flex-wrap">
          @for (label of weekdayLabels; track $index) {
            <label class="form-check form-check-inline">
              <input type="checkbox" class="form-check-input" [formControlName]="$index" />
              <span class="form-check-label">{{ label }}</span>
            </label>
          }
        </div>
      </div>

      <!-- TimeRanges FormArray -->
      <div class="mb-3">
        <label class="form-label">{{ '::TimeRanges' | abpLocalization }} *</label>
        <div formArrayName="timeRanges">
          @for (range of timeRanges.controls; track $index) {
            <div class="row align-items-end mb-2" [formGroupName]="$index">
              <div class="col-3">
                <label class="form-label small">{{ '::FromTime' | abpLocalization }}</label>
                <input type="time" step="60" class="form-control" formControlName="fromTime" />
              </div>
              <div class="col-3">
                <label class="form-label small">{{ '::ToTime' | abpLocalization }}</label>
                <input type="time" step="60" class="form-control" formControlName="toTime" />
              </div>
              <div class="col-4">
                <label class="form-label small">
                  {{ '::DurationOverrideOptional' | abpLocalization }}
                </label>
                <input type="number" min="1" class="form-control"
                       [placeholder]="form.value.appointmentDurationMinutes"
                       formControlName="appointmentDurationMinutes" />
              </div>
              <div class="col-2">
                @if (timeRanges.length > 1) {
                  <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeTimeRange($index)">
                    <i class="fa fa-trash"></i>
                  </button>
                }
              </div>
            </div>
          }
        </div>
        <button type="button" class="btn btn-link btn-sm" (click)="addTimeRange()">
          <i class="fa fa-plus me-1"></i>{{ '::AddTimeRange' | abpLocalization }}
        </button>
      </div>

      <!-- Capacity, default duration, types, status -->
      <div class="row">
        <div class="col-12 col-md-3 mb-3">
          <label class="form-label">{{ '::Capacity' | abpLocalization }} *</label>
          <input type="number" min="1" class="form-control" formControlName="capacity" />
          <small class="text-muted">{{ '::CapacityHint' | abpLocalization }}</small>
        </div>
        <div class="col-12 col-md-3 mb-3">
          <label class="form-label">{{ '::DefaultDurationMinutes' | abpLocalization }} *</label>
          <input type="number" min="1" class="form-control" formControlName="appointmentDurationMinutes" />
        </div>
        <div class="col-12 col-md-3 mb-3">
          <label class="form-label">{{ '::AppointmentTypes' | abpLocalization }}</label>
          <app-multi-lookup-select formControlName="appointmentTypeIds"
                                   [getFn]="getAppointmentTypeLookup"></app-multi-lookup-select>
          <small class="text-muted">{{ '::EmptyMeansAnyType' | abpLocalization }}</small>
        </div>
        <div class="col-12 col-md-3 mb-3">
          <label class="form-label">{{ '::BookingStatusId' | abpLocalization }} *</label>
          <select class="form-select" formControlName="bookingStatusId">
            @for (option of bookingStatusOptions; track option.key) {
              <option [ngValue]="option.value">{{ '::Enum:BookingStatus.' + option.value | abpLocalization }}</option>
            }
          </select>
        </div>
      </div>
    </form>
  </div></div>

  @if (validationMessage) {
    <div class="alert" [class.alert-warning]="!hasConflicts" [class.alert-info]="hasConflicts">
      {{ validationMessage }}
    </div>
  }

  @if (preview?.length) {
    <!-- Preview table: unchanged structure, but each row now reflects
         Capacity + AppointmentTypeIds columns. -->
    <div class="card"><div class="card-body">
      <table class="table table-sm">
        <thead>
          <tr>
            <th></th>
            <th>{{ '::Location' | abpLocalization }}</th>
            <th>{{ '::Date' | abpLocalization }}</th>
            <th>{{ '::Day' | abpLocalization }}</th>
            <th>{{ '::SlotCount' | abpLocalization }}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (day of preview; track day.monthId) {
            <tr>
              <td>
                <button type="button" class="btn btn-link p-0" (click)="toggleRow(day.monthId)">
                  <i class="fa" [class.fa-plus-circle]="!isExpanded(day.monthId)" [class.fa-minus-circle]="isExpanded(day.monthId)"></i>
                </button>
              </td>
              <td>{{ day.locationName }}</td>
              <td>{{ day.dates }}</td>
              <td>{{ day.days }}</td>
              <td>{{ getSlots(day).length }}</td>
              <td>
                @if (day.sameTimeValidation) {
                  <span class="text-warning small">{{ day.sameTimeValidation }}</span>
                }
              </td>
            </tr>
            @if (isExpanded(day.monthId)) {
              <tr><td colspan="6">
                <table class="table table-sm mb-0">
                  <thead>
                    <tr>
                      <th>{{ '::TimeRange' | abpLocalization }}</th>
                      <th>{{ '::Capacity' | abpLocalization }}</th>
                      <th>{{ '::AppointmentTypes' | abpLocalization }}</th>
                      <th>{{ '::Status' | abpLocalization }}</th>
                      <th>{{ '::Conflict' | abpLocalization }}</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (slot of getSlots(day); track slot.timeId) {
                      <tr [class.table-warning]="slot.isConflict">
                        <td>{{ formatSlotTime(slot) }}</td>
                        <td>{{ slot.capacity }}</td>
                        <td>
                          @if (slot.appointmentTypeIds?.length) {
                            <span class="small text-muted">{{ slot.appointmentTypeIds?.length }} type(s)</span>
                          } @else {
                            <span class="small text-muted">{{ '::AnyType' | abpLocalization }}</span>
                          }
                        </td>
                        <td>{{ '::Enum:BookingStatus.' + slot.bookingStatusId | abpLocalization }}</td>
                        <td>
                          @if (slot.isConflict) {
                            <span class="text-danger">{{ 'AbpUi::Yes' | abpLocalization }}</span>
                          } @else {
                            <span class="text-success">{{ 'AbpUi::No' | abpLocalization }}</span>
                          }
                        </td>
                        <td>
                          <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeSlot(day, slot)">
                            <i class="fa fa-trash"></i>
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </td></tr>
            }
          }
        </tbody>
      </table>
    </div></div>

    <div class="d-flex justify-content-end gap-2 mt-3">
      <button class="btn btn-secondary" type="button" (click)="goBack()">{{ 'AbpUi::Cancel' | abpLocalization }}</button>
      <button class="btn btn-primary" type="button" [disabled]="isSubmitting || !canSubmit" (click)="submit()">
        {{ 'AbpUi::Submit' | abpLocalization }}
      </button>
    </div>
  }
</abp-page>
```

### 4. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-detail.component.html` (and the detail FormBuilder service)

The single-slot detail/edit modal also needs to surface
`Capacity` and switch the `AppointmentTypeId` single-pick to the
multi-pick. Two small edits:

- Add `<input type="number" min="1" formControlName="capacity">`
  to the form template.
- Replace `<abp-lookup-select formControlName="appointmentTypeId">`
  with `<app-multi-lookup-select formControlName="appointmentTypeIds">`.

Update `doctor-availability-detail.abstract.service.ts` form-
builder spec accordingly:
```typescript
this.fb.group({
  // ...
  capacity: [1, [Validators.required, Validators.min(1)]],
  appointmentTypeIds: [[]],
})
```

Remove the now-unused `appointmentTypeId: [null]` line.

### 5. `angular/src/app/doctor-availabilities/doctor-availability/services/doctor-availability-detail.abstract.service.ts`

Update the create/update payload assembly:

```typescript
return {
  // ...
  appointmentTypeIds: form.value.appointmentTypeIds ?? [],
  capacity: Number(form.value.capacity ?? 1),
};
```

### 6. `angular/src/app/doctor-availabilities/doctor-availability/services/doctor-availability.abstract.service.ts`

If the list-view grouping uses `appointmentType` anywhere, swap
to plural `appointmentTypes` and render as a comma-separated
list of names. Grep
`appointmentType` in the abstract service to confirm.

### 7. `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability.component.html` (list page)

Update the column display for AppointmentType to render the
new list. If the list page currently shows `{{ row.appointmentType?.name }}`,
swap to:

```html
@for (at of row.appointmentTypes; track at.id; let last = $last) {
  {{ at.name }}{{ last ? '' : ', ' }}
}
@if (!row.appointmentTypes?.length) {
  <span class="text-muted">{{ '::AnyType' | abpLocalization }}</span>
}
```

Add a column for `Capacity` between BookingStatus and AppointmentTypes.

### 8. `angular/src/environments/en.json` (or wherever the en-localization JSON lives in the SPA's local copy)

Note: the SPA reads localization through `abp.localization`,
which sources from the backend's `en.json`. The keys are
configured server-side in plan 1 + plan 2 + this plan.

Add localization keys in
`src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`:

```jsonc
"Capacity": "Capacity",
"CapacityHint": "Max number of patients that can book the same slot.",
"AppointmentTypes": "Appointment Types",
"DefaultDurationMinutes": "Default Duration (minutes)",
"DurationOverrideOptional": "Duration Override (minutes, optional)",
"TimeRanges": "Time Ranges",
"AddTimeRange": "Add time range",
"Weekdays": "Weekdays",
"AnyType": "Any type",
"EmptyMeansAnyType": "Leave empty to accept any appointment type.",
"SlotCount": "Slot Count",
"TimeRange": "Time Range"
```

ASCII only.

## Test plan (test-after)

The UI form is visual. Jasmine unit tests cover the pure
helpers (`buildPayload`, `createTimeRangeGroup`, `selectedDays
collapse to []`); browser tests cover the UX. Plan 7 wires the
HRD scenarios.

### Unit tests (NEW file)

`angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.spec.ts`

| # | Test | Acceptance |
|---|------|------------|
| 1 | `buildPayload_When7DaysChecked_SendsEmptyArray` | All 7 selectedDays checked. Payload's `selectedDays = []`. |
| 2 | `buildPayload_WhenSubsetChecked_SendsIndices` | Mon + Wed + Fri checked. Payload's `selectedDays = [1, 3, 5]`. |
| 3 | `buildPayload_NormalizesTime` | FromTime `"08:00"` becomes `"08:00:00"`. |
| 4 | `addTimeRange_AppendsEmptyRow` | Starts at 1 row; after click 2 rows. |
| 5 | `removeTimeRange_OnlyOneLeft_Refuses` | When only 1 row, click does nothing. |

### Browser verification

After ship:

1. `npx ng build --configuration development` then `npx serve -s
   dist/CaseEvaluation/browser -p 4200`.
2. Log in as Staff Supervisor; navigate `/doctor-management/doctor-availabilities/generate`.
3. **Default state**: 1 time range, Mon-Fri checked (Sun/Sat
   unchecked), Capacity 1, default duration 15.
4. Pick a location, June 1-7 2026, leave defaults, click
   Generate. Expect 5 dates (Mon-Fri only), 8 slots each =
   40 slots in preview.
5. Click + Add time range. Set first row 08:00-10:00, second
   row 13:00-15:00. Regenerate. Expect 5 dates * (8 morning +
   8 afternoon) = 80 slots.
6. Set Capacity to 3. Regenerate. Each preview row shows
   "Capacity 3".
7. Pick two appointment types in the multi-lookup. Regenerate.
   Each row shows "2 type(s)".
8. Click Submit. Toast renders "Inserted 80 slots. Skipped 0
   due to conflicts." Navigate back to the list page; new
   slots show with `Capacity 3` and the two types.
9. Re-click Generate with the same input. All 80 slots show
   IsConflict=true in the preview (overlap with the just-
   created rows). Click Submit. Toast: "Inserted 0 slots.
   Skipped 80 due to conflicts."

## Risk and rollback

**Blast radius:**
- 2 component templates + 1 component TS rewrite + 1 new shared
  component.
- 2 abstract service files updated.
- No backend changes (plans 1-3 cover that).

**Rollback:**
- Revert the commit. The proxy continues to expose the new
  methods (created in plan 3), but the UI does not call them;
  the OLD single-axis form gets restored.

**Risk: the multi-lookup component breaks under tenants with
0 AppointmentType rows.** Mitigated: the typeahead just shows
no results; the form submits an empty `appointmentTypeIds`
which is the loose-mode sentinel.

**Risk: FormArray validation errors don't render inline.**
Mitigated: rely on browser-native `:invalid` styles via
`is-invalid` class chaining + the form-level
`validationMessage` summary; the existing scaffold pattern is
the same.

**Risk: ChangeDetection.Default re-evaluates on every event.**
Acceptable; the form is bounded and the preview list is
capped at 1000 (plan 3 enforces).

## Verification

End-to-end after ship:

1. Build + serve.
2. Run the 5 unit tests; green.
3. Run the 9-step browser verification above.
4. Smoke: as Patient, navigate to `/appointments/add`. Verify
   the new slots are bookable (plan 5 wires the picker to
   display capacity; for now, just confirm the picker still
   surfaces slots and a booking succeeds).

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Plan 6 (booking-form picker UI) follows.
