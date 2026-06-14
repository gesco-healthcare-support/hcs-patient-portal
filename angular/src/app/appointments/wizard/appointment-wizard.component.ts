import { Component, Injector, OnDestroy, OnInit, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ConfigStateService as AbpConfigStateService, ListService } from '@abp/ng.core';
import { DateAdapter, TimeAdapter } from '@abp/ng.theme.shared';
import { NgbDateAdapter, NgbTimeAdapter } from '@ng-bootstrap/ng-bootstrap';
import { NgxValidateCoreModule } from '@ngx-validate/core';

import { AppointmentAddComponent } from '../appointment-add.component';
import { AppointmentViewService } from '../appointment/services/appointment.service';
import { AppointmentAddScheduleComponent } from '../sections/appointment-add-schedule.component';
import { AppointmentAddPatientDemographicsComponent } from '../sections/appointment-add-patient-demographics.component';
import { AppointmentAddEmployerDetailsComponent } from '../sections/appointment-add-employer-details.component';
import { AppointmentAddAttorneySectionComponent } from '../sections/appointment-add-attorney-section.component';
import { AppointmentAddClaimPartiesSectionComponent } from '../sections/appointment-add-claim-parties-section.component';
import { AppointmentAddClaimInformationComponent } from '../sections/appointment-add-claim-information.component';
import { AppointmentAddDocumentsComponent } from '../sections/appointment-add-documents.component';
import { AppointmentAddAuthorizedUsersComponent } from '../sections/appointment-add-authorized-users.component';
import { AppointmentAddCustomFieldsComponent } from '../sections/appointment-add-custom-fields.component';
import { ConfirmAddressDialogComponent } from '../../shared/address/confirm-address-dialog.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { ExternalNavbarComponent } from '../../shared/components/external-navbar/external-navbar.component';
import { SubmitQueryModalComponent } from '../../user-queries/submit-query-modal.component';
import { performFullLogout } from '../../shared/auth/full-logout';

interface WizardStep {
  key: string;
  title: string;
  sub: string;
  icon: string;
  tint: string;
}

// Step order. Insurance + Claim Examiner share one step here because the
// existing AppointmentAddClaimPartiesSectionComponent renders both together
// (the prototype splits them; that finer split comes with the per-field
// .ra-* restyle). Attorney steps stay for all roles, matching the current
// app's behavior (shouldShow*AttorneySection returns true for everyone).
const STEPS: WizardStep[] = [
  { key: 'schedule', title: 'Schedule', sub: 'Type & slot', icon: 'calendar', tint: 'tint-blue' },
  { key: 'patient', title: 'Patient', sub: 'Demographics', icon: 'user', tint: 'tint-blue' },
  { key: 'applicant', title: 'Applicant', sub: 'Attorney', icon: 'user', tint: 'tint-blue' },
  { key: 'defense', title: 'Defense', sub: 'Attorney', icon: 'user', tint: 'tint-slate' },
  { key: 'parties', title: 'Insurance', sub: 'Carrier & examiner', icon: 'doc', tint: 'tint-teal' },
  { key: 'claim', title: 'Claim', sub: 'Injuries', icon: 'doc', tint: 'tint-purple' },
  { key: 'docs', title: 'Docs', sub: 'Uploads', icon: 'doc', tint: 'tint-blue' },
  { key: 'review', title: 'Review', sub: 'Confirm', icon: 'check', tint: 'tint-green' },
];

/**
 * Request-an-Appointment / Re-evaluation wizard (redesign). EXTENDS
 * AppointmentAddComponent so it inherits the entire battle-tested booking
 * engine -- the 55-field reactive form, every cascade subscription, the
 * multi-POST submit, reval/re-request modes, address standardization -- with
 * zero logic duplication. This subclass only adds the redesigned .ra-* shell:
 * the header band, the stepper, and the sticky footer nav, reusing the existing
 * section components inside each step (their per-field .ra-* restyle is a
 * follow-up). Mounted at /appointments/request while /appointments/add keeps
 * the legacy template.
 */
@Component({
  selector: 'app-appointment-wizard',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    NgxValidateCoreModule,
    IconComponent,
    ExternalNavbarComponent,
    SubmitQueryModalComponent,
    AppointmentAddScheduleComponent,
    AppointmentAddPatientDemographicsComponent,
    AppointmentAddEmployerDetailsComponent,
    AppointmentAddAttorneySectionComponent,
    AppointmentAddClaimPartiesSectionComponent,
    AppointmentAddClaimInformationComponent,
    AppointmentAddDocumentsComponent,
    AppointmentAddAuthorizedUsersComponent,
    AppointmentAddCustomFieldsComponent,
    ConfirmAddressDialogComponent,
  ],
  providers: [
    ListService,
    AppointmentViewService,
    { provide: NgbDateAdapter, useClass: DateAdapter },
    { provide: NgbTimeAdapter, useClass: TimeAdapter },
  ],
  templateUrl: './appointment-wizard.component.html',
  styleUrl: './appointment-wizard.component.scss',
})
export class AppointmentWizardComponent
  extends AppointmentAddComponent
  implements OnInit, OnDestroy
{
  private readonly shellRouter = inject(Router);
  private readonly shellInjector = inject(Injector);
  private readonly shellConfig = inject(AbpConfigStateService);

  protected readonly steps = STEPS;
  protected current = 0;
  protected furthest = 0;
  protected submitQueryVisible = false;

  ngOnInit(): void {
    document.body.classList.add('redesign-shell');
  }

  ngOnDestroy(): void {
    document.body.classList.remove('redesign-shell');
  }

  protected get currentStep(): WizardStep {
    return this.steps[this.current];
  }
  protected get isLastStep(): boolean {
    return this.current === this.steps.length - 1;
  }
  protected get eyebrow(): string {
    return this.isReevaluation ? 'Follow-up evaluation' : 'New evaluation';
  }
  protected get wizardTitle(): string {
    return this.isReevaluation ? 'Request a Re-evaluation' : 'Request an Appointment';
  }
  protected get wizardSubtitle(): string {
    return this.isReevaluation
      ? 'Look up the prior appointment, then confirm the details for the follow-up.'
      : 'Complete the steps below. Your progress is saved automatically as a draft.';
  }
  protected get navUserEmail(): string {
    const u = this.shellConfig.getOne('currentUser') as {
      email?: string;
      userName?: string;
    } | null;
    return u?.email || u?.userName || '';
  }

  protected stepState(i: number): 'current' | 'done' | 'disabled' {
    if (i === this.current) return 'current';
    if (i <= this.furthest) return 'done';
    return 'disabled';
  }
  protected jumpTo(i: number): void {
    if (i <= this.furthest) this.current = i;
  }
  protected nextStep(): void {
    this.current = Math.min(this.current + 1, this.steps.length - 1);
    this.furthest = Math.max(this.furthest, this.current);
  }
  protected prevStep(): void {
    this.current = Math.max(0, this.current - 1);
  }

  protected openQuery(): void {
    this.submitQueryVisible = true;
  }
  protected openDocuments(): void {
    void this.shellRouter.navigateByUrl('/');
  }
  protected signOut(): void {
    void performFullLogout(this.shellInjector);
  }
}
