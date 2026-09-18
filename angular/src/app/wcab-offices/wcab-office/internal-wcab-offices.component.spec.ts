import { TestBed } from '@angular/core/testing';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { WcabOfficeService } from '../../proxy/wcab-offices/wcab-office.service';
import { InternalWcabOfficesComponent } from './internal-wcab-offices.component';

/**
 * WCAB offices CRUD over WcabOfficeService.
 *
 * DELIBERATELY NOT COVERED HERE: the Escape handler and the two close methods it routes
 * through. reports/overlay-escape-keys.spec.ts already constructs this component and asserts
 * that behaviour under #628, which is why those lines already read as covered. Re-asserting
 * them here would add specs and no coverage, and would put the same guarantee in two places
 * where only one of them explains why it exists.
 *
 * The create-versus-update choice is asserted on the ARGUMENTS, never on the outcome. Both
 * branches produce an identical success, so an outcome assertion cannot tell the branch that
 * was taken from the one that was not.
 */
describe('InternalWcabOfficesComponent', () => {
  interface Signal<T> {
    (): T;
    set(value: T): void;
  }
  interface Probe {
    ngOnInit(): void;
    loading: Signal<boolean>;
    isBusy: Signal<boolean>;
    rows: Signal<unknown[]>;
    form: Signal<Record<string, unknown> | null>;
    confirmDelete: Signal<unknown>;
    openNew(): void;
    openEdit(row: unknown): void;
    patch(partial: Record<string, unknown>): void;
    save(): void;
    askDelete(row: unknown): void;
    confirmDeleteRow(): void;
  }

  let getList: jasmine.Spy;
  let create: jasmine.Spy;
  let update: jasmine.Spy;
  let remove: jasmine.Spy;
  let warn: jasmine.Spy;
  let success: jasmine.Spy;

  // A fully populated row, so the "carried through unchanged on update" promise in the
  // component doc has something to actually carry. Synthetic throughout.
  const office = {
    id: 'office-1',
    name: 'Example Board Office',
    abbreviation: 'EBO',
    address: '1 Example Way',
    city: 'Example City',
    zipCode: '00000',
    stateId: 'state-1',
    isActive: true,
    concurrencyStamp: 'stamp-1',
  };
  const row = { wcabOffice: office };

  beforeEach(() => {
    getList = jasmine.createSpy('getList').and.returnValue(of({ items: [row], totalCount: 1 }));
    create = jasmine.createSpy('create').and.returnValue(of(office));
    update = jasmine.createSpy('update').and.returnValue(of(office));
    remove = jasmine.createSpy('delete').and.returnValue(of(undefined));
    warn = jasmine.createSpy('warn');
    success = jasmine.createSpy('success');

    TestBed.configureTestingModule({
      imports: [InternalWcabOfficesComponent],
      providers: [
        {
          provide: WcabOfficeService,
          useValue: { getList, create, update, delete: remove },
        },
        { provide: ToasterService, useValue: { warn, success, error: () => undefined } },
        // The component's template imports ConfigRailComponent, which injects
        // PermissionService (config-rail.component.ts:40). Without this stub the real one is
        // constructed and drags in ConfigStateService -> RestService -> CORE_OPTIONS, which
        // no test module provides. Stubbing the head of that chain is enough; the rail's
        // RouterLink imports never instantiate because this fixture is never change-detected.
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
      ],
    });
  });

  function probe(): Probe {
    // Never change-detected: the template is irrelevant to every assertion below, and
    // rendering it would couple these specs to markup they do not describe.
    return TestBed.createComponent(InternalWcabOfficesComponent)
      .componentInstance as unknown as Probe;
  }

  describe('load', () => {
    it('asks for a single large page rather than paging', () => {
      probe().ngOnInit();
      expect(getList).toHaveBeenCalledWith({ maxResultCount: 200, skipCount: 0 });
    });

    it('publishes the returned rows and clears the loading flag', () => {
      const cmp = probe();
      cmp.ngOnInit();
      expect(cmp.rows()).toEqual([row]);
      expect(cmp.loading()).toBeFalse();
    });

    it('empties the rows when the request fails', () => {
      getList.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.ngOnInit();
      expect(cmp.rows()).toEqual([]);
    });

    it('clears the loading flag even when the request fails', () => {
      getList.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.ngOnInit();
      expect(cmp.loading()).toBeFalse();
    });
  });

  describe('opening the modal', () => {
    it('opens a blank active office for a new record', () => {
      const cmp = probe();
      cmp.openNew();
      expect(cmp.form()).toEqual({
        id: null,
        name: '',
        abbreviation: '',
        address: null,
        city: null,
        zipCode: null,
        stateId: null,
        isActive: true,
      });
    });

    it('loads every persisted field into the form, not just the two the modal shows', () => {
      const cmp = probe();
      cmp.openEdit(row);
      expect(cmp.form()).toEqual({
        id: 'office-1',
        name: 'Example Board Office',
        abbreviation: 'EBO',
        address: '1 Example Way',
        city: 'Example City',
        zipCode: '00000',
        stateId: 'state-1',
        isActive: true,
        concurrencyStamp: 'stamp-1',
      });
    });

    it('falls back to empty strings and nulls when the record is sparse', () => {
      const cmp = probe();
      cmp.openEdit({ wcabOffice: {} });
      expect(cmp.form()).toEqual({
        id: null,
        name: '',
        abbreviation: '',
        address: null,
        city: null,
        zipCode: null,
        stateId: null,
        isActive: true,
        concurrencyStamp: undefined,
      });
    });

    it('tolerates a row with no office at all', () => {
      const cmp = probe();
      expect(() => cmp.openEdit({})).not.toThrow();
      expect(cmp.form()?.['id']).toBeNull();
    });
  });

  describe('patch', () => {
    it('merges into the open form', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patch({ name: 'Renamed' });
      expect(cmp.form()?.['name']).toBe('Renamed');
      expect(cmp.form()?.['isActive']).toBeTrue();
    });

    it('does nothing when no form is open', () => {
      const cmp = probe();
      cmp.patch({ name: 'Renamed' });
      expect(cmp.form()).toBeNull();
    });
  });

  describe('save', () => {
    it('refuses with no form open', () => {
      const cmp = probe();
      cmp.save();
      expect(create).not.toHaveBeenCalled();
      expect(update).not.toHaveBeenCalled();
    });

    it('refuses while a save is already in flight', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patch({ name: 'A', abbreviation: 'B' });
      cmp.isBusy.set(true);
      cmp.save();
      expect(create).not.toHaveBeenCalled();
    });

    it('warns and does not call the service when the name is blank', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patch({ name: '   ', abbreviation: 'EBO' });
      cmp.save();
      expect(warn).toHaveBeenCalledWith('Name is required.');
      expect(create).not.toHaveBeenCalled();
    });

    it('warns and does not call the service when the code is blank', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patch({ name: 'Example Board Office', abbreviation: '   ' });
      cmp.save();
      expect(warn).toHaveBeenCalledWith('Code is required.');
      expect(create).not.toHaveBeenCalled();
    });

    it('creates with trimmed values when there is no id', () => {
      const cmp = probe();
      cmp.openNew();
      cmp.patch({ name: '  Example Board Office  ', abbreviation: '  EBO  ' });
      cmp.save();
      expect(create).toHaveBeenCalledWith({
        name: 'Example Board Office',
        abbreviation: 'EBO',
        address: null,
        city: null,
        zipCode: null,
        stateId: null,
        isActive: true,
      });
      expect(update).not.toHaveBeenCalled();
    });

    it('updates the edited id and carries the concurrency stamp', () => {
      const cmp = probe();
      cmp.openEdit(row);
      cmp.save();
      expect(update).toHaveBeenCalledWith('office-1', {
        name: 'Example Board Office',
        abbreviation: 'EBO',
        address: '1 Example Way',
        city: 'Example City',
        zipCode: '00000',
        stateId: 'state-1',
        isActive: true,
        concurrencyStamp: 'stamp-1',
      });
      expect(create).not.toHaveBeenCalled();
    });

    it('preserves the fields the simplified modal never shows', () => {
      const cmp = probe();
      cmp.openEdit(row);
      cmp.patch({ name: 'Renamed Office' });
      cmp.save();
      const body = update.calls.mostRecent().args[1] as Record<string, unknown>;
      expect(body['address']).toBe('1 Example Way');
      expect(body['stateId']).toBe('state-1');
    });

    it('closes the modal and reloads on success', () => {
      const cmp = probe();
      cmp.openEdit(row);
      getList.calls.reset();
      cmp.save();
      expect(success).toHaveBeenCalledWith('WCAB office saved.');
      expect(cmp.form()).toBeNull();
      expect(getList).toHaveBeenCalled();
    });

    it('releases the busy flag on success', () => {
      const cmp = probe();
      cmp.openEdit(row);
      cmp.save();
      expect(cmp.isBusy()).toBeFalse();
    });

    it('keeps the form open and releases the busy flag when the save fails', () => {
      update.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.openEdit(row);
      cmp.save();
      expect(cmp.form()).not.toBeNull();
      expect(cmp.isBusy()).toBeFalse();
    });
  });

  describe('delete', () => {
    it('arms the confirmation with the chosen row', () => {
      const cmp = probe();
      cmp.askDelete(row);
      expect(cmp.confirmDelete()).toBe(row);
    });

    it('does nothing when nothing is armed', () => {
      const cmp = probe();
      cmp.confirmDeleteRow();
      expect(remove).not.toHaveBeenCalled();
    });

    it('does nothing when the armed row has no id', () => {
      const cmp = probe();
      cmp.askDelete({ wcabOffice: { name: 'No id' } });
      cmp.confirmDeleteRow();
      expect(remove).not.toHaveBeenCalled();
    });

    it('refuses while another write is in flight', () => {
      const cmp = probe();
      cmp.askDelete(row);
      cmp.isBusy.set(true);
      cmp.confirmDeleteRow();
      expect(remove).not.toHaveBeenCalled();
    });

    // The positive control for the three refusals above: with the same component and the
    // same armed row, the only difference being the guard condition, the call IS made.
    it('deletes the armed office by id', () => {
      const cmp = probe();
      cmp.askDelete(row);
      cmp.confirmDeleteRow();
      expect(remove).toHaveBeenCalledWith('office-1');
    });

    it('clears the confirmation and reloads on success', () => {
      const cmp = probe();
      cmp.askDelete(row);
      getList.calls.reset();
      cmp.confirmDeleteRow();
      expect(success).toHaveBeenCalledWith('Office deleted.');
      expect(cmp.confirmDelete()).toBeNull();
      expect(getList).toHaveBeenCalled();
    });

    it('clears the confirmation and does not reload when the delete fails', () => {
      remove.and.returnValue(throwError(() => new Error('boom')));
      const cmp = probe();
      cmp.askDelete(row);
      getList.calls.reset();
      cmp.confirmDeleteRow();
      expect(cmp.confirmDelete()).toBeNull();
      expect(getList).not.toHaveBeenCalled();
      expect(cmp.isBusy()).toBeFalse();
    });
  });
});
