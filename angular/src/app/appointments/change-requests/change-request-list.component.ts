import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LocalizationPipe, UtcToLocalPipe } from '@abp/ng.core';
import { PageComponent } from '@abp/ng.components/page';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import type { AppointmentChangeRequestDto } from '../../proxy/appointment-change-requests/models';
import { AppointmentChangeRequestApprovalService } from '../../proxy/appointment-change-requests/appointment-change-request-approval.service';
import { ChangeRequestApproveModalComponent } from './change-request-approve-modal.component';
import { ChangeRequestRejectModalComponent } from './change-request-reject-modal.component';

/**
 * AP1 supervisor approval page. One component drives both the "Pending
 * Reschedules" and "Pending Cancellations" queues, selected by the route
 * `data.changeRequestType`. Feeds from `getPending` (server defaults the
 * status filter to Pending) and hosts the approve/reject modals. A plain
 * Bootstrap table is used (the queue is small); the approve/reject endpoints
 * enforce the `.Approve` / `.Reject` permissions server-side.
 */
@Component({
  selector: 'app-change-request-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    RouterLink,
    LocalizationPipe,
    UtcToLocalPipe,
    PageComponent,
    ChangeRequestApproveModalComponent,
    ChangeRequestRejectModalComponent,
  ],
  templateUrl: './change-request-list.component.html',
})
export class ChangeRequestListComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly approvalService = inject(AppointmentChangeRequestApprovalService);

  changeRequestType: ChangeRequestType = ChangeRequestType.Reschedule;
  items: AppointmentChangeRequestDto[] = [];
  isLoading = false;

  approveVisible = false;
  rejectVisible = false;
  selected: AppointmentChangeRequestDto | null = null;

  get titleKey(): string {
    return this.changeRequestType === ChangeRequestType.Cancel
      ? '::ChangeRequest:PendingCancellations'
      : '::ChangeRequest:PendingReschedules';
  }

  ngOnInit(): void {
    const dataType = this.route.snapshot.data['changeRequestType'];
    if (dataType === ChangeRequestType.Cancel || dataType === ChangeRequestType.Reschedule) {
      this.changeRequestType = dataType;
    }
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.approvalService
      .getPending({ changeRequestType: this.changeRequestType, skipCount: 0, maxResultCount: 100 })
      .subscribe({
        next: (res) => {
          this.items = res.items ?? [];
          this.isLoading = false;
        },
        error: () => {
          this.items = [];
          this.isLoading = false;
        },
      });
  }

  reasonOf(row: AppointmentChangeRequestDto): string {
    return (
      (this.changeRequestType === ChangeRequestType.Cancel
        ? row.cancellationReason
        : row.reScheduleReason) ?? ''
    );
  }

  openApprove(row: AppointmentChangeRequestDto): void {
    this.selected = row;
    this.rejectVisible = false;
    this.approveVisible = true;
  }

  openReject(row: AppointmentChangeRequestDto): void {
    this.selected = row;
    this.approveVisible = false;
    this.rejectVisible = true;
  }

  onHandled(): void {
    this.selected = null;
    this.load();
  }
}
