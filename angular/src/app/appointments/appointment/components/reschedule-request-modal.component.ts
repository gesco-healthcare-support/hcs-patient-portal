import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ModalComponent, ModalCloseDirective, ButtonComponent } from '@abp/ng.theme.shared';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import type { AppointmentChangeRequestDto } from '../../../proxy/appointment-change-requests/models';
import {
  AvailabilityCalendarComponent,
  type AvailabilitySelection,
} from '../../availability-calendar/availability-calendar.component';
import { canSubmitReschedule, rescheduleModalOptions } from './reschedule-submit.util';

/**
 * AP1 reschedule-request modal. Mirrors the approve/reject modal pattern
 * (ABP `<abp-modal>` + `[(visible)]` two-way binding + `(succeeded)` output --
 * the in-use appointment modal stack, not MatDialog/NgbModal). Submits the
 * change request only; the host owns the toast and the reload.
 *
 * <p>PHASE 4B (2026-08-04) -- ROLE-SPLIT BODY. Date selection moved from the requestor to
 * internal staff, so:</p>
 * <ul>
 *   <li>Internal staff filing a reschedule (`requesterIsStaff`) pick the new date with the
 *       shared {@link AvailabilityCalendarComponent} -- the SAME component and rules the booking
 *       flow uses. It replaced a raw `<select>` of every Available slot in a 90-day window that
 *       applied no lead-time or horizon gating at all, so a requestor could pick a date the
 *       server then rejected.</li>
 *   <li>External requestors get NO date control. They submit a reason and staff choose the date
 *       at approval, which is why `newDoctorAvailabilityId` is now nullable on the wire.</li>
 * </ul>
 *
 * Usage:
 *   <app-reschedule-request-modal
 *     [appointmentId]="..." [locationId]="..." [appointmentTypeId]="..."
 *     [requesterIsStaff]="..."
 *     [(visible)]="rescheduleVisible" (succeeded)="onChangeRequestSucceeded($event)">
 *   </app-reschedule-request-modal>
 */
@Component({
  selector: 'app-reschedule-request-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
    AvailabilityCalendarComponent,
  ],
  templateUrl: './reschedule-request-modal.component.html',
  styles: [],
})
export class RescheduleRequestModalComponent implements OnChanges {
  @Input() appointmentId: string | null = null;
  @Input() locationId: string | null = null;
  @Input() appointmentTypeId: string | null = null;
  // C2 (2026-07-01): staff filer -> both-parties-consent note; external -> opposing-party note.
  // Phase 4b also uses this to decide whether the date picker renders at all.
  @Input() requesterIsStaff = false;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentChangeRequestDto>();

  private readonly changeRequestService = inject(AppointmentChangeRequestService);

  /** Null for external requestors, who propose no date (phase 4b). */
  newDoctorAvailabilityId: string | null = null;
  /** `YYYY-MM-DD`; drives the calendar's selected day. Staff only. */
  selectedDate: string | null = null;
  selectedTime: string | null = null;
  reason = '';
  isBusy = false;
  // F-M04 parity (2026-06-29): surface a request failure inside the modal instead
  // of leaving an enabled-but-dead Submit button. Matches the cancellation modal;
  // without it an unmapped BusinessException (e.g. NewSlotNotAvailable when the
  // chosen slot fills before submit) only reaches ABP's generic error dialog. The
  // dialog stays dismissible and the reason/slot are preserved for a retry.
  errorMessage: string | null = null;

  readonly maxReasonLength = 500;

  /**
   * Staff get the wide dialog so the two-month datepicker popup is not clipped. Delegated to a
   * pure helper that returns FROZEN CONSTANTS -- see `rescheduleModalOptions` for why a fresh
   * object literal here hangs the browser.
   */
  get modalOptions(): object {
    return rescheduleModalOptions(this.requesterIsStaff);
  }

  get minimumBookingRuleMessage(): string {
    return 'Appointments must be at least 3 days from today.';
  }

  get canSubmit(): boolean {
    if (this.isBusy) {
      return false;
    }
    return canSubmitReschedule({
      requesterIsStaff: this.requesterIsStaff,
      slotId: this.newDoctorAvailabilityId,
      time: this.selectedTime,
      reason: this.reason,
      maxReasonLength: this.maxReasonLength,
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset each time the modal opens (false -> true). The calendar loads its own
    // availability from locationId / appointmentTypeId, so there is nothing to fetch here.
    if (changes['visible'] && this.visible && !changes['visible'].previousValue) {
      this.resetForm();
    }
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.resetForm();
      this.isBusy = false;
    }
  }

  /**
   * The calendar emits the date first with no time (so a parent can intercept before a slot is
   * committed), then again with the time and the resolved slot id. Mirror both so Submit only
   * unlocks once a real slot is identified.
   */
  onSlotSelected(selection: AvailabilitySelection): void {
    this.selectedDate = selection.date;
    this.selectedTime = selection.time;
    this.newDoctorAvailabilityId = selection.doctorAvailabilityId;
  }

  onDateCleared(): void {
    this.selectedDate = null;
    this.selectedTime = null;
    this.newDoctorAvailabilityId = null;
  }

  submit(): void {
    if (!this.appointmentId || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    this.errorMessage = null;
    this.changeRequestService
      .requestReschedule(this.appointmentId, {
        // Null when an external requestor filed: staff pick the date at approval.
        newDoctorAvailabilityId: this.newDoctorAvailabilityId,
        reScheduleReason: this.reason.trim(),
        isBeyondLimit: false,
      })
      .subscribe({
        next: (dto: AppointmentChangeRequestDto) => {
          this.succeeded.emit(dto);
          this.setVisible(false);
        },
        error: (err: { error?: { error?: { message?: string } } }) => {
          // Clear busy so Submit + Close/Escape work again, and show why it failed.
          this.isBusy = false;
          this.errorMessage =
            err?.error?.error?.message ??
            'This reschedule request could not be submitted. If you chose a slot it may no longer be available -- pick another, or try again.';
        },
      });
  }

  private resetForm(): void {
    this.reason = '';
    this.newDoctorAvailabilityId = null;
    this.selectedDate = null;
    this.selectedTime = null;
    this.errorMessage = null;
  }
}
