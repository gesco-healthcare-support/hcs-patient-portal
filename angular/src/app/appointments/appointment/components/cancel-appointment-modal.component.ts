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
import { LocalizationPipe, RestService } from '@abp/ng.core';
import {
  ModalComponent,
  ModalCloseDirective,
  ButtonComponent,
  ToasterService,
} from '@abp/ng.theme.shared';
import { firstValueFrom } from 'rxjs';
import type { AppointmentDto } from '../../../proxy/appointments/models';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';

/**
 * Cancel-appointment modal -- G-02-05 one-step internal-staff cancel of an
 * Approved appointment (OLD AppointmentDomain.Update CancelledNoBill branch).
 * Collects a required reason + the NoBill/Late outcome and POSTs to the rich
 * approval endpoint (Authorize = Appointments.Edit). Uses RestService directly
 * (mirroring this view's other calls) so the new endpoint needs no proxy
 * regeneration.
 *
 * Usage from parent:
 *   <app-cancel-appointment-modal
 *     [appointmentId]="appointment.id"
 *     [(visible)]="cancelModalVisible"
 *     (succeeded)="onActionSucceeded($event)"
 *   ></app-cancel-appointment-modal>
 */
@Component({
  selector: 'app-cancel-appointment-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
  ],
  templateUrl: './cancel-appointment-modal.component.html',
  styles: [],
})
export class CancelAppointmentModalComponent {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentDto>();

  private readonly restService = inject(RestService);
  private readonly toaster = inject(ToasterService);

  readonly noBillOutcome = AppointmentStatusType.CancelledNoBill;
  readonly lateOutcome = AppointmentStatusType.CancelledLate;

  reason = '';
  cancellationOutcome: AppointmentStatusType = AppointmentStatusType.CancelledNoBill;
  isBusy = false;

  readonly maxReasonLength = 500;
  readonly minReasonLength = 5;

  get canSubmit(): boolean {
    const trimmed = this.reason.trim().length;
    return (
      !this.isBusy && trimmed >= this.minReasonLength && this.reason.length <= this.maxReasonLength
    );
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.reason = '';
      this.cancellationOutcome = AppointmentStatusType.CancelledNoBill;
      this.isBusy = false;
    }
  }

  async submit(): Promise<void> {
    if (!this.appointmentId || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    try {
      const dto = await firstValueFrom(
        this.restService.request<
          { cancellationOutcome: AppointmentStatusType; reason: string },
          AppointmentDto
        >(
          {
            method: 'POST',
            url: `/api/app/appointment-approvals/${this.appointmentId}/cancel`,
            body: { cancellationOutcome: this.cancellationOutcome, reason: this.reason.trim() },
          },
          { apiName: 'Default' },
        ),
      );
      this.toaster.success('Appointment has been cancelled');
      this.succeeded.emit(dto);
      this.setVisible(false);
    } catch {
      // ABP default error handler renders the BusinessException toast.
      this.isBusy = false;
    }
  }
}
