import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import { SystemParametersService } from '../../proxy/system-parameters-controllers/system-parameters.service';
import { InternalGenerateSlotsComponent } from './internal-generate-slots.component';

/**
 * Sweep #641. This component had no spec, so the two things the sweep changed inside it had
 * nothing holding them: the earliest-date sort and the `setRange` ternary.
 *
 * <p>Both are small, and both are the kind of edit that looks obviously equivalent in review
 * and is not. The sort feeds the lead-time check that decides whether generation is allowed
 * at all, and `setRange` decides whether a blank duration override means "inherit" or zero.</p>
 */
describe('InternalGenerateSlotsComponent (sweep #641)', () => {
  interface Probe {
    mode: { set(v: string): void };
    selectedDates: { set(v: string[]): void };
    timeRanges: {
      set(v: unknown[]): void;
      (): { fromTime: string; toTime: string; durationOverride: number | null }[];
    };
    setRange(i: number, field: string, value: string): void;
    earliestGeneratedIso(): string | null;
  }

  function create() {
    TestBed.configureTestingModule({
      imports: [InternalGenerateSlotsComponent],
      providers: [
        {
          provide: DoctorAvailabilityService,
          useValue: {
            getLocationLookup: () => of({ items: [] }),
            getList: () => of({ items: [] }),
          },
        },
        { provide: SystemParametersService, useValue: { get: () => of({}) } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
      ],
    });
    const fixture = TestBed.createComponent(InternalGenerateSlotsComponent);
    return fixture.componentInstance as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('earliestGeneratedIso in pick mode', () => {
    it('returns the earliest picked day whatever order they were clicked in', () => {
      // The sweep made the sort comparator explicit (S2871). These are yyyy-mm-dd, where
      // lexicographic already IS chronological, so this pins that the change was a no-op.
      const c = create();
      c.mode.set('pick');
      c.selectedDates.set(['2026-06-20', '2026-06-02', '2026-06-15']);
      expect(c.earliestGeneratedIso()).toBe('2026-06-02');
    });

    it('does not mutate the picked-date order it was given', () => {
      // `.slice()` before `.sort()` matters: the array is a signal value the template reads.
      const c = create();
      const picked = ['2026-06-20', '2026-06-02'];
      c.mode.set('pick');
      c.selectedDates.set(picked);
      c.earliestGeneratedIso();
      expect(picked).toEqual(['2026-06-20', '2026-06-02']);
    });

    it('returns null when nothing is picked', () => {
      const c = create();
      c.mode.set('pick');
      c.selectedDates.set([]);
      expect(c.earliestGeneratedIso()).toBeNull();
    });

    it('sorts across a year boundary', () => {
      const c = create();
      c.mode.set('pick');
      c.selectedDates.set(['2027-01-04', '2026-12-28']);
      expect(c.earliestGeneratedIso()).toBe('2026-12-28');
    });
  });

  describe('setRange', () => {
    function seed(c: Probe) {
      c.timeRanges.set([
        { fromTime: '09:00', toTime: '12:00', durationOverride: null },
        { fromTime: '13:00', toTime: '17:00', durationOverride: 45 },
      ]);
    }

    it('writes a plain string field on the addressed row only', () => {
      const c = create();
      seed(c);
      c.setRange(1, 'fromTime', '14:00');
      expect(c.timeRanges()[1].fromTime).toBe('14:00');
      expect(c.timeRanges()[0].fromTime).withContext('other rows untouched').toBe('09:00');
    });

    it('converts a duration override to a number', () => {
      const c = create();
      seed(c);
      c.setRange(0, 'durationOverride', '30');
      expect(c.timeRanges()[0].durationOverride).toBe(30);
    });

    it('treats a cleared duration override as inherit, not as zero', () => {
      // The distinction the lifted ternary has to preserve: '' must become null so the
      // default duration applies. Number('') is 0, which would generate zero-length slots.
      const c = create();
      seed(c);
      c.setRange(1, 'durationOverride', '');
      expect(c.timeRanges()[1].durationOverride).toBeNull();
    });

    it('leaves every other field of the addressed row intact', () => {
      const c = create();
      seed(c);
      c.setRange(1, 'durationOverride', '20');
      expect(c.timeRanges()[1].fromTime).toBe('13:00');
      expect(c.timeRanges()[1].toTime).toBe('17:00');
    });

    it('is inert for an index that does not exist', () => {
      const c = create();
      seed(c);
      c.setRange(9, 'fromTime', '23:00');
      expect(c.timeRanges().map((r) => r.fromTime)).toEqual(['09:00', '13:00']);
    });
  });
});
