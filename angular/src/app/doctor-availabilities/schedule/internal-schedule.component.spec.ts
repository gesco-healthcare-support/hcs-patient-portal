import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import { InternalScheduleComponent } from './internal-schedule.component';

/**
 * Task 5 wiring: the location default (which differs deliberately from the
 * sibling availabilities grid), the inclusive/exclusive date-range conversion,
 * and chip-click routing.
 */
describe('InternalScheduleComponent', () => {
  let getScheduleSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;

  /** Reaches the component's protected/private members, as the sibling spec does. */
  interface ScheduleInternals {
    locationId: () => string;
    onLocationChange: (id: string) => void;
    onDatesSet: (info: { start: Date; end: Date }) => void;
    onEventClick: (info: { event: { id: string; extendedProps: Record<string, unknown> } }) => void;
    loadFailed: () => boolean;
    events: () => unknown[];
  }

  beforeEach(() => {
    getScheduleSpy = jasmine.createSpy('getSchedule').and.returnValue(of([]));
    navigateSpy = jasmine.createSpy('navigateByUrl').and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [InternalScheduleComponent],
      providers: [
        {
          provide: DoctorAvailabilityService,
          useValue: {
            getLocationLookup: jasmine.createSpy('getLocationLookup').and.returnValue(
              of({
                items: [
                  { id: 'loc-1', displayName: 'Downtown' },
                  { id: 'loc-2', displayName: 'Uptown' },
                ],
              }),
            ),
            getSchedule: getScheduleSpy,
          },
        },
        { provide: Router, useValue: { navigateByUrl: navigateSpy } },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(InternalScheduleComponent);
    fixture.detectChanges();
    return fixture;
  }

  function internals(fixture: { componentInstance: unknown }): ScheduleInternals {
    return fixture.componentInstance as ScheduleInternals;
  }

  it('defaults to the first location rather than all locations', () => {
    // The sibling grid defaults to "" (All locations); this screen must not,
    // because it renders patient names and the endpoint requires a location.
    expect(internals(create()).locationId()).toBe('loc-1');
  });

  it('renders one option per location and no all-locations option', () => {
    const el = create().nativeElement as HTMLElement;
    const options = el.querySelectorAll('#sched-location option');

    expect(options.length).toBe(2);
    expect(options[0].textContent?.trim()).toBe('Downtown');
  });

  it('converts FullCalendar exclusive end into an inclusive toDate', () => {
    const fixture = create();
    const cmp = internals(fixture);

    // FullCalendar reports Mon 3 Aug .. Mon 10 Aug for the week of the 3rd-9th.
    cmp.onDatesSet({ start: new Date(2026, 7, 3), end: new Date(2026, 7, 10) });
    fixture.detectChanges();

    const args = getScheduleSpy.calls.mostRecent().args[0];
    expect(args.fromDate).toBe('2026-08-03T00:00:00');
    expect(args.toDate).toBe('2026-08-09T00:00:00');
  });

  it('reloads for the newly chosen location', () => {
    const fixture = create();
    const cmp = internals(fixture);
    cmp.onDatesSet({ start: new Date(2026, 7, 3), end: new Date(2026, 7, 10) });
    fixture.detectChanges();

    cmp.onLocationChange('loc-2');
    fixture.detectChanges();

    expect(getScheduleSpy.calls.mostRecent().args[0].locationId).toBe('loc-2');
  });

  it('routes to the appointment when a chip is clicked', () => {
    const cmp = internals(create());

    cmp.onEventClick({
      event: { id: 'appt-9', extendedProps: { kind: 'appointment', appointmentId: 'appt-9' } },
    });

    expect(navigateSpy).toHaveBeenCalledWith('/appointments/view/appt-9');
  });

  it('ignores a click on a slot background band', () => {
    const cmp = internals(create());

    cmp.onEventClick({ event: { id: 'slot-1', extendedProps: { kind: 'slot' } } });

    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('surfaces a failed load instead of showing an empty calendar silently', () => {
    getScheduleSpy.and.returnValue(throwError(() => new Error('boom')));
    const fixture = create();
    const cmp = internals(fixture);

    cmp.onDatesSet({ start: new Date(2026, 7, 3), end: new Date(2026, 7, 10) });
    fixture.detectChanges();

    expect(cmp.loadFailed()).toBeTrue();
    expect(cmp.events().length).toBe(0);
  });
});
