import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { ExternalNavbarComponent } from '../../shared/components/external-navbar/external-navbar.component';
import { SubmitQueryModalComponent } from '../../user-queries/submit-query-modal.component';
import { performFullLogout } from '../../shared/auth/full-logout';
import { resolveExternalUserDisplayName } from '../../shared/auth/external-user-display-name';

type WizardMode = 'appointment' | 'reval';
type StepState = 'current' | 'done' | 'disabled';

interface WizardStep {
  key: string;
  title: string;
  sub: string;
  icon: string;
  tint: string;
}

/** Per-step presentation (mirrors the prototype's ra-steps card heads). */
const STEP_DEFS: Record<string, WizardStep> = {
  schedule: {
    key: 'schedule',
    title: 'Schedule',
    sub: 'Type & slot',
    icon: 'calendar',
    tint: 'tint-blue',
  },
  patient: {
    key: 'patient',
    title: 'Patient',
    sub: 'Demographics',
    icon: 'user',
    tint: 'tint-blue',
  },
  applicant: {
    key: 'applicant',
    title: 'Applicant',
    sub: 'Attorney',
    icon: 'user',
    tint: 'tint-blue',
  },
  defense: { key: 'defense', title: 'Defense', sub: 'Attorney', icon: 'user', tint: 'tint-slate' },
  insurance: {
    key: 'insurance',
    title: 'Insurance',
    sub: 'Carrier',
    icon: 'doc',
    tint: 'tint-teal',
  },
  examiner: {
    key: 'examiner',
    title: 'Examiner',
    sub: 'Adjuster',
    icon: 'user',
    tint: 'tint-amber',
  },
  claim: { key: 'claim', title: 'Claim', sub: 'Injuries', icon: 'doc', tint: 'tint-purple' },
  docs: { key: 'docs', title: 'Docs', sub: 'Uploads', icon: 'doc', tint: 'tint-blue' },
  review: { key: 'review', title: 'Review', sub: 'Confirm', icon: 'check', tint: 'tint-green' },
};

const STANDARD_ORDER = [
  'schedule',
  'patient',
  'applicant',
  'defense',
  'insurance',
  'examiner',
  'claim',
  'docs',
  'review',
];
// Claim Examiner booker: attorney steps are hidden (prototype hideAttorneys).
const EXAMINER_ORDER = ['schedule', 'patient', 'insurance', 'examiner', 'claim', 'docs', 'review'];

/**
 * Request-an-Appointment / Re-evaluation wizard SHELL (redesign). Recreated
 * from design_handoff_appointment_portal `Request an Appointment - Redesign.html`.
 * This is the scaffold: the .ra-head band, the role-aware stepper, the sticky
 * footer nav, and the navbar shell. Step content + the mirrored FormGroup,
 * cascades, and multi-POST submit are wired in subsequent increments. Mounted
 * at /appointments/request while /appointments/add keeps the legacy form.
 */
@Component({
  selector: 'app-appointment-wizard',
  standalone: true,
  imports: [IconComponent, ExternalNavbarComponent, SubmitQueryModalComponent],
  templateUrl: './appointment-wizard.component.html',
  styleUrl: './appointment-wizard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentWizardComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly configState = inject(ConfigStateService);
  private readonly rest = inject(RestService);
  private readonly injector = inject(Injector);

  protected mode: WizardMode = 'appointment';
  protected readonly steps = signal<WizardStep[]>(STANDARD_ORDER.map((k) => STEP_DEFS[k]));
  protected readonly current = signal(0);
  protected readonly furthest = signal(0);
  protected readonly currentStep = computed(() => this.steps()[this.current()]);

  // navbar slots (same resolution as the external home shell)
  protected clinicName = 'Appointment Portal';
  protected navRoleLabel = '';
  protected navUserEmail = '';
  protected readonly navUserName = signal('');
  protected readonly navOrgName = signal<string | null>(null);
  protected submitQueryVisible = false;

  private firmName = '';
  private user: {
    name?: string;
    surname?: string;
    email?: string;
    userName?: string;
    roles?: string[];
  } | null = null;

  protected get eyebrow(): string {
    return this.mode === 'reval' ? 'Follow-up evaluation' : 'New evaluation';
  }
  protected get title(): string {
    return this.mode === 'reval' ? 'Request a Re-evaluation' : 'Request an Appointment';
  }
  protected get subtitle(): string {
    return this.mode === 'reval'
      ? 'Look up the prior appointment, then confirm the details for the follow-up.'
      : 'Complete the steps below. Your progress is saved automatically as a draft.';
  }
  protected get isLastStep(): boolean {
    return this.current() === this.steps().length - 1;
  }

  ngOnInit(): void {
    document.body.classList.add('redesign-shell');

    this.mode = this.route.snapshot.queryParamMap.get('type') === '2' ? 'reval' : 'appointment';

    this.user = this.configState.getOne('currentUser') as typeof this.user;
    const roles = (this.user?.roles ?? []).map((r) => r.toLowerCase());
    const onlyExaminer =
      roles.includes('claim examiner') &&
      !roles.includes('patient') &&
      !roles.includes('applicant attorney') &&
      !roles.includes('defense attorney');
    this.steps.set((onlyExaminer ? EXAMINER_ORDER : STANDARD_ORDER).map((k) => STEP_DEFS[k]));

    const tenant = this.configState.getOne('currentTenant') as { name?: string } | null;
    this.clinicName = tenant?.name || 'Appointment Portal';
    this.navUserEmail = this.user?.email || this.user?.userName || '';
    this.navRoleLabel = this.primaryRoleLabel(roles);
    this.refreshDisplayName();
    this.loadFirmName();
  }

  ngOnDestroy(): void {
    document.body.classList.remove('redesign-shell');
  }

  // ---- stepper ----
  protected stepState(i: number): StepState {
    if (i === this.current()) return 'current';
    if (i <= this.furthest()) return 'done';
    return 'disabled';
  }
  protected jumpTo(i: number): void {
    if (i <= this.furthest()) this.current.set(i);
  }
  protected next(): void {
    const n = Math.min(this.current() + 1, this.steps().length - 1);
    this.current.set(n);
    this.furthest.update((f) => Math.max(f, n));
  }
  protected back(): void {
    this.current.update((c) => Math.max(0, c - 1));
  }

  // ---- navbar handlers (mirror the external home) ----
  protected openProfile(): void {
    this.router.navigateByUrl('/user-management/patients/my-profile');
  }
  protected openMyDocuments(): void {
    this.router.navigateByUrl('/');
  }
  protected openQuery(): void {
    this.submitQueryVisible = true;
  }
  protected signOut(): void {
    void performFullLogout(this.injector);
  }
  protected submit(): void {
    // Multi-POST submit orchestration is wired in a later increment.
  }

  private primaryRoleLabel(roles: string[]): string {
    if (roles.includes('patient')) return 'Patient';
    if (roles.includes('applicant attorney')) return 'Applicant Attorney';
    if (roles.includes('defense attorney')) return 'Defense Attorney';
    if (roles.includes('claim examiner')) return 'Claim Examiner';
    return '';
  }

  private refreshDisplayName(): void {
    const display = resolveExternalUserDisplayName(
      this.user?.name,
      this.user?.surname,
      this.firmName,
      this.user?.userName,
    );
    this.navUserName.set(display);
    this.navOrgName.set(this.firmName || null);
  }

  private loadFirmName(): void {
    this.rest
      .request<
        unknown,
        { firmName?: string }
      >({ method: 'GET', url: '/api/app/external-users/me' }, { apiName: 'Default' })
      .subscribe({
        next: (profile) => {
          this.firmName = profile?.firmName ?? '';
          this.refreshDisplayName();
        },
        error: () => {
          this.firmName = '';
        },
      });
  }
}
