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
 * toast. External / Intake Staff submissions stay Pending.
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
  // C2 (2026-07-01): a staff filer sees the both-parties-consent note; an
  // external party sees the opposing-party note.
  @Input() requesterIsStaff = false;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentChangeRequestDto>();

  private readonly changeRequestService = inject(AppointmentChangeRequestService);

  reason = '';
  isBusy = false;
  // F-M04 (2026-06-25): surface a request failure inside the modal instead of
  // leaving an enabled-but-dead Submit button. The dialog stays dismissible.
  errorMessage: string | null = null;

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
      this.errorMessage = null;
    }
  }

  submit(): void {
    if (!this.appointmentId || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    this.errorMessage = null;
    this.changeRequestService
      .requestCancellation(this.appointmentId, { reason: this.reason.trim() })
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
            'This appointment cannot be cancelled in its current status.';
        },
      });
  }
}
