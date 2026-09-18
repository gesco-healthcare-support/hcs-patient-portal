import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { IntakeAssignmentsComponent } from './intake-assignments.component';
import { IntakeAssignmentsService } from '../proxy/host-operators/intake-assignments.service';

/**
 * Intake office-assignment management for IT Admin and the host Staff Supervisor.
 *
 * <p>It had NO spec and sat at 1 of 66 lines covered.</p>
 *
 * <p>These rows are not cosmetic: the component doc records that they are the deny-by-default
 * boundary the impersonation grant enforces, and that assigning eagerly provisions a shadow
 * intake user in the practice database while unassigning disables it. So the grouping is the
 * thing to hold -- the server returns one flat row per (staff, practice) pair and this component
 * groups them client-side. A row dropped or merged into the wrong staff member shows the wrong
 * person as having access to a practice, which is exactly the claim these rows exist to make.</p>
 *
 * <p>The constructor issues two lookups (`getAssignableOperators`, `getOfficeOptions`) that
 * subscribe with NO error handler. Their failure paths are therefore not exercised -- an
 * assertion there could only pass vacuously, because the error is asynchronous and nothing
 * throws synchronously to catch. Logged to the backlog. `getList` IS guarded and its failure
 * is tested.</p>
 *
 * <p>All staff names, emails and practice names below are synthetic.</p>
 */
describe('IntakeAssignmentsComponent', () => {
  let service: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let assignments: unknown[];

  interface Probe {
    [key: string]: any;
  }

  function row(over: Record<string, unknown> = {}) {
    return {
      operatorUserId: 'u-1',
      operatorName: 'Ada Lovelace',
      operatorEmail: 'ada@example.test',
      officeId: 'o-1',
      officeName: 'Encino',
      ...over,
    };
  }

  function create(options: { operators?: unknown[]; offices?: unknown[] } = {}): Probe {
    assignments = assignments ?? [];

    service = {
      getAssignableOperators: jasmine
        .createSpy('getAssignableOperators')
        .and.returnValue(of({ items: options.operators ?? [] })),
      getOfficeOptions: jasmine
        .createSpy('getOfficeOptions')
        .and.returnValue(of({ items: options.offices ?? [] })),
      getList: jasmine.createSpy('getList').and.callFake(() => of({ items: assignments })),
      assign: jasmine.createSpy('assign').and.returnValue(of({})),
      unassign: jasmine.createSpy('unassign').and.returnValue(of(undefined)),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };

    TestBed.configureTestingModule({
      providers: [
        { provide: IntakeAssignmentsService, useValue: service },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(IntakeAssignmentsComponent)
      .componentInstance as unknown as Probe;
  }

  afterEach(() => {
    assignments = [];
    TestBed.resetTestingModule();
  });

  describe('initial load', () => {
    it('loads the staff list, the practice list and the assignments', () => {
      create();
      expect(service['getAssignableOperators']).toHaveBeenCalled();
      expect(service['getOfficeOptions']).toHaveBeenCalled();
      expect(service['getList']).toHaveBeenCalled();
    });

    it('stores the two lookups', () => {
      const c = create({
        operators: [{ id: 'u-1', displayName: 'Ada Lovelace' }],
        offices: [{ id: 'o-1', displayName: 'Encino' }],
      });
      expect(c.operators().length).toBe(1);
      expect(c.offices().length).toBe(1);
    });

    it('treats a lookup with no items as empty', () => {
      // The constructor reads these once, so the empty case is the default fixture.
      const c = create();
      expect(c.operators()).toEqual([]);
      expect(c.offices()).toEqual([]);
    });

    it('empties the assignments when the list request fails', () => {
      // This one IS guarded, so its failure is safe to assert on.
      const c = create();
      service['getList'].and.returnValue(throwError(() => ({ status: 500 })));
      c.operatorId = 'u-1';
      c.officeId = 'o-1';
      c.assign();
      expect(c.staff()).toEqual([]);
    });
  });

  describe('grouping the flat assignment rows', () => {
    function withRows(rows: unknown[]): Probe {
      assignments = rows;
      return create();
    }

    it('gathers every practice under one staff member', () => {
      const c = withRows([
        row({ officeId: 'o-1', officeName: 'Encino' }),
        row({ officeId: 'o-2', officeName: 'Glendale' }),
      ]);

      expect(c.staff().length).toBe(1);
      expect(c.staff()[0].practices.map((p: { officeId: string }) => p.officeId)).toEqual([
        'o-1',
        'o-2',
      ]);
    });

    it('keeps separate staff separate', () => {
      const c = withRows([
        row({ operatorUserId: 'u-1', operatorName: 'Ada Lovelace' }),
        row({ operatorUserId: 'u-2', operatorName: 'Grace Hopper' }),
      ]);
      expect(c.staff().length).toBe(2);
    });

    it('DROPS a row with no staff id rather than inventing a group for it', () => {
      /**
       * A row with no operator cannot be attributed to anyone. Grouping it under an
       * empty key would render a nameless staff row that appears to hold access.
       */
      const c = withRows([row({ operatorUserId: null }), row({ operatorUserId: 'u-1' })]);

      expect(c.staff().length).toBe(1);
      expect(c.staff()[0].operatorUserId).toBe('u-1');
    });

    it('orders staff by name and their practices by practice name', () => {
      const c = withRows([
        row({ operatorUserId: 'u-2', operatorName: 'Zoe Zeta', officeName: 'Zulu' }),
        row({ operatorUserId: 'u-2', operatorName: 'Zoe Zeta', officeName: 'Alpha' }),
        row({ operatorUserId: 'u-1', operatorName: 'Ada Lovelace', officeName: 'Encino' }),
      ]);

      expect(c.staff().map((s: { operatorName: string }) => s.operatorName)).toEqual([
        'Ada Lovelace',
        'Zoe Zeta',
      ]);
      expect(c.staff()[1].practices.map((p: { officeName: string }) => p.officeName)).toEqual([
        'Alpha',
        'Zulu',
      ]);
    });

    it('substitutes empty strings for a row missing its names', () => {
      const c = withRows([row({ operatorName: null, operatorEmail: null, officeName: null })]);
      expect(c.staff()[0].operatorName).toBe('');
      expect(c.staff()[0].operatorEmail).toBe('');
      expect(c.staff()[0].practices[0].officeName).toBe('');
    });
  });

  describe('searching', () => {
    function seeded(): Probe {
      assignments = [
        row({
          operatorUserId: 'u-1',
          operatorName: 'Ada Lovelace',
          operatorEmail: 'ada@example.test',
          officeName: 'Encino',
        }),
        row({
          operatorUserId: 'u-2',
          operatorName: 'Grace Hopper',
          operatorEmail: 'grace@example.test',
          officeName: 'Glendale',
        }),
      ];
      return create();
    }

    it('returns everyone when nothing is typed', () => {
      const c = seeded();
      expect(c.staff().length).toBe(2);
    });

    it('matches on the staff name', () => {
      const c = seeded();
      c.search.set('hopper');
      expect(c.staff().map((s: { operatorUserId: string }) => s.operatorUserId)).toEqual(['u-2']);
    });

    it('matches on the staff email', () => {
      const c = seeded();
      c.search.set('ada@example.test');
      expect(c.staff().map((s: { operatorUserId: string }) => s.operatorUserId)).toEqual(['u-1']);
    });

    it('matches on a PRACTICE name, keeping the staff member who holds it', () => {
      // Searching by practice is how an admin answers "who can see Glendale".
      const c = seeded();
      c.search.set('glendale');
      expect(c.staff().map((s: { operatorUserId: string }) => s.operatorUserId)).toEqual(['u-2']);
    });

    it('ignores case and surrounding whitespace', () => {
      const c = seeded();
      c.search.set('   ENCINO   ');
      expect(c.staff().map((s: { operatorUserId: string }) => s.operatorUserId)).toEqual(['u-1']);
    });

    it('returns nobody when nothing matches', () => {
      const c = seeded();
      c.search.set('no-such-person');
      expect(c.staff()).toEqual([]);
    });
  });

  describe('expanding a staff row', () => {
    it('opens and closes one row', () => {
      const c = create();
      expect(c.isExpanded('u-1')).toBeFalse();

      c.toggleExpand('u-1');
      expect(c.isExpanded('u-1')).toBeTrue();

      c.toggleExpand('u-1');
      expect(c.isExpanded('u-1')).toBeFalse();
    });

    it('replaces the set rather than mutating it', () => {
      // OnPush reads this signal by reference; an in-place add would not repaint.
      const c = create();
      const before = c.expanded();
      c.toggleExpand('u-1');
      expect(c.expanded()).not.toBe(before);
    });

    it('keeps other rows open when one is toggled', () => {
      const c = create();
      c.toggleExpand('u-1');
      c.toggleExpand('u-2');
      c.toggleExpand('u-1');
      expect(c.isExpanded('u-2')).toBeTrue();
    });
  });

  describe('assigning a practice', () => {
    function ready(c: Probe): void {
      c.operatorId = 'u-1';
      c.officeId = 'o-1';
    }

    it('does nothing without both a staff member and a practice', () => {
      const c = create();
      c.assign();
      expect(service['assign']).not.toHaveBeenCalled();

      c.operatorId = 'u-1';
      c.assign();
      expect(service['assign']).not.toHaveBeenCalled();
    });

    it('does nothing while another write is running', () => {
      const c = create();
      ready(c);
      c.busy.set(true);
      c.assign();
      expect(service['assign']).not.toHaveBeenCalled();
    });

    it('sends both ids', () => {
      const c = create();
      ready(c);
      c.assign();
      expect(service['assign']).toHaveBeenCalledWith({ operatorUserId: 'u-1', officeId: 'o-1' });
    });

    it('clears the form and reloads on success', () => {
      const c = create();
      ready(c);
      service['getList'].calls.reset();

      c.assign();

      expect(toaster.success).toHaveBeenCalledWith('Staff assigned.');
      expect(c.operatorId).toBe('');
      expect(c.officeId).toBe('');
      expect(service['getList']).toHaveBeenCalled();
      expect(c.busy()).toBeFalse();
    });

    it('AUTO-EXPANDS the staff member just assigned, so the new practice is visible', () => {
      // Without this the row collapses back and the admin cannot see whether the
      // assignment they just made actually landed.
      const c = create();
      ready(c);
      expect(c.isExpanded('u-1')).toBeFalse();

      c.assign();

      expect(c.isExpanded('u-1')).toBeTrue();
    });

    it('expands the staff member who was assigned, not whoever is selected afterwards', () => {
      // The selects are cleared inside the same handler, so the id has to be captured
      // before that happens.
      const c = create();
      c.operatorId = 'u-9';
      c.officeId = 'o-1';

      c.assign();

      expect(c.isExpanded('u-9')).toBeTrue();
    });

    it('releases the button when the assign fails', () => {
      const c = create();
      ready(c);
      service['assign'].and.returnValue(throwError(() => ({ status: 409 })));

      c.assign();

      expect(c.busy()).toBeFalse();
    });
  });

  describe('unassigning a practice', () => {
    const staffRow = {
      operatorUserId: 'u-1',
      operatorName: 'Ada',
      operatorEmail: '',
      practices: [],
    };
    const practice = { officeId: 'o-1', officeName: 'Encino' };

    it('does nothing while another write is running', () => {
      const c = create();
      c.busy.set(true);
      c.unassign(staffRow, practice);
      expect(service['unassign']).not.toHaveBeenCalled();
    });

    it('does nothing when either id is missing', () => {
      const c = create();
      c.unassign({ ...staffRow, operatorUserId: '' }, practice);
      c.unassign(staffRow, { officeId: '', officeName: '' });
      expect(service['unassign']).not.toHaveBeenCalled();
    });

    it('sends both ids, reports and reloads', () => {
      const c = create();
      service['getList'].calls.reset();

      c.unassign(staffRow, practice);

      expect(service['unassign']).toHaveBeenCalledWith('u-1', 'o-1');
      expect(toaster.success).toHaveBeenCalledWith('Assignment removed.');
      expect(service['getList']).toHaveBeenCalled();
      expect(c.busy()).toBeFalse();
    });

    it('releases the button when the unassign fails', () => {
      const c = create();
      service['unassign'].and.returnValue(throwError(() => ({ status: 409 })));

      c.unassign(staffRow, practice);

      expect(c.busy()).toBeFalse();
    });
  });
});
