import {
  Component,
  ElementRef,
  Injector,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  ConfigStateService as AbpConfigStateService,
  ListService,
  LocalizationPipe,
  RestService,
} from '@abp/ng.core';
import { Confirmation, ConfirmationService, DateAdapter, TimeAdapter } from '@abp/ng.theme.shared';
import { NgbDateAdapter, NgbTimeAdapter } from '@ng-bootstrap/ng-bootstrap';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { Subscription } from 'rxjs';
import { debounceTime } from 'rxjs/operators';

import { AppointmentAddComponent } from '../appointment-add.component';
import { MyAttorneyProfileService } from '../../proxy/my-attorney-profiles/my-attorney-profile.service';
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
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ExternalNavbarComponent } from '../../shared/components/external-navbar/external-navbar.component';
import { SubmitQueryModalComponent } from '../../user-queries/submit-query-modal.component';
import { performFullLogout } from '../../shared/auth/full-logout';
import { resolveExternalUserDisplayName } from '../../shared/auth/external-user-display-name';
import { SsnMaskPipe } from '../../shared/pipes/ssn-mask.pipe';
import * as wizardCopy from './wizard-copy.util';
import {
  StepErrorField,
  WIZARD_FIELD_LABELS,
  WIZARD_STEP_CONTROLS,
  collectStepErrors,
} from './step-errors.util';
import { AppointmentDraftService } from '../../proxy/appointment-drafts/appointment-draft.service';
import type { AppointmentDraftDto } from '../../proxy/appointment-drafts/models';

interface WizardStep {
  key: string;
  title: string;
  sub: string;
  icon: string;
  tint: string;
}

// Step order (9 steps, matching the prototype). Insurance and Examiner are
// separate steps, both driven by AppointmentAddClaimPartiesSectionComponent via
// its `only` input. Attorney steps stay for all roles, matching the current
// app's behavior (shouldShow*AttorneySection returns true for everyone).
const STEPS: WizardStep[] = [
  { key: 'schedule', title: 'Schedule', sub: 'Type & slot', icon: 'calendar', tint: 'tint-blue' },
  { key: 'patient', title: 'Patient', sub: 'Demographics', icon: 'user', tint: 'tint-blue' },
  { key: 'applicant', title: 'Applicant', sub: 'Attorney', icon: 'user', tint: 'tint-blue' },
  { key: 'defense', title: 'Defense', sub: 'Attorney', icon: 'user', tint: 'tint-slate' },
  { key: 'insurance', title: 'Insurance', sub: 'Carrier', icon: 'doc', tint: 'tint-teal' },
  { key: 'examiner', title: 'Examiner', sub: 'Adjuster', icon: 'user', tint: 'tint-amber' },
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
    SkeletonComponent,
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
    SsnMaskPipe,
    LocalizationPipe,
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
  private readonly shellRest = inject(RestService);
  private readonly draftService = inject(AppointmentDraftService);
  private readonly confirmation = inject(ConfirmationService);
  // #9 (task_2303a00a): self-scoped current-attorney profile, used to pre-fill the booker's
  // own attorney step when they are themselves an applicant/defense attorney.
  private readonly myAttorneyProfile = inject(MyAttorneyProfileService);

  protected readonly steps = STEPS;
  protected current = 0;
  protected furthest = 0;
  protected submitQueryVisible = false;
  protected readonly erroredSteps = new Set<number>();
  // Resolved appointment-type / location display names for the review step
  // (the form stores only the GUID ids).
  protected readonly typeNames = new Map<string, string>();
  protected readonly locationNames = new Map<string, string>();
  // Same id->name resolution as type/location, for the full review mirror: the
  // form stores GUID ids for state / language / WCAB office, so the review
  // resolves them to display names rather than printing raw ids.
  protected readonly stateNames = new Map<string, string>();
  protected readonly languageNames = new Map<string, string>();
  protected readonly wcabNames = new Map<string, string>();

  // navbar firm-aware display name (same resolution as the external home)
  protected navDisplayName = '';
  protected firmName = '';

  // draft autosave (#15: localStorage is the instant per-keystroke cache; the
  // server draft is the durable cross-session store).
  private readonly DRAFT_KEY = 'ra-wizard-draft';
  private readonly draftSub = new Subscription();
  // #15 draft state -- only active for a fresh 'new' booking (set in ngOnInit).
  protected draftEnabled = false;
  protected submitted = false;
  protected draftState: 'idle' | 'saving' | 'saved' = 'idle';
  protected leavePromptVisible = false;
  private leaveResolver?: (allow: boolean) => void;

  // Per-step control -> label metadata lives in step-errors.util so the label
  // map is coverage-tested against the control list. Continue-gate validation
  // reads WIZARD_STEP_CONTROLS; the engine has already applied the conditional
  // validators (patient email, panel number, AA/DA-when-enabled, required claim
  // examiner + insurance name).

  // Invalid fields on the current step, surfaced as an error summary when the
  // booker's Continue is blocked. Empty while the step is valid or not yet
  // submitted. Also drives focus to the summary so the reason is never silent.
  protected stepErrorSummary: StepErrorField[] = [];
  @ViewChild('stepErrorBanner') private stepErrorBanner?: ElementRef<HTMLElement>;

  ngOnInit(): void {
    this.loadNavName();
    // #15: server-persisted drafts only for a fresh 'new' booking. Reval /
    // re-request prefill from a source appointment, so a saved draft would
    // collide with that prefill (Adrian decision 2026-06-22).
    this.draftEnabled = this.bookingMode === 'new';
    if (this.draftEnabled) {
      this.initDraft();
      this.draftSub.add(
        this.form.valueChanges.pipe(debounceTime(600)).subscribe(() => this.saveDraft()),
      );
    }
    // #9: pre-fill the booker's own attorney step from their profile (fills only a blank
    // section, so the draft/reval prefill above always wins).
    this.prefillBookingAttorney();
    // Cache type + location names so the review step can show them by id.
    this.getAppointmentTypeLookup({ maxResultCount: 200 }).subscribe((r) =>
      (r.items ?? []).forEach((i) => this.typeNames.set(i.id ?? '', i.displayName ?? '')),
    );
    this.getLocationLookup({ maxResultCount: 200 }).subscribe((r) =>
      (r.items ?? []).forEach((i) => this.locationNames.set(i.id ?? '', i.displayName ?? '')),
    );
    this.getStateLookup({ maxResultCount: 1000 }).subscribe((r) =>
      (r.items ?? []).forEach((i) => this.stateNames.set(i.id ?? '', i.displayName ?? '')),
    );
    this.getAppointmentLanguageLookup({ maxResultCount: 200 }).subscribe((r) =>
      (r.items ?? []).forEach((i) => this.languageNames.set(i.id ?? '', i.displayName ?? '')),
    );
    // WCAB offices have no lookup helper on the parent, so fetch the same
    // endpoint the claim-information modal uses to resolve venue names by id.
    this.shellRest
      .request<unknown, { items?: { id?: string; displayName?: string }[] }>(
        {
          method: 'GET',
          url: '/api/app/appointment-injury-details/wcab-office-lookup',
          params: { skipCount: 0, maxResultCount: 200 },
        },
        { apiName: 'Default' },
      )
      .subscribe((r) =>
        (r?.items ?? []).forEach((i) => this.wcabNames.set(i.id ?? '', i.displayName ?? '')),
      );
  }

  ngOnDestroy(): void {
    this.draftSub.unsubscribe();
    // #15: do NOT wipe here -- the server draft is the durable store (survives
    // navigate-away) and is cleared explicitly on submit / discard. The leave
    // guard already runs before destroy for a dirty form.
  }

  protected get currentStep(): WizardStep {
    return this.steps[this.current];
  }
  protected get isLastStep(): boolean {
    return this.current === this.steps.length - 1;
  }
  protected get eyebrow(): string {
    return wizardCopy.wizardEyebrow(this.isInternalBooker, this.isReevaluation);
  }
  protected get wizardTitle(): string {
    return wizardCopy.wizardTitle(this.isInternalBooker, this.isReevaluation);
  }
  protected get wizardSubtitle(): string {
    return wizardCopy.wizardSubtitle(this.isInternalBooker, this.isReevaluation);
  }
  // Review-step submit note; staff get edit-after copy instead of the
  // patient-voiced "contact staff" warning.
  protected get reviewSubmitNote(): string {
    return wizardCopy.reviewSubmitNote(this.isInternalBooker);
  }
  protected get navUserEmail(): string {
    const u = this.shellConfig.getOne('currentUser') as {
      email?: string;
      userName?: string;
    } | null;
    return u?.email || u?.userName || '';
  }

  // ---- review-step display helpers ----
  protected typeName(): string {
    return this.typeNames.get(this.form.get('appointmentTypeId')?.value ?? '') ?? '-';
  }
  protected locationName(): string {
    return this.locationNames.get(this.form.get('locationId')?.value ?? '') ?? '-';
  }
  protected fieldVal(name: string): string {
    const v = this.form.get(name)?.value;
    return v === null || v === undefined || v === '' ? '-' : String(v);
  }
  protected patientFullName(): string {
    const n = [
      this.form.get('firstName')?.value,
      this.form.get('middleName')?.value,
      this.form.get('lastName')?.value,
    ]
      .filter(Boolean)
      .join(' ')
      .trim();
    return n || '-';
  }
  // Resolve a stored GUID id to its display name for the review mirror; '-' when
  // unset or not (yet) in the lookup map.
  protected stateName(id: string | null | undefined): string {
    return (id && this.stateNames.get(id)) || '-';
  }
  protected languageName(id: string | null | undefined): string {
    return (id && this.languageNames.get(id)) || '-';
  }
  protected wcabName(id: string | null | undefined): string {
    return (id && this.wcabNames.get(id)) || '-';
  }
  protected attorneyName(prefix: 'applicantAttorney' | 'defenseAttorney'): string {
    const n = [
      this.form.get(`${prefix}FirstName`)?.value,
      this.form.get(`${prefix}LastName`)?.value,
    ]
      .filter(Boolean)
      .join(' ')
      .trim();
    return n || '-';
  }

  protected stepState(i: number): 'current' | 'done' | 'error' | 'disabled' {
    if (i === this.current) return 'current';
    // F-M05 (2026-06-25): flag the Schedule step if the user advanced past it
    // without loading a reval / re-request source, so the dead-end is visible
    // in the stepper rather than only at a silent Submit.
    if (this.isSourceLoadRequired && this.steps[i]?.key === 'schedule') return 'error';
    if (this.erroredSteps.has(i)) return 'error';
    if (i <= this.furthest) return 'done';
    return 'disabled';
  }
  protected jumpTo(i: number): void {
    if (i <= this.furthest) {
      this.current = i;
      // The summary is per-current-step; clear it so a prior step's errors do
      // not bleed onto the step the booker jumped to.
      this.stepErrorSummary = [];
    }
  }
  protected nextStep(): void {
    if (!this.validateCurrentStep()) {
      this.erroredSteps.add(this.current);
      // Surface WHY Continue is blocked: move focus to the just-populated
      // summary so the reason is announced and scrolled into view.
      this.focusStepErrors();
      return;
    }
    this.erroredSteps.delete(this.current);
    this.stepErrorSummary = [];
    this.current = Math.min(this.current + 1, this.steps.length - 1);
    this.furthest = Math.max(this.furthest, this.current);
    // #15: each Continue is a server checkpoint so the draft survives leaving.
    this.persistServerDraft();
  }

  /**
   * Validate the current step's controls (+ claim/docs gates) before advancing.
   * Populates stepErrorSummary with the invalid fields so the shell can tell the
   * booker exactly what to fix, and marks each touched so its field reddens.
   */
  private validateCurrentStep(): boolean {
    const key = this.currentStep.key;
    const fieldErrors = collectStepErrors(
      this.form,
      WIZARD_STEP_CONTROLS[key] ?? [],
      WIZARD_FIELD_LABELS,
    );
    for (const error of fieldErrors) {
      this.form.get(error.control)?.markAsTouched();
    }
    this.stepErrorSummary = fieldErrors;
    let valid = fieldErrors.length === 0;

    if (key === 'claim' && this.injuryDrafts.length === 0) {
      this.claimInformationMissing = true;
      valid = false;
    }
    if (
      key === 'docs' &&
      this.isPqmeType &&
      this.hasPanelStrikeList &&
      !this.stagedDocuments.some((d) => d.isStrikeList)
    ) {
      this.panelStrikeListMissing = true;
      valid = false;
    }
    return valid;
  }
  protected prevStep(): void {
    this.current = Math.max(0, this.current - 1);
    this.stepErrorSummary = [];
  }

  /**
   * Moves focus to the error summary after a blocked Continue so keyboard and
   * screen-reader users get the reason, and it scrolls into view for everyone.
   * Deferred a tick so the @if-rendered banner exists before focus() runs.
   */
  private focusStepErrors(): void {
    setTimeout(() => this.stepErrorBanner?.nativeElement.focus(), 0);
  }

  // ---- navbar firm-aware display name (mirrors the external home) ----
  private loadNavName(): void {
    const u = this.shellConfig.getOne('currentUser') as {
      name?: string;
      surname?: string;
      userName?: string;
    } | null;
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

  // #9 (task_2303a00a): a booker who is themselves an attorney had to retype their own
  // details on the attorney step. Pre-fill that step (applicant OR defense, per their
  // MyAttorneyProfile.kind) from their saved profile. Only fills a BLANK section, so a
  // resumed draft or a reval/re-request prefill (which load their own values first) always
  // wins; a 404 (booker is not an attorney) is a silent no-op.
  private prefillBookingAttorney(): void {
    // Only attorney bookers have a profile to pull from; gating on role here avoids a spurious
    // 404 (and its console error) for patient / claim-examiner bookers.
    const currentUser = this.shellConfig.getOne('currentUser') as { roles?: string[] } | null;
    const roles = currentUser?.roles ?? [];
    if (!roles.some((r) => r === 'Applicant Attorney' || r === 'Defense Attorney')) {
      return;
    }
    this.myAttorneyProfile.get().subscribe({
      next: (p) => {
        const prefix =
          p?.kind === 'defense'
            ? 'defenseAttorney'
            : p?.kind === 'applicant'
              ? 'applicantAttorney'
              : null;
        if (!prefix) {
          return;
        }
        // Do not clobber an already-populated section.
        const firstName = this.form.get(prefix + 'FirstName');
        if (!firstName || (firstName.value ?? '') !== '') {
          return;
        }
        const values: Record<string, unknown> = {
          Enabled: true,
          FirstName: p.firstName ?? null,
          LastName: p.lastName ?? null,
          Email: p.email ?? null,
          FirmName: p.firmName ?? null,
          WebAddress: p.webAddress ?? null,
          PhoneNumber: p.phoneNumber ?? null,
          FaxNumber: p.faxNumber ?? null,
          Street: p.street ?? null,
          City: p.city ?? null,
          StateId: p.stateId ?? null,
          ZipCode: p.zipCode ?? null,
        };
        Object.entries(values).forEach(([suffix, val]) =>
          this.form.get(prefix + suffix)?.setValue(val),
        );
      },
      error: () => {
        /* booker is not an attorney (404) or lookup failed -- no prefill */
      },
    });
  }

  // ---- draft save / resume (#15) ------------------------------------------
  // localStorage is the instant per-keystroke cache; the server draft is the
  // durable cross-session store written at checkpoints (step Continue + the
  // leave prompt's Save). Resume reads the server draft on open. Patient
  // demographics for a non-patient booker are owned by the async profile load,
  // so they may not round-trip on refresh.

  /** On open: offer to resume a server draft, else restore a same-session local cache. */
  private initDraft(): void {
    this.draftService.getMine().subscribe({
      next: (draft) => {
        if (draft?.payloadJson) {
          this.promptResume(draft);
        } else {
          this.restoreDraft();
        }
      },
      error: () => this.restoreDraft(),
    });
  }

  private promptResume(draft: AppointmentDraftDto): void {
    const label = draft.label ? ` ${draft.label}` : '';
    this.confirmation
      .warn(
        `You have an unfinished${label} request. Resume where you left off, or start fresh?`,
        'Resume saved request?',
        { yesText: 'Resume', cancelText: 'Start fresh' },
      )
      .subscribe((status) => {
        if (status === Confirmation.Status.confirm) {
          this.applyDraftPayload(draft.payloadJson);
          this.draftState = 'saved';
        } else {
          this.discardServerDraft();
        }
      });
  }

  private applyDraftPayload(json?: string): void {
    if (!json) {
      return;
    }
    try {
      const d = JSON.parse(json) as { v?: Record<string, unknown>; step?: number };
      if (d.v) this.form.patchValue(d.v);
      if (typeof d.step === 'number') {
        this.current = d.step;
        this.furthest = Math.max(this.furthest, d.step);
      }
    } catch {
      /* corrupt payload -- ignore */
    }
  }

  /** Checkpoint persist to the server (step Continue + the leave prompt's Save). */
  private persistServerDraft(): void {
    if (!this.draftEnabled) {
      return;
    }
    const payloadJson = JSON.stringify({ v: this.form.getRawValue(), step: this.current });
    // Non-PHI resume label: the appointment-type display name (e.g. "AME").
    const label = this.typeNames.get(this.form.get('appointmentTypeId')?.value ?? '') || null;
    this.draftState = 'saving';
    this.draftService.upsert({ payloadJson, currentStep: this.current, label }).subscribe({
      next: () => (this.draftState = 'saved'),
      error: () => (this.draftState = 'idle'),
    });
  }

  private discardServerDraft(): void {
    this.draftService.discardMine().subscribe({ error: () => undefined });
    localStorage.removeItem(this.DRAFT_KEY);
    this.draftState = 'idle';
  }

  private saveDraft(): void {
    try {
      localStorage.setItem(
        this.DRAFT_KEY,
        JSON.stringify({ v: this.form.getRawValue(), step: this.current }),
      );
    } catch {
      /* serialization / quota -- autosave is best-effort */
    }
  }

  private restoreDraft(): void {
    try {
      const raw = localStorage.getItem(this.DRAFT_KEY);
      if (!raw) return;
      const d = JSON.parse(raw) as { v?: Record<string, unknown>; step?: number };
      if (d.v) this.form.patchValue(d.v);
      if (typeof d.step === 'number') {
        this.current = d.step;
        this.furthest = Math.max(this.furthest, d.step);
      }
    } catch {
      /* corrupt draft -- ignore */
    }
  }

  // ---- leave guard (#15 CanDeactivate) ------------------------------------
  /**
   * Called by appointmentWizardCanDeactivateGuard. Prompts Save / Discard / Stay
   * when the booker abandons a dirty 'new' booking; a clean form, a
   * reval/re-request session, or a successful submit leaves without prompting.
   */
  canDeactivate(): boolean | Promise<boolean> {
    if (!this.draftEnabled || this.submitted || !this.form.dirty) {
      return true;
    }
    this.leavePromptVisible = true;
    return new Promise<boolean>((resolve) => (this.leaveResolver = resolve));
  }

  protected onLeaveSave(): void {
    this.persistServerDraft();
    this.leavePromptVisible = false;
    this.resolveLeave(true);
  }

  protected onLeaveDiscard(): void {
    this.discardServerDraft();
    this.leavePromptVisible = false;
    this.resolveLeave(true);
  }

  protected onLeaveStay(): void {
    this.leavePromptVisible = false;
    this.resolveLeave(false);
  }

  private resolveLeave(allow: boolean): void {
    this.leaveResolver?.(allow);
    this.leaveResolver = undefined;
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

  // Where leaving the wizard lands: staff book inside the shell (-> the
  // appointments list), external users came from the home page (-> home). Shared
  // by the post-booking redirect and the header Back button.
  private get landingUrl(): string {
    return this.isInternalBooker ? '/appointments' : '/';
  }

  protected override navigateAfterBooking(): void {
    // #15: booking succeeded -> the draft is consumed; drop it so it never
    // resurfaces, and flag submitted so the leave guard does not prompt.
    this.submitted = true;
    if (this.draftEnabled) {
      this.discardServerDraft();
    }
    void this.shellRouter.navigateByUrl(this.landingUrl);
  }

  // Header Back button label + action. Distinct from the footer "Back", which
  // steps within the wizard; this leaves the wizard entirely so neither audience
  // is stranded (external users have no sidebar to fall back on).
  protected get backLabel(): string {
    return this.isInternalBooker ? 'Back to appointments' : 'Back to home';
  }
  protected backOut(): void {
    void this.shellRouter.navigateByUrl(this.landingUrl);
  }
}
