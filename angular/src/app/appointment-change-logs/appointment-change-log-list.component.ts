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
import { PagedResultDto } from '@abp/ng.core';
import {
  AppointmentChangeLogDto,
  AppointmentChangeLogService,
  GetAppointmentChangeLogsInput,
} from '../proxy/appointment-change-logs';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { ChangeLogTimelineComponent } from './change-log-timeline.component';

/**
 * Group K (G-02-01): internal-only global change-log list across all audited
 * appointment intake entities, with field-level filters + paging. Backed by
 * AppointmentChangeLogService.getList, which returns PHI-redacted rows -- masked
 * values arrive as valueRedacted, so the template never renders raw PHI.
 */
@Component({
  selector: 'app-appointment-change-log-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, IconComponent, ChangeLogTimelineComponent],
  templateUrl: './appointment-change-log-list.component.html',
})
export class AppointmentChangeLogListComponent implements OnInit {
  private readonly fb = new FormBuilder();
  private readonly changeLogService = inject(AppointmentChangeLogService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly entityTypeOptions = [
    'Appointment',
    'Injury Detail',
    'Body Part',
    'Claim Examiner',
    'Primary Insurance',
  ];
  readonly changeTypeOptions = ['Created', 'Updated', 'Deleted'];

  readonly filterForm = this.fb.group({
    requestConfirmationNumber: [''],
    entityType: [''],
    fieldName: [''],
    changeType: [''],
    startTime: [''],
    endTime: [''],
  });

  isLoading = false;
  rows: AppointmentChangeLogDto[] = [];
  totalCount = 0;
  readonly pageSize = 25;
  pageIndex = 0;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    const raw = this.filterForm.getRawValue();
    const input: GetAppointmentChangeLogsInput = {
      requestConfirmationNumber: raw.requestConfirmationNumber || undefined,
      entityType: raw.entityType || undefined,
      fieldName: raw.fieldName || undefined,
      changeType: raw.changeType || undefined,
      startTime: raw.startTime || undefined,
      endTime: raw.endTime || undefined,
      skipCount: this.pageIndex * this.pageSize,
      maxResultCount: this.pageSize,
    };

    this.changeLogService.getList(input).subscribe({
      next: (result: PagedResultDto<AppointmentChangeLogDto>) => {
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

  applyFilters(): void {
    this.pageIndex = 0;
    this.load();
  }

  clearFilters(): void {
    this.filterForm.reset({
      requestConfirmationNumber: '',
      entityType: '',
      fieldName: '',
      changeType: '',
      startTime: '',
      endTime: '',
    });
    this.pageIndex = 0;
    this.load();
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
}
