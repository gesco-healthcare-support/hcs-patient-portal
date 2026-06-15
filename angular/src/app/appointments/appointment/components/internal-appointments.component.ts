import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { forkJoin } from 'rxjs';
import { filter, switchMap } from 'rxjs/operators';

import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import type {
  AppointmentWithNavigationPropertiesDto,
  GetAppointmentsInput,
} from '../../../proxy/appointments/models';
import type { LookupDto } from '../../../proxy/shared/models';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';
import type { AppointmentChangeRequestDto } from '../../../proxy/appointment-change-requests/models';
import { AppointmentChangeRequestApprovalService } from '../../../proxy/appointment-change-requests/appointment-change-request-approval.service';
import { StatusPillComponent } from '../../../shared/ui/status-pill/status-pill.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import {
  EXTERNAL_STATUS_SEGMENTS,
  appointmentStatusToSegment,
  type ExternalStatusSegment,
} from '../../../shared/ui/status-pill/appointment-status.util';
import { RescheduleRequestModalComponent } from './reschedule-request-modal.component';
import { CancellationRequestModalComponent } from './cancellation-request-modal.component';
import { planAutoApprove } from './change-request-auto-approve';
import {
  avatarColor,
  avatarInitials,
  bucketChipCounts,
  decideByInfo,
  isRowActionable,
  pillOfStatus,
  segmentStatuses,
  toCsvContent,
  type DecideBy,
} from './internal-appointments.util';

type Row = AppointmentWithNavigationPropertiesDto;

/** Drawer filter draft (panel / type / location / date range / booker). */
interface FilterState {
  panelNumber?: string;
  appointmentTypeId?: string;
  locationId?: string;
  dateMin?: string;
  dateMax?: string;
  identityUserId?: string;
  bookerLabel?: string;
}

const PAGE_SIZES = [10, 25, 50] as const;

/**
 * Redesigned internal appointments list (Prompt 10). Replaces the legacy
 * NgxDatatable-based AppointmentComponent; renders inside the internal shell at
 * /appointments for staff. Server-paged (getList) with a per-status counts
 * endpoint (getStatusCounts) feeding the chips; pill bucketing + decide-by
 * urgency live in internal-appointments.util. Kebab Reschedule/Cancel reuse the
 * existing change-request modals (auto-approved when the caller can approve);
 * Delete is gated by the Appointments.Delete permission (Intake lacks it).
 */
@Component({
  selector: 'app-internal-appointments',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    StatusPillComponent,
    IconComponent,
    SkeletonComponent,
    RescheduleRequestModalComponent,
    CancellationRequestModalComponent,
  ],
  templateUrl: './internal-appointments.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InternalAppointmentsComponent implements OnInit {
  private readonly appointmentService = inject(AppointmentService);
  private readonly approvalService = inject(AppointmentChangeRequestApprovalService);
  private readonly permissionService = inject(PermissionService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly localization = inject(LocalizationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly segments = EXTERNAL_STATUS_SEGMENTS;
  protected readonly pageSizes = PAGE_SIZES;

  // ---- query state ----
  protected readonly search = signal('');
  protected readonly activeSegment = signal<ExternalStatusSegment>('all');
  protected readonly filters = signal<FilterState>({});
  protected readonly draft = signal<FilterState>({});
  protected readonly showFilters = signal(false);
  protected readonly page = signal(1);
  protected readonly pageSize = signal<number>(PAGE_SIZES[0]);

  // ---- results ----
  protected readonly rows = signal<Row[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly chipCounts = signal<Record<ExternalStatusSegment, number>>(
    bucketChipCounts([]),
  );

  // ---- lookups (small bounded sets, loaded once) ----
  protected readonly types = signal<LookupDto<string>[]>([]);
  protected readonly locations = signal<LookupDto<string>[]>([]);
  protected readonly bookerResults = signal<LookupDto<string>[]>([]);

  // ---- selection (keyed by id; row kept for CSV export across pages) ----
  private readonly selected = signal<Map<string, Row>>(new Map());
  protected readonly selectedCount = computed(() => this.selected().size);

  // ---- per-row kebab + reschedule/cancel modals ----
  protected readonly menuId = signal<string | null>(null);
  protected readonly selectedRow = signal<Row | null>(null);
  protected readonly rescheduleVisible = signal(false);
  protected readonly cancelVisible = signal(false);

  protected readonly canDelete = this.permissionService.getGrantedPolicy(
    'CaseEvaluation.Appointments.Delete',
  );
  protected readonly canCreate = this.permissionService.getGrantedPolicy(
    'CaseEvaluation.Appointments.Create',
  );

  protected newAppointment(): void {
    void this.router.navigateByUrl('/appointments/add');
  }

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalCount() / this.pageSize())),
  );
  protected readonly pages = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1),
  );
  protected readonly rangeStart = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  protected readonly rangeEnd = computed(() =>
    Math.min(this.page() * this.pageSize(), this.totalCount()),
  );
  protected readonly activeFilterCount = computed(() => {
    const f = this.filters();
    return [
      f.panelNumber,
      f.appointmentTypeId,
      f.locationId,
      f.dateMin,
      f.dateMax,
      f.identityUserId,
    ].filter((v) => !!v).length;
  });
  protected readonly allOnPageSelected = computed(() => {
    const list = this.rows();
    return list.length > 0 && list.every((r) => this.selected().has(this.rowId(r)));
  });

  private searchTimer?: ReturnType<typeof setTimeout>;
  private bookerTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.loadLookups();
    // The query param is the single driver for the active chip: setSegment writes
    // it, this subscription reacts (also serves dashboard deep-links to
    // /appointments?appointmentStatus=N without a manual chip click).
    this.route.queryParamMap.subscribe((params) => {
      const raw = params.get('appointmentStatus');
      const parsed = raw !== null && raw !== '' ? Number(raw) : NaN;
      const segment = Number.isNaN(parsed)
        ? 'all'
        : appointmentStatusToSegment(parsed as AppointmentStatusType);
      if (segment !== this.activeSegment()) {
        this.activeSegment.set(segment);
        this.page.set(1);
      }
      this.loadList();
    });
    this.loadCounts();
  }

  // ---- loaders ----
  private buildInput(forList: boolean): GetAppointmentsInput {
    const f = this.filters();
    const input: GetAppointmentsInput = {
      filterText: this.search().trim() || undefined,
      panelNumber: f.panelNumber || undefined,
      appointmentTypeId: f.appointmentTypeId || undefined,
      locationId: f.locationId || undefined,
      identityUserId: f.identityUserId || undefined,
      appointmentDateMin: f.dateMin || undefined,
      appointmentDateMax: f.dateMax ? this.endOfDay(f.dateMax) : undefined,
      // Required by the paged input contract; the counts endpoint ignores paging.
      skipCount: (this.page() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };
    if (forList) {
      const statuses = segmentStatuses(this.activeSegment());
      input.appointmentStatuses = statuses.length ? statuses : undefined;
    }
    return input;
  }

  private loadList(): void {
    this.loading.set(true);
    this.appointmentService.getList(this.buildInput(true)).subscribe({
      next: (res) => {
        this.rows.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private loadCounts(): void {
    // Counts ignore the status filter (the endpoint omits it) so each chip shows
    // its true total within the OTHER active filters.
    this.appointmentService.getStatusCounts(this.buildInput(false)).subscribe({
      next: (counts) => this.chipCounts.set(bucketChipCounts(counts)),
      error: () => this.chipCounts.set(bucketChipCounts([])),
    });
  }

  private loadLookups(): void {
    const req = { filter: '', skipCount: 0, maxResultCount: 200 };
    this.appointmentService
      .getAppointmentTypeLookup(req)
      .subscribe((res) => this.types.set(res.items ?? []));
    this.appointmentService
      .getLocationLookup(req)
      .subscribe((res) => this.locations.set(res.items ?? []));
  }

  // ---- chips ----
  protected setSegment(segment: ExternalStatusSegment): void {
    const status = segment === 'all' ? null : (segmentStatuses(segment)[0] ?? null);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { appointmentStatus: status },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected chipCount(segment: ExternalStatusSegment): number {
    return this.chipCounts()[segment] ?? 0;
  }

  // ---- search ----
  protected onSearch(value: string): void {
    this.search.set(value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.page.set(1);
      this.loadList();
      this.loadCounts();
    }, 300);
  }

  protected clearSearch(): void {
    this.search.set('');
    this.page.set(1);
    this.loadList();
    this.loadCounts();
  }

  // ---- filter drawer ----
  protected toggleFilters(): void {
    if (!this.showFilters()) {
      this.draft.set({ ...this.filters() });
    }
    this.showFilters.update((v) => !v);
  }

  protected updateDraft(patch: Partial<FilterState>): void {
    this.draft.update((d) => ({ ...d, ...patch }));
  }

  protected applyFilters(): void {
    this.filters.set({ ...this.draft() });
    this.showFilters.set(false);
    this.page.set(1);
    this.loadList();
    this.loadCounts();
  }

  protected clearFilters(): void {
    this.draft.set({});
    this.filters.set({});
    this.bookerResults.set([]);
    this.page.set(1);
    this.loadList();
    this.loadCounts();
  }

  // ---- booker typeahead (resolves to identityUserId via the existing lookup) ----
  protected onBookerInput(value: string): void {
    this.updateDraft({ bookerLabel: value, identityUserId: undefined });
    clearTimeout(this.bookerTimer);
    const term = value.trim();
    if (term.length < 2) {
      this.bookerResults.set([]);
      return;
    }
    this.bookerTimer = setTimeout(() => {
      this.appointmentService
        .getIdentityUserLookup({ filter: term, skipCount: 0, maxResultCount: 8 })
        .subscribe((res) => this.bookerResults.set(res.items ?? []));
    }, 300);
  }

  protected pickBooker(user: LookupDto<string>): void {
    this.updateDraft({ identityUserId: user.id ?? undefined, bookerLabel: user.displayName ?? '' });
    this.bookerResults.set([]);
  }

  // ---- pagination ----
  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }
    this.page.set(page);
    this.loadList();
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(Number(size));
    this.page.set(1);
    this.loadList();
  }

  // ---- selection ----
  protected isSelected(row: Row): boolean {
    return this.selected().has(this.rowId(row));
  }

  protected toggleSelect(row: Row): void {
    const next = new Map(this.selected());
    const id = this.rowId(row);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.set(id, row);
    }
    this.selected.set(next);
  }

  protected toggleSelectAll(): void {
    const next = new Map(this.selected());
    if (this.allOnPageSelected()) {
      this.rows().forEach((r) => next.delete(this.rowId(r)));
    } else {
      this.rows().forEach((r) => next.set(this.rowId(r), r));
    }
    this.selected.set(next);
  }

  protected clearSelection(): void {
    this.selected.set(new Map());
  }

  // ---- kebab actions ----
  protected toggleMenu(row: Row): void {
    const id = this.rowId(row);
    this.menuId.set(this.menuId() === id ? null : id);
  }

  protected closeMenu(): void {
    this.menuId.set(null);
  }

  protected review(row: Row): void {
    this.closeMenu();
    void this.router.navigate(['/appointments/view', this.rowId(row)]);
  }

  protected openReschedule(row: Row): void {
    this.closeMenu();
    this.selectedRow.set(row);
    this.cancelVisible.set(false);
    this.rescheduleVisible.set(true);
  }

  protected openCancel(row: Row): void {
    this.closeMenu();
    this.selectedRow.set(row);
    this.rescheduleVisible.set(false);
    this.cancelVisible.set(true);
  }

  /**
   * Ported from the legacy AppointmentComponent: the change-request modal has
   * already submitted; if the caller can approve (internal staff), chain the
   * NoBill auto-approve, else it stays Pending for the supervisor queue.
   */
  protected onChangeRequestSucceeded(dto: AppointmentChangeRequestDto): void {
    const canApprove = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentChangeRequests.Approve',
    );
    const plan = planAutoApprove(dto.changeRequestType, canApprove);

    if (!plan || !dto.id) {
      this.toaster.success(
        this.localization.instant(
          dto.changeRequestType === ChangeRequestType.Cancel
            ? '::Appointment:Toast:CancelRequested'
            : '::Appointment:Toast:RescheduleRequested',
        ),
      );
      this.reload();
      return;
    }

    const approve$ =
      plan.kind === 'reschedule'
        ? this.approvalService.approveReschedule(dto.id, { rescheduleOutcome: plan.outcome })
        : this.approvalService.approveCancellation(dto.id, { cancellationOutcome: plan.outcome });

    approve$.subscribe({
      next: () => {
        this.toaster.success(
          this.localization.instant(
            plan.kind === 'reschedule'
              ? '::Appointment:Toast:RescheduleApproved'
              : '::Appointment:Toast:CancelApproved',
          ),
        );
        this.reload();
      },
      error: () => this.reload(),
    });
  }

  protected deleteRow(row: Row): void {
    this.closeMenu();
    this.confirmation
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter((status) => status === Confirmation.Status.confirm),
        switchMap(() => this.appointmentService.delete(this.rowId(row))),
      )
      .subscribe(() => {
        this.dropSelection(this.rowId(row));
        this.reload();
      });
  }

  // ---- bulk bar ----
  protected bulkDelete(): void {
    if (!this.canDelete || this.selectedCount() === 0) {
      return;
    }
    const ids = Array.from(this.selected().keys());
    this.confirmation
      .warn('::Appointment:BulkDeleteConfirmation', '::AreYouSure', {
        messageLocalizationParams: [String(ids.length)],
      })
      .pipe(
        filter((status) => status === Confirmation.Status.confirm),
        switchMap(() => forkJoin(ids.map((id) => this.appointmentService.delete(id)))),
      )
      .subscribe(() => {
        this.clearSelection();
        this.reload();
      });
  }

  protected exportCsv(): void {
    const rows = Array.from(this.selected().values());
    if (rows.length === 0) {
      return;
    }
    const headers = [
      'Confirmation #',
      'Patient',
      'Type',
      'Appointment',
      'Status',
      'Panel #',
      'Location',
      'Decide by',
    ];
    const matrix = rows.map((r) => {
      const decide = this.decideBy(r);
      return [
        r.appointment?.requestConfirmationNumber ?? '',
        this.patientName(r),
        r.appointmentType?.name ?? '',
        r.appointment?.appointmentDate ?? '',
        pillOfStatus(r.appointment?.appointmentStatus),
        r.appointment?.panelNumber ?? '',
        r.location?.name ?? '',
        decide ? decide.due.toISOString().slice(0, 10) : '',
      ].map((cell) => this.asciiOnly(String(cell)));
    });
    const content = toCsvContent(headers, matrix);
    this.downloadCsv(content, 'appointments.csv');
  }

  // ---- view helpers ----
  protected rowId(row: Row): string {
    return row.appointment?.id ?? '';
  }

  protected patientName(row: Row): string {
    const p = row.patient;
    return p ? `${p.firstName ?? ''} ${p.lastName ?? ''}`.trim() : '';
  }

  protected initials(row: Row): string {
    return avatarInitials(row.patient?.firstName, row.patient?.lastName);
  }

  protected avatar(row: Row): string {
    return avatarColor(this.patientName(row) || this.rowId(row));
  }

  protected pill(row: Row) {
    return pillOfStatus(row.appointment?.appointmentStatus);
  }

  protected actionable(row: Row): boolean {
    return isRowActionable(this.pill(row));
  }

  protected decideBy(row: Row): DecideBy | null {
    if (this.pill(row) !== 'Pending') {
      return null;
    }
    return decideByInfo(row.appointment?.creationTime, new Date());
  }

  private reload(): void {
    this.loadList();
    this.loadCounts();
  }

  private dropSelection(id: string): void {
    if (this.selected().has(id)) {
      const next = new Map(this.selected());
      next.delete(id);
      this.selected.set(next);
    }
  }

  private endOfDay(date: string): string {
    return `${date}T23:59:59`;
  }

  private asciiOnly(value: string): string {
    // HIPAA/export hygiene: keep the CSV ASCII-only (matches code-standards).
    return value.replace(/[^\x20-\x7E]/g, '');
  }

  private downloadCsv(content: string, filename: string): void {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
