import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalLocationsComponent } from './internal-locations.component';
import { LocationService } from '../../proxy/locations/location.service';

/**
 * Locations CRUD in the internal shell.
 *
 * <p>It sat at 56 of 89 lines uncovered. An existing spec (sweep #658) covers the Escape
 * handler, including the two-modal precedence; none of that is repeated here.</p>
 *
 * <p>The load-bearing part is the save body. The generated proxy types are STALE -- they still
 * declare a singular `appointmentTypeId` -- so the component builds the real body with
 * `appointmentTypeIds[]` and casts it. Nothing in the type system checks that any more, which
 * makes it exactly the kind of thing that breaks silently, so the body shape is pinned field by
 * field here. `displayRows` sorting a copy is the other one: the signal it reads is the loaded
 * row list.</p>
 *
 * <p>All location names, addresses and identifiers below are synthetic.</p>
 */
describe('InternalLocationsComponent surfaces', () => {
  let service: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; warn: jasmine.Spy; error: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  function row(over: Record<string, unknown> = {}) {
    return {
      location: {
        id: 'loc-1',
        name: 'Encino',
        facilityId: 'FAC-1',
        address: '1 Test Way',
        city: 'Encino',
        zipCode: '90000',
        stateId: 'st-1',
        parkingFee: 10,
        isActive: true,
        concurrencyStamp: 'stamp-1',
        ...((over['location'] as Record<string, unknown>) ?? {}),
      },
      state: { name: 'California' },
      appointmentTypes: [{ id: 't-1', name: 'AME' }],
      ...over,
    };
  }

  function create(): Probe {
    service = {
      getStateLookup: jasmine.createSpy('getStateLookup').and.returnValue(of({ items: [] })),
      getAppointmentTypeLookup: jasmine
        .createSpy('getAppointmentTypeLookup')
        .and.returnValue(of({ items: [] })),
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [] })),
      create: jasmine.createSpy('create').and.returnValue(of({})),
      update: jasmine.createSpy('update').and.returnValue(of({})),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
    };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: LocationService, useValue: service },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(InternalLocationsComponent)
      .componentInstance as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('initial load', () => {
    it('loads the two lookups and the list', () => {
      const c = create();
      c.ngOnInit();
      expect(service['getStateLookup']).toHaveBeenCalled();
      expect(service['getAppointmentTypeLookup']).toHaveBeenCalled();
      expect(service['getList']).toHaveBeenCalled();
    });

    it('maps the state lookup', () => {
      const c = create();
      service['getStateLookup'].and.returnValue(
        of({ items: [{ id: 'st-1', displayName: 'California' }] }),
      );
      c.ngOnInit();
      expect(c.states()).toEqual([{ id: 'st-1', name: 'California' }]);
    });

    it('maps the appointment-type lookup', () => {
      const c = create();
      service['getAppointmentTypeLookup'].and.returnValue(
        of({ items: [{ id: 't-1', displayName: 'AME' }] }),
      );
      c.ngOnInit();
      expect(c.allTypes()).toEqual([{ id: 't-1', name: 'AME' }]);
    });

    it('leaves a lookup empty when it fails rather than blocking the page', () => {
      const c = create();
      service['getStateLookup'].and.returnValue(throwError(() => ({ status: 500 })));
      service['getAppointmentTypeLookup'].and.returnValue(throwError(() => ({ status: 500 })));
      c.ngOnInit();
      expect(c.states()).toEqual([]);
      expect(c.allTypes()).toEqual([]);
    });

    it('substitutes empty strings for a lookup row missing its fields', () => {
      const c = create();
      service['getStateLookup'].and.returnValue(of({ items: [{}] }));
      c.ngOnInit();
      expect(c.states()).toEqual([{ id: '', name: '' }]);
    });

    it('stores the rows and clears the loading flag', () => {
      const c = create();
      service['getList'].and.returnValue(of({ items: [row()] }));
      c.ngOnInit();
      expect(c.rows().length).toBe(1);
      expect(c.loading()).toBeFalse();
    });

    it('empties the table and still clears loading when the list fails', () => {
      const c = create();
      service['getList'].and.returnValue(throwError(() => ({ status: 500 })));
      c.ngOnInit();
      expect(c.rows()).toEqual([]);
      expect(c.loading()).toBeFalse();
    });
  });

  describe('row display', () => {
    it('lists the appointment-type names', () => {
      const c = create();
      expect(c.typeNames(row())).toEqual(['AME']);
    });

    it('drops a type with no name rather than showing a blank chip', () => {
      const c = create();
      expect(
        c.typeNames(row({ appointmentTypes: [{ id: 't-1' }, { id: 't-2', name: 'QME' }] })),
      ).toEqual(['QME']);
    });

    it('returns nothing for a row with no types', () => {
      const c = create();
      expect(c.typeNames(row({ appointmentTypes: undefined }))).toEqual([]);
    });

    it('reads the state name from the nav property', () => {
      const c = create();
      expect(c.stateName(row())).toBe('California');
      expect(c.stateName(row({ state: undefined }))).toBe('');
    });
  });

  describe('sorting', () => {
    it('reads each sortable column', () => {
      const c = create();
      expect(c.sortValue(row(), 'name')).toBe('Encino');
      expect(c.sortValue(row(), 'address')).toBe('1 Test Way');
      expect(c.sortValue(row(), 'state')).toBe('California');
      expect(c.sortValue(row(), 'parkingFee')).toBe(10);
      expect(c.sortValue(row(), 'not-a-column')).toBeNull();
    });

    it('orders active before inactive by mapping status to a number', () => {
      const c = create();
      expect(c.sortValue(row({ location: { isActive: true } }), 'status')).toBe(1);
      expect(c.sortValue(row({ location: { isActive: false } }), 'status')).toBe(0);
    });

    it('maps absent text to an empty string and an absent fee to null', () => {
      const c = create();
      expect(c.sortValue(row({ location: { name: null } }), 'name')).toBe('');
      expect(c.sortValue(row({ location: { parkingFee: null } }), 'parkingFee')).toBeNull();
    });

    it('sorts a COPY, leaving the loaded rows in their original order', () => {
      const c = create();
      const loaded = [
        row({ location: { id: 'b', name: 'Beta' } }),
        row({ location: { id: 'a', name: 'Alpha' } }),
      ];
      c.rows.set(loaded);

      c.onSort({ key: 'name', dir: 'asc' });

      expect(c.displayRows().map((r: { location: { id: string } }) => r.location.id)).toEqual([
        'a',
        'b',
      ]);
      expect(c.rows().map((r: { location: { id: string } }) => r.location.id))
        .withContext('the loaded order must survive')
        .toEqual(['b', 'a']);
    });
  });

  describe('the form', () => {
    it('opens blank and active for a new location', () => {
      const c = create();
      c.openNew();
      expect(c.form().id).toBeNull();
      expect(c.form().name).toBe('');
      expect(c.form().isActive).toBeTrue();
      expect(c.form().typeIds).toEqual([]);
      expect(c.form().parkingFee).toBe(0);
    });

    it('opens an existing location from its nav-property row', () => {
      const c = create();
      c.openEdit(row());
      expect(c.form().id).toBe('loc-1');
      expect(c.form().name).toBe('Encino');
      expect(c.form().facilityId).toBe('FAC-1');
      expect(c.form().typeIds).toEqual(['t-1']);
      expect(c.form().concurrencyStamp).toBe('stamp-1');
    });

    it('defaults every absent field rather than carrying undefined into the inputs', () => {
      const c = create();
      c.openEdit({ location: {}, appointmentTypes: undefined });
      expect(c.form().name).toBe('');
      expect(c.form().parkingFee).toBe(0);
      expect(c.form().isActive).toBeTrue();
      expect(c.form().typeIds).toEqual([]);
    });

    it('merges a patch and ignores one with no form open', () => {
      const c = create();
      expect(() => c.patch({ name: 'x' })).not.toThrow();

      c.openNew();
      c.patch({ name: 'Encino' });
      c.patch({ city: 'Encino' });
      expect(c.form().name).toBe('Encino');
      expect(c.form().city).toBe('Encino');
    });

    it('toggles an appointment type on and off', () => {
      const c = create();
      c.openNew();
      expect(c.isTypeOn('t-1')).toBeFalse();

      c.toggleType('t-1');
      expect(c.isTypeOn('t-1')).toBeTrue();

      c.toggleType('t-1');
      expect(c.isTypeOn('t-1')).toBeFalse();
    });

    it('keeps the other selected types when one is removed', () => {
      const c = create();
      c.openNew();
      c.toggleType('t-1');
      c.toggleType('t-2');
      c.toggleType('t-1');
      expect(c.form().typeIds).toEqual(['t-2']);
    });

    it('ignores a type toggle with no form open', () => {
      const c = create();
      expect(() => c.toggleType('t-1')).not.toThrow();
      expect(c.isTypeOn('t-1')).toBeFalse();
    });

    it('refuses to close while a save is in flight', () => {
      const c = create();
      c.openNew();
      c.isBusy.set(true);
      c.closeModal();
      expect(c.form()).not.toBeNull();
    });
  });

  describe('saving', () => {
    function filled(c: Probe): void {
      c.openNew();
      c.patch({ name: 'Encino', facilityId: 'FAC-1' });
    }

    it('does nothing with no form open or while busy', () => {
      const c = create();
      c.save();
      expect(service['create']).not.toHaveBeenCalled();

      filled(c);
      c.isBusy.set(true);
      c.save();
      expect(service['create']).not.toHaveBeenCalled();
    });

    it('requires a name and a facility id, naming each', () => {
      const c = create();
      c.openNew();
      c.patch({ name: '   ', facilityId: 'FAC-1' });
      c.save();
      expect(toaster.warn).toHaveBeenCalledWith('Name is required.');
      expect(service['create']).not.toHaveBeenCalled();

      c.patch({ name: 'Encino', facilityId: '  ' });
      c.save();
      expect(toaster.warn).toHaveBeenCalledWith('Facility ID is required.');
      expect(service['create']).not.toHaveBeenCalled();
    });

    it('sends the appointment types as a LIST, which the proxy type no longer describes', () => {
      /**
       * The generated create/update types still declare a singular `appointmentTypeId`, so
       * the real body is built by hand and cast. Nothing type-checks this any more, which
       * is exactly why it is asserted rather than assumed.
       */
      const c = create();
      filled(c);
      c.toggleType('t-1');
      c.toggleType('t-2');

      c.save();

      const body = service['create'].calls.mostRecent().args[0];
      expect(body.appointmentTypeIds).toEqual(['t-1', 't-2']);
    });

    it('trims the name and facility id and nulls the blank optional fields', () => {
      const c = create();
      c.openNew();
      c.patch({ name: '  Encino  ', facilityId: '  FAC-1  ', address: '', city: '', zipCode: '' });

      c.save();

      const body = service['create'].calls.mostRecent().args[0];
      expect(body.name).toBe('Encino');
      expect(body.facilityId).toBe('FAC-1');
      expect(body.address).toBeNull();
      expect(body.city).toBeNull();
      expect(body.zipCode).toBeNull();
      expect(body.stateId).toBeNull();
    });

    it('coerces the parking fee to a number, and a non-numeric one to zero', () => {
      const c = create();
      filled(c);
      c.patch({ parkingFee: '15' });
      c.save();
      expect(service['create'].calls.mostRecent().args[0].parkingFee).toBe(15);

      // A successful save clears the form, so the second case needs its own one --
      // without this, patch() is a no-op and the assertion re-reads the FIRST call.
      filled(c);
      c.patch({ parkingFee: 'not-a-number' });
      c.save();
      expect(service['create'].calls.mostRecent().args[0].parkingFee).toBe(0);
    });

    it('creates when the form has no id', () => {
      const c = create();
      filled(c);
      c.save();
      expect(service['create']).toHaveBeenCalled();
      expect(service['update']).not.toHaveBeenCalled();
    });

    it('updates by id, carrying the concurrency stamp', () => {
      const c = create();
      c.openEdit(row());
      c.save();
      expect(service['update']).toHaveBeenCalled();
      const [id, body] = service['update'].calls.mostRecent().args;
      expect(id).toBe('loc-1');
      expect(body.concurrencyStamp).toBe('stamp-1');
    });

    it('closes, reports and reloads on success', () => {
      const c = create();
      filled(c);
      service['getList'].calls.reset();

      c.save();

      expect(toaster.success).toHaveBeenCalledWith('Location saved.');
      expect(c.form()).toBeNull();
      expect(service['getList']).toHaveBeenCalled();
      expect(c.isBusy()).toBeFalse();
    });

    it('keeps the form open and releases the button when the save fails', () => {
      const c = create();
      service['create'].and.returnValue(throwError(() => ({ status: 400 })));
      filled(c);

      c.save();

      expect(c.form()).not.toBeNull();
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('deleting', () => {
    it('asks first', () => {
      const c = create();
      c.askDelete(row());
      expect(c.confirmDelete()).not.toBeNull();
    });

    it('can be cancelled, but not while a delete is running', () => {
      const c = create();
      c.askDelete(row());
      c.isBusy.set(true);
      c.cancelDelete();
      expect(c.confirmDelete()).not.toBeNull();

      c.isBusy.set(false);
      c.cancelDelete();
      expect(c.confirmDelete()).toBeNull();
    });

    it('does nothing without a row to delete or while busy', () => {
      const c = create();
      c.confirmDeleteRow();
      expect(service['delete']).not.toHaveBeenCalled();

      c.askDelete(row());
      c.isBusy.set(true);
      c.confirmDeleteRow();
      expect(service['delete']).not.toHaveBeenCalled();
    });

    it('does nothing for a row whose location carries no id', () => {
      const c = create();
      c.askDelete({ location: {} });
      c.confirmDeleteRow();
      expect(service['delete']).not.toHaveBeenCalled();
    });

    it('deletes, reports and reloads', () => {
      const c = create();
      c.askDelete(row());
      service['getList'].calls.reset();

      c.confirmDeleteRow();

      expect(service['delete']).toHaveBeenCalledWith('loc-1');
      expect(toaster.success).toHaveBeenCalledWith('Location deleted.');
      expect(c.confirmDelete()).toBeNull();
      expect(service['getList']).toHaveBeenCalled();
    });

    it('closes the confirmation when the server refuses the delete', () => {
      // ABP surfaces the in-use guard message itself; leaving the dialog open would
      // stack a second message behind it with no way to tell which applies.
      const c = create();
      service['delete'].and.returnValue(throwError(() => ({ status: 409 })));
      c.askDelete(row());

      c.confirmDeleteRow();

      expect(c.confirmDelete()).toBeNull();
      expect(c.isBusy()).toBeFalse();
    });
  });
});
