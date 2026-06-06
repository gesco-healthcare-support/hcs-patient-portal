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
import { ModalComponent, ModalCloseDirective, ButtonComponent } from '@abp/ng.theme.shared';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import type { AppointmentChangeRequestDto } from '../../../proxy/appointment-change-requests/models';

/**
 * AP1 cancellation-request modal. Same ABP `<abp-modal>` pattern as the reject
 * modal: a single required reason. Submits the change request and emits the
 * resulting DTO; the host owns the auto-approve chain (internal staff) and the
 * toast. External / Clinic Staff submissions stay Pending.
 *
 * Usage:
 *   <app-cancellation-request-modal
 *     [appointmentId]="..." [(visible)]="cancelVisible"
 *     (succeeded)="onChangeRequestSucceeded($event)">
 *   </app-cancellation-request-modal>
 */
@Component({
  selector: 'app-cancellation-request-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
  ],
  templateUrl: './cancellation-request-modal.component.html',
  styles: [],
})
export class CancellationRequestModalComponent {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentChangeRequestDto>();

  private readonly changeRequestService = inject(AppointmentChangeRequestService);

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
      this.reason = '';
      this.isBusy = false;
    }
  }

  submit(): void {
    if (!this.appointmentId || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    this.changeRequestService
      .requestCancellation(this.appointmentId, { reason: this.reason.trim() })
      .subscribe({
        next: (dto: AppointmentChangeRequestDto) => {
          this.succeeded.emit(dto);
          this.setVisible(false);
        },
        error: () => {
          this.isBusy = false;
        },
      });
  }
}
