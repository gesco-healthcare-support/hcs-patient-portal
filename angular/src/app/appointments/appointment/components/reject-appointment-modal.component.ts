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
import { AppointmentApprovalService } from '../../../proxy/appointments/appointment-approval.service';
import type { AppointmentDto, RejectAppointmentInput } from '../../../proxy/appointments/models';

/**
 * Reject modal -- OLD-parity reject flow.
 *
 * Mirrors OLD's "Reject appointment request" popup at
 * `P:\PatientPortalOld\patientappointment-portal\src\app\components\
 * appointment-request\appointments\view\appointment-view.component.html`:132-135:
 *   - Header: "Reject appointment request"
 *   - Body  : single textarea labelled "Write Rejection Reason", 5 rows
 *   - Footer: Reject (disabled until reason populated) + Close
 *
 * A1 (2026-05-05): switch from the thin endpoint
 * (POST /api/app/appointments/{id}/reject, Authorize=Edit) to the rich
 * endpoint (POST /api/app/appointment-approvals/{id}/reject,
 * Authorize=Reject) so the per-action permission gate matches the OLD
 * intent and the AppointmentRejectedEto dispatch path is exercised.
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

  private approvalService = inject(AppointmentApprovalService);
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
    this.approvalService.rejectAppointment(this.appointmentId, input).subscribe({
      next: (dto: AppointmentDto) => {
        // OLD-verbatim toast (P:\PatientPortalOld\...\view\
        // appointment-view.component.ts:117).
        this.toaster.success('Appointment booking request has been Rejected');
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
