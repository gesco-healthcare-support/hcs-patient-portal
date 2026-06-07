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
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import type { AppointmentChangeRequestDto } from '../../proxy/appointment-change-requests/models';
import { AppointmentChangeRequestApprovalService } from '../../proxy/appointment-change-requests/appointment-change-request-approval.service';

/**
 * AP1 supervisor reject modal (`<abp-modal>` pattern). Type-aware: routes to
 * rejectReschedule / rejectCancellation. A rejection reason is required (the
 * server also enforces `ChangeRequestRejectionRequiresNotes`).
 */
@Component({
  selector: 'app-change-request-reject-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
  ],
  templateUrl: './change-request-reject-modal.component.html',
})
export class ChangeRequestRejectModalComponent {
  @Input() changeRequest: AppointmentChangeRequestDto | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() handled = new EventEmitter<void>();

  private readonly approvalService = inject(AppointmentChangeRequestApprovalService);

  reason = '';
  isBusy = false;

  readonly maxReasonLength = 2000;

  get isCancel(): boolean {
    return this.changeRequest?.changeRequestType === ChangeRequestType.Cancel;
  }

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
    if (!this.changeRequest?.id || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    const id = this.changeRequest.id;
    const input = { reason: this.reason.trim() };
    const request$ = this.isCancel
      ? this.approvalService.rejectCancellation(id, input)
      : this.approvalService.rejectReschedule(id, input);
    request$.subscribe({
      next: () => {
        this.handled.emit();
        this.setVisible(false);
      },
      error: () => {
        this.isBusy = false;
      },
    });
  }
}
