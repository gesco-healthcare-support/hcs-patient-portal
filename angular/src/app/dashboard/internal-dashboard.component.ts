import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { DashboardService } from '../proxy/dashboards/dashboard.service';
import type { DashboardDto, DashboardKpiDto } from '../proxy/dashboards/models';
import { DashboardRange } from '../proxy/dashboards/dashboard-range.enum';
import { resolveInternalRoleKey } from '../shared/auth/internal-user-roles';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../shared/ui/skeleton/skeleton.component';
import type { IconName } from '../shared/ui/icon/icon.registry';

/** A resolved donut slice (pill + label + count + CSS color). */
interface DonutSlice {
  pill: string;
  label: string;
  count: number;
  color: string;
}

/** Pill -> display label + donut color, matching the prototype DH_STATUS. */
const PILL_LABEL: Record<string, string> = {
  Pending: 'Pending',
  InfoRequested: 'Info Requested',
  Approved: 'Approved',
  Rescheduled: 'Rescheduled',
  Cancelled: 'Cancelled',
  Rejected: 'Rejected',
};
const PILL_COLOR: Record<string, string> = {
  Pending: 'var(--st-pending-dot)',
  InfoRequested: '#8a63c9',
  Approved: 'var(--green-500)',
  Rescheduled: 'var(--blue-500)',
  Cancelled: 'var(--n-300)',
  Rejected: 'var(--st-rejected-dot)',
};

/**
 * Redesigned internal dashboard (Prompt 9). Replaces the legacy
 * DashboardComponent switch; renders inside the internal shell at /dashboard.
 * Host vs tenant is driven by the DTO's isHost (the backend already branches
 * scope); Supervisor vs Intake presentation is driven by the resolved role key.
 * Pure-CSS charts (conic-gradient donut + flexbox bars); clickable KPIs deep-link
 * to the filtered appointments list.
 */
@Component({
  selector: 'app-internal-dashboard',
  standalone: true,
  imports: [DatePipe, IconComponent, SkeletonComponent],
  templateUrl: './internal-dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InternalDashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly configState = inject(ConfigStateService);
  private readonly router = inject(Router);

  protected readonly Range = DashboardRange;
  protected readonly range = signal(DashboardRange.Week);
  protected readonly data = signal<DashboardDto | null>(null);
  protected readonly loading = signal(true);

  private readonly roles = signal<string[]>([]);
  protected readonly roleKey = computed(() => resolveInternalRoleKey(this.roles()));
  protected readonly isHost = computed(() => this.data()?.isHost ?? false);
  protected readonly isIntake = computed(() => this.roleKey() === 'intake');

  protected readonly tenantName = computed(() => {
    const tenant = this.configState.getOne('currentTenant') as { name?: string } | null;
    return tenant?.name || 'your clinic';
  });

  protected readonly donut = computed<DonutSlice[]>(() =>
    (this.data()?.statusBreakdown ?? []).map((s) => ({
      pill: s.pill ?? '',
      label: PILL_LABEL[s.pill ?? ''] ?? s.pill ?? '',
      count: s.count ?? 0,
      color: PILL_COLOR[s.pill ?? ''] ?? 'var(--n-300)',
    })),
  );
  protected readonly donutTotal = computed(() => this.donut().reduce((a, s) => a + s.count, 0));
  protected readonly donutGradient = computed(() => {
    const total = this.donutTotal();
    if (total === 0) {
      return 'conic-gradient(var(--n-100) 0deg 360deg)';
    }
    let acc = 0;
    const stops = this.donut().map((s) => {
      const start = (acc / total) * 360;
      acc += s.count;
      const end = (acc / total) * 360;
      return `${s.color} ${start}deg ${end}deg`;
    });
    return `conic-gradient(${stops.join(', ')})`;
  });

  protected readonly trendMax = computed(() =>
    Math.max(1, ...(this.data()?.trend ?? []).map((t) => t.count ?? 0)),
  );

  ngOnInit(): void {
    const user = this.configState.getOne('currentUser') as { roles?: string[] } | null;
    this.roles.set(user?.roles ?? []);
    this.load();
  }

  protected setRange(range: DashboardRange): void {
    if (this.range() === range) {
      return;
    }
    this.range.set(range);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.dashboardService.getDashboard(this.range()).subscribe({
      next: (dto) => {
        this.data.set(dto);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  // ---- KPI delta badge ----
  protected hasDelta(kpi?: DashboardKpiDto): boolean {
    return !!kpi && (kpi.value ?? 0) !== (kpi.previousValue ?? 0);
  }
  protected deltaDir(kpi?: DashboardKpiDto): 'up' | 'down' {
    return (kpi?.value ?? 0) >= (kpi?.previousValue ?? 0) ? 'up' : 'down';
  }
  protected deltaText(kpi?: DashboardKpiDto): string {
    const diff = (kpi?.value ?? 0) - (kpi?.previousValue ?? 0);
    return (diff > 0 ? '+' : '') + diff;
  }

  // ---- trend ----
  protected barHeight(count?: number): string {
    return `${((count ?? 0) / this.trendMax()) * 100}%`;
  }

  // ---- deadline display ----
  protected daysLabel(days?: number): string {
    return (days ?? 0) <= 0 ? 'due today' : `${days}d left`;
  }
  protected daysWarn(days?: number): boolean {
    return (days ?? 0) > 1;
  }

  // ---- activity relative time ----
  protected timeAgo(iso?: string): string {
    if (!iso) {
      return '';
    }
    const minutes = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 60000));
    if (minutes < 1) return 'just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  }

  protected icon(name?: string): IconName {
    return (name || 'doc') as IconName;
  }

  // ---- navigation (clickable KPIs / rows deep-link to filtered lists) ----
  protected openByStatus(status: number): void {
    void this.router.navigate(['/appointments'], { queryParams: { appointmentStatus: status } });
  }
  protected openChangeRequests(): void {
    void this.router.navigateByUrl('/appointments/change-requests/reschedules');
  }
  protected openAppointments(): void {
    void this.router.navigateByUrl('/appointments');
  }
  protected openReports(): void {
    void this.router.navigateByUrl('/reports');
  }
  protected openDeadline(id?: string): void {
    if (id) {
      void this.router.navigate(['/appointments/view', id]);
    }
  }
  protected openTenants(): void {
    void this.router.navigateByUrl('/saas/tenants');
  }
  protected openDoctors(): void {
    void this.router.navigateByUrl('/doctor-management/doctors');
  }
}
