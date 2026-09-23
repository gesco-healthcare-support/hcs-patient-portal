import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
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

/**
 * The rest of the dashboard: the status donut, the intake trend chart, the deadline and
 * activity labels, and the tiles that deep-link into filtered lists.
 *
 * <p>None of it was covered. The render tests above use the HOST view, which draws none of
 * the clinic charts, and nothing called the handlers. The charts are computeds over the
 * loaded DTO, so they are driven by setting `data` directly rather than by rendering; the
 * component is created but never change-detected, so `ngOnInit` runs only where called.</p>
 *
 * <p>All figures below are synthetic.</p>
 */
describe('InternalDashboardComponent charts, labels and navigation', () => {
  let getDashboard: jasmine.Spy;
  let router: { navigate: jasmine.Spy; navigateByUrl: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  function create(): Probe {
    getDashboard = jasmine.createSpy('getDashboard').and.returnValue(of({} as DashboardDto));
    router = {
      navigate: jasmine.createSpy('navigate'),
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    };
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
        {
          provide: ConfigStateService,
          useValue: { getOne: (k: string) => (k === 'currentUser' ? { roles: [] } : null) },
        },
        { provide: Router, useValue: router },
      ],
    });
    return TestBed.createComponent(InternalDashboardComponent)
      .componentInstance as unknown as Probe;
  }

  /** Loads a partial DTO; every computed under test reads it through `?.` and `??`. */
  function withData(c: Probe, dto: Partial<DashboardDto>): Probe {
    c['data'].set(dto as DashboardDto);
    return c;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('clears the loading state when the dashboard fails to load', () => {
    const c = create();
    getDashboard.and.returnValue(throwError(() => new Error('boom')));

    c['ngOnInit']();

    expect(c['loading']()).toBeFalse();
    expect(c['data']()).toBeNull();
  });

  describe('the status donut', () => {
    it('labels and colours a known status', () => {
      const c = withData(create(), { statusBreakdown: [{ pill: 'NoShow', count: 2 }] });
      expect(c['donut']()).toEqual([
        { pill: 'NoShow', label: 'No Show', count: 2, color: 'var(--n-500)' },
      ]);
    });

    it('falls back to the raw key and a neutral colour for a status it does not know', () => {
      const c = withData(create(), { statusBreakdown: [{ pill: 'Archived', count: 1 }] });
      expect(c['donut']()[0].label).toBe('Archived');
      expect(c['donut']()[0].color).toBe('var(--n-300)');
    });

    it('reads a slice with no key or count as empty rather than undefined', () => {
      const c = withData(create(), { statusBreakdown: [{}] });
      expect(c['donut']()).toEqual([{ pill: '', label: '', count: 0, color: 'var(--n-300)' }]);
    });

    it('draws an empty ring when every count is zero', () => {
      const c = withData(create(), { statusBreakdown: [{ pill: 'Pending', count: 0 }] });
      expect(c['donutTotal']()).toBe(0);
      expect(c['donutGradient']()).toBe('conic-gradient(var(--n-100) 0deg 360deg)');
    });

    it('gives each status an arc proportional to its count', () => {
      const c = withData(create(), {
        statusBreakdown: [
          { pill: 'Approved', count: 3 },
          { pill: 'Pending', count: 1 },
        ],
      });
      expect(c['donutGradient']()).toBe(
        'conic-gradient(var(--green-500) 0deg 270deg, var(--st-pending-dot) 270deg 360deg)',
      );
    });
  });

  describe('the intake trend chart', () => {
    it('scales to the larger of intake and completions', () => {
      const c = withData(create(), {
        trend: [
          { count: 4, completedCount: 1 },
          { count: 2, completedCount: 6 },
        ],
      });
      expect(c['trendMax']()).toBe(6);
      expect(c['trendMid']()).toBe(3);
    });

    it('never scales to zero, so an empty chart does not divide by zero', () => {
      const c = withData(create(), { trend: [{}] });
      expect(c['trendMax']()).toBe(1);
      expect(c['barHeight'](undefined)).toBe('0%');
    });

    it('sizes a bar as a share of the scale', () => {
      const c = withData(create(), { trend: [{ count: 4 }] });
      expect(c['barHeight'](2)).toBe('50%');
    });

    it('plots each completion at its column centre, inverted so the maximum is at the top', () => {
      const c = withData(create(), {
        trend: [
          { count: 4, completedCount: 4 },
          { count: 4, completedCount: 2 },
        ],
      });
      expect(c['trendLine']()).toEqual([
        { x: 25, y: 0, count: 4 },
        { x: 75, y: 50, count: 2 },
      ]);
      expect(c['trendLinePath']()).toBe('25,0 75,50');
    });

    it('plots a missing completion count on the baseline', () => {
      const c = withData(create(), { trend: [{ count: 2 }] });
      expect(c['trendLine']()).toEqual([{ x: 50, y: 100, count: 0 }]);
    });
  });

  describe('deadline labels', () => {
    it('says due today for today, for an overdue deadline, and when the days are unknown', () => {
      const c = create();
      expect(c['daysLabel'](0)).toBe('due today');
      expect(c['daysLabel'](-2)).toBe('due today');
      expect(c['daysLabel'](undefined)).toBe('due today');
    });

    it('counts the days left otherwise', () => {
      expect(create()['daysLabel'](5)).toBe('5d left');
    });

    it('flags a deadline more than a day out, and not one a day or less out', () => {
      const c = create();
      expect(c['daysWarn'](2)).toBeTrue();
      expect(c['daysWarn'](1)).toBeFalse();
      expect(c['daysWarn'](undefined)).toBeFalse();
    });
  });

  describe('activity times', () => {
    const MINUTE = 60_000;
    /** An ISO timestamp this far before now; a negative value is in the future. */
    const ago = (ms: number) => new Date(Date.now() - ms).toISOString();

    it('is blank without a timestamp', () => {
      expect(create()['timeAgo'](undefined)).toBe('');
    });

    it('says just now inside the first minute, and for a clock that runs ahead', () => {
      const c = create();
      expect(c['timeAgo'](ago(10_000))).toBe('just now');
      expect(c['timeAgo'](ago(-5 * MINUTE))).toBe('just now');
    });

    it('counts minutes, then hours, then days', () => {
      // Each value sits well inside its band, so the few milliseconds between building the
      // timestamp and reading the clock cannot tip it into the next one.
      const c = create();
      expect(c['timeAgo'](ago(5 * MINUTE))).toBe('5m ago');
      expect(c['timeAgo'](ago(3 * 60 * MINUTE))).toBe('3h ago');
      expect(c['timeAgo'](ago(2 * 24 * 60 * MINUTE))).toBe('2d ago');
    });

    it('falls back to the document icon when an activity names none', () => {
      const c = create();
      expect(c['icon'](undefined)).toBe('doc');
      expect(c['icon']('')).toBe('doc');
      expect(c['icon']('calendar')).toBe('calendar');
    });
  });

  describe('deep links', () => {
    it('opens the appointment list filtered to a status', () => {
      const c = create();
      c['openByStatus'](2);
      expect(router.navigate).toHaveBeenCalledWith(['/appointments'], {
        queryParams: { appointmentStatus: 2 },
      });
    });

    for (const [method, url] of [
      ['openChangeRequests', '/appointments/change-requests/reschedules'],
      ['openAppointments', '/appointments'],
      ['openReports', '/reports'],
      ['openTenants', '/users/tenants'],
      ['openDoctors', '/doctor-management/doctors'],
    ]) {
      it(`${method} opens ${url}`, () => {
        const c = create();
        c[method]();
        expect(router.navigateByUrl).toHaveBeenCalledWith(url);
      });
    }

    it('opens the appointment behind a deadline', () => {
      const c = create();
      c['openDeadline']('appt-1');
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/view', 'appt-1']);
    });

    it('ignores a deadline row with no appointment id', () => {
      const c = create();
      c['openDeadline'](undefined);
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });
});
