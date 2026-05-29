import { ChangeDetectionStrategy, Component, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import { LookupSelectComponent, LookupTypeaheadMtmComponent } from '@volo/abp.commercial.ng.ui';
import { bookingStatusOptions } from '../../../proxy/enums/booking-status.enum';
import type {
  DoctorAvailabilityCreateRangeResultDto,
  DoctorAvailabilityGenerateInputDto,
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
    LookupTypeaheadMtmComponent,
  ],
  templateUrl: './doctor-availability-generate.component.html',
  styles: [],
})
export class DoctorAvailabilityGenerateComponent implements OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly service = inject(DoctorAvailabilityService);
  private readonly toaster = inject(ToasterService);
  private readonly localization = inject(LocalizationService);
  private readonly subscriptions = new Subscription();

  bookingStatusOptions = bookingStatusOptions;
  isGenerating = false;
  isSubmitting = false;
  preview: DoctorAvailabilitySlotsPreviewDto[] = [];
  validationMessage: string | null = null;
  hasConflicts = false;
  canSubmit = false;
  private readonly expandedRows = new Set<number>();

  weekdayLabelKeys = [
    'AbpUi::Sunday',
    'AbpUi::Monday',
    'AbpUi::Tuesday',
    'AbpUi::Wednesday',
    'AbpUi::Thursday',
    'AbpUi::Friday',
    'AbpUi::Saturday',
  ];

  form: FormGroup;

  getLocationLookup = this.service.getLocationLookup;
  getAppointmentTypeLookup = this.service.getAppointmentTypeLookup;

  constructor() {
    this.form = this.fb.group({
      locationId: this.fb.control<string | null>(null, { validators: [Validators.required] }),
      appointmentTypeIds: this.fb.control<string[]>([]),
      fromDate: this.fb.control<string | null>(null, { validators: [Validators.required] }),
      toDate: this.fb.control<string | null>(null, { validators: [Validators.required] }),
      selectedDays: this.fb.group({
        0: this.fb.nonNullable.control(false),
        1: this.fb.nonNullable.control(true),
        2: this.fb.nonNullable.control(true),
        3: this.fb.nonNullable.control(true),
        4: this.fb.nonNullable.control(true),
        5: this.fb.nonNullable.control(true),
        6: this.fb.nonNullable.control(false),
      }),
      timeRanges: this.fb.array<FormGroup<TimeRangeFormShape>>([this.createTimeRangeGroup()]),
      bookingStatusId: this.fb.control<number | null>(this.bookingStatusOptions[0]?.value ?? null, {
        validators: [Validators.required],
      }),
      appointmentDurationMinutes: this.fb.control(15, {
        validators: [Validators.required, Validators.min(1)],
      }),
      capacity: this.fb.control(3, { validators: [Validators.required, Validators.min(1)] }),
    });
  }

  get timeRanges(): FormArray<FormGroup<TimeRangeFormShape>> {
    return this.form.get('timeRanges') as FormArray<FormGroup<TimeRangeFormShape>>;
  }

  get selectedDaysGroup(): FormGroup {
    return this.form.get('selectedDays') as FormGroup;
  }

  private createTimeRangeGroup(): FormGroup<TimeRangeFormShape> {
    return this.fb.group<TimeRangeFormShape>({
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

  ngOnDestroy(): void {
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

  // The ABP <abp-lookup-typeahead-mtm> control writes an array of
  // { id, name, ... } lookup objects, not bare ids. The backend DTO expects
  // Guid[]; collapse to ids here regardless of which shape arrived.
  private toIdArray(value: unknown): string[] {
    if (!Array.isArray(value)) return [];
    return value
      .map((v) => (typeof v === 'string' ? v : ((v as { id?: string } | null)?.id ?? '')))
      .filter((v): v is string => !!v);
  }

  buildPayload(): DoctorAvailabilityGenerateInputDto {
    const value = this.form.getRawValue();
    const selectedDaysObj = value.selectedDays as Record<string, boolean>;
    const checkedDays = Object.entries(selectedDaysObj)
      .filter(([, checked]) => checked)
      .map(([day]) => Number(day))
      .sort((a, b) => a - b);
    const selectedDays = checkedDays.length === 7 ? [] : checkedDays;
    const timeRangesValue = value.timeRanges as Array<{
      fromTime: string | null;
      toTime: string | null;
      appointmentDurationMinutes: number | null;
    }>;
    return {
      fromDate: value.fromDate ?? undefined,
      toDate: value.toDate ?? undefined,
      selectedDays,
      timeRanges: timeRangesValue.map((r) => ({
        fromTime: this.normalizeTime(r.fromTime) ?? undefined,
        toTime: this.normalizeTime(r.toTime) ?? undefined,
        appointmentDurationMinutes: r.appointmentDurationMinutes ?? null,
      })),
      bookingStatusId: value.bookingStatusId ?? undefined,
      locationId: value.locationId ?? undefined,
      appointmentTypeIds: this.toIdArray(value.appointmentTypeIds),
      appointmentDurationMinutes: Number(value.appointmentDurationMinutes),
      capacity: Number(value.capacity),
    };
  }

  generate(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.validationMessage = this.localization.instant('::FormErrorsValidation');
      return;
    }

    this.isGenerating = true;
    this.validationMessage = null;

    this.service
      .generatePreview(this.buildPayload())
      .pipe(finalize(() => (this.isGenerating = false)))
      .subscribe((result) => {
        this.preview = result ?? [];
        this.updateConflictState();
        this.expandedRows.clear();
      });
  }

  submit(): void {
    if (!this.canSubmit || this.isSubmitting) {
      return;
    }

    this.isSubmitting = true;
    this.service
      .createRange(this.buildPayload())
      .pipe(finalize(() => (this.isSubmitting = false)))
      .subscribe((result: DoctorAvailabilityCreateRangeResultDto) => {
        const inserted = result?.insertedCount ?? 0;
        const skipped = result?.skippedConflictCount ?? 0;
        const message = this.localization.instant(
          {
            key: '::SlotsInsertedSummary',
            defaultValue: 'Inserted {0} slots. Skipped {1} due to conflicts.',
          },
          String(inserted),
          String(skipped),
        );
        const title = this.localization.instant('::SlotsGeneratedTitle');
        this.toaster.success(message, title);
        this.goBack();
      });
  }

  reset(): void {
    this.form.reset({
      locationId: null,
      appointmentTypeIds: [],
      fromDate: null,
      toDate: null,
      selectedDays: { 0: false, 1: true, 2: true, 3: true, 4: true, 5: true, 6: false },
      bookingStatusId: this.bookingStatusOptions[0]?.value ?? null,
      appointmentDurationMinutes: 15,
      capacity: 3,
    });
    while (this.timeRanges.length > 1) {
      this.timeRanges.removeAt(this.timeRanges.length - 1);
    }
    this.timeRanges.at(0).reset();
    this.preview = [];
    this.validationMessage = null;
    this.hasConflicts = false;
    this.canSubmit = false;
    this.expandedRows.clear();
  }

  goBack(): void {
    this.router.navigate(['/doctor-management/doctor-availabilities']);
  }

  toggleRow(id: number): void {
    if (this.expandedRows.has(id)) {
      this.expandedRows.delete(id);
      return;
    }
    this.expandedRows.add(id);
  }

  isExpanded(id: number): boolean {
    return this.expandedRows.has(id);
  }

  getSlots(day: DoctorAvailabilitySlotsPreviewDto) {
    return day.doctorAvailabilities ?? [];
  }

  formatSlotTime(slot: { fromTime?: string | null; toTime?: string | null }): string {
    return `${slot.fromTime ?? ''} - ${slot.toTime ?? ''}`;
  }

  removeSlot(day: DoctorAvailabilitySlotsPreviewDto, slot: { timeId?: number }): void {
    const slots = this.getSlots(day).filter((item) => item.timeId !== slot.timeId);
    day.doctorAvailabilities = slots;
    if (slots.length === 0) {
      this.preview = this.preview.filter((item) => item !== day);
    }
    this.updateConflictState();
  }

  private updateConflictState(): void {
    const allSlots = this.preview.reduce(
      (acc, day) => acc.concat(this.getSlots(day)),
      [] as Array<{ isConflict?: boolean }>,
    );
    const anyConflict = allSlots.some((slot) => !!slot.isConflict);
    this.hasConflicts = anyConflict;
    if (anyConflict) {
      this.validationMessage = this.localization.instant('::SomeSlotsConflict');
    } else if (allSlots.length === 0 && !this.isGenerating) {
      this.validationMessage = this.localization.instant('::NoSlotsGenerated');
    } else {
      this.validationMessage = null;
    }
    this.canSubmit = allSlots.length > 0;
  }
}
