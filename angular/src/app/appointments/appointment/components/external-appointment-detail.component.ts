import { CommonModule } from '@angular/common';
import { Component, Injector, OnDestroy, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
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

interface CalloutCopy {
  icon: string;
  title: string;
  body: string;
}

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
    body: 'Review the request, update the highlighted details, and resubmit. (Fix-it editing is coming soon.)',
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
}
