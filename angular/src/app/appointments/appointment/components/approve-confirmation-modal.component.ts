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
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { LocalizationPipe } from '@abp/ng.core';
import {
  ButtonComponent,
  ModalCloseDirective,
  ModalComponent,
  ToasterService,
} from '@abp/ng.theme.shared';

import { AppointmentApprovalService } from '../../../proxy/appointments/appointment-approval.service';
import type { AppointmentDto } from '../../../proxy/appointments/models';

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
 *     endpoint persists `InternalUserComments`, fires the state-machine
 *     transition, and publishes
 *     `AppointmentApprovedEto` for the email cascade and package-document
 *     queue (Phase 14).
 *
 * Field set:
 *   - Any comments?: OPTIONAL textarea, label and placeholder verbatim from
 *     OLD `appointment-view.component.html`:141-144.
 *   - OLD's REQUIRED "Responsible User" select was retired on 2026-11-02 (the
 *     office's email-routing decision), with its lookup call. Approve is
 *     therefore enabled whenever the modal is not busy: nothing has to load
 *     or be picked first.
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
    internalUserComments: [null as string | null],
  });

  isBusy = false;

  /**
   * The Approve button's only gate: a request already running. Nothing has to be picked or loaded
   * first (2026-11-02), so a staff member can always approve an open booking.
   */
  get approveDisabled(): boolean {
    return this.isBusy;
  }

  ngOnChanges(changes: SimpleChanges): void {
    // A closed modal forgets the typed comment, so a reopen starts empty.
    if (changes['visible'] && !this.visible) {
      this.form.reset();
    }
  }

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
    const v = this.form.getRawValue();
    this.approvalService
      .approveAppointment(this.appointmentId, {
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
