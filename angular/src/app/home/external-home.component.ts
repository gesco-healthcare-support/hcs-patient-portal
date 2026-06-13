import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  OnDestroy,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigStateService, ListService, RestService } from '@abp/ng.core';
import { Router } from '@angular/router';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentWithNavigationPropertiesDto } from '../proxy/appointments/models';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { StatusPillComponent } from '../shared/ui/status-pill/status-pill.component';
import { ExternalNavbarComponent } from '../shared/components/external-navbar/external-navbar.component';
import { SubmitQueryModalComponent } from '../user-queries/submit-query-modal.component';
import {
  EXTERNAL_STATUS_SEGMENTS,
  ExternalStatusSegment,
  appointmentStatusToPill,
  appointmentStatusToSegment,
} from '../shared/ui/status-pill/appointment-status.util';
import { AppointmentPillStatus } from '../shared/ui/status-pill/status-pill.component';
import { performFullLogout } from '../shared/auth/full-logout';
import { resolveExternalUserDisplayName } from '../shared/auth/external-user-display-name';
import { avatarColor, avatarInitials } from '../shared/ui/avatar.util';

/** Flattened row the cards/table render (mapped from the nav-properties DTO). */
interface ExtRow {
  id: string;
  type: string;
  location: string;
  patientFirst: string;
  patientLast: string;
  patientName: string;
  patientInitials: string;
  patientColor: string;
  claimNumber: string;
  adjNumber: string;
  confirmation: string;
  appointmentDate?: string;
  pill: AppointmentPillStatus;
  segment: Exclude<ExternalStatusSegment, 'all'>;
}

/** Per-role presentation config (mirrors ext-data.js ROLES). */
interface RoleConfig {
  label: string;
  canBook: boolean;
  canReeval: boolean;
  showPatientCol: boolean;
  showDob: boolean;
  defaultView: 'cards' | 'table';
  listTitle: string;
  listSub: string;
  heroSub: string;
}

const PATIENT_CONFIG: RoleConfig = {
  label: 'Patient',
  canBook: true,
  canReeval: true,
  showPatientCol: false,
  showDob: false,
  defaultView: 'cards',
  listTitle: 'My appointment requests',
  listSub: 'Track the status of your evaluation requests',
  heroSub: 'Book a new evaluation or check the status of your existing requests.',
};

const ROLE_CONFIGS: { match: string; config: RoleConfig }[] = [
  { match: 'patient', config: PATIENT_CONFIG },
  {
    match: 'applicant attorney',
    config: {
      label: 'Applicant Attorney',
      canBook: true,
      canReeval: true,
      showPatientCol: true,
      showDob: true,
      defaultView: 'table',
      listTitle: 'Case appointments',
      listSub: 'Evaluations across the applicants you represent',
      heroSub: 'Book evaluations for your applicants and track every request in one place.',
    },
  },
  {
    match: 'defense attorney',
    config: {
      label: 'Defense Attorney',
      canBook: false,
      canReeval: false,
      showPatientCol: true,
      showDob: true,
      defaultView: 'table',
      listTitle: 'Case appointments',
      listSub: 'Evaluations on the claims you are defending',
      heroSub:
        'Review evaluation requests, respond to changes, and manage documents for your claims.',
    },
  },
  {
    match: 'claim examiner',
    config: {
      label: 'Claim Examiner',
      canBook: false,
      canReeval: false,
      showPatientCol: true,
      showDob: true,
      defaultView: 'table',
      listTitle: 'Claim appointments',
      listSub: 'Evaluations on the claims you administer',
      heroSub:
        'Oversee evaluation requests and respond to change and document requests on your claims.',
    },
  },
];

/**
 * External Role Home (redesign) -- replaces the LeptonX-navbar + ngx-datatable
 * home for the 4 external roles (Patient, Applicant/Defense Attorney, Claim
 * Examiner). Recreated from design_handoff_appointment_portal/External Role
 * Home - Redesign.html. Loads the caller's involvement-filtered appointments
 * once (server applies the S-NEW-2 visibility filter) and does segments/search/
 * filters client-side, matching the prototype.
 */
@Component({
  selector: 'app-external-home',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    IconComponent,
    StatusPillComponent,
    ExternalNavbarComponent,
    SubmitQueryModalComponent,
  ],
  templateUrl: './external-home.component.html',
  styleUrl: './external-home.component.scss',
  providers: [ListService, AppointmentService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExternalHomeComponent implements OnInit, OnDestroy {
  private readonly configState = inject(ConfigStateService);
  private readonly appointmentService = inject(AppointmentService);
  private readonly rest = inject(RestService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  protected readonly list = inject(ListService);

  protected readonly segments = EXTERNAL_STATUS_SEGMENTS;

  protected role: RoleConfig = PATIENT_CONFIG;
  protected clinicName = 'Appointment Portal';
  protected navRoleLabel = '';
  protected navUserEmail = '';
  // Signals: these resolve from an async firm-name lookup (loadFirmName), so
  // OnPush needs them reactive to repaint once the firm name arrives.
  protected readonly heroName = signal('there');
  protected readonly navUserName = signal('');
  protected readonly navOrgName = signal<string | null>(null);

  protected submitQueryVisible = false;

  // client-side list state
  protected readonly rows = signal<ExtRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly q = signal('');
  protected readonly activeSeg = signal<ExternalStatusSegment>('all');
  protected readonly view = signal<'cards' | 'table'>('cards');
  protected readonly showFilters = signal(false);
  protected readonly filters = signal<Record<string, string>>({});
  protected readonly draft = signal<Record<string, string>>({});

  private firmName = '';

  protected readonly counts = computed(() => {
    const rows = this.rows();
    const c: Record<ExternalStatusSegment, number> = {
      all: rows.length,
      pending: 0,
      info: 0,
      approved: 0,
      rescheduled: 0,
      cancelled: 0,
      rejected: 0,
    };
    for (const r of rows) c[r.segment]++;
    return c;
  });

  protected readonly activeFilterKeys = computed(() =>
    Object.keys(this.filters()).filter((k) => this.filters()[k]),
  );

  protected readonly filtered = computed(() => {
    let r = this.rows();
    const seg = this.activeSeg();
    if (seg !== 'all') r = r.filter((x) => x.segment === seg);

    const t = this.q().trim().toLowerCase();
    if (t) {
      r = r.filter((x) =>
        [x.patientName, x.confirmation, x.claimNumber, x.adjNumber, x.type, x.location]
          .join(' ')
          .toLowerCase()
          .includes(t),
      );
    }

    const f = this.filters();
    if (f['type']) r = r.filter((x) => x.type === f['type']);
    if (f['location']) r = r.filter((x) => x.location === f['location']);
    if (f['conf'])
      r = r.filter((x) => x.confirmation.toLowerCase().includes(f['conf'].toLowerCase()));
    if (f['claim'])
      r = r.filter((x) => x.claimNumber.toLowerCase().includes(f['claim'].toLowerCase()));
    if (f['adj']) r = r.filter((x) => x.adjNumber.toLowerCase().includes(f['adj'].toLowerCase()));
    return r;
  });

  ngOnInit(): void {
    // Redesigned full-bleed shell: hides the LeptonX toolbar/avatar and lets
    // the content fill the viewport. Scoped via this body class so the
    // not-yet-ported external pages keep their shell. See styles.scss.
    document.body.classList.add('redesign-shell');
    const user = this.currentUser;
    this.role = this.resolveRole(user?.roles ?? []);
    this.view.set(this.role.defaultView);
    this.navRoleLabel = this.role.label;
    this.clinicName =
      this.currentTenant?.name || this.currentTenant?.tenantName || 'Appointment Portal';
    this.navUserEmail = user?.email || user?.userName || '';

    this.loadFirmName();
    this.refreshDisplayName();

    // Load the caller's appointments (server applies the external involvement
    // filter); a high page size lets us do segments/search/filters client-side.
    this.list.maxResultCount = 500;
    this.list
      .hookToQuery((query) => this.appointmentService.getList({ ...query, maxResultCount: 500 }))
      .subscribe((res) => {
        this.rows.set((res.items ?? []).map((r) => this.toRow(r)));
        this.loading.set(false);
      });
  }

  ngOnDestroy(): void {
    document.body.classList.remove('redesign-shell');
  }

  private toRow(r: AppointmentWithNavigationPropertiesDto): ExtRow {
    const a = r.appointment;
    const status = a?.appointmentStatus ?? AppointmentStatusType.Pending;
    const injury = r.appointmentInjuryDetails?.[0]?.appointmentInjuryDetail;
    const first = r.patient?.firstName ?? '';
    const last = r.patient?.lastName ?? '';
    return {
      id: a?.id ?? '',
      type: r.appointmentType?.name ?? '—',
      location: r.location?.name ?? '—',
      patientFirst: first,
      patientLast: last,
      patientName: `${first} ${last}`.trim() || '—',
      patientInitials: avatarInitials(first, last),
      patientColor: avatarColor(`${first}${last}` || 'patient'),
      claimNumber: injury?.claimNumber ?? '—',
      adjNumber: injury?.wcabAdj ?? '—',
      confirmation: a?.requestConfirmationNumber ?? '—',
      appointmentDate: a?.appointmentDate,
      pill: appointmentStatusToPill(status),
      segment: appointmentStatusToSegment(status),
    };
  }

  private resolveRole(roles: string[]): RoleConfig {
    const set = new Set(roles.map((r) => (r ?? '').toLowerCase()));
    return ROLE_CONFIGS.find((rc) => set.has(rc.match))?.config ?? PATIENT_CONFIG;
  }

  private refreshDisplayName(): void {
    const u = this.currentUser;
    const display = resolveExternalUserDisplayName(u?.name, u?.surname, this.firmName, u?.userName);
    this.navUserName.set(display);
    this.navOrgName.set(this.firmName || null);
    // Hero greeting reuses the same firm-fallback display name (locked
    // precedence: First Last -> FirmName -> email); 'there' only if all blank.
    this.heroName.set(display || 'there');
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

  // ---- filter panel ----
  protected openFilters(): void {
    this.draft.set({ ...this.filters() });
    this.showFilters.update((v) => !v);
  }
  protected applyFilters(): void {
    this.filters.set({ ...this.draft() });
    this.showFilters.set(false);
  }
  protected resetFilters(): void {
    this.draft.set({});
    this.filters.set({});
  }
  protected clearChip(key: string): void {
    const next = { ...this.filters() };
    delete next[key];
    this.filters.set(next);
    this.draft.set(next);
  }
  protected setDraft(key: string, value: string): void {
    this.draft.update((d) => ({ ...d, [key]: value }));
  }
  protected chipLabel(key: string): string {
    return (
      { type: 'Type', conf: 'Conf #', location: 'Location', claim: 'Claim #', adj: 'ADJ #' }[key] ??
      key
    );
  }
  protected get typeOptions(): string[] {
    return [...new Set(this.rows().map((r) => r.type))].filter((t) => t && t !== '—').sort();
  }
  protected get locationOptions(): string[] {
    return [...new Set(this.rows().map((r) => r.location))].filter((l) => l && l !== '—').sort();
  }

  // ---- navigation / actions (all hit real routes) ----
  protected requestAppointment(): void {
    this.router.navigateByUrl('/appointments/add?type=1');
  }
  protected requestReEvaluation(): void {
    this.router.navigateByUrl('/appointments/add?type=2');
  }
  protected viewAppointment(id: string): void {
    this.router.navigate(['/appointments/view', id]);
  }
  protected viewDocuments(id: string): void {
    // The appointment detail page hosts the Document Manager (OLD parity); the
    // standalone My Documents page is a later redesign slice.
    this.router.navigate(['/appointments/view', id]);
  }
  protected openProfile(): void {
    this.router.navigateByUrl('/user-management/patients/my-profile');
  }
  protected openMyDocuments(): void {
    // TODO(redesign slice 7): dedicated My Documents page. Route to profile for now.
    this.router.navigateByUrl('/user-management/patients/my-profile');
  }
  protected openQuery(): void {
    this.submitQueryVisible = true;
  }
  protected async signOut(): Promise<void> {
    await performFullLogout(this.injector);
  }

  private get currentUser(): {
    email?: string;
    userName?: string;
    name?: string;
    surname?: string;
    roles?: string[];
  } | null {
    return (this.configState.getOne('currentUser') as never) ?? null;
  }

  private get currentTenant(): { name?: string; tenantName?: string } | null {
    return (this.configState.getOne('currentTenant') as never) ?? null;
  }
}
