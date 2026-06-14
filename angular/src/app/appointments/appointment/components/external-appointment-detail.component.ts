import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, Injector, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfigStateService as AbpConfigStateService, RestService } from '@abp/ng.core';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { AppointmentViewComponent } from './appointment-view.component';
import { RescheduleRequestModalComponent } from './reschedule-request-modal.component';
import { CancellationRequestModalComponent } from './cancellation-request-modal.component';
import { AppointmentDocumentsComponent } from '../../../appointment-documents/appointment-documents.component';
import { AppointmentPacketComponent } from '../../../appointment-packet/appointment-packet.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { ExternalNavbarComponent } from '../../../shared/components/external-navbar/external-navbar.component';
import { SubmitQueryModalComponent } from '../../../user-queries/submit-query-modal.component';
import { performFullLogout } from '../../../shared/auth/full-logout';
import { resolveExternalUserDisplayName } from '../../../shared/auth/external-user-display-name';
import { appointmentStatusToPill } from '../../../shared/ui/status-pill/appointment-status.util';
import type { AppointmentPillStatus } from '../../../shared/ui/status-pill/status-pill.component';
import type { PatientDto, PatientUpdateDto } from '../../../proxy/patients/models';
import { firstValueFrom } from 'rxjs';

interface CalloutCopy {
  icon: string;
  title: string;
  body: string;
}

interface OpenInfoRequest {
  note: string;
  flaggedFields: { key: string; hint?: string | null }[];
}

// Display labels for flagged-field keys (mirrors the staff Request-info modal).
const FIELD_LABELS: Record<string, string> = {
  panelNumber: 'Panel number',
  appointmentDate: 'Appointment date',
  dateOfBirth: 'Date of birth',
  socialSecurityNumber: 'Social Security #',
  address: 'Address',
  cellPhoneNumber: 'Cell phone',
  appointmentLanguageId: 'Language',
  applicantAttorneyEmail: 'Applicant attorney email',
  defenseAttorneyFirmName: 'Defense attorney firm',
  appointmentInsuranceName: 'Insurance company',
  appointmentClaimExaminerEmail: 'Claim examiner email',
  documents: 'Documents',
};

const CALLOUTS: Record<string, CalloutCopy> = {
  pending: {
    icon: 'clock',
    title: 'Awaiting clinic review',
    body: "Your request was submitted and is pending staff approval. We'll email you when it's confirmed.",
  },
  approved: {
    icon: 'check',
    title: 'Appointment confirmed',
    body: 'Please arrive 15 minutes early with a photo ID. You can request a change below.',
  },
  rejected: {
    icon: 'alert',
    title: 'Request not approved',
    body: 'See the reason below. You can submit a new request addressing the issue.',
  },
  cancelled: {
    icon: 'x',
    title: 'Appointment cancelled',
    body: 'This appointment was cancelled. You can submit a new request if it is still needed.',
  },
  rescheduled: {
    icon: 'refresh',
    title: 'Appointment rescheduled',
    body: 'This appointment has been rescheduled -- the new date and time are shown above.',
  },
  'info-requested': {
    icon: 'help',
    title: 'The clinic needs more information',
    body: 'Update the highlighted details below, then resubmit. Your request returns to the clinic for review.',
  },
};

/**
 * External Appointment Detail (redesign). EXTENDS AppointmentViewComponent so it
 * inherits the full load + change-request + document-manager + authorized-user
 * engine with zero duplication; this subclass only adds the redesigned .ad-*
 * read-only presentation (status banner, sticky section nav, field ledgers) and
 * reuses the existing document-manager + change-request modal components.
 * Mounted at a temp route; external-only. (Distinct from the ABP-Suite
 * AppointmentDetailModalComponent, which is the list-page edit modal.)
 */
@Component({
  selector: 'app-external-appointment-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RescheduleRequestModalComponent,
    CancellationRequestModalComponent,
    AppointmentDocumentsComponent,
    AppointmentPacketComponent,
    IconComponent,
    ExternalNavbarComponent,
    SubmitQueryModalComponent,
  ],
  templateUrl: './external-appointment-detail.component.html',
  styleUrl: './external-appointment-detail.component.scss',
})
export class ExternalAppointmentDetailComponent
  extends AppointmentViewComponent
  implements OnInit, OnDestroy
{
  private readonly shellRouter = inject(Router);
  private readonly shellInjector = inject(Injector);
  private readonly shellConfig = inject(AbpConfigStateService);
  private readonly shellRest = inject(RestService);
  private readonly detailRoute = inject(ActivatedRoute);

  // Send Back fix-it: the open info request (staff note + flagged fields).
  protected infoRequest: OpenInfoRequest | null = null;
  protected resubmitConfirmVisible = false;
  protected isResubmitting = false;
  // Local edit model for flagged patient-demographic fields. Kept separate from
  // the inherited reactive form, which super.ngOnInit() disables for external
  // read-only viewing. Patient-level keys are editable inline here; other flagged
  // keys (schedule/attorney/insurance/documents) are shown for context and fixed
  // via their own affordances (documents via the document manager below).
  protected edits: Record<string, string> = {};
  private readonly patientEditableKeys = [
    'socialSecurityNumber',
    'cellPhoneNumber',
    'address',
    'dateOfBirth',
  ];

  protected navClinicName = 'Appointment Portal';
  protected navDisplayName = '';
  protected firmName = '';
  protected navRoleLabelText = '';
  protected navUserEmailText = '';
  protected submitQueryVisible = false;

  override ngOnInit(): void {
    super.ngOnInit();
    document.body.classList.add('redesign-shell');
    this.loadNavName();
    this.loadInfoRequest();
  }

  ngOnDestroy(): void {
    document.body.classList.remove('redesign-shell');
  }

  // ---- status banner ----
  protected get pill(): AppointmentPillStatus {
    return appointmentStatusToPill(this.currentStatus ?? AppointmentStatusType.Pending);
  }
  protected get bannerVariant(): string {
    return this.pill === 'InfoRequested' ? 'info-requested' : this.pill.toLowerCase();
  }
  protected get statusLabel(): string {
    return this.pill === 'InfoRequested' ? 'Info requested' : this.pill;
  }
  protected get callout(): CalloutCopy {
    return CALLOUTS[this.bannerVariant] ?? CALLOUTS['pending'];
  }
  protected get showOutcomeNote(): boolean {
    return ['approved', 'rejected', 'cancelled', 'rescheduled'].includes(this.bannerVariant);
  }

  // ---- appointment nav-prop accessors ----
  protected get apptTypeName(): string {
    return (
      (this.appointment as { appointmentType?: { name?: string } } | null)?.appointmentType?.name ??
      ''
    );
  }
  protected get locationDisplayName(): string {
    return (this.appointment as { location?: { name?: string } } | null)?.location?.name ?? '';
  }
  protected get confNo(): string {
    return this.appointment?.appointment?.requestConfirmationNumber ?? '';
  }
  protected get apptDate(): string | null | undefined {
    return this.appointment?.appointment?.appointmentDate;
  }
  protected get requestedOn(): string | null | undefined {
    return (this.appointment?.appointment as { creationTime?: string } | undefined)?.creationTime;
  }
  protected get patientDisplayName(): string {
    return [this.fv('patientFirstName'), this.fv('patientLastName')]
      .filter(Boolean)
      .join(' ')
      .trim();
  }

  /** Form value as a display string ('' when empty). */
  protected fv(name: string): string {
    const v = this.form.get(name)?.value;
    return v === null || v === undefined || v === '' ? '' : String(v);
  }

  protected scrollTo(id: string): void {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
  }

  // ---- navbar ----
  protected openProfileNav(): void {
    void this.shellRouter.navigateByUrl('/user-management/patients/my-profile');
  }
  protected openDocumentsNav(): void {
    void this.shellRouter.navigateByUrl('/');
  }
  protected backToHome(): void {
    void this.shellRouter.navigateByUrl('/');
  }
  protected openQuery(): void {
    this.submitQueryVisible = true;
  }
  protected signOut(): void {
    void performFullLogout(this.shellInjector);
  }

  private loadNavName(): void {
    const u = this.shellConfig.getOne('currentUser') as {
      name?: string;
      surname?: string;
      userName?: string;
      email?: string;
      roles?: string[];
    } | null;
    const t = this.shellConfig.getOne('currentTenant') as { name?: string } | null;
    this.navClinicName = t?.name || 'Appointment Portal';
    this.navUserEmailText = u?.email || u?.userName || '';
    this.navRoleLabelText = u?.roles?.[0] ?? '';
    this.navDisplayName = resolveExternalUserDisplayName(u?.name, u?.surname, '', u?.userName);
    this.shellRest
      .request<
        unknown,
        { firmName?: string }
      >({ method: 'GET', url: '/api/app/external-users/me' }, { apiName: 'Default' })
      .subscribe({
        next: (p) => {
          this.firmName = p?.firmName ?? '';
          this.navDisplayName = resolveExternalUserDisplayName(
            u?.name,
            u?.surname,
            this.firmName,
            u?.userName,
          );
        },
        error: () => {
          /* firm name optional */
        },
      });
  }

  // ---- Send Back fix-it ----

  private get patientNav(): PatientDto | undefined {
    return (this.appointment as { patient?: PatientDto } | null)?.patient ?? undefined;
  }

  /** Load the open info request and seed the inline edit model for patient fields. */
  private loadInfoRequest(): void {
    const id = this.detailRoute.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }
    this.shellRest
      .request<
        unknown,
        OpenInfoRequest | null
      >({ method: 'GET', url: `/api/app/appointment-info-requests/open/${id}` }, { apiName: 'Default' })
      .subscribe({
        next: (r) => {
          this.infoRequest = r ?? null;
          this.seedEdits();
        },
        error: () => {
          /* no open request -> ordinary read-only view */
        },
      });
  }

  /** Seed editable patient fields from the loaded appointment. SSN starts blank --
   *  the read DTO masks it to last-4, so it must never be echoed back as the value. */
  private seedEdits(): void {
    const p = this.patientNav;
    for (const f of this.infoRequest?.flaggedFields ?? []) {
      if (!this.isPatientEditable(f.key)) {
        continue;
      }
      if (f.key === 'socialSecurityNumber') {
        this.edits[f.key] = '';
      } else if (f.key === 'dateOfBirth') {
        this.edits[f.key] = (p?.dateOfBirth ?? '').slice(0, 10);
      } else {
        this.edits[f.key] = String((p as Record<string, unknown> | undefined)?.[f.key] ?? '');
      }
    }
  }

  protected get hasFlaggedFields(): boolean {
    return (this.infoRequest?.flaggedFields?.length ?? 0) > 0;
  }
  protected isPatientEditable(key: string): boolean {
    return this.patientEditableKeys.includes(key);
  }
  protected fieldLabel(key: string): string {
    return FIELD_LABELS[key] ?? key;
  }
  protected hintFor(key: string): string {
    return this.infoRequest?.flaggedFields.find((f) => f.key === key)?.hint ?? '';
  }
  protected inputType(key: string): string {
    return key === 'dateOfBirth' ? 'date' : 'text';
  }

  protected openResubmitConfirm(): void {
    this.resubmitConfirmVisible = true;
  }
  protected cancelResubmit(): void {
    this.resubmitConfirmVisible = false;
  }

  /**
   * Resubmit a sent-back appointment. If any patient-demographic field was flagged,
   * save the edits first via the booking patient-update endpoint (fetching a fresh
   * DTO for the concurrency stamp; SSN is only written when the user typed a new
   * value, never the masked read value), then transition InfoRequested -> Pending.
   */
  protected async confirmResubmit(): Promise<void> {
    const id = this.detailRoute.snapshot.paramMap.get('id');
    if (!id || this.isResubmitting) {
      return;
    }
    this.isResubmitting = true;
    try {
      await this.savePatientEditsIfAny();
      await firstValueFrom(
        this.shellRest.request<unknown, void>(
          { method: 'POST', url: `/api/app/appointment-info-requests/resubmit/${id}` },
          { apiName: 'Default' },
        ),
      );
      this.resubmitConfirmVisible = false;
      window.location.reload();
    } catch {
      this.isResubmitting = false;
    }
  }

  private async savePatientEditsIfAny(): Promise<void> {
    const flagged = this.infoRequest?.flaggedFields ?? [];
    const editablePatientKeys = flagged.map((f) => f.key).filter((k) => this.isPatientEditable(k));
    const patientId = this.patientNav?.id ?? this.appointment?.appointment?.patientId;
    if (editablePatientKeys.length === 0 || !patientId) {
      return;
    }
    // Fetch a fresh, complete patient DTO so required fields + concurrency stamp are intact.
    const current = await firstValueFrom(
      this.shellRest.request<unknown, PatientDto>(
        { method: 'GET', url: `/api/app/patients/for-appointment-booking/${patientId}` },
        { apiName: 'Default' },
      ),
    );
    const dto = { ...current } as PatientUpdateDto;
    // The read DTO masks SSN: never echo the mask back. Send the typed value, or null
    // so the server's never-clear rule preserves the stored SSN.
    dto.socialSecurityNumber = this.edits['socialSecurityNumber']?.trim() || null;
    if (editablePatientKeys.includes('cellPhoneNumber')) {
      dto.cellPhoneNumber = this.edits['cellPhoneNumber']?.trim() || null;
    }
    if (editablePatientKeys.includes('address')) {
      dto.address = this.edits['address']?.trim() || null;
    }
    if (editablePatientKeys.includes('dateOfBirth') && this.edits['dateOfBirth']) {
      dto.dateOfBirth = this.edits['dateOfBirth'];
    }
    await firstValueFrom(
      this.shellRest.request<PatientUpdateDto, PatientDto>(
        {
          method: 'PUT',
          url: `/api/app/patients/for-appointment-booking/${patientId}`,
          body: dto,
        },
        { apiName: 'Default' },
      ),
    );
  }
}
