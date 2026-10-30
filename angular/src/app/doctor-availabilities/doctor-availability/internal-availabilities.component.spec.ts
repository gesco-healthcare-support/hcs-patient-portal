import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import { InternalAvailabilitiesComponent } from './internal-availabilities.component';
import { BookingStatus } from '../../proxy/enums/booking-status.enum';

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
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
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
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
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

  // The backdrop used to close the confirmation through its own (click) binding, which was
  // mouse-only; a document click listener does it now. Rendered and clicked for real.
  describe('and on a click on the backdrop', () => {
    function rendered() {
      const { fixture, cmp } = probe();
      fixture.detectChanges();
      cmp.confirmDay.set(aDay);
      fixture.detectChanges();
      return { cmp, root: fixture.nativeElement as HTMLElement };
    }

    it('closes the confirmation when the click lands on the backdrop', () => {
      const { cmp, root } = rendered();

      (root.querySelector('.ra-scrim') as HTMLElement).click();

      expect(cmp.confirmDay()).toBeNull();
    });

    it('leaves it open when the click lands inside the dialog', () => {
      const { cmp, root } = rendered();

      (root.querySelector('.ra-modal h3') as HTMLElement).click();

      expect(cmp.confirmDay()).not.toBeNull();
    });
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
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
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

/**
 * The status filter over the week grid, the selected-location label, and a location lookup
 * that returns nothing or fails.
 *
 * <p>Built without change detection, so `ngOnInit` runs only where a test calls it. Rows are
 * dated inside the component's own current week, read back from `weekDates()`, so the grid
 * buckets them whatever day the suite runs on.</p>
 *
 * <p>All identifiers below are synthetic.</p>
 */
describe('InternalAvailabilitiesComponent status filter and location lookup', () => {
  let getLocationLookup: jasmine.Spy;
  let getList: jasmine.Spy;

  interface Probe {
    [key: string]: any;
  }

  beforeEach(() => {
    getLocationLookup = jasmine.createSpy('getLocationLookup').and.returnValue(of({ items: [] }));
    getList = jasmine.createSpy('getList').and.returnValue(of({ items: [] }));

    TestBed.configureTestingModule({
      imports: [InternalAvailabilitiesComponent],
      providers: [
        {
          provide: DoctorAvailabilityService,
          useValue: {
            getLocationLookup,
            getList,
            getSlotPatientNames: () => of([]),
            delete: () => of(undefined),
            deleteByDate: () => of({ deletedCount: 0, skippedSlotIds: [] }),
          },
        },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
      ],
    });
  });

  function probe(): Probe {
    return TestBed.createComponent(InternalAvailabilitiesComponent)
      .componentInstance as unknown as Probe;
  }

  /** One slot on the first day of the component's current week. */
  function slotOn(
    c: Probe,
    id: string,
    fromTime: string,
    status: { bookingStatusId?: BookingStatus; remainingCapacity?: number },
  ) {
    const day: Date = c.weekDates()[0];
    const pad = (n: number) => String(n).padStart(2, '0');
    const iso = `${day.getFullYear()}-${pad(day.getMonth() + 1)}-${pad(day.getDate())}`;
    return { doctorAvailability: { id, availableDate: `${iso}T00:00:00`, fromTime, ...status } };
  }

  /** An open slot, a fully booked one and a reserved one, all on the same day. */
  function seededWeek(): Probe {
    const c = probe();
    c.rows.set([
      slotOn(c, 'open', '09:00', { remainingCapacity: 1 }),
      slotOn(c, 'booked', '10:00', { remainingCapacity: 0 }),
      slotOn(c, 'held', '11:00', { bookingStatusId: BookingStatus.Reserved }),
    ]);
    return c;
  }

  const shownIds = (c: Probe) => c.displayColumns()[0].slots.map((s: { id: string }) => s.id);

  it('shows only booked and reserved slots under the busy filter', () => {
    const c = seededWeek();
    c.statusFilter.set('busy');
    expect(shownIds(c)).toEqual(['booked', 'held']);
  });

  it('shows only the chosen status under any other filter', () => {
    const c = seededWeek();
    c.statusFilter.set('available');
    expect(shownIds(c)).toEqual(['open']);
  });

  it('keeps the day counts complete while a filter narrows the slots', () => {
    const c = seededWeek();
    c.statusFilter.set('available');
    expect(c.displayColumns()[0].total).toBe(3);
    expect(c.displayColumns()[0].busy).toBe(2);
  });

  it('names the selected location, and nothing while all locations are shown', () => {
    const c = probe();
    c.locations.set([
      { id: 'loc-1', name: 'Downtown' },
      { id: 'loc-2', name: 'Uptown' },
    ]);
    c.locationId.set('loc-2');
    expect(c.selectedLocationName()).toBe('Uptown');

    c.locationId.set('');
    expect(c.selectedLocationName()).toBe('');
  });

  it('stops loading without querying slots when there are no locations', () => {
    getLocationLookup.and.returnValue(of({}));
    const c = probe();

    c.ngOnInit();

    expect(c.loading()).toBeFalse();
    expect(getList).not.toHaveBeenCalled();
  });

  it('stops loading when the location lookup fails', () => {
    getLocationLookup.and.returnValue(throwError(() => ({ status: 500 })));
    const c = probe();

    c.ngOnInit();

    expect(c.loading()).toBeFalse();
    expect(getList).not.toHaveBeenCalled();
  });
});

/**
 * 2026-10-30 -- capacity on existing slots, down to 0 (closed to new requests).
 *
 * The per-day change is the assertion worth naming: it must reach only the SELECTED location's
 * slots that day, even when the loaded week holds another location's slot on the same day, and
 * the location travels with the request so the server can refuse anything else. A refusal shows
 * the server's own message in the dialog (which slot, how many booked), and the controls are not
 * rendered without the Edit permission.
 */
describe('InternalAvailabilitiesComponent capacity editing', () => {
  let getList: jasmine.Spy;
  let setCapacity: jasmine.Spy;
  let success: jasmine.Spy;
  let granted: boolean;

  interface Probe {
    [key: string]: any;
  }

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [InternalAvailabilitiesComponent],
      providers: [
        {
          provide: DoctorAvailabilityService,
          useValue: {
            getLocationLookup: () => of({ items: [{ id: 'loc-1', displayName: 'Downtown' }] }),
            getList,
            getSlotPatientNames: () => of([]),
            delete: () => of(undefined),
            deleteByDate: () => of({ deletedCount: 0, skippedSlotIds: [] }),
            setCapacity,
          },
        },
        { provide: ToasterService, useValue: { success, error: () => undefined } },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
        {
          provide: PermissionService,
          useValue: {
            getGrantedPolicy: (p: string) =>
              granted && p === 'CaseEvaluation.DoctorAvailabilities.Edit',
          },
        },
      ],
    });
  }

  beforeEach(() => {
    granted = true;
    getList = jasmine.createSpy('getList').and.returnValue(of({ items: [] }));
    setCapacity = jasmine
      .createSpy('setCapacity')
      .and.returnValue(of({ updatedCount: 1, capacity: 0 }));
    success = jasmine.createSpy('success');
  });

  function isoOf(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }

  function row(id: string, locationId: string, iso: string, fromTime: string, extra: object = {}) {
    return {
      doctorAvailability: {
        id,
        locationId,
        availableDate: `${iso}T00:00:00`,
        fromTime,
        toTime: fromTime.replace(':00', ':30'),
        capacity: 3,
        remainingCapacity: 3,
        concurrencyStamp: `stamp-${id}`,
        ...extra,
      },
    };
  }

  /** Renders the week for loc-1 with the given rows on its first day. */
  function rendered(rows: (iso: string) => unknown[]) {
    configure();
    const fixture = TestBed.createComponent(InternalAvailabilitiesComponent);
    const cmp = fixture.componentInstance as unknown as Probe;
    const iso = isoOf(cmp.weekDates()[0]);
    getList.and.returnValue(of({ items: rows(iso) }));
    fixture.detectChanges();
    cmp.onLocationChange('loc-1');
    fixture.detectChanges();
    return { fixture, cmp, iso, root: fixture.nativeElement as HTMLElement };
  }

  it('labels a slot by what is booked against its capacity, and a 0 slot as Closed', () => {
    const { root } = rendered((iso) => [
      row('a', 'loc-1', iso, '09:00', { remainingCapacity: 1 }),
      row('b', 'loc-1', iso, '10:00', { capacity: 0, remainingCapacity: 0 }),
    ]);

    const labels = Array.from(root.querySelectorAll('.av-slot .c')).map((e) =>
      e.textContent?.trim(),
    );
    expect(labels).toEqual(['Booked 2 of 3', 'Closed']);
    expect(root.querySelectorAll('.av-slot.closed').length).toBe(1);
  });

  it('changes one slot to 0 with its concurrency stamp, then reloads the week', () => {
    const { fixture, cmp, root } = rendered((iso) => [row('a', 'loc-1', iso, '09:00')]);

    (root.querySelector('.av-slot .x.cap') as HTMLElement).click();
    fixture.detectChanges();
    expect(cmp.capacityValue()).toBe(3);
    cmp.capacityValue.set(0);
    getList.calls.reset();
    cmp.saveCapacity();

    expect(setCapacity).toHaveBeenCalledTimes(1);
    const [input, config] = setCapacity.calls.mostRecent().args;
    expect(input).toEqual({
      slots: [{ id: 'a', concurrencyStamp: 'stamp-a' }],
      capacity: 0,
      locationId: 'loc-1',
    });
    expect(config).toEqual({ skipHandleError: true });
    expect(success).toHaveBeenCalledWith('1 slot(s) closed to new requests.');
    expect(cmp.capacityEdit()).toBeNull();
    expect(getList).toHaveBeenCalled();
  });

  it('sets a whole day only for the selected location, and names that location in the request', () => {
    // The week holds a slot of ANOTHER location on the same day (a stale row); it must not be sent.
    const { cmp, iso } = rendered((d) => [
      row('a', 'loc-1', d, '09:00'),
      row('b', 'loc-1', d, '10:00'),
      row('other', 'loc-2', d, '09:00'),
    ]);
    setCapacity.and.returnValue(of({ updatedCount: 2, capacity: 1 }));

    const col = cmp.columns().find((c: { iso: string }) => c.iso === iso);
    cmp.openDayCapacity(col);
    cmp.capacityValue.set(1);
    cmp.saveCapacity();

    const [input] = setCapacity.calls.mostRecent().args;
    expect(input.locationId).toBe('loc-1');
    expect(input.slots.map((s: { id: string }) => s.id)).toEqual(['a', 'b']);
    expect(input.capacity).toBe(1);
    expect(success).toHaveBeenCalledWith('Capacity set to 1 for 2 slot(s).');
  });

  it('does not open the per-day change in the All-locations view', () => {
    const { cmp, iso } = rendered((d) => [row('a', 'loc-1', d, '09:00')]);
    cmp.locationId.set('');

    cmp.openDayCapacity(cmp.columns().find((c: { iso: string }) => c.iso === iso));

    expect(cmp.capacityEdit()).toBeNull();
  });

  it('shows the server refusal in the dialog and keeps it open', () => {
    const message =
      'Capacity cannot be set to 0: the 9:00 AM slot on 12/04/2026 already has 2 active appointments.';
    setCapacity.and.returnValue(throwError(() => ({ status: 400, error: { error: { message } } })));
    const { fixture, cmp, root } = rendered((iso) => [row('a', 'loc-1', iso, '09:00')]);

    (root.querySelector('.av-slot .x.cap') as HTMLElement).click();
    cmp.capacityValue.set(0);
    cmp.saveCapacity();
    fixture.detectChanges();

    expect(cmp.capacityEdit()).not.toBeNull();
    expect(root.querySelector('.ia-capacity-error')?.textContent?.trim()).toBe(message);
    expect(cmp.isBusy()).toBeFalse();
  });

  it('explains a conflict when the slots changed after the week loaded', () => {
    setCapacity.and.returnValue(
      throwError(() => ({ status: 409, error: { error: { message: 'x' } } })),
    );
    const { cmp, iso } = rendered((d) => [row('a', 'loc-1', d, '09:00')]);

    cmp.openDayCapacity(cmp.columns().find((c: { iso: string }) => c.iso === iso));
    cmp.saveCapacity();

    expect(cmp.capacityError()).toContain('reload the week');
  });

  it('refuses a value outside 0 to 50 without calling the server', () => {
    const { cmp, iso } = rendered((d) => [row('a', 'loc-1', d, '09:00')]);
    cmp.openDayCapacity(cmp.columns().find((c: { iso: string }) => c.iso === iso));

    for (const bad of [-1, 51, 1.5, null]) {
      cmp.capacityValue.set(bad);
      cmp.saveCapacity();
    }

    expect(setCapacity).not.toHaveBeenCalled();
  });

  it('renders no capacity controls without the Edit permission', () => {
    granted = false;
    const { root } = rendered((iso) => [row('a', 'loc-1', iso, '09:00')]);

    expect(root.querySelector('.av-slot .x.cap')).toBeNull();
    expect(root.querySelector('.ia-daycap')).toBeNull();
    // The delete controls are unchanged by this permission.
    expect(root.querySelector('.av-slot .x:not(.cap)')).not.toBeNull();
  });

  it('closes the dialog on Escape', () => {
    const { cmp, iso } = rendered((d) => [row('a', 'loc-1', d, '09:00')]);
    cmp.openDayCapacity(cmp.columns().find((c: { iso: string }) => c.iso === iso));

    cmp.onEscapeKey();

    expect(cmp.capacityEdit()).toBeNull();
  });
});
