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
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { LocalizationPipe } from '@abp/ng.core';
import {
  ButtonComponent,
  ModalCloseDirective,
  ModalComponent,
  ToasterService,
} from '@abp/ng.theme.shared';

import { AppointmentApprovalService } from '../../../proxy/appointments/appointment-approval.service';
import type { AppointmentDto } from '../../../proxy/appointments/models';
import type { LookupDto } from '../../../proxy/shared/models';

/**
 * A1 (2026-05-05) -- staff-initiated Approve modal.
 *
 * Mirrors OLD's "Approve appointment request" popup at
 * `P:\PatientPortalOld\patientappointment-portal\src\app\components\
 * appointment-request\appointments\view\appointment-view.component.html`:1-154
 * but ports to NEW's stack:
 *   - ABP `<abp-modal>` instead of OLD's hand-rolled bootbox.
 *   - Reactive form (FormBuilder) instead of OLD's RxValidation.
 *   - Calls the rich endpoint `POST /api/app/appointment-approvals/{id}/approve`
 *     (Phase 12) instead of OLD's PATCH `/api/Appointments/{id}` -- the rich
 *     endpoint persists `PrimaryResponsibleUserId` + `InternalUserComments`,
 *     fires the state-machine transition, and publishes
 *     `AppointmentApprovedEto` for the email cascade and package-document
 *     queue (Phase 14).
 *
 * OLD-parity field set:
 *   - Responsible User: REQUIRED select, sourced from OLD's
 *     `internalUserNameLookUps` -- NEW serves the same shape via
 *     `AppointmentApprovalService.getInternalUserLookup` (5 internal roles
 *     per `BookingFlowRoles.InternalUserRoles`).
 *   - Any comments?: OPTIONAL textarea, label and placeholder verbatim from
 *     OLD `appointment-view.component.html`:141-144.
 *
 * Submit gate: button disabled until form is valid (Responsible User
 * selected). Mirrors OLD's `[disabled]="!appointmentRequestFromGroup.valid"`.
 *
 * Header text and the success-toast message are OLD-verbatim:
 *   - Header  : "Approve appointment request"
 *   - Subtitle: "Please approve an appointment request from here."
 *   - Toast   : "Appointment booking request has been approved"
 */
@Component({
  selector: 'app-approve-confirmation-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    ModalComponent,
    ModalCloseDirective,
    ButtonComponent,
  ],
  templateUrl: './approve-confirmation-modal.component.html',
})
export class ApproveConfirmationModalComponent implements OnChanges {
  @Input() appointmentId: string | null = null;
  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() succeeded = new EventEmitter<AppointmentDto>();

  private readonly fb = inject(FormBuilder);
  private readonly approvalService = inject(AppointmentApprovalService);
  private readonly toaster = inject(ToasterService);

  readonly form = this.fb.group({
    primaryResponsibleUserId: [null as string | null, [Validators.required]],
    internalUserComments: [null as string | null],
  });

  isBusy = false;
  responsibleUsers: LookupDto<string>[] = [];
  isLoadingUsers = false;
  loadError: string | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    // Lazy-load the responsible-user list the first time the modal becomes
    // visible. Resetting visibility false -> true (reopen) re-fetches in
    // case the staff list changed since the prior open.
    if (changes['visible'] && this.visible && !this.isLoadingUsers) {
      this.loadResponsibleUsers();
    }
    if (changes['visible'] && !this.visible) {
      this.form.reset();
      this.loadError = null;
    }
  }

  setVisible(value: boolean): void {
    this.visible = value;
    this.visibleChange.emit(value);
    if (!value) {
      this.isBusy = false;
    }
  }

  private async loadResponsibleUsers(): Promise<void> {
    this.isLoadingUsers = true;
    this.loadError = null;
    try {
      // No filter/paging -- the demo tenant has < 25 internal users; the
      // backend endpoint pages on demand if the list grows.
      const result = await firstValueFrom(
        this.approvalService.getInternalUserLookup({
          filter: '',
          skipCount: 0,
          maxResultCount: 100,
        }),
      );
      this.responsibleUsers = result.items ?? [];
    } catch {
      this.loadError = 'Failed to load responsible-user list. Please retry.';
      this.responsibleUsers = [];
    } finally {
      this.isLoadingUsers = false;
    }
  }

  retryLoad(): void {
    void this.loadResponsibleUsers();
  }

  confirm(): void {
    if (!this.appointmentId || this.isBusy || this.form.invalid) {
      return;
    }
    this.isBusy = true;
    const v = this.form.getRawValue();
    this.approvalService
      .approveAppointment(this.appointmentId, {
        primaryResponsibleUserId: v.primaryResponsibleUserId ?? '',
        overridePatientMatch: false,
        internalUserComments: v.internalUserComments?.trim() || null,
      })
      .subscribe({
        next: (dto) => {
          // OLD-verbatim toast (P:\PatientPortalOld\...\view\
          // appointment-view.component.ts:113).
          this.toaster.success('Appointment booking request has been approved');
          this.succeeded.emit(dto);
          this.setVisible(false);
        },
        error: () => {
          this.isBusy = false;
        },
      });
  }
}
