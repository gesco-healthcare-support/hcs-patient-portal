import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import { AppLookupSelectComponent } from '../../shared/components/app-lookup-select.component';
import {
  AvailabilityCalendarComponent,
  AvailabilitySelection,
} from '../availability-calendar/availability-calendar.component';
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
    AvailabilityCalendarComponent,
  ],
  templateUrl: './appointment-add-schedule.component.html',
  styleUrl: './appointment-add-schedule.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddScheduleComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) checkForAppointmentTypeSelected = false;
  @Input({ required: true }) minimumBookingRuleMessage = '';
  @Input({ required: true }) getAppointmentTypeLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input({ required: true }) getLocationLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input() isFieldInvalid: (name: string) => boolean = () => false;
  // AF4 (2026-06-04): parent-owned flag; true when the selected type is PQME, so
  // the Panel Number label shows a required marker. The enable/disable + required
  // validator are applied programmatically on the parent control (AF3/AF4); this
  // input drives the visual affordance only.
  @Input() isPqmeType = false;
  // 2026-06-23: non-empty when the lookup resolved with zero bookable dates for
  // the chosen type+location, so the date UI explains WHY (lead-time window)
  // instead of showing a silently all-disabled calendar. Empty = hide.
  @Input() noBookableDatesMessage = '';

  @Output() locationSelected = new EventEmitter<string>();
  @Output() appointmentDateCleared = new EventEmitter<void>();

  /** `YYYY-MM-DD` for the calendar, from whatever shape the form control holds. */
  protected get selectedDateKey(): string | null {
    const raw = this.form.get('appointmentDate')?.value as string | null;
    if (!raw) return null;
    return raw.includes('-') && raw.length >= 10 ? raw.slice(0, 10) : null;
  }

  /**
   * Adapts the calendar's output onto the existing form controls, so nothing downstream of the form
   * changes.
   *
   * <p>`appointmentDate` is patched WITH events on purpose: the parent subscribes to it and applies
   * the role-horizon interception (external users get a contact-staff notice beyond 60 days). The
   * other two are patched silently because no parent rule depends on them.</p>
   */
  protected onSlotSelected(selection: AvailabilitySelection): void {
    this.form.patchValue(
      { appointmentTime: selection.time, doctorAvailabilityId: selection.doctorAvailabilityId },
      { emitEvent: false },
    );
    this.form.patchValue({ appointmentDate: selection.date });
  }
}
