import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import {
  ModalComponent,
  ModalCloseDirective,
  ButtonComponent,
  ToasterService,
} from '@abp/ng.theme.shared';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import type { AppointmentDto, RejectAppointmentInput } from '../../../proxy/appointments/models';

/**
 * W1-1 Reject modal -- captures a required reason and POSTs to
 * /api/app/appointments/{id}/reject. Reason is required (W1-1 Q2).
 *
 * Usage from parent:
 *   <app-reject-appointment-modal
 *     [appointmentId]="appointment.id"
 *     [(visible)]="rejectVisible"
 *     (succeeded)="onActionSuccess($event)"
 *   ></app-reject-appointment-modal>
 */
@Component({
  selector: 'app-reject-appointment-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
  ],
  templateUrl: './reject-appointment-modal.component.html',
  styles: [],
})
export class RejectAppointmentModalComponent {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentDto>();

  private appointmentService = inject(AppointmentService);
  private toaster = inject(ToasterService);

  reason = '';
  isBusy = false;

  readonly maxReasonLength = 500;

  get canSubmit(): boolean {
    return (
      !this.isBusy && this.reason.trim().length > 0 && this.reason.length <= this.maxReasonLength
    );
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      // reset state when closing
      this.reason = '';
      this.isBusy = false;
    }
  }

  submit(): void {
    if (!this.appointmentId || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    const input: RejectAppointmentInput = { reason: this.reason.trim() };
    this.appointmentService.reject(this.appointmentId, input).subscribe({
      next: (dto) => {
        this.toaster.success('::Appointment:Toast:Rejected');
        this.succeeded.emit(dto);
        this.setVisible(false);
      },
      error: () => {
        // ABP default error handler renders the BusinessException toast.
        this.isBusy = false;
      },
    });
  }
}
