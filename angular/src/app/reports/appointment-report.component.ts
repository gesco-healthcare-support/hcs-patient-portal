import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { EnvironmentService, PagedResultDto } from '@abp/ng.core';
import {
  AppointmentReportRowDto,
  GetAppointmentReportInput,
  ReportService,
} from '../proxy/reports';
import type { AppointmentStatusCountDto } from '../proxy/appointments/models';
import {
  AppointmentStatusType,
  appointmentStatusTypeOptions,
} from '../proxy/enums/appointment-status-type.enum';
import { AppointmentTypeService } from '../proxy/appointment-types';
import { LocationService } from '../proxy/locations';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../shared/ui/skeleton/skeleton.component';
import { StatusPillComponent } from '../shared/ui/status-pill/status-pill.component';
import type { AppointmentPillStatus } from '../shared/ui/status-pill/status-pill.component';
import {
  appointmentStatusToPill,
  appointmentStatusToSegment,
} from '../shared/ui/status-pill/appointment-status.util';

type ColKey =
  | 'conf'
  | 'type'
  | 'location'
  | 'date'
  | 'status'
  | 'patient'
  | 'dob'
  | 'email'
  | 'phone';

/**
 * G-08-01: the Appointment Request Report -- an internal-only, cross-appointment
 * worklist (gated by CaseEvaluation.Reports). Redesign (Prompt 13, 2026-06-15):
 * re-skinned onto the internal-shell tokens (ia-* toolbar/table) with status
 * summary cards (raw counts bucketed client-side via appointmentStatusToPill, so
 * the cards stay in lockstep with the list pills), a column picker, and CSV
 * alongside PDF export. Rows arrive PHI-redacted (SSN last-4, DOB birth year);
 * SSN is never shown on screen (export-only). Empty until the user searches.
 */
@Component({
  selector: 'app-appointment-report',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    IconComponent,
    SkeletonComponent,
    StatusPillComponent,
  ],
  templateUrl: './appointment-report.component.html',
})
export class AppointmentReportComponent implements OnInit {
  private readonly fb = new FormBuilder();
  private readonly reportService = inject(ReportService);
  private readonly appointmentTypeService = inject(AppointmentTypeService);
  private readonly locationService = inject(LocationService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly http = inject(HttpClient);
  private readonly environmentService = inject(EnvironmentService);

  readonly statusOptions = appointmentStatusTypeOptions;
  typeOptions: { id: string; name: string }[] = [];
  locationOptions: { id: string; name: string }[] = [];

  // Status summary cards -- the six UI pill buckets, in list order. Counts are
  // filled by bucketing the server's raw per-status counts (Option A).
  readonly statSegments: { seg: string; label: string; sw: string }[] = [
    { seg: 'pending', label: 'Pending', sw: 'var(--st-pending-dot)' },
    { seg: 'info', label: 'Info Requested', sw: 'var(--st-purple-fg)' },
    { seg: 'approved', label: 'Approved', sw: 'var(--green-500)' },
    { seg: 'rescheduled', label: 'Rescheduled', sw: 'var(--blue-500)' },
    { seg: 'cancelled', label: 'Cancelled', sw: 'var(--st-rejected-dot)' },
    { seg: 'rejected', label: 'Rejected', sw: 'var(--n-700)' },
  ];
  segmentCounts: Record<string, number> = {};

  // Column-picker visibility. All columns start visible.
  colsOpen = false;
  readonly colDefs: { key: ColKey; label: string }[] = [
    { key: 'conf', label: 'Confirmation #' },
    { key: 'type', label: 'Type' },
    { key: 'location', label: 'Location' },
    { key: 'date', label: 'Appointment' },
    { key: 'status', label: 'Status' },
    { key: 'patient', label: 'Patient' },
    { key: 'dob', label: 'Date of birth' },
    { key: 'email', label: 'Email' },
    { key: 'phone', label: 'Phone' },
  ];
  cols: Record<ColKey, boolean> = {
    conf: true,
    type: true,
    location: true,
    date: true,
    status: true,
    patient: true,
    dob: true,
    email: true,
    phone: true,
  };

  readonly filterForm = this.fb.group({
    filterText: [''],
    appointmentTypeId: [''],
    locationId: [''],
    appointmentStatus: [''],
    appointmentDateMin: [''],
    appointmentDateMax: [''],
  });

  isLoading = false;
  hasSearched = false;
  validationError = '';
  rows: AppointmentReportRowDto[] = [];
  totalCount = 0;
  readonly pageSize = 25;
  pageIndex = 0;

  ngOnInit(): void {
    // Populate the advanced-search dropdowns. Lookups are small; pull all.
    this.appointmentTypeService.getList({ maxResultCount: 1000 }).subscribe((result) => {
      this.typeOptions = (result?.items ?? []).map((t) => ({ id: t.id ?? '', name: t.name ?? '' }));
      this.cdr.markForCheck();
    });
    this.locationService.getList({ maxResultCount: 1000 }).subscribe((result) => {
      this.locationOptions = (result?.items ?? []).map((l) => ({
        id: l.location?.id ?? '',
        name: l.location?.name ?? '',
      }));
      this.cdr.markForCheck();
    });
  }

  applyFilters(): void {
    this.validationError = '';

    if (!this.hasAnyFilter()) {
      this.validationError = 'Enter at least one search value.';
      return;
    }
    if (!this.isDateRangeValid()) {
      this.validationError = 'Enter both From and To dates, with From on or before To.';
      return;
    }

    this.pageIndex = 0;
    this.load();
  }

  clearFilters(): void {
    this.filterForm.reset({
      filterText: '',
      appointmentTypeId: '',
      locationId: '',
      appointmentStatus: '',
      appointmentDateMin: '',
      appointmentDateMax: '',
    });
    this.validationError = '';
    this.rows = [];
    this.totalCount = 0;
    this.segmentCounts = {};
    this.hasSearched = false;
    this.pageIndex = 0;
  }

  exportPdf(): void {
    if (!this.guardExport()) {
      return;
    }
    void this.download('pdf');
  }

  exportCsv(): void {
    if (!this.guardExport()) {
      return;
    }
    void this.download('csv');
  }

  private guardExport(): boolean {
    this.validationError = '';
    if (!this.hasAnyFilter()) {
      this.validationError = 'Enter at least one search value.';
      return false;
    }
    if (!this.isDateRangeValid()) {
      this.validationError = 'Enter both From and To dates, with From on or before To.';
      return false;
    }
    return true;
  }

  // Authenticated blob download: HttpClient (with ABP's auth interceptor) +
  // a synthetic anchor click. NEVER window.open -- a new tab carries no Bearer
  // token and the export 401s. (See angular/src/app/CLAUDE.md.)
  private async download(kind: 'pdf' | 'csv'): Promise<void> {
    const base = this.environmentService.getApiUrl('Default') ?? '';
    const raw = this.filterForm.getRawValue();

    let params = new HttpParams();
    if (raw.filterText) params = params.set('filterText', raw.filterText);
    if (raw.appointmentTypeId) params = params.set('appointmentTypeId', raw.appointmentTypeId);
    if (raw.locationId) params = params.set('locationId', raw.locationId);
    if (raw.appointmentStatus) params = params.set('appointmentStatus', raw.appointmentStatus);
    if (raw.appointmentDateMin) params = params.set('appointmentDateMin', raw.appointmentDateMin);
    if (raw.appointmentDateMax) params = params.set('appointmentDateMax', raw.appointmentDateMax);

    try {
      const response = await firstValueFrom(
        this.http.get(`${base}/api/app/reports/export-${kind}`, {
          params,
          observe: 'response',
          responseType: 'blob',
        }),
      );

      const blob = response.body;
      if (!blob) {
        this.validationError = 'Empty export response from the server.';
        this.cdr.markForCheck();
        return;
      }

      const disposition = response.headers.get('content-disposition') || '';
      const match = /filename\*?=(?:UTF-8'')?"?([^";]+)/i.exec(disposition);
      const fileName = match ? decodeURIComponent(match[1]) : `appointment-request-report.${kind}`;

      const objectUrl = URL.createObjectURL(blob);
      try {
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = fileName;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
      } finally {
        setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
      }
    } catch {
      this.validationError = 'Export failed. Please try again.';
      this.cdr.markForCheck();
    }
  }

  load(): void {
    this.isLoading = true;
    this.hasSearched = true;
    this.reportService.getList(this.buildInput()).subscribe({
      next: (result: PagedResultDto<AppointmentReportRowDto>) => {
        this.rows = result?.items ?? [];
        this.totalCount = result?.totalCount ?? 0;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.rows = [];
        this.totalCount = 0;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
    this.loadStatusCounts();
  }

  // Status summary cards for the current filter set (status filter excluded
  // server-side). Raw per-status counts are bucketed into the six UI pills.
  private loadStatusCounts(): void {
    this.reportService.getStatusCounts(this.buildInput()).subscribe({
      next: (counts: AppointmentStatusCountDto[]) => {
        const acc: Record<string, number> = {};
        for (const c of counts ?? []) {
          if (c.status === undefined || c.status === null) {
            continue;
          }
          const seg = appointmentStatusToSegment(c.status);
          acc[seg] = (acc[seg] ?? 0) + (c.count ?? 0);
        }
        this.segmentCounts = acc;
        this.cdr.markForCheck();
      },
      error: () => {
        this.segmentCounts = {};
        this.cdr.markForCheck();
      },
    });
  }

  statCount(seg: string): number {
    return this.segmentCounts[seg] ?? 0;
  }

  toggleCol(key: ColKey): void {
    this.cols = { ...this.cols, [key]: !this.cols[key] };
  }

  get shownColumnCount(): number {
    return this.colDefs.filter((c) => this.cols[c.key]).length;
  }

  pill(value?: AppointmentStatusType): AppointmentPillStatus {
    return appointmentStatusToPill(value ?? AppointmentStatusType.Pending);
  }

  statusLabel(value?: AppointmentStatusType): string {
    const match = this.statusOptions.find((option) => option.value === value);
    return match ? match.key : '';
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  prevPage(): void {
    if (this.pageIndex > 0) {
      this.pageIndex--;
      this.load();
    }
  }

  nextPage(): void {
    if (this.pageIndex + 1 < this.totalPages) {
      this.pageIndex++;
      this.load();
    }
  }

  openAppointment(appointmentId?: string | null): void {
    if (appointmentId) {
      this.router.navigate(['/appointments/view', appointmentId]);
    }
  }

  private hasAnyFilter(): boolean {
    const raw = this.filterForm.getRawValue();
    return !!(
      raw.filterText ||
      raw.appointmentTypeId ||
      raw.locationId ||
      raw.appointmentStatus ||
      raw.appointmentDateMin ||
      raw.appointmentDateMax
    );
  }

  private isDateRangeValid(): boolean {
    const raw = this.filterForm.getRawValue();
    const from = raw.appointmentDateMin;
    const to = raw.appointmentDateMax;
    if (!from && !to) {
      return true;
    }
    if (!from || !to) {
      return false;
    }
    // ISO yyyy-mm-dd strings order lexicographically.
    return from <= to;
  }

  private buildInput(): GetAppointmentReportInput {
    const raw = this.filterForm.getRawValue();
    return {
      filterText: raw.filterText || undefined,
      appointmentTypeId: raw.appointmentTypeId || undefined,
      locationId: raw.locationId || undefined,
      appointmentStatus: raw.appointmentStatus
        ? (Number(raw.appointmentStatus) as AppointmentStatusType)
        : undefined,
      appointmentDateMin: raw.appointmentDateMin || undefined,
      appointmentDateMax: raw.appointmentDateMax || undefined,
      skipCount: this.pageIndex * this.pageSize,
      maxResultCount: this.pageSize,
    };
  }
}
