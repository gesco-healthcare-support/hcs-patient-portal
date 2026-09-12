import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import { InternalAvailabilitiesComponent } from './internal-availabilities.component';

/**
 * Covers the "All locations" default (issue #1): on load the page should query
 * every location's slots (locationId omitted), a specific selection should scope
 * the query, and the location dropdown should carry an "All locations" option.
 */
describe('InternalAvailabilitiesComponent -- all-locations view', () => {
  let getListSpy: jasmine.Spy;

  beforeEach(() => {
    getListSpy = jasmine.createSpy('getList').and.returnValue(of({ items: [] }));
    const serviceMock = {
      getLocationLookup: jasmine.createSpy('getLocationLookup').and.returnValue(
        of({
          items: [
            { id: 'loc-1', displayName: 'Downtown' },
            { id: 'loc-2', displayName: 'Uptown' },
          ],
        }),
      ),
      getList: getListSpy,
      getSlotPatientNames: jasmine.createSpy('getSlotPatientNames').and.returnValue(of([])),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
      deleteByDate: jasmine
        .createSpy('deleteByDate')
        .and.returnValue(of({ deletedCount: 0, skippedSlotIds: [] })),
    };

    TestBed.configureTestingModule({
      imports: [InternalAvailabilitiesComponent],
      providers: [
        { provide: DoctorAvailabilityService, useValue: serviceMock },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(InternalAvailabilitiesComponent);
    fixture.detectChanges(); // triggers ngOnInit + first render
    return fixture;
  }

  it('defaults to "All locations" (empty locationId) on load', () => {
    const cmp = create().componentInstance as unknown as { locationId: () => string };
    expect(cmp.locationId()).toBe('');
  });

  it('omits locationId from getList while All locations is selected', () => {
    create();
    expect(getListSpy).toHaveBeenCalled();
    expect(getListSpy.calls.mostRecent().args[0].locationId).toBeUndefined();
  });

  it('sends the chosen locationId when a specific location is selected', () => {
    const cmp = create().componentInstance as unknown as {
      onLocationChange: (id: string) => void;
    };
    cmp.onLocationChange('loc-2');
    expect(getListSpy.calls.mostRecent().args[0].locationId).toBe('loc-2');
  });

  it('renders an "All locations" option plus one per location', () => {
    const el = create().nativeElement as HTMLElement;
    const options = el.querySelectorAll('#ia-location option');
    expect(options).toHaveSize(3);
    expect(options[0].textContent?.trim()).toBe('All locations');
  });
});

/**
 * Sweep #641. The delete-day confirmation had a click-to-dismiss scrim and nothing else, so
 * a keyboard-only user could open it and not get out.
 *
 * <p>Escape routes through `cancelDeleteDay` rather than clearing the signal itself, so the
 * isBusy guard is inherited. That matters: a delete already in flight must not be
 * dismissable, or the user is left looking at a page that is still mutating.</p>
 */
describe('InternalAvailabilitiesComponent Escape handling (sweep #641)', () => {
  interface Probe {
    confirmDay: { set(v: unknown): void; (): unknown };
    isBusy: { set(v: boolean): void };
    onEscapeKey(): void;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [InternalAvailabilitiesComponent],
      providers: [
        {
          provide: DoctorAvailabilityService,
          useValue: {
            getLocationLookup: () => of({ items: [] }),
            getList: () => of({ items: [] }),
            getSlotPatientNames: () => of([]),
            delete: () => of(undefined),
            deleteByDate: () => of({ deletedCount: 0, skippedSlotIds: [] }),
          },
        },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
      ],
    });
  });

  function probe() {
    const fixture = TestBed.createComponent(InternalAvailabilitiesComponent);
    return { fixture, cmp: fixture.componentInstance as unknown as Probe };
  }

  const aDay = { iso: '2026-06-15', dow: 'Mon', dayNum: 15 };

  it('closes the delete-day confirmation', () => {
    const { cmp } = probe();
    cmp.confirmDay.set(aDay);
    cmp.onEscapeKey();
    expect(cmp.confirmDay()).toBeNull();
  });

  it('does not dismiss a delete that is in flight', () => {
    const { cmp } = probe();
    cmp.confirmDay.set(aDay);
    cmp.isBusy.set(true);
    cmp.onEscapeKey();
    expect(cmp.confirmDay()).not.toBeNull();
  });

  it('is inert when nothing is open', () => {
    const { cmp } = probe();
    expect(() => cmp.onEscapeKey()).not.toThrow();
    expect(cmp.confirmDay()).toBeNull();
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    const { cmp } = probe();
    cmp.confirmDay.set(aDay);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(cmp.confirmDay()).toBeNull();
  });
});
