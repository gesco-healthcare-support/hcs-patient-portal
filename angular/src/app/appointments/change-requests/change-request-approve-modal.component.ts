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
import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import type { AppointmentChangeRequestDto } from '../../proxy/appointment-change-requests/models';
import { AppointmentChangeRequestApprovalService } from '../../proxy/appointment-change-requests/appointment-change-request-approval.service';

interface OutcomeOption {
  value: AppointmentStatusType;
  labelKey: string;
}

/**
 * AP1 supervisor approve modal (`<abp-modal>` pattern). Type-aware: shows the
 * NoBill/Late outcomes for the request's kind and calls approveReschedule /
 * approveCancellation. Defaults to NoBill on open. `concurrencyStamp` is
 * omitted -- the server's status-based ChangeRequestAlreadyHandled guard
 * protects against double-processing.
 */
@Component({
  selector: 'app-change-request-approve-modal',
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
  templateUrl: './change-request-approve-modal.component.html',
})
export class ChangeRequestApproveModalComponent implements OnChanges {
  @Input() changeRequest: AppointmentChangeRequestDto | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() handled = new EventEmitter<void>();

  private readonly approvalService = inject(AppointmentChangeRequestApprovalService);

  outcome: AppointmentStatusType | null = null;
  isBusy = false;

  get isCancel(): boolean {
    return this.changeRequest?.changeRequestType === ChangeRequestType.Cancel;
  }

  get titleKey(): string {
    return this.isCancel
      ? '::ChangeRequest:Modal:ApproveCancellationTitle'
      : '::ChangeRequest:Modal:ApproveRescheduleTitle';
  }

  get outcomeOptions(): OutcomeOption[] {
    return this.isCancel
      ? [
          {
            value: AppointmentStatusType.CancelledNoBill,
            labelKey: '::ChangeRequest:Outcome:NoBill',
          },
          { value: AppointmentStatusType.CancelledLate, labelKey: '::ChangeRequest:Outcome:Late' },
        ]
      : [
          {
            value: AppointmentStatusType.RescheduledNoBill,
            labelKey: '::ChangeRequest:Outcome:NoBill',
          },
          {
            value: AppointmentStatusType.RescheduledLate,
            labelKey: '::ChangeRequest:Outcome:Late',
          },
        ];
  }

  get canSubmit(): boolean {
    return !this.isBusy && this.outcome !== null;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible && !changes['visible'].previousValue) {
      this.outcome = this.isCancel
        ? AppointmentStatusType.CancelledNoBill
        : AppointmentStatusType.RescheduledNoBill;
    }
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.isBusy = false;
    }
  }

  submit(): void {
    if (!this.changeRequest?.id || this.outcome === null || !this.canSubmit) {
      return;
    }
    this.isBusy = true;
    const id = this.changeRequest.id;
    const request$ = this.isCancel
      ? this.approvalService.approveCancellation(id, { cancellationOutcome: this.outcome })
      : this.approvalService.approveReschedule(id, { rescheduleOutcome: this.outcome });
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
