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
import { EnvironmentService, LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import {
  AppointmentReportRowDto,
  GetAppointmentReportInput,
  ReportService,
} from '../proxy/reports';
import {
  AppointmentStatusType,
  appointmentStatusTypeOptions,
} from '../proxy/enums/appointment-status-type.enum';
import { AppointmentTypeService } from '../proxy/appointment-types';
import { LocationService } from '../proxy/locations';

/**
 * G-08-01: the Appointment Request Report -- an internal-only, cross-appointment
 * worklist (gated by CaseEvaluation.Reports). Mirrors the legacy report's quick
 * search + advanced filters and its ten columns. Rows arrive already
 * PHI-redacted from the server (SSN masked to last 4, DOB to birth year), so
 * the template renders them verbatim. The grid stays empty until the user
 * enters a search, matching the legacy behavior.
 */
@Component({
  selector: 'app-appointment-report',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe],
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
    this.hasSearched = false;
    this.pageIndex = 0;
  }

  exportPdf(): void {
    this.validationError = '';

    if (!this.hasAnyFilter()) {
      this.validationError = 'Enter at least one search value.';
      return;
    }
    if (!this.isDateRangeValid()) {
      this.validationError = 'Enter both From and To dates, with From on or before To.';
      return;
    }

    void this.downloadPdf();
  }

  // Authenticated blob download: HttpClient (with ABP's auth interceptor) +
  // a synthetic anchor click. NEVER window.open -- a new tab carries no Bearer
  // token and the export 401s. (See angular/src/app/CLAUDE.md.)
  private async downloadPdf(): Promise<void> {
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
        this.http.get(`${base}/api/app/reports/export-pdf`, {
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
      const fileName = match ? decodeURIComponent(match[1]) : 'appointment-request-report.pdf';

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
