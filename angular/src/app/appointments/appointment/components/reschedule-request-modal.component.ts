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
import { DoctorAvailabilityService } from '../../../proxy/doctor-availabilities/doctor-availability.service';
import type { DoctorAvailabilityDto } from '../../../proxy/doctor-availabilities/models';

/**
 * AP1 reschedule-request modal. Mirrors the approve/reject modal pattern
 * (ABP `<abp-modal>` + `[(visible)]` two-way binding + `(succeeded)` output --
 * the in-use appointment modal stack, not MatDialog/NgbModal). Submits the
 * change request only; the host decides whether to chain the auto-approve call
 * (internal staff) or leave it Pending (external), and owns the toast.
 *
 * Usage:
 *   <app-reschedule-request-modal
 *     [appointmentId]="..." [locationId]="..." [appointmentTypeId]="..."
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
  ],
  templateUrl: './reschedule-request-modal.component.html',
  styles: [],
})
export class RescheduleRequestModalComponent implements OnChanges {
  @Input() appointmentId: string | null = null;
  @Input() locationId: string | null = null;
  @Input() appointmentTypeId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentChangeRequestDto>();

  private readonly changeRequestService = inject(AppointmentChangeRequestService);
  private readonly availabilityService = inject(DoctorAvailabilityService);

  slots: DoctorAvailabilityDto[] = [];
  newDoctorAvailabilityId: string | null = null;
  reason = '';
  isBusy = false;
  isLoadingSlots = false;

  readonly maxReasonLength = 500;

  get canSubmit(): boolean {
    return (
      !this.isBusy &&
      !!this.newDoctorAvailabilityId &&
      this.reason.trim().length > 0 &&
      this.reason.length <= this.maxReasonLength
    );
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Load slots fresh each time the modal opens (false -> true).
    if (changes['visible'] && this.visible && !changes['visible'].previousValue) {
      this.reason = '';
      this.newDoctorAvailabilityId = null;
      this.loadSlots();
    }
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.reason = '';
      this.newDoctorAvailabilityId = null;
      this.isBusy = false;
    }
  }

  slotLabel(slot: DoctorAvailabilityDto): string {
    const date = slot.availableDate ? slot.availableDate.substring(0, 10) : '';
    const from = slot.fromTime ?? '';
    const to = slot.toTime ?? '';
    return `${date} ${from} - ${to}`.trim();
  }

  private loadSlots(): void {
    if (!this.locationId || !this.appointmentTypeId) {
      this.slots = [];
      return;
    }
    this.isLoadingSlots = true;
    const from = new Date();
    const to = new Date();
    to.setDate(to.getDate() + 90);
    this.availabilityService
      .getDoctorAvailabilityLookup({
        locationId: this.locationId,
        appointmentTypeId: this.appointmentTypeId,
        availableDateFrom: from.toISOString(),
        availableDateTo: to.toISOString(),
      })
      .subscribe({
        next: (slots) => {
          this.slots = slots ?? [];
          this.isLoadingSlots = false;
        },
        error: () => {
          this.slots = [];
          this.isLoadingSlots = false;
        },
      });
  }

  submit(): void {
    if (!this.appointmentId || !this.newDoctorAvailabilityId || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    this.changeRequestService
      .requestReschedule(this.appointmentId, {
        newDoctorAvailabilityId: this.newDoctorAvailabilityId,
        reScheduleReason: this.reason.trim(),
        isBeyondLimit: false,
      })
      .subscribe({
        next: (dto: AppointmentChangeRequestDto) => {
          this.succeeded.emit(dto);
          this.setVisible(false);
        },
        // ABP's default error handler renders the BusinessException toast.
        error: () => {
          this.isBusy = false;
        },
      });
  }
}
