import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { Subscription, forkJoin, of } from 'rxjs';
import { LocalizationPipe } from '@abp/ng.core';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';
import { bookingStatusOptions } from '../../../proxy/enums/booking-status.enum';
import type {
  DoctorAvailabilityGenerateInputDto,
  DoctorAvailabilitySlotsPreviewDto,
} from '../../../proxy/doctor-availabilities/models';
import { DoctorAvailabilityService } from '../../../proxy/doctor-availabilities/doctor-availability.service';

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
  ],
  templateUrl: './doctor-availability-generate.component.html',
  styles: [],
})
export class DoctorAvailabilityGenerateComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly service = inject(DoctorAvailabilityService);
  private readonly subscriptions = new Subscription();

  bookingStatusOptions = bookingStatusOptions;
  isGenerating = false;
  isSubmitting = false;
  preview: DoctorAvailabilitySlotsPreviewDto[] = [];
  validationMessage: string | null = null;
  hasConflicts = false;
  canSubmit = false;
  private readonly expandedRows = new Set<number>();

  form = this.fb.group({
    slotMode: ['dates', [Validators.required]],
    locationId: [null, [Validators.required]],
    appointmentTypeId: [null, []],
    fromDate: [null, [Validators.required]],
    toDate: [null, [Validators.required]],
    fromTime: [null, [Validators.required]],
    toTime: [null, [Validators.required]],
    bookingStatusId: [this.bookingStatusOptions[0]?.value ?? null, [Validators.required]],
    appointmentDurationMinutes: [15, [Validators.required, Validators.min(1)]],
    useCurrentMonth: [true, []],
    selectedMonth: [null, []],
    fromDay: [null, []],
    toDay: [null, []],
  });

  getLocationLookup = this.service.getLocationLookup;
  getAppointmentTypeLookup = this.service.getAppointmentTypeLookup;

  monthOptions = [
    { id: 1, name: 'January' },
    { id: 2, name: 'February' },
    { id: 3, name: 'March' },
    { id: 4, name: 'April' },
    { id: 5, name: 'May' },
    { id: 6, name: 'June' },
    { id: 7, name: 'July' },
    { id: 8, name: 'August' },
    { id: 9, name: 'September' },
    { id: 10, name: 'October' },
    { id: 11, name: 'November' },
    { id: 12, name: 'December' },
  ];

  weekdayOptions = [
    { id: 0, name: 'Sunday' },
    { id: 1, name: 'Monday' },
    { id: 2, name: 'Tuesday' },
    { id: 3, name: 'Wednesday' },
    { id: 4, name: 'Thursday' },
    { id: 5, name: 'Friday' },
    { id: 6, name: 'Saturday' },
  ];

  get currentMonthName() {
    return this.monthOptions[new Date().getMonth()]?.name ?? '';
  }

  ngOnInit() {
    this.updateSlotModeValidators(this.form.get('slotMode')?.value ?? 'dates');

    const useCurrentMonthControl = this.form.get('useCurrentMonth');
    const selectedMonthControl = this.form.get('selectedMonth');
    const slotModeControl = this.form.get('slotMode');

    if (useCurrentMonthControl) {
      this.subscriptions.add(
        useCurrentMonthControl.valueChanges.subscribe((value) => {
          if (value) {
            selectedMonthControl?.patchValue(null, { emitEvent: false });
          }
          this.updateMonthValidators(!!value);
        }),
      );
    }

    if (selectedMonthControl) {
      this.subscriptions.add(
        selectedMonthControl.valueChanges.subscribe((value) => {
          if (value !== null && value !== undefined && value !== '') {
            useCurrentMonthControl?.patchValue(false, { emitEvent: false });
            this.updateMonthValidators(false);
          }
        }),
      );
    }

    if (slotModeControl) {
      this.subscriptions.add(
        slotModeControl.valueChanges.subscribe((value) => {
          this.updateSlotModeValidators(value);
        }),
      );
    }
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }

  private normalizeTime(value: string | null | undefined): string | null {
    if (!value) {
      return null;
    }

    const trimmed = value.trim();
    if (!trimmed) {
      return null;
    }

    const main = trimmed.split('.')[0];
    const parts = main.split(':');

    if (parts.length === 2) {
      return `${parts[0]}:${parts[1]}:00`;
    }

    return main;
  }

  goBack() {
    this.router.navigate(['/doctor-management/doctor-availabilities']);
  }

  reset() {
    this.form.reset({
      slotMode: 'dates',
      bookingStatusId: this.bookingStatusOptions[0]?.value ?? null,
      appointmentDurationMinutes: 15,
      useCurrentMonth: true,
    });
    this.preview = [];
    this.validationMessage = null;
  }

  generate() {
    if (this.form.invalid) {
      return;
    }

    const value = this.form.value;
    const basePayload = {
      locationId: value.locationId,
      appointmentTypeId: value.appointmentTypeId,
      fromTime: this.normalizeTime(value.fromTime),
      toTime: this.normalizeTime(value.toTime),
      bookingStatusId: value.bookingStatusId,
      appointmentDurationMinutes: Number(value.appointmentDurationMinutes),
    };

    let payloads: DoctorAvailabilityGenerateInputDto[] = [];

    if (value.slotMode === 'weekdays') {
      const month = this.resolveSelectedMonth();
      const year = new Date().getFullYear();
      const fromDay =
        value.fromDay === null || value.fromDay === undefined ? null : Number(value.fromDay);
      const toDay = value.toDay === null || value.toDay === undefined ? null : Number(value.toDay);
      const dates = this.buildWeekdayDates(year, month, fromDay, toDay);
      payloads = dates.map((date) => ({
        ...basePayload,
        fromDate: date,
        toDate: date,
      }));
    } else {
      payloads = [
        {
          ...basePayload,
          fromDate: value.fromDate,
          toDate: value.toDate,
        },
      ];
    }

    this.isGenerating = true;
    this.validationMessage = null;

    this.service
      .generatePreview(payloads)
      .pipe(finalize(() => (this.isGenerating = false)))
      .subscribe((result) => {
        this.preview = result ?? [];
        this.updateConflictState();
        if (!this.hasConflicts) {
          this.preview.forEach((item) => (item.sameTimeValidation = null));
        }
        this.expandedRows.clear();
      });
  }

  submit() {
    if (this.preview.length < 1 || !this.canSubmit) {
      return;
    }

    const slots = this.preview
      .reduce((acc, day) => acc.concat(this.getSlots(day)), [])
      .filter((slot) => !slot.isConflict);
    if (slots.length < 1) {
      return;
    }

    const requests = slots.map((slot) =>
      this.service.create({
        availableDate: slot.availableDate,
        fromTime: slot.fromTime,
        toTime: slot.toTime,
        bookingStatusId: slot.bookingStatusId,
        locationId: slot.locationId,
        appointmentTypeId: slot.appointmentTypeId ?? null,
      }),
    );

    this.isSubmitting = true;
    forkJoin(requests.length ? requests : [of(null)])
      .pipe(finalize(() => (this.isSubmitting = false)))
      .subscribe(() => {
        this.goBack();
      });
  }

  toggleRow(id: number) {
    if (this.expandedRows.has(id)) {
      this.expandedRows.delete(id);
      return;
    }

    this.expandedRows.add(id);
  }

  isExpanded(id: number) {
    return this.expandedRows.has(id);
  }

  getSlots(day: DoctorAvailabilitySlotsPreviewDto) {
    return day.doctorAvailabilities ?? [];
  }

  formatSlotTime(slot: { appointmentFromTime?: string | null; fromTime?: string | null }) {
    if (slot.appointmentFromTime) {
      return slot.appointmentFromTime;
    }

    return slot.fromTime ?? '';
  }

  removeSlot(day: DoctorAvailabilitySlotsPreviewDto, slot: { timeId: number }) {
    const slots = this.getSlots(day).filter((item) => item.timeId !== slot.timeId);

    day.doctorAvailabilities = slots;

    if (slots.length === 0) {
      this.preview = this.preview.filter((item) => item !== day);
    }

    this.updateConflictState();
  }

  private updateConflictState() {
    const allSlots = this.preview.reduce((acc, day) => acc.concat(this.getSlots(day)), []);
    const anyConflict = allSlots.some((slot) => !!slot.isConflict);
    this.hasConflicts = anyConflict;
    if (anyConflict) {
      this.validationMessage =
        'Some generated slots already exist. Please remove them before submitting.';
    } else if (allSlots.length === 0 && this.form.valid && !this.isGenerating) {
      // S-7.4 (W-UI-11): backend returns an empty array for inverted FromDate>ToDate,
      // inverted FromTime>ToTime, or zero-duration FromTime==ToTime inputs. The form
      // is otherwise "valid" so the framework does not flag the field; surface an
      // inline message instead of leaving Submit silently disabled.
      this.validationMessage =
        'No slots were generated. Check that your start date is before your end date and your start time is before your end time.';
    } else {
      this.validationMessage = null;
    }
    this.canSubmit = allSlots.length > 0 && !anyConflict;
  }

  private resolveSelectedMonth(): number {
    if (this.form.value.useCurrentMonth) {
      return new Date().getMonth() + 1;
    }

    return Number(this.form.value.selectedMonth) || new Date().getMonth() + 1;
  }

  private buildWeekdayDates(
    year: number,
    month: number,
    fromDay: number | null,
    toDay: number | null,
  ): string[] {
    const start = new Date(year, month - 1, 1);
    const end = new Date(year, month, 0);
    const dates: string[] = [];

    for (let d = new Date(start); d <= end; d.setDate(d.getDate() + 1)) {
      const day = d.getDay();
      const inRange = this.isWeekdayInRange(day, fromDay, toDay);
      if (!inRange) {
        continue;
      }
      dates.push(this.formatLocalDate(d));
    }

    return dates;
  }

  private isWeekdayInRange(day: number, fromDay: number | null, toDay: number | null): boolean {
    if (fromDay === null || toDay === null) {
      return true;
    }

    if (fromDay <= toDay) {
      return day >= fromDay && day <= toDay;
    }

    return day >= fromDay || day <= toDay;
  }

  private formatLocalDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private updateMonthValidators(useCurrentMonth: boolean) {
    const selectedMonthControl = this.form.get('selectedMonth');
    if (!selectedMonthControl) {
      return;
    }

    if (useCurrentMonth) {
      selectedMonthControl.clearValidators();
    } else {
      selectedMonthControl.setValidators([Validators.required]);
    }
    selectedMonthControl.updateValueAndValidity({ emitEvent: false });
  }

  private updateSlotModeValidators(mode: string) {
    const fromDateControl = this.form.get('fromDate');
    const toDateControl = this.form.get('toDate');

    if (!fromDateControl || !toDateControl) {
      return;
    }

    if (mode === 'weekdays') {
      fromDateControl.clearValidators();
      toDateControl.clearValidators();
    } else {
      fromDateControl.setValidators([Validators.required]);
      toDateControl.setValidators([Validators.required]);
    }

    fromDateControl.updateValueAndValidity({ emitEvent: false });
    toDateControl.updateValueAndValidity({ emitEvent: false });
  }
}
