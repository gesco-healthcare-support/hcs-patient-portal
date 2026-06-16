import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { genderOptions } from '../proxy/enums/gender.enum';
import { LookupOption, PeopleSectionGateway } from './people-section.gateway';
import { PatientDetailComponent } from './patient-detail.component';
import { PeopleEditModalComponent } from './people-edit-modal.component';
import {
  avatarColor,
  derivePortalStatus,
  emptyPeopleFilters,
  initials,
  matchesPeopleFilters,
  matchesPeopleSearch,
  PEOPLE_SECTIONS,
  PeopleFilters,
  PeopleSection,
  PeopleSectionKey,
  PersonFormState,
  PersonRow,
  PORTAL_LABEL,
} from './people.util';

/** Patient-only optional columns toggled by the column chooser. */
const OPTIONAL_COLUMNS = [
  { key: 'gender', label: 'Gender' },
  { key: 'street', label: 'Street' },
  { key: 'zip', label: 'Zip' },
  { key: 'interpreter', label: 'Interpreter' },
  { key: 'apptNumber', label: 'Appt #' },
];

/**
 * People hub (Prompt 15). One standalone component mounted at all four people
 * routes; reads data.section to pick the section, so the rail is permission-gated
 * routerLinks. Patients get a full detail view, column chooser, and filter panel;
 * all sections get search + CRUD modal + Invite-to-portal. Portal status is
 * linked (IdentityUser attached), invited (active invitation -- resolved via the
 * permission-gated B3 endpoint), or none.
 */
@Component({
  selector: 'app-internal-people',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    IconComponent,
    PatientDetailComponent,
    PeopleEditModalComponent,
  ],
  templateUrl: './internal-people.component.html',
})
export class InternalPeopleComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly gateway = inject(PeopleSectionGateway);
  private readonly permission = inject(PermissionService);
  private readonly toaster = inject(ToasterService);

  protected readonly sections = PEOPLE_SECTIONS;
  protected readonly optionalColumns = OPTIONAL_COLUMNS;
  protected readonly portalLabel = PORTAL_LABEL;
  protected readonly genders = genderOptions;
  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;
  protected readonly canInvite = this.permission.getGrantedPolicy(
    'CaseEvaluation.UserManagement.InviteExternalUser',
  );

  protected readonly section = signal<PeopleSectionKey>('patients');
  protected readonly meta = computed<PeopleSection>(
    () => this.sections.find((s) => s.key === this.section()) ?? this.sections[0],
  );
  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  protected readonly rows = signal<PersonRow[]>([]);
  protected readonly search = signal('');
  protected readonly filters = signal<PeopleFilters>(emptyPeopleFilters());
  protected readonly showFilters = signal(false);
  protected readonly cols = signal<Record<string, boolean>>({});
  protected readonly showCols = signal(false);
  protected readonly detailRow = signal<PersonRow | null>(null);
  protected readonly form = signal<PersonFormState | null>(null);
  protected readonly states = signal<LookupOption[]>([]);
  protected readonly languages = signal<LookupOption[]>([]);

  protected readonly displayRows = computed(() => {
    const q = this.search();
    const f = this.filters();
    return this.rows().filter((r) => matchesPeopleSearch(r, q) && matchesPeopleFilters(r, f));
  });
  protected readonly stateOptions = computed<LookupOption[]>(() =>
    this.distinct(
      (r) => r.stateId,
      (r) => r.stateName,
    ),
  );
  protected readonly languageOptions = computed<LookupOption[]>(() =>
    this.distinct(
      (r) => r.appointmentLanguageId,
      (r) => r.languageName,
    ),
  );

  constructor() {
    this.route.data.pipe(takeUntilDestroyed()).subscribe((data) => {
      this.section.set((data['section'] as PeopleSectionKey) ?? 'patients');
      this.resetView();
      this.load();
    });
    // Lookups for the edit modal's State + Language selects (loaded once).
    this.gateway
      .stateLookup()
      .subscribe({ next: (s) => this.states.set(s), error: () => undefined });
    this.gateway
      .languageLookup()
      .subscribe({ next: (l) => this.languages.set(l), error: () => undefined });
  }

  // ---- rail ----
  protected canSee(s: PeopleSection): boolean {
    return this.permission.getGrantedPolicy(s.policy);
  }

  // ---- per-action gating: hide actions the user cannot perform (e.g. Intake
  // sees Patients but no Delete), so the UI never offers a button that 403s. ----
  protected readonly canCreate = computed(() =>
    this.permission.getGrantedPolicy(`${this.meta().policy}.Create`),
  );
  protected readonly canEdit = computed(() =>
    this.permission.getGrantedPolicy(`${this.meta().policy}.Edit`),
  );
  protected readonly canDelete = computed(() =>
    this.permission.getGrantedPolicy(`${this.meta().policy}.Delete`),
  );

  /** Row click: open the patient detail (view), else edit a party (when permitted). */
  protected onRowClick(row: PersonRow): void {
    if (this.meta().isPatient) {
      this.openDetail(row);
    } else if (this.canEdit()) {
      this.openEdit(row);
    }
  }

  protected genderLabel(id: number | null | undefined): string {
    return this.genders.find((g) => g.value === id)?.key ?? '--';
  }

  // ---- toolbar ----
  protected patchFilter(partial: Partial<PeopleFilters>): void {
    this.filters.set({ ...this.filters(), ...partial });
  }
  protected clearFilters(): void {
    this.filters.set(emptyPeopleFilters());
    this.showFilters.set(false);
  }
  protected toggleCol(key: string): void {
    this.cols.set({ ...this.cols(), [key]: !this.cols()[key] });
  }
  protected colOn(key: string): boolean {
    return !!this.cols()[key];
  }

  // ---- detail ----
  protected openDetail(row: PersonRow): void {
    if (this.meta().isPatient) {
      this.detailRow.set(row);
    }
  }
  protected closeDetail(): void {
    this.detailRow.set(null);
  }

  // ---- CRUD ----
  protected openNew(): void {
    this.form.set(this.blankForm());
  }
  protected openEdit(row: PersonRow): void {
    this.form.set(this.formFromRow(row));
  }
  protected closeModal(): void {
    if (!this.isBusy()) {
      this.form.set(null);
    }
  }
  protected saveForm(draft: PersonFormState): void {
    if (this.isBusy()) {
      return;
    }
    if (!draft.firstName.trim() || !draft.lastName.trim()) {
      this.toaster.warn('First and last name are required.');
      return;
    }
    const section = this.section();
    const request$ = draft.id
      ? this.gateway.update(section, draft)
      : this.gateway.create(section, draft);
    this.isBusy.set(true);
    request$.pipe(finalize(() => this.isBusy.set(false))).subscribe({
      next: () => {
        this.toaster.success(`${this.capitalize(this.meta().singular)} saved.`);
        this.form.set(null);
        // If the open detail patient was edited, close it so a re-open reloads fresh.
        if (this.detailRow()?.id === draft.id) {
          this.detailRow.set(null);
        }
        this.load();
      },
      error: () => undefined,
    });
  }
  protected tryDelete(row: PersonRow): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .delete(this.section(), row.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success(`${this.capitalize(this.meta().singular)} deleted.`);
          this.load();
        },
        // ABP surfaces the server-side in-use guard message (409) itself.
        error: () => undefined,
      });
  }

  // ---- invite deep-link ----
  protected invitePerson(row: PersonRow): void {
    this.router.navigate(['/users/invite'], {
      queryParams: { email: row.email ?? '', userType: this.meta().inviteUserType },
    });
  }

  // ---- internals ----
  private load(): void {
    this.loading.set(true);
    this.gateway
      .list(this.section())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (rows) => {
          this.rows.set(rows);
          this.resolveInvited(rows);
        },
        error: () => this.rows.set([]),
      });
  }

  private resolveInvited(rows: PersonRow[]): void {
    if (!this.canInvite) {
      return;
    }
    const emails = Array.from(
      new Set(
        rows.filter((r) => !r.identityUserId && r.email).map((r) => r.email!.trim().toLowerCase()),
      ),
    );
    if (emails.length === 0) {
      return;
    }
    this.gateway.activeInvitedEmails(emails).subscribe({
      next: (list) => {
        const invited = new Set(list.map((e) => e.toLowerCase()));
        this.rows.set(
          this.rows().map((r) => ({
            ...r,
            portal: derivePortalStatus(r.identityUserId, r.email, invited),
          })),
        );
      },
      error: () => undefined,
    });
  }

  private distinct(
    idFn: (r: PersonRow) => string | null | undefined,
    nameFn: (r: PersonRow) => string | null | undefined,
  ): LookupOption[] {
    const map = new Map<string, string>();
    for (const r of this.rows()) {
      const id = idFn(r);
      const name = nameFn(r);
      if (id && name && !map.has(id)) {
        map.set(id, name);
      }
    }
    return Array.from(map, ([id, name]) => ({ id, name }));
  }

  private resetView(): void {
    this.search.set('');
    this.filters.set(emptyPeopleFilters());
    this.showFilters.set(false);
    this.showCols.set(false);
    this.cols.set({});
    this.detailRow.set(null);
    this.form.set(null);
  }

  private blankForm(): PersonFormState {
    return {
      id: null,
      identityUserId: null,
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
      middleName: '',
      genderId: null,
      dateOfBirth: '',
      cellPhoneNumber: '',
      phoneNumberTypeId: null,
      socialSecurityNumber: '',
      street: '',
      city: '',
      stateId: '',
      zipCode: '',
      appointmentLanguageId: '',
      othersLanguageName: '',
      interpreterVendorName: '',
      apptNumber: '',
      firmName: '',
      firmAddress: '',
      webAddress: '',
      faxNumber: '',
    };
  }

  private formFromRow(r: PersonRow): PersonFormState {
    return {
      id: r.id,
      identityUserId: r.identityUserId ?? null,
      firstName: r.firstName,
      lastName: r.lastName,
      email: r.email ?? '',
      phoneNumber: r.phoneNumber ?? '',
      middleName: r.middleName ?? '',
      genderId: r.genderId ?? null,
      dateOfBirth: r.dateOfBirth ? r.dateOfBirth.substring(0, 10) : '',
      cellPhoneNumber: r.cellPhoneNumber ?? '',
      phoneNumberTypeId: r.phoneNumberTypeId ?? null,
      // Never pre-fill the stored SSN -- write-only; blank preserves it on save.
      socialSecurityNumber: '',
      street: r.street ?? '',
      city: r.city ?? '',
      stateId: r.stateId ?? '',
      zipCode: r.zipCode ?? '',
      appointmentLanguageId: r.appointmentLanguageId ?? '',
      othersLanguageName: r.othersLanguageName ?? '',
      interpreterVendorName: r.interpreterVendorName ?? '',
      apptNumber: r.apptNumber ?? '',
      firmName: r.firmName ?? '',
      firmAddress: r.firmAddress ?? '',
      webAddress: r.webAddress ?? '',
      faxNumber: r.faxNumber ?? '',
    };
  }

  private capitalize(value: string): string {
    return value.length ? value.charAt(0).toUpperCase() + value.slice(1) : value;
  }
}
