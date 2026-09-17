import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalConfigurationComponent } from './internal-configuration.component';
import { ConfigSectionGateway } from './config-section.gateway';
import { AppointmentTypeFieldConfigService } from '../proxy/appointment-type-field-configs/appointment-type-field-config.service';
import { AppointmentTypeService } from '../proxy/appointment-types/appointment-type.service';
import type { ConfigRow } from './cf-config.util';

/**
 * The Configuration hub -- one component mounted at all five lookup routes.
 *
 * <p>It sat at 90 of 148 lines uncovered. An existing spec (sweep #653) covers the Escape
 * handler and `typesSummary`; neither is repeated here.</p>
 *
 * <p>The guarantee that matters most here is about NOT mutating what was loaded: `displayRows`
 * sorts a copy, because the signal it reads is the loaded row list and sorting it in place would
 * reorder the source every time a header is clicked -- so the unsorted state could never be
 * restored. A mutation pass proved that one directly.</p>
 *
 * <p>Its neighbour is weaker than it looks, and the test says so rather than implying otherwise.
 * `openEdit` also copies the document type's appointment-type array, but removing that copy
 * breaks nothing today, because `toggleAppointmentType` replaces the array instead of editing it
 * in place. The copy is defensive depth against that toggle changing, not a guard the current
 * code depends on.</p>
 *
 * <p>The delete guard is the other focus: the client refuses a system row or one still in use
 * before the server does, so the UI never presents an action it knows will 400 or 409.</p>
 *
 * <p>The route's `data` is a bare Subject, so nothing loads until a test enters a section.</p>
 *
 * <p>All lookup names, descriptions and identifiers below are synthetic.</p>
 */
describe('InternalConfigurationComponent surfaces', () => {
  let gateway: Record<string, jasmine.Spy>;
  let fieldConfigService: Record<string, jasmine.Spy>;
  let appointmentTypeService: { getList: jasmine.Spy };
  let toaster: { success: jasmine.Spy; warn: jasmine.Spy; error: jasmine.Spy };
  let routeData: Subject<Record<string, unknown>>;
  let granted: Set<string>;

  interface Probe {
    [key: string]: any;
  }

  const TYPES = 'CaseEvaluation.AppointmentTypes';
  const DOCTYPES = 'CaseEvaluation.AppointmentDocumentTypes';
  const FIELD_CONFIG = 'CaseEvaluation.CustomFields.Edit';

  function row(over: Partial<ConfigRow> = {}): ConfigRow {
    return {
      id: 'cfg-1',
      name: 'PQME',
      description: 'Panel QME',
      isSystem: false,
      usageCount: 0,
      ...over,
    } as ConfigRow;
  }

  function create(options: { policies?: string[] } = {}): Probe {
    routeData = new Subject();
    granted = new Set(options.policies ?? []);

    gateway = {
      list: jasmine.createSpy('list').and.returnValue(of([])),
      create: jasmine.createSpy('create').and.returnValue(of({})),
      update: jasmine.createSpy('update').and.returnValue(of({})),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
    };
    fieldConfigService = {
      getByAppointmentTypeId: jasmine.createSpy('getByAppointmentTypeId').and.returnValue(of([])),
      saveForAppointmentType: jasmine
        .createSpy('saveForAppointmentType')
        .and.returnValue(of(undefined)),
    };
    appointmentTypeService = {
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [] })),
    };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { data: routeData } },
        { provide: ConfigSectionGateway, useValue: gateway },
        { provide: AppointmentTypeFieldConfigService, useValue: fieldConfigService },
        { provide: AppointmentTypeService, useValue: appointmentTypeService },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.has(p) },
        },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(InternalConfigurationComponent)
      .componentInstance as unknown as Probe;
  }

  function enter(section?: string): void {
    routeData.next(section === undefined ? {} : { section });
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('section routing', () => {
    it('loads nothing until the route delivers a section', () => {
      create();
      expect(gateway['list']).not.toHaveBeenCalled();
    });

    it('defaults to appointment types when the route names no section', () => {
      const c = create();
      enter();
      expect(c.section()).toBe('types');
      expect(gateway['list']).toHaveBeenCalledWith('types');
    });

    it('adopts the section the route names', () => {
      const c = create();
      enter('states');
      expect(c.section()).toBe('states');
      expect(c.meta().key).toBe('states');
    });

    it('falls back to the first section for a key that is not one of the five', () => {
      const c = create();
      enter('nonsense');
      expect(c.meta().key).toBe('types');
    });

    it('closes an open field-config panel and modal when the section changes', () => {
      // Both belong to the section being left; a panel keyed by an appointment type id
      // means nothing on the States table.
      const c = create();
      enter('types');
      c.fcOpenId.set('cfg-1');
      c.form.set({ name: 'PQME' });

      enter('states');

      expect(c.fcOpenId()).toBeNull();
      expect(c.form()).toBeNull();
    });

    it('loads appointment-type options only for a section that offers them', () => {
      const c = create();
      enter('doctypes');
      expect(appointmentTypeService.getList).toHaveBeenCalled();
      expect(c.meta().hasAppointmentTypes).toBeTrue();
    });

    it('does not load them for a section that has no use for them', () => {
      const c = create();
      enter('types');
      expect(appointmentTypeService.getList).not.toHaveBeenCalled();
      expect(c.appointmentTypeOptions()).toEqual([]);
    });

    it('maps the appointment-type options it does load', () => {
      const c = create();
      appointmentTypeService.getList.and.returnValue(of({ items: [{ id: 't-1', name: 'AME' }] }));
      enter('doctypes');
      expect(c.appointmentTypeOptions()).toEqual([{ id: 't-1', name: 'AME' }]);
    });

    it('leaves the options empty when that lookup fails', () => {
      const c = create();
      appointmentTypeService.getList.and.returnValue(throwError(() => ({ status: 500 })));
      enter('doctypes');
      expect(c.appointmentTypeOptions()).toEqual([]);
    });
  });

  describe('loading the rows', () => {
    it('stores the rows and clears the loading flag', () => {
      const c = create();
      gateway['list'].and.returnValue(of([row()]));
      enter('types');
      expect(c.rows().length).toBe(1);
      expect(c.loading()).toBeFalse();
    });

    it('empties the table and still clears loading when the request fails', () => {
      const c = create();
      gateway['list'].and.returnValue(throwError(() => ({ status: 500 })));
      enter('types');
      expect(c.rows()).toEqual([]);
      expect(c.loading()).toBeFalse();
    });
  });

  describe('sorting', () => {
    it('reads each sortable column', () => {
      const c = create();
      enter('types');
      expect(c.sortValue(row({ name: 'PQME' }), 'name')).toBe('PQME');
      expect(c.sortValue(row({ description: 'Panel QME' }), 'description')).toBe('Panel QME');
      expect(c.sortValue(row({ usageCount: 4 }), 'usage')).toBe(4);
      expect(c.sortValue(row(), 'not-a-column')).toBeNull();
    });

    it('maps an absent description to an empty string and an absent count to null', () => {
      const c = create();
      enter('types');
      expect(c.sortValue(row({ description: null }), 'description')).toBe('');
      expect(c.sortValue(row({ usageCount: null }), 'usage')).toBeNull();
    });

    it('sorts a COPY, leaving the loaded rows in their original order', () => {
      /**
       * `rows` is the signal the load writes and the table reads. Sorting it in place
       * would permanently reorder the source on every header click, so the "unsorted"
       * state could never be restored.
       */
      const c = create();
      enter('types');
      const loaded = [row({ id: 'b', name: 'Beta' }), row({ id: 'a', name: 'Alpha' })];
      c.rows.set(loaded);

      c.onSort({ key: 'name', dir: 'asc' });

      expect(c.displayRows().map((r: ConfigRow) => r.id)).toEqual(['a', 'b']);
      expect(c.rows().map((r: ConfigRow) => r.id))
        .withContext('the loaded order must survive')
        .toEqual(['b', 'a']);
    });

    it('orders in both directions', () => {
      const c = create();
      enter('types');
      c.rows.set([row({ id: 'a', name: 'Alpha' }), row({ id: 'b', name: 'Beta' })]);

      c.onSort({ key: 'name', dir: 'desc' });
      expect(c.displayRows().map((r: ConfigRow) => r.id)).toEqual(['b', 'a']);
    });
  });

  describe('permission gating', () => {
    it('derives create, edit and delete from the active section policy', () => {
      const c = create({ policies: [`${TYPES}.Create`, `${TYPES}.Delete`] });
      enter('types');
      expect(c.canCreate()).toBeTrue();
      expect(c.canEdit()).toBeFalse();
      expect(c.canDelete()).toBeTrue();
    });

    it('re-derives them when the section changes', () => {
      const c = create({ policies: [`${TYPES}.Edit`] });
      enter('types');
      expect(c.canEdit()).toBeTrue();

      enter('doctypes');
      expect(c.canEdit()).toBeFalse();
    });

    it('expands the field-config panel on an appointment-type row when permitted', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.onRowClick(row({ id: 'cfg-1' }));
      expect(c.fcOpenId()).toBe('cfg-1');
      expect(c.form()).toBeNull();
    });

    it('opens the edit modal instead when field config is not permitted', () => {
      const c = create({ policies: [`${TYPES}.Edit`] });
      enter('types');
      c.onRowClick(row());
      expect(c.fcOpenId()).toBeNull();
      expect(c.form()).not.toBeNull();
    });

    it('opens the edit modal on a section that has no field config at all', () => {
      const c = create({ policies: [`${DOCTYPES}.Edit`] });
      enter('doctypes');
      c.onRowClick(row());
      expect(c.form()).not.toBeNull();
    });

    it('does nothing when the user may neither edit nor configure fields', () => {
      const c = create();
      enter('types');
      c.onRowClick(row());
      expect(c.form()).toBeNull();
      expect(c.fcOpenId()).toBeNull();
    });
  });

  describe('table helpers', () => {
    it('shows a dash when usage is not tracked for the section', () => {
      const c = create();
      enter('types');
      expect(c.usageLabel(row({ usageCount: null }))).toBe('--');
    });

    it('pluralises the usage noun on both sides of one', () => {
      const c = create();
      enter('types');
      expect(c.usageLabel(row({ usageCount: 1 }))).toBe('1 appointment');
      expect(c.usageLabel(row({ usageCount: 2 }))).toBe('2 appointments');
      expect(c.usageLabel(row({ usageCount: 0 }))).toBe('0 appointments');
    });

    it('locks delete for a system row or one still referenced', () => {
      const c = create();
      enter('types');
      expect(c.locked(row({ isSystem: true }))).toBeTrue();
      expect(c.locked(row({ usageCount: 3 }))).toBeTrue();
      expect(c.locked(row())).toBeFalse();
    });

    it('explains in the tooltip why delete is unavailable', () => {
      const c = create();
      enter('types');
      expect(c.deleteTitle(row({ isSystem: true }))).toBe('System -- locked');
      expect(c.deleteTitle(row({ usageCount: 3 }))).toBe('In use -- locked');
      expect(c.deleteTitle(row())).toBe('Delete');
    });
  });

  describe('the create and edit modal', () => {
    it('opens a blank, active draft for a new row', () => {
      const c = create();
      enter('types');
      c.openNew();
      expect(c.form()).toEqual({
        id: null,
        name: '',
        description: '',
        isActive: true,
        isSystem: false,
        appointmentTypeIds: [],
        appliesToAll: false,
      });
    });

    it('copies the row into the draft', () => {
      const c = create();
      enter('types');
      c.openEdit(row({ id: 'cfg-9', name: 'AME', concurrencyStamp: 'stamp-1' }));
      expect(c.form().id).toBe('cfg-9');
      expect(c.form().name).toBe('AME');
      expect(c.form().concurrencyStamp).toBe('stamp-1');
    });

    it('leaves the row untouched while its type list is edited', () => {
      /**
       * What this pins is the end state: editing the modal never reaches the loaded row,
       * so cancelling cannot leave the table showing changes that were never saved.
       *
       * It does NOT prove the `[...]` copy in openEdit by itself, and the comment here
       * previously claimed that it did. A mutation pass on 2026-09-17 removed that copy
       * and NO test failed -- because `toggleAppointmentType` replaces the array (filter
       * or spread) rather than editing it in place, so the row is safe either way today.
       * The copy is defensive depth: it is what keeps this true if that toggle ever
       * becomes an in-place edit. The paired mutation -- alias the row AND mutate inside
       * the toggle -- does fail this test, which is what shows the copy earns its place.
       */
      const c = create();
      enter('doctypes');
      const source = row({ appointmentTypeIds: ['t-1'] });
      c.openEdit(source);

      c.toggleAppointmentType('t-2');

      expect(c.form().appointmentTypeIds).toEqual(['t-1', 't-2']);
      expect(source.appointmentTypeIds).withContext('the row must be untouched').toEqual(['t-1']);
    });

    it('defaults an absent active flag to true and an absent type list to empty', () => {
      const c = create();
      enter('doctypes');
      c.openEdit(row({ isActive: undefined, appointmentTypeIds: undefined }));
      expect(c.form().isActive).toBeTrue();
      expect(c.form().appointmentTypeIds).toEqual([]);
      expect(c.form().appliesToAll).toBeFalse();
    });

    it('merges a patch into the open draft', () => {
      const c = create();
      enter('types');
      c.openNew();
      c.patch({ name: 'AME' });
      c.patch({ description: 'Agreed ME' });
      expect(c.form().name).toBe('AME');
      expect(c.form().description).toBe('Agreed ME');
    });

    it('ignores a patch when no modal is open', () => {
      const c = create();
      enter('types');
      expect(() => c.patch({ name: 'AME' })).not.toThrow();
      expect(c.form()).toBeNull();
    });

    it('reports which appointment types are ticked', () => {
      const c = create();
      enter('doctypes');
      c.openEdit(row({ appointmentTypeIds: ['t-1'] }));
      expect(c.isTypeSelected('t-1')).toBeTrue();
      expect(c.isTypeSelected('t-2')).toBeFalse();
    });

    it('reports nothing ticked when no modal is open', () => {
      const c = create();
      enter('doctypes');
      expect(c.isTypeSelected('t-1')).toBeFalse();
    });

    it('unticks a type that was already selected', () => {
      const c = create();
      enter('doctypes');
      c.openEdit(row({ appointmentTypeIds: ['t-1', 't-2'] }));
      c.toggleAppointmentType('t-1');
      expect(c.form().appointmentTypeIds).toEqual(['t-2']);
    });

    it('ignores a type toggle when no modal is open', () => {
      const c = create();
      enter('doctypes');
      expect(() => c.toggleAppointmentType('t-1')).not.toThrow();
    });
  });

  describe('saving', () => {
    it('requires a name', () => {
      const c = create();
      enter('types');
      c.openNew();
      c.patch({ name: '   ' });

      c.save();

      expect(gateway['create']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalledWith('Name is required.');
    });

    it('creates when the draft has no id', () => {
      const c = create();
      enter('types');
      c.openNew();
      c.patch({ name: 'AME' });

      c.save();

      expect(gateway['create']).toHaveBeenCalled();
      expect(gateway['create'].calls.mostRecent().args[0]).toBe('types');
      expect(gateway['update']).not.toHaveBeenCalled();
    });

    it('updates when the draft carries an id', () => {
      const c = create();
      enter('types');
      c.openEdit(row({ id: 'cfg-9' }));

      c.save();

      expect(gateway['update']).toHaveBeenCalled();
      expect(gateway['create']).not.toHaveBeenCalled();
    });

    it('closes the modal, reports and reloads on success', () => {
      const c = create();
      enter('types');
      gateway['list'].calls.reset();
      c.openEdit(row());

      c.save();

      expect(c.form()).toBeNull();
      expect(toaster.success).toHaveBeenCalled();
      expect(gateway['list']).toHaveBeenCalled();
      expect(c.isBusy()).toBeFalse();
    });

    it('keeps the modal open and releases the button when the save fails', () => {
      const c = create();
      enter('types');
      gateway['update'].and.returnValue(throwError(() => ({ status: 400 })));
      c.openEdit(row());

      c.save();

      expect(c.form()).not.toBeNull();
      expect(c.isBusy()).toBeFalse();
    });

    it('ignores a second save while the first is in flight', () => {
      const c = create();
      enter('types');
      c.openEdit(row());
      c.isBusy.set(true);

      c.save();

      expect(gateway['update']).not.toHaveBeenCalled();
    });

    it('does nothing when no modal is open', () => {
      const c = create();
      enter('types');
      c.save();
      expect(gateway['create']).not.toHaveBeenCalled();
    });
  });

  describe('deleting', () => {
    it('refuses a system row before the server has to', () => {
      const c = create();
      enter('types');
      c.tryDelete(row({ isSystem: true }));
      expect(gateway['delete']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalledWith("System appointment types can't be deleted.");
    });

    it('refuses a row still in use, and says how many references there are', () => {
      const c = create();
      enter('types');
      c.tryDelete(row({ usageCount: 3 }));
      expect(gateway['delete']).not.toHaveBeenCalled();
      expect(toaster.warn.calls.mostRecent().args[0]).toContain('3 appointment(s)');
    });

    it('deletes an unused, non-system row and reloads', () => {
      const c = create();
      enter('types');
      gateway['list'].calls.reset();

      c.tryDelete(row({ id: 'cfg-9', usageCount: 0 }));

      expect(gateway['delete']).toHaveBeenCalledWith('types', 'cfg-9');
      expect(toaster.success).toHaveBeenCalled();
      expect(gateway['list']).toHaveBeenCalled();
    });

    it('ignores a delete while another request is in flight', () => {
      const c = create();
      enter('types');
      c.isBusy.set(true);
      c.tryDelete(row());
      expect(gateway['delete']).not.toHaveBeenCalled();
    });

    it('releases the busy flag when the server refuses the delete', () => {
      // ABP surfaces the 400/409 itself; the button has to come back either way.
      const c = create();
      enter('types');
      gateway['delete'].and.returnValue(throwError(() => ({ status: 409 })));
      c.tryDelete(row());
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('the field-configuration panel', () => {
    it('opens a row and loads its saved configuration', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));
      expect(c.fcOpenId()).toBe('cfg-1');
      expect(fieldConfigService['getByAppointmentTypeId']).toHaveBeenCalledWith('cfg-1');
    });

    it('collapses the row that is already open without refetching', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));
      fieldConfigService['getByAppointmentTypeId'].calls.reset();

      c.toggleFieldConfig(row({ id: 'cfg-1' }));

      expect(c.fcOpenId()).toBeNull();
      expect(fieldConfigService['getByAppointmentTypeId']).not.toHaveBeenCalled();
    });

    it('switches to another row and loads that one', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));
      c.toggleFieldConfig(row({ id: 'cfg-2' }));
      expect(c.fcOpenId()).toBe('cfg-2');
    });

    it('falls back to the default field state when the configuration cannot be read', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      fieldConfigService['getByAppointmentTypeId'].and.returnValue(
        throwError(() => ({ status: 500 })),
      );

      c.toggleFieldConfig(row({ id: 'cfg-1' }));

      expect(c.fcOpenId()).withContext('the panel still opens').toBe('cfg-1');
      expect(Object.keys(c.fieldState()).length).toBeGreaterThan(0);
    });

    it('returns a neutral default for a field the state does not describe', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      expect(c.fieldOf('not-a-field')).toEqual({
        hidden: false,
        readOnly: false,
        required: false,
        defaultValue: '',
      });
    });

    it('patches one field and leaves its other flags alone', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));
      const key = Object.keys(c.fieldState())[0];

      c.setField(key, { required: true });

      expect(c.fieldOf(key).required).toBeTrue();
      expect(c.fieldOf(key).hidden).toBeFalse();
    });

    it('ignores a patch aimed at a field outside the catalog', () => {
      // The backend stores any name, but the catalog is the source of truth; a stale
      // key must not create a row that no booking form would honour.
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));

      c.setField('not-a-field', { required: true });

      expect(c.fieldState()['not-a-field']).toBeUndefined();
    });

    it('does not save without an open appointment type', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.saveFieldConfig();
      expect(fieldConfigService['saveForAppointmentType']).not.toHaveBeenCalled();
    });

    it('does not save while a save is already running', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));
      c.fcBusy.set(true);

      c.saveFieldConfig();

      expect(fieldConfigService['saveForAppointmentType']).not.toHaveBeenCalled();
    });

    it('saves the batch, reports it and closes the panel', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));
      const key = Object.keys(c.fieldState())[0];
      c.setField(key, { required: true });

      c.saveFieldConfig();

      expect(fieldConfigService['saveForAppointmentType']).toHaveBeenCalled();
      const [typeId, batch] = fieldConfigService['saveForAppointmentType'].calls.mostRecent().args;
      expect(typeId).toBe('cfg-1');
      expect(batch.length)
        .withContext('only fields that deviate from the default are sent')
        .toBe(1);
      expect(toaster.success).toHaveBeenCalledWith('Field configuration saved.');
      expect(c.fcOpenId()).toBeNull();
      expect(c.fcBusy()).toBeFalse();
    });

    it('sends nothing at all when no field deviates from the default', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));

      c.saveFieldConfig();

      const [, batch] = fieldConfigService['saveForAppointmentType'].calls.mostRecent().args;
      expect(batch).toEqual([]);
    });

    it('keeps the panel open and releases the button when the save fails', () => {
      const c = create({ policies: [FIELD_CONFIG] });
      enter('types');
      c.toggleFieldConfig(row({ id: 'cfg-1' }));
      fieldConfigService['saveForAppointmentType'].and.returnValue(
        throwError(() => ({ status: 500 })),
      );

      c.saveFieldConfig();

      expect(c.fcOpenId()).toBe('cfg-1');
      expect(c.fcBusy()).toBeFalse();
    });
  });
});
