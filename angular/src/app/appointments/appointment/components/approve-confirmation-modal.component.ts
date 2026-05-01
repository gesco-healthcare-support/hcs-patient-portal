import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import {
  ModalComponent,
  ModalCloseDirective,
  ButtonComponent,
  ToasterService,
} from '@abp/ng.theme.shared';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import type { AppointmentDto } from '../../../proxy/appointments/models';

/**
 * W1-1 Approve confirmation modal -- "Are you sure?" before firing the
 * approve endpoint. Per W1-1 Q4 (Adrian wants protection against
 * accidental Approve clicks).
 */
@Component({
  selector: 'app-approve-confirmation-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [CommonModule, LocalizationPipe, ModalComponent, ModalCloseDirective, ButtonComponent],
  templateUrl: './approve-confirmation-modal.component.html',
  styles: [],
})
export class ApproveConfirmationModalComponent {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentDto>();

  private appointmentService = inject(AppointmentService);
  private toaster = inject(ToasterService);

  isBusy = false;

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.isBusy = false;
    }
  }

  confirm(): void {
    if (!this.appointmentId || this.isBusy) {
      return;
    }
    this.isBusy = true;
    this.appointmentService.approve(this.appointmentId).subscribe({
      next: (dto) => {
        this.toaster.success('::Appointment:Toast:Approved');
        this.succeeded.emit(dto);
        this.setVisible(false);
      },
      error: () => {
        this.isBusy = false;
      },
    });
  }
}
