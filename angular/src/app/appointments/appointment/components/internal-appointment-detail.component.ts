import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { NgbDatepickerModule } from '@ng-bootstrap/ng-bootstrap';

import { AppointmentViewComponent } from './appointment-view.component';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestApprovalService } from '../../../proxy/appointment-change-requests/appointment-change-request-approval.service';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';
import type { AppointmentChangeRequestDto } from '../../../proxy/appointment-change-requests/models';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';
import type { AppointmentInfoRequestRoundDto } from '../../../proxy/appointment-info-requests/models';
import { appointmentStatusToPill } from '../../../shared/ui/status-pill/appointment-status.util';
import type { AppointmentPillStatus } from '../../../shared/ui/status-pill/status-pill.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import { SsnInputComponent } from '../../../shared/components/ssn-input.component';
import { AppLookupSelectComponent } from '../../../shared/components/app-lookup-select.component';
import { AppointmentDocumentsComponent } from '../../../appointment-documents/appointment-documents.component';
import { AppointmentPacketComponent } from '../../../appointment-packet/appointment-packet.component';
import { ApproveConfirmationModalComponent } from './approve-confirmation-modal.component';
import { RejectAppointmentModalComponent } from './reject-appointment-modal.component';
import { RescheduleRequestModalComponent } from './reschedule-request-modal.component';
import { CancellationRequestModalComponent } from './cancellation-request-modal.component';
import { RequestInfoModalComponent } from './request-info-modal.component';
import { planAutoApprove } from './change-request-auto-approve';
import { decideByInfo, type DecideBy } from './internal-appointments.util';
import {
  bannerVariant,
  detailActions,
  statusLabel,
  type DetailAction,
} from './internal-detail.util';
import {
  changedRows,
  fixedSummary,
  flaggedSummary,
  latestRound,
  notePreview,
  wasResubmitted,
  type DiffRow,
} from './send-back-history.util';

/**
 * Internal Appointment Detail (redesign, Prompt 11). EXTENDS
 * AppointmentViewComponent so it inherits the full load + reactive form +
 * atomic save + lifecycle-modal + authorized-user engine with zero
 * duplication; this subclass adds the redesigned .ad-* presentation (status
 * banner, section cards, an Edit-details mode over the inherited form) and
 * wires reschedule/cancel through the change-request modals with the same
 * auto-approve chain the appointments list uses. Internal staff only -- the
 * external view/:id is a separate role-split route.
 */
@Component({
  selector: 'app-internal-appointment-detail',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDatepickerModule,
    IconComponent,
    SkeletonComponent,
    SsnInputComponent,
    AppLookupSelectComponent,
    AppointmentDocumentsComponent,
    AppointmentPacketComponent,
    ApproveConfirmationModalComponent,
    RejectAppointmentModalComponent,
    RescheduleRequestModalComponent,
    CancellationRequestModalComponent,
    RequestInfoModalComponent,
  ],
  templateUrl: './internal-appointment-detail.component.html',
})
export class InternalAppointmentDetailComponent extends AppointmentViewComponent implements OnInit {
  // Parent declares these private, so re-inject under local names.
  private readonly detailRouter = inject(Router);
  private readonly detailRoute = inject(ActivatedRoute);
  private readonly detailAppointments = inject(AppointmentService);
  private readonly approvalService = inject(AppointmentChangeRequestApprovalService);
  private readonly detailPermission = inject(PermissionService);
  private readonly detailToaster = inject(ToasterService);
  private readonly detailLocalization = inject(LocalizationService);
  private readonly infoRequestApi = inject(AppointmentInfoRequestService);

  /** Edit-details mode: read ledgers (false) vs editable form inputs (true). */
  protected editMode = false;
  private formSnapshot: Record<string, unknown> | null = null;

  /** Send Back rounds (newest-first) for the staff review cards. */
  protected infoHistory: AppointmentInfoRequestRoundDto[] = [];

  // Resolve the patient's appointment-language GUID to a display name for the
  // read ledger (mirrors the inherited stateName map; the parent only loads states).
  private readonly languageNamesById = new Map<string, string>();

  override ngOnInit(): void {
    super.ngOnInit();
    this.getAppointmentLanguageLookup({ filter: '', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (res) => {
        (res.items ?? []).forEach((item) => {
          if (item.id) {
            this.languageNamesById.set(item.id, item.displayName ?? '');
          }
        });
      },
    });
    this.loadHistory();
  }

  protected languageName(id?: string | null): string {
    return id ? (this.languageNamesById.get(id) ?? '') : '';
  }

  // ---- status banner ----
  protected get pill(): AppointmentPillStatus {
    return appointmentStatusToPill(this.currentStatus ?? AppointmentStatusType.Pending);
  }
  protected get bannerVariant(): string {
    return bannerVariant(this.pill);
  }
  protected get statusLabel(): string {
    return statusLabel(this.pill);
  }
  protected get actions(): DetailAction[] {
    return this.isInternalUser ? detailActions(this.pill) : [];
  }
  protected can(action: DetailAction): boolean {
    return this.actions.includes(action);
  }

  // ---- send-back review (Branch 2) ----
  private loadHistory(): void {
    const id = this.detailRoute.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }
    this.infoRequestApi.getHistory(id).subscribe({
      next: (rounds) => (this.infoHistory = rounds ?? []),
    });
  }

  /** Show the "Resubmitted" badge when the latest round was resubmitted and still Pending. */
  protected get showResubmittedBadge(): boolean {
    return this.pill === 'Pending' && wasResubmitted(this.infoHistory);
  }

  /** The latest round when it is resolved (drives the diff card + banner meta). */
  protected get resubmittedRound(): AppointmentInfoRequestRoundDto | null {
    const round = latestRound(this.infoHistory);
    return round?.isResolved ? round : null;
  }

  /** Changed field rows for the "What changed since your request" card. */
  protected get diffRows(): DiffRow[] {
    return changedRows(this.resubmittedRound);
  }

  /** Collapsible state for the What-changed diff card (open by default). */
  protected diffOpen = true;

  protected toggleDiff(): void {
    this.diffOpen = !this.diffOpen;
  }

  /** Collapsible state for the Request history card (collapsed by default; R2-1). */
  protected historyOpen = false;

  protected toggleHistory(): void {
    this.historyOpen = !this.historyOpen;
  }

  protected roundFixedSummary(round: AppointmentInfoRequestRoundDto): string {
    return fixedSummary(round);
  }
  protected roundFlaggedSummary(round: AppointmentInfoRequestRoundDto): string {
    return flaggedSummary(round);
  }
  protected roundNotePreview(note?: string | null): string {
    return notePreview(note);
  }

  // ---- meta / nav-prop accessors ----
  protected get apptTypeName(): string {
    return this.appointment?.appointmentType?.name ?? '';
  }
  protected get locationName(): string {
    return this.appointment?.location?.name ?? '';
  }
  protected get confNo(): string {
    return this.appointment?.appointment?.requestConfirmationNumber ?? '';
  }
  protected get apptDate(): string | Date | null | undefined {
    return this.appointment?.appointment?.appointmentDate;
  }
  protected get requestedOn(): string | Date | null | undefined {
    return this.appointment?.appointment?.creationTime;
  }
  protected get modifiedOn(): string | Date | null | undefined {
    return this.appointment?.appointment?.lastModificationTime;
  }
  protected get patientDisplayName(): string {
    return [this.fv('patientFirstName'), this.fv('patientLastName')]
      .filter(Boolean)
      .join(' ')
      .trim();
  }

  /** Inherited form value as a display string ('' when empty). */
  protected fv(name: string): string {
    const v = this.form.get(name)?.value;
    return v === null || v === undefined || v === '' ? '' : String(v);
  }

  /** Gender display label (enum name) for the read ledger. */
  protected get genderLabel(): string {
    const id = this.form.get('patientGenderId')?.value;
    return this.genderOptions.find((o) => o.value === id)?.key ?? '';
  }

  /** True when the patient needs an interpreter (drives the vendor row). */
  protected get needsInterpreter(): boolean {
    return !!this.form.get('patientNeedsInterpreter')?.value;
  }

  // ---- staff panel ----
  protected get bookerEmail(): string {
    // QA F-011: prefer the actual booker (BookedByUserId, resolved server-side).
    // Fall back to identityUser (patient/owner) only for legacy rows booked
    // before BookedByUserId existed and with no audit CreatorId to resolve.
    const booker = this.appointment?.bookedByUser;
    return (
      booker?.email ??
      booker?.userName ??
      this.appointment?.identityUser?.email ??
      this.appointment?.identityUser?.userName ??
      ''
    );
  }
  protected get decideBy(): DecideBy | null {
    if (this.pill !== 'Pending') {
      return null;
    }
    return decideByInfo(this.appointment?.appointment?.creationTime, new Date());
  }
  protected get approvalComments(): string {
    return this.pill === 'Approved' ? this.fv('internalUserComments') : '';
  }
  protected get rejectionReason(): string {
    return this.pill === 'Rejected' ? this.fv('rejectionNotes') : '';
  }

  // ---- edit-details mode ----
  protected enterEdit(): void {
    this.formSnapshot = this.form.getRawValue();
    this.editMode = true;
  }
  protected cancelEdit(): void {
    if (this.formSnapshot) {
      this.form.patchValue(this.formSnapshot, { emitEvent: false });
    }
    this.editMode = false;
  }
  protected async saveEdit(): Promise<void> {
    try {
      await this.save();
      this.editMode = false;
    } catch {
      // Parent sets errorMessage; stay in edit mode so the user can retry.
    }
  }

  // ---- action launchers (delegate to the inherited engine) ----
  protected approve(): void {
    this.dispatchAction('approve');
  }
  protected reject(): void {
    this.dispatchAction('reject');
  }
  protected reschedule(): void {
    this.openRescheduleRequest();
  }
  protected cancel(): void {
    this.openCancelRequest();
  }
  protected requestInfo(): void {
    this.openRequestInfo();
  }
  protected back(): void {
    void this.detailRouter.navigateByUrl('/appointments');
  }
  protected openChangeLog(): void {
    const id = this.appointment?.appointment?.id;
    if (id) {
      void this.detailRouter.navigate(['/appointments/view', id, 'change-log']);
    }
  }
  protected get canViewChangeLog(): boolean {
    return this.detailPermission.getGrantedPolicy('CaseEvaluation.AppointmentChangeLogs');
  }
  protected get canDownloadDemographics(): boolean {
    return this.detailPermission.getGrantedPolicy('CaseEvaluation.Reports');
  }

  /**
   * Override the parent's external "stays Pending" handler: internal staff get
   * the same auto-approve chain as the appointments list -- if the caller can
   * approve change requests, chain the NoBill approval, else it stays Pending.
   */
  override onChangeRequestSucceeded(dto: AppointmentChangeRequestDto): void {
    const canApprove = this.detailPermission.getGrantedPolicy(
      'CaseEvaluation.AppointmentChangeRequests.Approve',
    );
    const plan = planAutoApprove(dto.changeRequestType, canApprove);

    if (!plan || !dto.id) {
      this.detailToaster.success(
        this.detailLocalization.instant(
          dto.changeRequestType === ChangeRequestType.Cancel
            ? '::Appointment:Toast:CancelRequested'
            : '::Appointment:Toast:RescheduleRequested',
        ),
      );
      this.reloadDetail();
      return;
    }

    const approve$ =
      plan.kind === 'reschedule'
        ? this.approvalService.approveReschedule(dto.id, { rescheduleOutcome: plan.outcome })
        : this.approvalService.approveCancellation(dto.id, { cancellationOutcome: plan.outcome });

    approve$.subscribe({
      next: () => {
        this.detailToaster.success(
          this.detailLocalization.instant(
            plan.kind === 'reschedule'
              ? '::Appointment:Toast:RescheduleApproved'
              : '::Appointment:Toast:CancelApproved',
          ),
        );
        this.reloadDetail();
      },
      error: () => this.reloadDetail(),
    });
  }

  private reloadDetail(): void {
    const id = this.appointment?.appointment?.id ?? this.detailRoute.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }
    this.loadHistory();
    this.detailAppointments.getWithNavigationProperties(id).subscribe({
      next: (data) => {
        this.appointment = data;
      },
    });
  }
}
