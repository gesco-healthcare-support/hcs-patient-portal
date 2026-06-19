import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, Injector, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfigStateService as AbpConfigStateService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import {
  allFixed,
  buildCorrectionsPayload,
  fixItProgress,
  isInlineEditable,
} from './external-fix-it.util';
import { AppointmentViewComponent } from './appointment-view.component';
import { RescheduleRequestModalComponent } from './reschedule-request-modal.component';
import { CancellationRequestModalComponent } from './cancellation-request-modal.component';
import { AppointmentDocumentsComponent } from '../../../appointment-documents/appointment-documents.component';
import { AppointmentPacketComponent } from '../../../appointment-packet/appointment-packet.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import { ExternalNavbarComponent } from '../../../shared/components/external-navbar/external-navbar.component';
import { SubmitQueryModalComponent } from '../../../user-queries/submit-query-modal.component';
import { performFullLogout } from '../../../shared/auth/full-logout';
import { resolveExternalUserDisplayName } from '../../../shared/auth/external-user-display-name';
import { appointmentStatusToPill } from '../../../shared/ui/status-pill/appointment-status.util';
import type { AppointmentPillStatus } from '../../../shared/ui/status-pill/status-pill.component';
import type { PatientDto } from '../../../proxy/patients/models';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';
import type { AppointmentInfoRequestRoundDto } from '../../../proxy/appointment-info-requests/models';
import { InfoRequestHistoryComponent } from './info-request-history.component';
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
    SkeletonComponent,
    ExternalNavbarComponent,
    SubmitQueryModalComponent,
    InfoRequestHistoryComponent,
  ],
  templateUrl: './external-appointment-detail.component.html',
  styleUrl: './external-appointment-detail.component.scss',
})
export class ExternalAppointmentDetailComponent extends AppointmentViewComponent implements OnInit {
  private readonly shellRouter = inject(Router);
  private readonly shellInjector = inject(Injector);
  private readonly shellConfig = inject(AbpConfigStateService);
  private readonly shellRest = inject(RestService);
  private readonly detailRoute = inject(ActivatedRoute);
  private readonly fixItToaster = inject(ToasterService);
  private readonly infoRequestApi = inject(AppointmentInfoRequestService);

  // Send Back fix-it: the open info request (staff note + flagged fields).
  protected infoRequest: OpenInfoRequest | null = null;
  // Send Back / request-info rounds for the read-only History section (newest-first).
  protected historyRounds: AppointmentInfoRequestRoundDto[] = [];
  protected resubmitConfirmVisible = false;
  protected isResubmitting = false;
  // Local edit model for every inline-editable flagged field (patient demographics,
  // attorney/examiner email, insurance, defense firm). Kept separate from the
  // inherited reactive form, which super.ngOnInit() disables for external read-only
  // viewing. The writes go through the locked corrections endpoint; documents are
  // replaced via the existing documents section below.
  protected edits: Record<string, string> = {};
  protected readonly touched = new Set<string>();
  protected languageOptions: { id: string; name: string }[] = [];

  protected navClinicName = 'Appointment Portal';
  protected navDisplayName = '';
  protected firmName = '';
  protected navRoleLabelText = '';
  protected navUserEmailText = '';
  protected submitQueryVisible = false;

  override ngOnInit(): void {
    super.ngOnInit();
    this.loadNavName();
    this.loadInfoRequest();
    this.loadHistory();
    this.loadLanguageOptions();
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
    // #9: attorneys get their own self-edit profile; everyone else the patient one.
    const roles =
      (this.shellConfig.getOne('currentUser') as { roles?: string[] } | null)?.roles ?? [];
    const isAttorney = roles.includes('Applicant Attorney') || roles.includes('Defense Attorney');
    void this.shellRouter.navigateByUrl(
      isAttorney ? '/user-management/attorneys/my-profile' : '/user-management/patients/my-profile',
    );
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

  /**
   * Redesign swap (2026-06-15): external re-request launches the booking WIZARD
   * (/appointments/request) instead of the legacy add form. The internal
   * AppointmentViewComponent.reRequest() keeps targeting /appointments/add so
   * staff stay on the in-shell legacy form. Same mode=rerequest + source conf#
   * contract; the wizard inherits the re-request load + reSubmit path. Uses
   * shellRouter because the parent's `router` is private.
   */
  override reRequest(): void {
    const conf = this.appointment?.appointment?.requestConfirmationNumber;
    if (!conf) {
      return;
    }
    void this.shellRouter.navigate(['/appointments/request'], {
      queryParams: { mode: 'rerequest', source: conf },
    });
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

  /**
   * Load the Send Back / request-info rounds for the read-only History section.
   * Uses the typed proxy (the same endpoint the internal detail uses; it
   * authorizes external parties via the appointment read guard). A failure leaves
   * the History section hidden -- it is supplementary to the appointment detail.
   */
  private loadHistory(): void {
    const id = this.detailRoute.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }
    this.infoRequestApi.getHistory(id).subscribe({
      next: (rounds) => (this.historyRounds = rounds ?? []),
      error: () => {
        /* no history available -> History section stays hidden */
      },
    });
  }

  /**
   * Seed the inline edit model from the loaded appointment so each input starts
   * with the current value (SSN starts blank -- the read DTO masks it, so it must
   * never be echoed back). Pre-fill is best-effort; an empty start is harmless
   * because the requester types the correction regardless.
   */
  private seedEdits(): void {
    const p = this.patientNav;
    const appt = this.appointment?.appointment as Record<string, unknown> | undefined;
    const insurance = (this.appointment as { primaryInsurance?: { name?: string } } | null)
      ?.primaryInsurance;
    const defense = (
      this.appointment as {
        appointmentDefenseAttorney?: { defenseAttorney?: { firmName?: string } };
      } | null
    )?.appointmentDefenseAttorney?.defenseAttorney;

    const seed: Record<string, string> = {
      dateOfBirth: (p?.dateOfBirth ?? '').slice(0, 10),
      socialSecurityNumber: '',
      address: String(p?.address ?? ''),
      cellPhoneNumber: String(p?.cellPhoneNumber ?? ''),
      appointmentLanguageId: String(p?.appointmentLanguageId ?? ''),
      applicantAttorneyEmail: String(appt?.['applicantAttorneyEmail'] ?? ''),
      appointmentClaimExaminerEmail: String(appt?.['claimExaminerEmail'] ?? ''),
      appointmentInsuranceName: String(insurance?.name ?? ''),
      defenseAttorneyFirmName: String(defense?.firmName ?? ''),
    };

    for (const key of this.editableFlaggedKeys) {
      this.edits[key] = seed[key] ?? '';
    }
  }

  private loadLanguageOptions(): void {
    this.getAppointmentLanguageLookup({ filter: '', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (res) => {
        this.languageOptions = (res.items ?? []).map((i) => ({
          id: i.id ?? '',
          name: i.displayName ?? '',
        }));
      },
      error: () => {
        /* lookup optional; language stays a plain text field */
      },
    });
  }

  protected get hasFlaggedFields(): boolean {
    return (this.infoRequest?.flaggedFields?.length ?? 0) > 0;
  }

  /** All flagged keys, in the order staff selected them. */
  protected get flaggedKeys(): string[] {
    return (this.infoRequest?.flaggedFields ?? []).map((f) => f.key);
  }

  /** Flagged keys the requester edits inline (everything except documents). */
  protected get editableFlaggedKeys(): string[] {
    return this.flaggedKeys.filter((k) => isInlineEditable(k));
  }

  protected get documentFlagged(): boolean {
    return this.flaggedKeys.includes('documents');
  }

  protected isInlineEditable(key: string): boolean {
    return isInlineEditable(key);
  }
  protected isLanguage(key: string): boolean {
    return key === 'appointmentLanguageId';
  }
  protected isFixed(key: string): boolean {
    return this.touched.has(key);
  }
  protected onEdit(key: string, value: string): void {
    this.edits[key] = value;
    this.touched.add(key);
  }

  /** Acknowledge the document replacement (the upload happens in the Documents section). */
  protected ackDocumentReplaced(): void {
    this.touched.add('documents');
    this.fixItToaster.success('Thanks -- upload your replacement in the Documents section below.');
  }

  protected get fixedCount(): number {
    return fixItProgress(this.flaggedKeys, this.touched).fixed;
  }
  protected get totalFlagged(): number {
    return this.flaggedKeys.length;
  }
  protected get progressPct(): number {
    return this.totalFlagged === 0 ? 0 : Math.round((this.fixedCount / this.totalFlagged) * 100);
  }
  protected get canResubmit(): boolean {
    return allFixed(this.flaggedKeys, this.touched);
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

  /** Save the corrections without resubmitting -- the requester can finish later. */
  protected async saveLater(): Promise<void> {
    if (this.isResubmitting) {
      return;
    }
    if (await this.saveCorrections()) {
      this.fixItToaster.success('Saved -- finish anytime from your home page.');
    }
  }

  /**
   * Resubmit: persist the corrections through the locked corrections endpoint, then
   * transition InfoRequested -> Pending. Both calls run server-side under the
   * edit-access guard; the corrections endpoint re-locks to the flagged set.
   */
  protected async confirmResubmit(): Promise<void> {
    const id = this.detailRoute.snapshot.paramMap.get('id');
    if (!id || this.isResubmitting) {
      return;
    }
    this.isResubmitting = true;
    try {
      if (!(await this.saveCorrections())) {
        this.isResubmitting = false;
        return;
      }
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

  /** POST the flagged-field corrections; returns false on failure (ABP shows the error). */
  private async saveCorrections(): Promise<boolean> {
    const id = this.detailRoute.snapshot.paramMap.get('id');
    if (!id) {
      return false;
    }
    const payload = buildCorrectionsPayload(this.flaggedKeys, this.edits);
    if (Object.keys(payload).length === 0) {
      return true; // nothing to persist (e.g. a document-only correction)
    }
    try {
      await firstValueFrom(
        this.shellRest.request<typeof payload, void>(
          {
            method: 'POST',
            url: `/api/app/appointment-info-requests/corrections/${id}`,
            body: payload,
          },
          { apiName: 'Default' },
        ),
      );
      return true;
    } catch {
      return false;
    }
  }
}
