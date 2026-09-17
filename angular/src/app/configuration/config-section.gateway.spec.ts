import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ConfigSectionGateway } from './config-section.gateway';
import { AppointmentTypeService } from '../proxy/appointment-types/appointment-type.service';
import { AppointmentStatusService } from '../proxy/appointment-statuses/appointment-status.service';
import { AppointmentDocumentTypeService } from '../proxy/appointment-document-types/appointment-document-type.service';
import { AppointmentLanguageService } from '../proxy/appointment-languages/appointment-language.service';
import { StateService } from '../proxy/states/state.service';
import type { ConfigFormState } from './cf-config.util';

/**
 * The gateway that maps a Configuration section to its generated proxy service, so the hub
 * component stays free of per-section CRUD branching.
 *
 * <p>It had NO spec and sat at 2 of 39 lines covered. It is a plain injectable -- no component,
 * no template, no change detection -- so it is exercised through `TestBed.inject` rather than
 * the component harness the rest of this tranche uses.</p>
 *
 * <p>What matters here is that FIVE sections each route to a different proxy and normalise a
 * different DTO into one shared row shape. A section wired to the wrong service, or dropping a
 * field its DTO carries, is invisible in the hub -- the table renders, just with the wrong data
 * in it. So every section is asserted against its own service, and the two sections with extra
 * fields (document types, states) are asserted on those fields specifically.</p>
 *
 * <p>All names, descriptions and identifiers below are synthetic.</p>
 */
describe('ConfigSectionGateway', () => {
  let types: Record<string, jasmine.Spy>;
  let statuses: Record<string, jasmine.Spy>;
  let docTypes: Record<string, jasmine.Spy>;
  let languages: Record<string, jasmine.Spy>;
  let states: Record<string, jasmine.Spy>;

  /** The page size every section list is asked for. */
  const PAGE = { maxResultCount: 200, skipCount: 0 };

  function crud(label: string): Record<string, jasmine.Spy> {
    return {
      getList: jasmine.createSpy(`${label}.getList`).and.returnValue(of({ items: [] })),
      create: jasmine.createSpy(`${label}.create`).and.returnValue(of({})),
      update: jasmine.createSpy(`${label}.update`).and.returnValue(of({})),
      delete: jasmine.createSpy(`${label}.delete`).and.returnValue(of(undefined)),
    };
  }

  function form(over: Partial<ConfigFormState> = {}): ConfigFormState {
    return {
      id: null,
      name: 'PQME',
      description: 'Panel QME',
      isActive: true,
      isSystem: false,
      appointmentTypeIds: [],
      appliesToAll: false,
      ...over,
    } as ConfigFormState;
  }

  function create(): ConfigSectionGateway {
    types = crud('types');
    statuses = crud('statuses');
    docTypes = crud('docTypes');
    languages = crud('languages');
    states = crud('states');

    TestBed.configureTestingModule({
      providers: [
        { provide: AppointmentTypeService, useValue: types },
        { provide: AppointmentStatusService, useValue: statuses },
        { provide: AppointmentDocumentTypeService, useValue: docTypes },
        { provide: AppointmentLanguageService, useValue: languages },
        { provide: StateService, useValue: states },
      ],
    });
    return TestBed.inject(ConfigSectionGateway);
  }

  /** Subscribe and return the emitted rows. */
  function rows(g: ConfigSectionGateway, section: never): Record<string, unknown>[] {
    let out: Record<string, unknown>[] = [];
    g.list(section).subscribe((r) => (out = r as unknown as Record<string, unknown>[]));
    return out;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('listing', () => {
    it('routes each section to its OWN proxy service', () => {
      /**
       * The whole point of the gateway. A section wired to the wrong service still renders
       * a table -- with another lookup entirely in it -- so each is asserted against its
       * own spy, and against the others NOT being called.
       */
      const g = create();

      g.list('types' as never).subscribe();
      expect(types['getList']).toHaveBeenCalledWith(PAGE);
      expect(statuses['getList']).not.toHaveBeenCalled();

      g.list('statuses' as never).subscribe();
      expect(statuses['getList']).toHaveBeenCalledWith(PAGE);

      g.list('doctypes' as never).subscribe();
      expect(docTypes['getList']).toHaveBeenCalledWith(PAGE);

      g.list('languages' as never).subscribe();
      expect(languages['getList']).toHaveBeenCalledWith(PAGE);

      g.list('states' as never).subscribe();
      expect(states['getList']).toHaveBeenCalledWith(PAGE);
    });

    it('normalises an appointment type, keeping its description', () => {
      const g = create();
      types['getList'].and.returnValue(
        of({
          items: [
            { id: 't-1', name: 'PQME', description: 'Panel QME', usageCount: 4, isSystem: true },
          ],
        }),
      );

      expect(rows(g, 'types' as never)).toEqual([
        { id: 't-1', name: 'PQME', description: 'Panel QME', usageCount: 4, isSystem: true },
      ]);
    });

    it('normalises a status, which carries no description', () => {
      const g = create();
      statuses['getList'].and.returnValue(
        of({ items: [{ id: 's-1', name: 'Pending', usageCount: 2, isSystem: false }] }),
      );

      expect(rows(g, 'statuses' as never)).toEqual([
        { id: 's-1', name: 'Pending', usageCount: 2, isSystem: false },
      ]);
    });

    it('carries the document-type extras the other sections do not have', () => {
      // isActive, appointmentTypeIds and appliesToAll exist only here; dropping them
      // would blank the Active chip and the type multi-select with no other symptom.
      const g = create();
      docTypes['getList'].and.returnValue(
        of({
          items: [
            {
              id: 'd-1',
              name: 'Medical Report',
              usageCount: 0,
              isSystem: false,
              isActive: false,
              appointmentTypeIds: ['t-1', 't-2'],
              appliesToAll: true,
            },
          ],
        }),
      );

      expect(rows(g, 'doctypes' as never)).toEqual([
        {
          id: 'd-1',
          name: 'Medical Report',
          usageCount: 0,
          isSystem: false,
          isActive: false,
          appointmentTypeIds: ['t-1', 't-2'],
          appliesToAll: true,
        },
      ]);
    });

    it('defaults a document type with no active flag to ACTIVE', () => {
      // Absent must not read as inactive: that would hide a live document type from
      // every booking form with nothing on screen to explain it.
      const g = create();
      docTypes['getList'].and.returnValue(of({ items: [{ id: 'd-1', name: 'Report' }] }));
      expect(rows(g, 'doctypes' as never)[0]['isActive']).toBeTrue();
    });

    it('defaults an absent document-type list to empty rather than undefined', () => {
      const g = create();
      docTypes['getList'].and.returnValue(of({ items: [{ id: 'd-1', name: 'Report' }] }));
      expect(rows(g, 'doctypes' as never)[0]['appointmentTypeIds']).toEqual([]);
      expect(rows(g, 'doctypes' as never)[0]['appliesToAll']).toBeFalse();
    });

    it('carries the concurrency stamp for states, which are the only versioned section', () => {
      const g = create();
      states['getList'].and.returnValue(
        of({ items: [{ id: 'st-1', name: 'California', concurrencyStamp: 'stamp-1' }] }),
      );
      expect(rows(g, 'states' as never)[0]['concurrencyStamp']).toBe('stamp-1');
    });

    it('substitutes empty strings and null counts for a half-populated row', () => {
      const g = create();
      types['getList'].and.returnValue(of({ items: [{}] }));
      expect(rows(g, 'types' as never)).toEqual([
        { id: '', name: '', description: '', usageCount: null, isSystem: false },
      ]);
    });

    it('treats a payload with no items as an empty section', () => {
      const g = create();
      [types, statuses, docTypes, languages, states].forEach((s) =>
        s['getList'].and.returnValue(of({})),
      );
      (['types', 'statuses', 'doctypes', 'languages', 'states'] as const).forEach((section) => {
        expect(rows(g, section as never)).toEqual([]);
      });
    });

    it('coerces a truthy system flag to a real boolean', () => {
      // isSystem drives the delete lock, so a truthy-but-not-true value must still lock.
      const g = create();
      types['getList'].and.returnValue(of({ items: [{ id: 't-1', isSystem: 1 }] }));
      expect(rows(g, 'types' as never)[0]['isSystem']).toBeTrue();
    });
  });

  describe('creating', () => {
    it('routes each section to its own create', () => {
      const g = create();
      g.create('types' as never, form()).subscribe();
      expect(types['create']).toHaveBeenCalled();

      g.create('statuses' as never, form()).subscribe();
      expect(statuses['create']).toHaveBeenCalled();

      g.create('doctypes' as never, form()).subscribe();
      expect(docTypes['create']).toHaveBeenCalled();

      g.create('languages' as never, form()).subscribe();
      expect(languages['create']).toHaveBeenCalled();

      g.create('states' as never, form()).subscribe();
      expect(states['create']).toHaveBeenCalled();
    });

    it('trims the name for every section', () => {
      const g = create();
      g.create('statuses' as never, form({ name: '  Pending  ' })).subscribe();
      expect(statuses['create']).toHaveBeenCalledWith({ name: 'Pending' });
    });

    it('sends a description only for appointment types, trimmed', () => {
      const g = create();
      g.create('types' as never, form({ description: '  Panel QME  ' })).subscribe();
      expect(types['create']).toHaveBeenCalledWith({ name: 'PQME', description: 'Panel QME' });
    });

    it('sends a NULL description rather than an empty string', () => {
      // An empty string is a value the backend stores; null is its absence.
      const g = create();
      g.create('types' as never, form({ description: '   ' })).subscribe();
      expect(types['create']).toHaveBeenCalledWith({ name: 'PQME', description: null });
    });

    it('sends only the name for the three simple sections', () => {
      const g = create();
      g.create('languages' as never, form()).subscribe();
      expect(languages['create']).toHaveBeenCalledWith({ name: 'PQME' });

      g.create('states' as never, form()).subscribe();
      expect(states['create']).toHaveBeenCalledWith({ name: 'PQME' });
    });

    it('sends an EMPTY type list when a document type applies to all', () => {
      /**
       * Sending both "applies to all" and a specific list would be contradictory, and
       * which one the server honours is not something the UI should be betting on.
       */
      const g = create();
      g.create(
        'doctypes' as never,
        form({ appliesToAll: true, appointmentTypeIds: ['t-1', 't-2'] }),
      ).subscribe();

      expect(docTypes['create']).toHaveBeenCalledWith({
        name: 'PQME',
        isActive: true,
        appliesToAll: true,
        appointmentTypeIds: [],
      });
    });

    it('sends the chosen types when it does not apply to all', () => {
      const g = create();
      g.create(
        'doctypes' as never,
        form({ appliesToAll: false, appointmentTypeIds: ['t-1'] }),
      ).subscribe();

      expect(docTypes['create'].calls.mostRecent().args[0].appointmentTypeIds).toEqual(['t-1']);
    });
  });

  describe('updating', () => {
    it('routes each section to its own update, by id', () => {
      const g = create();
      const withId = form({ id: 'x-1' });

      g.update('types' as never, withId).subscribe();
      expect(types['update'].calls.mostRecent().args[0]).toBe('x-1');

      g.update('statuses' as never, withId).subscribe();
      expect(statuses['update'].calls.mostRecent().args[0]).toBe('x-1');

      g.update('doctypes' as never, withId).subscribe();
      expect(docTypes['update'].calls.mostRecent().args[0]).toBe('x-1');

      g.update('languages' as never, withId).subscribe();
      expect(languages['update'].calls.mostRecent().args[0]).toBe('x-1');

      g.update('states' as never, withId).subscribe();
      expect(states['update'].calls.mostRecent().args[0]).toBe('x-1');
    });

    it('carries the concurrency stamp on a state update, and only there', () => {
      // States are the versioned section; omitting the stamp turns an edit into a 409.
      const g = create();
      const withStamp = form({ id: 'st-1', concurrencyStamp: 'stamp-1' });

      g.update('states' as never, withStamp).subscribe();
      expect(states['update']).toHaveBeenCalledWith('st-1', {
        name: 'PQME',
        concurrencyStamp: 'stamp-1',
      });

      g.update('languages' as never, withStamp).subscribe();
      expect(languages['update']).toHaveBeenCalledWith('st-1', { name: 'PQME' });
    });

    it('applies the same applies-to-all rule as create', () => {
      const g = create();
      g.update(
        'doctypes' as never,
        form({ id: 'd-1', appliesToAll: true, appointmentTypeIds: ['t-1'] }),
      ).subscribe();

      expect(docTypes['update'].calls.mostRecent().args[1].appointmentTypeIds).toEqual([]);
    });

    it('trims the name and nulls a blank description on update too', () => {
      const g = create();
      g.update('types' as never, form({ id: 't-1', name: '  AME  ', description: '' })).subscribe();

      expect(types['update']).toHaveBeenCalledWith('t-1', { name: 'AME', description: null });
    });
  });

  describe('deleting', () => {
    it('routes each section to its own delete', () => {
      const g = create();

      g.delete('types' as never, 'x-1').subscribe();
      expect(types['delete']).toHaveBeenCalledWith('x-1');

      g.delete('statuses' as never, 'x-2').subscribe();
      expect(statuses['delete']).toHaveBeenCalledWith('x-2');

      g.delete('doctypes' as never, 'x-3').subscribe();
      expect(docTypes['delete']).toHaveBeenCalledWith('x-3');

      g.delete('languages' as never, 'x-4').subscribe();
      expect(languages['delete']).toHaveBeenCalledWith('x-4');

      g.delete('states' as never, 'x-5').subscribe();
      expect(states['delete']).toHaveBeenCalledWith('x-5');
    });

    it('does not touch any other section while deleting one', () => {
      const g = create();
      g.delete('languages' as never, 'x-1').subscribe();
      expect(types['delete']).not.toHaveBeenCalled();
      expect(statuses['delete']).not.toHaveBeenCalled();
      expect(docTypes['delete']).not.toHaveBeenCalled();
      expect(states['delete']).not.toHaveBeenCalled();
    });
  });
});
