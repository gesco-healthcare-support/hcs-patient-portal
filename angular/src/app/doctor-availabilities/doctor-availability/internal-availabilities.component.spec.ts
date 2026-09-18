import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
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

/**
 * The deletes, the patient-name chips and the grid presentation helpers.
 *
 * A third describe rather than a sibling spec file, because this file already runs two
 * independent describes each with its own configureTestingModule -- adding a third is the
 * file's own idiom, and a sibling would duplicate the import block and the service double for
 * no benefit.
 *
 * The delete-day guard on locationId is the assertion worth naming: the button is hidden in
 * the "All locations" view, so the handler guards it too. A hidden control is not a guard, and
 * nothing else pins the handler's own check.
 */
describe('InternalAvailabilitiesComponent CRUD and presentation', () => {
  interface Signal<T> {
    (): T;
    set(value: T): void;
  }
  interface Probe {
    ngOnInit(): void;
    isBusy: Signal<boolean>;
    locationId: Signal<string>;
    weekOffset: Signal<number>;
    view: Signal<'grid' | 'table'>;
    patientNames: Signal<Record<string, string[]>>;
    confirmDay: Signal<unknown>;
    changeWeek(delta: number): void;
    setView(v: 'grid' | 'table'): void;
    toggleExpand(iso: string): void;
    isExpanded(iso: string): boolean;
    goGenerate(): void;
    deleteSlot(slotId: string): void;
    askDeleteDay(col: unknown): void;
    confirmDeleteDay(): void;
    utilPct(col: unknown): number;
    count(col: unknown, key: string): number;
    previewSlots(col: unknown): unknown[];
  }

  let getList: jasmine.Spy;
  let getSlotPatientNames: jasmine.Spy;
  let deleteSlotSpy: jasmine.Spy;
  let deleteByDate: jasmine.Spy;
  let navigateByUrl: jasmine.Spy;
  let success: jasmine.Spy;

  const slot = (id: string) => ({ doctorAvailability: { id } });

  beforeEach(() => {
    getList = jasmine.createSpy('getList').and.returnValue(of({ items: [] }));
    getSlotPatientNames = jasmine.createSpy('getSlotPatientNames').and.returnValue(of([]));
    deleteSlotSpy = jasmine.createSpy('delete').and.returnValue(of(undefined));
    deleteByDate = jasmine
      .createSpy('deleteByDate')
      .and.returnValue(of({ deletedCount: 0, skippedSlotIds: [] }));
    navigateByUrl = jasmine.createSpy('navigateByUrl').and.returnValue(Promise.resolve(true));
    success = jasmine.createSpy('success');

    TestBed.configureTestingModule({
      imports: [InternalAvailabilitiesComponent],
      providers: [
        {
          provide: DoctorAvailabilityService,
          useValue: {
            getLocationLookup: () => of({ items: [{ id: 'loc-1', displayName: 'Downtown' }] }),
            getList,
            getSlotPatientNames,
            delete: deleteSlotSpy,
            deleteByDate,
          },
        },
        { provide: ToasterService, useValue: { success, error: () => undefined } },
        { provide: Router, useValue: { navigateByUrl } },
      ],
    });
  });

  function probe(): Probe {
    return TestBed.createComponent(InternalAvailabilitiesComponent)
      .componentInstance as unknown as Probe;
  }

  const column = {
    iso: '2026-06-15',
    dow: 'Mon',
    dayNum: 15,
    total: 4,
    busy: 1,
    slots: [
      { statusKey: 'booked' },
      { statusKey: 'available' },
      { statusKey: 'available' },
      { statusKey: 'reserved' },
    ],
  };

  describe('patient-name chips', () => {
    it('asks for the names of every loaded slot', () => {
      getList.and.returnValue(of({ items: [slot('slot-1'), slot('slot-2')] }));
      probe().ngOnInit();
      expect(getSlotPatientNames).toHaveBeenCalledWith(['slot-1', 'slot-2']);
    });

    it('drops slots that have no id', () => {
      getList.and.returnValue(of({ items: [slot('slot-1'), { doctorAvailability: {} }, {}] }));
      probe().ngOnInit();
      expect(getSlotPatientNames).toHaveBeenCalledWith(['slot-1']);
    });

    it('does not call out at all when the week is empty', () => {
      getList.and.returnValue(of({ items: [] }));
      probe().ngOnInit();
      expect(getSlotPatientNames).not.toHaveBeenCalled();
    });

    it('keys the returned names by slot id', () => {
      getList.and.returnValue(of({ items: [slot('slot-1')] }));
      getSlotPatientNames.and.returnValue(of([{ slotId: 'slot-1', names: ['Example Patient'] }]));
      const cmp = probe();
      cmp.ngOnInit();
      expect(cmp.patientNames()).toEqual({ 'slot-1': ['Example Patient'] });
    });

    it('ignores rows with no slot id', () => {
      getList.and.returnValue(of({ items: [slot('slot-1')] }));
      getSlotPatientNames.and.returnValue(of([{ names: ['Example Patient'] }]));
      const cmp = probe();
      cmp.ngOnInit();
      expect(cmp.patientNames()).toEqual({});
    });

    it('empties the map when the names cannot be fetched', () => {
      getList.and.returnValue(of({ items: [slot('slot-1')] }));
      getSlotPatientNames.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.ngOnInit();
      expect(cmp.patientNames()).toEqual({});
    });

    it('empties rows and names when the week itself fails to load', () => {
      getList.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.ngOnInit();
      expect(cmp.patientNames()).toEqual({});
    });
  });

  describe('deleting a single slot', () => {
    it('does nothing without a slot id', () => {
      probe().deleteSlot('');
      expect(deleteSlotSpy).not.toHaveBeenCalled();
    });

    it('does nothing while another write is in flight', () => {
      const cmp = probe();
      cmp.isBusy.set(true);
      cmp.deleteSlot('slot-1');
      expect(deleteSlotSpy).not.toHaveBeenCalled();
    });

    // Positive control for the two refusals above.
    it('deletes the slot and reloads the week', () => {
      const cmp = probe();
      cmp.ngOnInit();
      getList.calls.reset();
      cmp.deleteSlot('slot-1');
      expect(deleteSlotSpy).toHaveBeenCalledWith('slot-1');
      expect(success).toHaveBeenCalledWith('Slot deleted.');
      expect(getList).toHaveBeenCalled();
    });

    it('releases the busy flag when the delete is refused by the backend', () => {
      deleteSlotSpy.and.returnValue(throwError(() => new Error('FK')));
      const cmp = probe();
      cmp.deleteSlot('slot-1');
      expect(cmp.isBusy()).toBeFalse();
    });
  });

  describe('deleting a whole day', () => {
    it('arms the confirmation with the chosen column', () => {
      const cmp = probe();
      cmp.askDeleteDay(column);
      expect(cmp.confirmDay()).toBe(column);
    });

    it('does nothing when no day is armed', () => {
      const cmp = probe();
      cmp.locationId.set('loc-1');
      cmp.confirmDeleteDay();
      expect(deleteByDate).not.toHaveBeenCalled();
    });

    it('does nothing while another write is in flight', () => {
      const cmp = probe();
      cmp.locationId.set('loc-1');
      cmp.askDeleteDay(column);
      cmp.isBusy.set(true);
      cmp.confirmDeleteDay();
      expect(deleteByDate).not.toHaveBeenCalled();
    });

    // Per-day delete targets ONE location. In the All-locations view the button is hidden,
    // but a hidden control is not a guard -- this pins the handler's own check.
    it('refuses in the All locations view', () => {
      const cmp = probe();
      cmp.locationId.set('');
      cmp.askDeleteDay(column);
      cmp.confirmDeleteDay();
      expect(deleteByDate).not.toHaveBeenCalled();
    });

    it('deletes the armed day for the selected location', () => {
      const cmp = probe();
      cmp.locationId.set('loc-1');
      cmp.askDeleteDay(column);
      cmp.confirmDeleteDay();
      expect(deleteByDate).toHaveBeenCalledWith({
        locationId: 'loc-1',
        availableDate: '2026-06-15T00:00:00',
      });
    });

    it('reports how many were deleted when none were kept', () => {
      deleteByDate.and.returnValue(of({ deletedCount: 3, skippedSlotIds: [] }));
      const cmp = probe();
      cmp.locationId.set('loc-1');
      cmp.askDeleteDay(column);
      cmp.confirmDeleteDay();
      expect(success).toHaveBeenCalledWith('3 slot(s) deleted.');
    });

    it('reports the kept slots when some were booked or reserved', () => {
      deleteByDate.and.returnValue(of({ deletedCount: 3, skippedSlotIds: ['a', 'b'] }));
      const cmp = probe();
      cmp.locationId.set('loc-1');
      cmp.askDeleteDay(column);
      cmp.confirmDeleteDay();
      expect(success).toHaveBeenCalledWith('3 slot(s) deleted; 2 kept (booked or reserved).');
    });

    it('treats a response with no counts as zero', () => {
      deleteByDate.and.returnValue(of({}));
      const cmp = probe();
      cmp.locationId.set('loc-1');
      cmp.askDeleteDay(column);
      cmp.confirmDeleteDay();
      expect(success).toHaveBeenCalledWith('0 slot(s) deleted.');
    });

    it('clears the confirmation and reloads on success', () => {
      const cmp = probe();
      cmp.ngOnInit();
      cmp.locationId.set('loc-1');
      cmp.askDeleteDay(column);
      getList.calls.reset();
      cmp.confirmDeleteDay();
      expect(cmp.confirmDay()).toBeNull();
      expect(getList).toHaveBeenCalled();
      expect(cmp.isBusy()).toBeFalse();
    });

    it('clears the confirmation without reloading when the delete fails', () => {
      deleteByDate.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.locationId.set('loc-1');
      cmp.askDeleteDay(column);
      getList.calls.reset();
      cmp.confirmDeleteDay();
      expect(cmp.confirmDay()).toBeNull();
      expect(getList).not.toHaveBeenCalled();
    });
  });

  describe('toolbar', () => {
    it('moves the week forward and reloads', () => {
      const cmp = probe();
      cmp.ngOnInit();
      getList.calls.reset();
      cmp.changeWeek(1);
      expect(cmp.weekOffset()).toBe(1);
      expect(getList).toHaveBeenCalled();
    });

    it('moves the week back', () => {
      const cmp = probe();
      cmp.changeWeek(-2);
      expect(cmp.weekOffset()).toBe(-2);
    });

    it('switches the view without reloading', () => {
      const cmp = probe();
      cmp.ngOnInit();
      getList.calls.reset();
      cmp.setView('table');
      expect(cmp.view()).toBe('table');
      expect(getList).not.toHaveBeenCalled();
    });

    it('navigates to the slot generator', () => {
      probe().goGenerate();
      expect(navigateByUrl).toHaveBeenCalledWith(
        '/doctor-management/doctor-availabilities/generate',
      );
    });
  });

  describe('grid presentation', () => {
    it('reports no utilisation for an empty day rather than dividing by zero', () => {
      expect(probe().utilPct({ ...column, total: 0, busy: 0 })).toBe(0);
    });

    it('reports utilisation as a rounded percentage', () => {
      expect(probe().utilPct(column)).toBe(25);
    });

    it('counts the slots of one status', () => {
      const cmp = probe();
      expect(cmp.count(column, 'available')).toBe(2);
      expect(cmp.count(column, 'booked')).toBe(1);
      expect(cmp.count(column, 'cancelled')).toBe(0);
    });

    it('starts collapsed and expands per day', () => {
      const cmp = probe();
      expect(cmp.isExpanded('2026-06-15')).toBeFalse();
      cmp.toggleExpand('2026-06-15');
      expect(cmp.isExpanded('2026-06-15')).toBeTrue();
      expect(cmp.isExpanded('2026-06-16')).toBeFalse();
    });

    it('collapses again on a second toggle', () => {
      const cmp = probe();
      cmp.toggleExpand('2026-06-15');
      cmp.toggleExpand('2026-06-15');
      expect(cmp.isExpanded('2026-06-15')).toBeFalse();
    });

    it('caps a collapsed day at the preview length', () => {
      const many = { ...column, slots: Array.from({ length: 20 }, () => ({ statusKey: 'open' })) };
      expect(probe().previewSlots(many)).toHaveSize(8);
    });

    it('shows every slot once the day is expanded', () => {
      const many = { ...column, slots: Array.from({ length: 20 }, () => ({ statusKey: 'open' })) };
      const cmp = probe();
      cmp.toggleExpand(column.iso);
      expect(cmp.previewSlots(many)).toHaveSize(20);
    });

    it('does not pad a day that has fewer slots than the cap', () => {
      expect(probe().previewSlots(column)).toHaveSize(4);
    });
  });
});
