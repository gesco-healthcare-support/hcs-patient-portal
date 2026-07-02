import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ConfigStateService } from '@abp/ng.core';
import { Router } from '@angular/router';
import { InternalDashboardComponent } from './internal-dashboard.component';
import { DashboardService } from '../proxy/dashboards/dashboard.service';
import { DashboardRange } from '../proxy/dashboards/dashboard-range.enum';
import type { DashboardDto } from '../proxy/dashboards/models';

/**
 * QA item 6 (D1): host period hero + range switcher. Logic tests exercise
 * setRange()'s reload-with-range behavior (cast past `protected`, mirroring the
 * internal-shell spec's approach); render tests confirm the switcher + the new
 * cross-practice Approved/Rejected tiles appear on host and stay hidden for intake.
 */
describe('InternalDashboardComponent host period hero (item 6-D1)', () => {
  let getDashboard: jasmine.Spy;

  function hostDto(overrides: Partial<DashboardDto> = {}): DashboardDto {
    return {
      isHost: true,
      totalTenants: 4,
      totalDoctors: 4,
      totalLocations: 7,
      totalAppointments: 42,
      pendingAcrossTenants: 5,
      approvedRequests: { value: 8, previousValue: 5 },
      rejectedRequests: { value: 2, previousValue: 3 },
      pendingRequests: { value: 0, previousValue: 0 },
      pendingChangeRequests: { value: 0, previousValue: 0 },
      deadlines: [],
      deadlineApproachingCount: 0,
      trend: [],
      statusBreakdown: [],
      todaySchedule: [],
      recentActivity: [],
      tenants: [],
      ...overrides,
    } as DashboardDto;
  }

  function configFor(roles: string[], tenant: unknown) {
    return {
      getOne: (key: string) =>
        key === 'currentUser' ? { roles } : key === 'currentTenant' ? tenant : null,
    };
  }

  function setup(roles: string[], tenant: unknown = null) {
    getDashboard = jasmine.createSpy('getDashboard').and.returnValue(of(hostDto()));
    TestBed.configureTestingModule({
      imports: [InternalDashboardComponent],
      providers: [
        {
          provide: DashboardService,
          useValue: {
            getDashboard,
            getTenantBreakdown: () => of({ items: [], totalCount: 0 }),
          },
        },
        { provide: ConfigStateService, useValue: configFor(roles, tenant) },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
      ],
    });
    return TestBed.createComponent(InternalDashboardComponent);
  }

  afterEach(() => TestBed.resetTestingModule());

  it('setRange reloads the dashboard with the chosen range', () => {
    const c = setup(['IT Admin']).componentInstance as any;
    getDashboard.calls.reset();
    c.setRange(DashboardRange.Quarter);
    expect(c.range()).toBe(DashboardRange.Quarter);
    expect(getDashboard).toHaveBeenCalledWith(DashboardRange.Quarter);
  });

  it('setRange to the current range is a no-op (no reload)', () => {
    const c = setup(['IT Admin']).componentInstance as any;
    getDashboard.calls.reset();
    c.setRange(DashboardRange.Week); // Week is the default
    expect(getDashboard).not.toHaveBeenCalled();
  });

  it('renders the switcher and the Approved/Rejected period tiles on the host view', () => {
    const fixture = setup(['IT Admin']);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.dh-tf')).toBeTruthy();
    const labels = Array.from(el.querySelectorAll('.dh-kpi__lbl')).map((n) =>
      n.textContent?.trim(),
    );
    expect(labels).toContain('Approved');
    expect(labels).toContain('Rejected');
    expect(labels).toContain('Total Practices');
  });

  it('hides the range switcher for intake staff', () => {
    const fixture = setup(['Intake Staff'], { id: 't1' });
    getDashboard.and.returnValue(of(hostDto({ isHost: false })));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.dh-tf')).toBeNull();
  });
});
