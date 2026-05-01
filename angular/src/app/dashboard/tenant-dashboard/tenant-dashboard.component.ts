import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { LocalizationPipe, RestService } from '@abp/ng.core';

/**
 * W2-6: tenant dashboard counter cards. Single GET to /api/app/dashboard
 * returns the 13-field DashboardCountersDto; the host-side controller picks
 * the Tenant branch automatically based on the caller's permissions.
 *
 * 5 cards are populated with real counts; the remaining 8 stay at 0 until
 * day-of-exam states ship + W3 AppointmentChangeRequest lands. Card text
 * remains visible on 0 (no "no data yet" placeholder) per Adrian's Q11
 * lock so the layout stays consistent post-MVP when counts populate.
 */
type DashboardCountersDto = {
  pendingRequests: number;
  approvedThisWeek: number;
  rejectedThisWeek: number;
  pendingChangeRequests: number;
  requestsApproachingLegalDeadline: number;
  billedThisMonth: number;
  noShowThisMonth: number;
  rescheduledThisMonth: number;
  cancelledThisWeek: number;
  checkedInToday: number;
  checkedOutToday: number;
  totalDoctors: number;
  totalTenants: number;
};

@Component({
  selector: 'app-tenant-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe],
  templateUrl: './tenant-dashboard.component.html',
  styleUrls: ['./tenant-dashboard.component.scss'],
})
export class TenantDashboardComponent implements OnInit {
  private readonly restService = inject(RestService);
  private readonly router = inject(Router);

  readonly counters = signal<DashboardCountersDto | null>(null);
  readonly isLoading = signal(true);
  readonly errorMessage = signal('');

  ngOnInit(): void {
    this.restService
      .request<
        null,
        DashboardCountersDto
      >({ method: 'GET', url: '/api/app/dashboard' }, { apiName: 'Default' })
      .subscribe({
        next: (dto) => {
          this.counters.set(dto);
          this.isLoading.set(false);
        },
        error: () => {
          this.errorMessage.set('Failed to load dashboard counters.');
          this.isLoading.set(false);
        },
      });
  }

  /**
   * Card-click deep-link to /appointments?appointmentStatus=N. The list
   * page reads the query param via valueChanges and applies it to the
   * filter form, so the queue grid lands pre-filtered.
   */
  openByStatus(statusId: number): void {
    this.router.navigate(['/appointments'], { queryParams: { appointmentStatus: statusId } });
  }
}
