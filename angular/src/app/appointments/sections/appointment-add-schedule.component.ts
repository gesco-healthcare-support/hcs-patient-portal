import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import { AppLookupSelectComponent } from '../../shared/components/app-lookup-select.component';
import { NgbDatepickerModule, NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';
import { Observable } from 'rxjs';
import type { LookupDto, LookupRequestDto } from '../../proxy/shared/models';

/**
 * #121 phase T7 (2026-05-13) -- Appointment Details / Schedule card.
 * Extracted from `AppointmentAddComponent`. Renders the
 * AppointmentType + PanelNumber + Location + AppointmentDate +
 * AppointmentTime block (5 visible FormControls on the parent form,
 * plus the hidden doctorAvailabilityId tied to slot selection).
 *
 * State ownership:
 *   - parent  -> the 5 FormControls + doctorAvailabilityId. All
 *                cascade subscriptions stay on the parent (the
 *                constructor wires
 *                form.get('appointmentTypeId')?.valueChanges =>
 *                  applyFieldConfigsForAppointmentType
 *                  + loadCustomFieldsForAppointmentType
 *                  + loadAvailableDatesBySelection,
 *                form.get('locationId')?.valueChanges =>
 *                  updateLocationSelection + loadAvailableDatesBySelection,
 *                form.get('appointmentDate')?.valueChanges =>
 *                  rebuildAppointmentTimeOptions,
 *                form.get('appointmentTime')?.valueChanges =>
 *                  updateDoctorAvailabilityIdFromTime).
 *   - parent  -> availableDateKeys + availableSlotsByDate caches,
 *                appointmentTimeOptions, isAvailableDatesLoading,
 *                checkForAppointmentTypeSelected, minimumBookingDays /
 *                Message. fetchAllAvailableSlots HTTP call.
 *   - parent  -> markAppointmentDateDisabled + isAvailableAppointmentDate
 *                arrows; the ngbDatepicker [markDisabled] callback and
 *                the day-template highlight read directly from
 *                availableDateKeys, which is parent-owned cache.
 *   - child   -> template rendering only. The day template
 *                (#appointmentDateDayTpl) lives in the child template
 *                because it is only referenced from the ngbDatepicker
 *                inside the same template -- moving it preserves the
 *                [dayTemplate] binding without parent plumbing.
 *
 * Action surfaces (outputs):
 *   - `(locationSelected)` -- abp-lookup-select valueChange. Parent
 *      calls updateLocationSelection which triggers the available-
 *      slot HTTP fetch.
 *   - `(appointmentDateCleared)` -- "Clear date" button. Parent calls
 *      clearAppointmentDate() to null out date+time+doctorAvailabilityId.
 *
 * Trade-off: this is a minimum-viable template extraction matching
 * the T3 / T5 / T6 pattern. A deeper refactor could relocate
 * availableDateKeys + the slot fetcher into a child-owned service,
 * but submit-time reads of doctorAvailabilityId + the cascade
 * subscriptions wired in the parent constructor make that a larger
 * @ViewChild plumbing exercise. Out of scope for T7.
 */
@Component({
  selector: 'app-appointment-add-schedule',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    AppLookupSelectComponent,
    NgbDatepickerModule,
  ],
  templateUrl: './appointment-add-schedule.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddScheduleComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) checkForAppointmentTypeSelected = false;
  @Input({ required: true }) isAvailableDatesLoading = false;
  @Input({ required: true }) appointmentTimeOptions: Array<{
    value: string;
    label: string;
    doctorAvailabilityId: string;
  }> = [];
  @Input({ required: true }) minimumBookingRuleMessage = '';
  @Input({ required: true }) getAppointmentTypeLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input({ required: true }) getLocationLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input({ required: true }) markAppointmentDateDisabled!: (date: NgbDateStruct) => boolean;
  @Input({ required: true }) isAvailableAppointmentDate!: (date: NgbDateStruct) => boolean;
  @Input() isFieldInvalid: (name: string) => boolean = () => false;

  @Output() locationSelected = new EventEmitter<string>();
  @Output() appointmentDateCleared = new EventEmitter<void>();
}
