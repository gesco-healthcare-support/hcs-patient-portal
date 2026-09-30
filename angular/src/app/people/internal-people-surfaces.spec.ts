import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalPeopleComponent } from './internal-people.component';
import { PeopleSectionGateway } from './people-section.gateway';
import type { PersonRow } from './people.util';

/**
 * The People hub -- one component mounted at all four people routes (patients, applicant
 * attorneys, defense attorneys, claim examiners).
 *
 * <p>A spec already exists for this component, but it covers only the two things sweep #636
 * touched: the Escape handler and the date-of-birth sort key. The hub sat at 111 of 175 lines
 * uncovered, which is the gap this file closes. Nothing here duplicates that spec.</p>
 *
 * <p>What is pinned is the machinery a directory page goes wrong in quietly: which section is
 * active and what a section change resets, per-section permission gating, the search + filter +
 * sort pipeline, and the create/edit form round-trip. The last of those carries a rule that is
 * not cosmetic -- `formFromRow` must NEVER pre-fill the stored social-security number, because
 * the field is write-only and a blank tells the backend to preserve what it has. Pre-filling a
 * masked value would write the mask back over the real one.</p>
 *
 * <p>The route's `data` is a bare Subject, so the constructor's subscription does not fire until
 * a test pushes a section. That makes the initial load explicit rather than incidental.</p>
 *
 * <p>Built with `createComponent` because the constructor calls `takeUntilDestroyed()`, which
 * needs a real DestroyRef. Never change-detected, so the template does not render.</p>
 *
 * <p>All names, emails, addresses and dates below are synthetic.</p>
 */
describe('InternalPeopleComponent surfaces', () => {
  let gateway: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; warn: jasmine.Spy; error: jasmine.Spy };
  let router: { navigate: jasmine.Spy };
  let routeData: Subject<Record<string, unknown>>;
  let granted: Set<string>;

  interface Probe {
    [key: string]: any;
  }

  const PATIENTS = 'CaseEvaluation.Patients';
  const ATTORNEYS = 'CaseEvaluation.ApplicantAttorneys';
  const INVITE = 'CaseEvaluation.UserManagement.InviteExternalUser';

  function row(over: Partial<PersonRow> = {}): PersonRow {
    return {
      id: 'p-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phoneNumber: '555-0100',
      identityUserId: null,
      portal: 'none',
      ...over,
    } as PersonRow;
  }

  function create(options: { policies?: string[] } = {}): Probe {
    routeData = new Subject();
    granted = new Set(options.policies ?? []);

    gateway = {
      list: jasmine.createSpy('list').and.returnValue(of([])),
      activeInvitedEmails: jasmine.createSpy('activeInvitedEmails').and.returnValue(of([])),
      stateLookup: jasmine.createSpy('stateLookup').and.returnValue(of([])),
      languageLookup: jasmine.createSpy('languageLookup').and.returnValue(of([])),
      create: jasmine.createSpy('create').and.returnValue(of({})),
      update: jasmine.createSpy('update').and.returnValue(of({})),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
    };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
    };
    router = { navigate: jasmine.createSpy('navigate') };

    TestBed.configureTestingModule({
      providers: [
        { provide: ActivatedRoute, useValue: { data: routeData } },
        { provide: Router, useValue: router },
        { provide: PeopleSectionGateway, useValue: gateway },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.has(p) },
        },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(InternalPeopleComponent).componentInstance as unknown as Probe;
  }

  /** Deliver one route-data emission, which is what selects the section and loads. */
  function enter(section?: string): void {
    routeData.next(section === undefined ? {} : { section });
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('section routing', () => {
    it('loads both modal lookups once at construction', () => {
      create();
      expect(gateway['stateLookup']).toHaveBeenCalledTimes(1);
      expect(gateway['languageLookup']).toHaveBeenCalledTimes(1);
    });

    it('does not load a list until the route delivers a section', () => {
      // The constructor subscribes; a bare Subject emits nothing, so nothing loads yet.
      create();
      expect(gateway['list']).not.toHaveBeenCalled();
    });

    it('falls back to patients when the route carries no section', () => {
      const c = create();
      enter();
      expect(c.section()).toBe('patients');
      expect(gateway['list']).toHaveBeenCalledWith('patients');
    });

    it('adopts the section the route names', () => {
      const c = create();
      enter('da');
      expect(c.section()).toBe('da');
      expect(c.meta().key).toBe('da');
      expect(gateway['list']).toHaveBeenCalledWith('da');
    });

    it('falls back to the first section when the key is not one of the four', () => {
      const c = create();
      enter('not-a-section');
      expect(c.meta().key).toBe('patients');
    });

    it('resets the whole view when the section changes', () => {
      /**
       * Every one of these is per-section state. Carrying a patient filter or an open
       * edit modal across to Claim Examiners would apply a filter the new table has no
       * column for, or save a patient form against the wrong endpoint.
       */
      const c = create();
      enter('patients');
      c.search.set('lovelace');
      c.filters.set({ ...c.filters(), city: 'Encino' });
      c.showFilters.set(true);
      c.showCols.set(true);
      c.cols.set({ gender: true });
      c.detailRow.set(row());
      c.form.set({ firstName: 'Ada' });

      enter('ce');

      expect(c.search()).toBe('');
      expect(c.filters().city).toBe('');
      expect(c.showFilters()).toBeFalse();
      expect(c.showCols()).toBeFalse();
      expect(c.cols()).toEqual({});
      expect(c.detailRow()).toBeNull();
      expect(c.form()).toBeNull();
    });
  });

  describe('permission gating', () => {
    it('shows a rail section only when its policy is granted', () => {
      const c = create({ policies: [PATIENTS] });
      enter('patients');
      expect(c.canSee({ policy: PATIENTS })).toBeTrue();
      expect(c.canSee({ policy: ATTORNEYS })).toBeFalse();
    });

    it('derives create, edit and delete from the ACTIVE section policy', () => {
      const c = create({ policies: [`${PATIENTS}.Create`, `${PATIENTS}.Edit`] });
      enter('patients');
      expect(c.canCreate()).toBeTrue();
      expect(c.canEdit()).toBeTrue();
      expect(c.canDelete()).toBeFalse();
    });

    it('re-derives the action gates when the section changes', () => {
      // Intake can see Patients but not attorneys; the gates must follow the section
      // rather than latch whatever was granted on the first one.
      const c = create({ policies: [`${PATIENTS}.Edit`] });
      enter('patients');
      expect(c.canEdit()).toBeTrue();

      enter('aa');
      expect(c.canEdit()).toBeFalse();
    });
  });

  describe('row click', () => {
    it('opens the detail view on a patient row', () => {
      const c = create({ policies: [`${PATIENTS}.Edit`] });
      enter('patients');
      const r = row();
      c.onRowClick(r);
      expect(c.detailRow()).toBe(r);
      expect(c.form()).toBeNull();
    });

    it('opens the edit modal on a non-patient row when editing is allowed', () => {
      const c = create({ policies: [`${ATTORNEYS}.Edit`] });
      enter('aa');
      c.onRowClick(row({ firmName: 'Analytical Engines LLP' }));
      expect(c.form()).not.toBeNull();
      expect(c.detailRow()).toBeNull();
    });

    it('does nothing on a non-patient row without edit permission', () => {
      const c = create();
      enter('aa');
      c.onRowClick(row());
      expect(c.form()).toBeNull();
      expect(c.detailRow()).toBeNull();
    });

    it('refuses to open a detail view outside the patients section', () => {
      // The detail panel renders patient demographics, which no other section carries.
      const c = create();
      enter('ce');
      c.openDetail(row());
      expect(c.detailRow()).toBeNull();
    });

    it('closes the detail view', () => {
      const c = create();
      enter('patients');
      c.openDetail(row());
      c.closeDetail();
      expect(c.detailRow()).toBeNull();
    });
  });

  describe('search and filtering', () => {
    function seeded(): Probe {
      const c = create();
      enter('patients');
      c.rows.set([
        row({ id: 'a', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test' }),
        row({
          id: 'g',
          firstName: 'Grace',
          lastName: 'Hopper',
          email: 'grace@example.test',
          phoneNumber: '555-0199',
          city: 'Encino',
          firmName: 'Compiler Partners',
        }),
      ]);
      return c;
    }

    it('returns every row when nothing is searched or filtered', () => {
      const c = seeded();
      expect(c.displayRows().length).toBe(2);
    });

    it('matches on name, email, phone and firm alike', () => {
      const c = seeded();
      c.search.set('hopper');
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['g']);

      c.search.set('ada@example.test');
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['a']);

      c.search.set('555-0199');
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['g']);

      c.search.set('compiler');
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['g']);
    });

    it('matches a city filter case-insensitively and on a substring', () => {
      const c = seeded();
      c.patchFilter({ city: 'ENC' });
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['g']);
    });

    it('applies the search and the filters together, not either alone', () => {
      const c = seeded();
      c.search.set('grace');
      c.patchFilter({ city: 'Nowhere' });
      expect(c.displayRows()).toEqual([]);
    });

    it('filters on portal status', () => {
      const c = seeded();
      c.rows.set([row({ id: 'a', portal: 'linked' }), row({ id: 'b', portal: 'none' })]);
      c.patchFilter({ portal: 'linked' });
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['a']);
    });

    it('patchFilter changes one key and leaves the rest alone', () => {
      const c = seeded();
      c.patchFilter({ city: 'Encino' });
      c.patchFilter({ stateId: 'st-1' });
      expect(c.filters().city).toBe('Encino');
      expect(c.filters().stateId).toBe('st-1');
    });

    it('clearFilters empties the filters and closes the panel', () => {
      const c = seeded();
      c.patchFilter({ city: 'Encino' });
      c.showFilters.set(true);
      c.clearFilters();
      expect(c.filters().city).toBe('');
      expect(c.showFilters()).toBeFalse();
    });
  });

  describe('the sort key accessor', () => {
    /**
     * Every visible column maps to a sort key, and a key that returns the wrong field
     * sorts the table by something other than the header the user clicked. The
     * date-of-birth branch is covered by the sweep spec and is not repeated here.
     */
    const cases: [string, Partial<PersonRow>, unknown][] = [
      ['name', { lastName: 'Hopper' }, 'Hopper'],
      ['firm', { firmName: 'Compiler Partners' }, 'Compiler Partners'],
      ['email', { email: 'grace@example.test' }, 'grace@example.test'],
      ['phone', { phoneNumber: '555-0199' }, '555-0199'],
      ['cityState', { city: 'Encino' }, 'Encino'],
      ['language', { languageName: 'Spanish' }, 'Spanish'],
      ['gender', { genderId: 2 }, 2],
      ['street', { street: '1 Test Way' }, '1 Test Way'],
      ['zip', { zipCode: '90000' }, '90000'],
      ['interpreter', { interpreterVendorName: 'Vendor A' }, 'Vendor A'],
      ['apptNumber', { apptNumber: 'A-1' }, 'A-1'],
      ['portal', { portal: 'linked' }, 'linked'],
    ];

    cases.forEach(([key, over, expected]) => {
      it(`reads the ${key} column`, () => {
        const c = create();
        enter('patients');
        expect(c.sortValue(row(over), key)).toBe(expected);
      });
    });

    it('returns an empty string rather than null for an absent optional text field', () => {
      // Blank sorts last; null and '' are both blank to the comparator, but the empty
      // string keeps the accessor's return type uniform for text columns.
      const c = create();
      enter('patients');
      expect(c.sortValue(row({ firmName: null }), 'firm')).toBe('');
      expect(c.sortValue(row({ genderId: null }), 'gender')).toBeNull();
    });

    it('returns null for a column that has no sort key', () => {
      const c = create();
      enter('patients');
      expect(c.sortValue(row(), 'not-a-column')).toBeNull();
    });

    it('orders the displayed rows by the chosen key and direction', () => {
      const c = create();
      enter('patients');
      c.rows.set([row({ id: 'h', lastName: 'Hopper' }), row({ id: 'l', lastName: 'Lovelace' })]);

      c.onSort({ key: 'name', dir: 'asc' });
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['h', 'l']);

      c.onSort({ key: 'name', dir: 'desc' });
      expect(c.displayRows().map((r: PersonRow) => r.id)).toEqual(['l', 'h']);
    });
  });

  describe('the column chooser', () => {
    it('toggles a column on and back off', () => {
      const c = create();
      enter('patients');
      expect(c.colOn('gender')).toBeFalse();
      c.toggleCol('gender');
      expect(c.colOn('gender')).toBeTrue();
      c.toggleCol('gender');
      expect(c.colOn('gender')).toBeFalse();
    });

    it('leaves the other columns alone when one is toggled', () => {
      const c = create();
      enter('patients');
      c.toggleCol('gender');
      c.toggleCol('zip');
      expect(c.colOn('gender')).toBeTrue();
      expect(c.colOn('zip')).toBeTrue();
    });
  });

  describe('the derived filter dropdowns', () => {
    it('lists each state once, keyed by id', () => {
      const c = create();
      enter('patients');
      c.rows.set([
        row({ id: '1', stateId: 'st-1', stateName: 'California' }),
        row({ id: '2', stateId: 'st-1', stateName: 'California' }),
        row({ id: '3', stateId: 'st-2', stateName: 'Nevada' }),
      ]);
      expect(c.stateOptions()).toEqual([
        { id: 'st-1', name: 'California' },
        { id: 'st-2', name: 'Nevada' },
      ]);
    });

    it('skips a row whose id or name is missing', () => {
      // A half-populated nav property would otherwise produce a blank dropdown entry.
      const c = create();
      enter('patients');
      c.rows.set([
        row({ id: '1', stateId: 'st-1', stateName: null }),
        row({ id: '2', stateId: null, stateName: 'Orphan' }),
      ]);
      expect(c.stateOptions()).toEqual([]);
    });

    it('lists each language once', () => {
      const c = create();
      enter('patients');
      c.rows.set([
        row({ id: '1', appointmentLanguageId: 'lang-1', languageName: 'Spanish' }),
        row({ id: '2', appointmentLanguageId: 'lang-1', languageName: 'Spanish' }),
      ]);
      expect(c.languageOptions()).toEqual([{ id: 'lang-1', name: 'Spanish' }]);
    });
  });

  describe('loading the list', () => {
    it('stores the rows and clears the loading flag', () => {
      const c = create();
      gateway['list'].and.returnValue(of([row()]));
      enter('patients');
      expect(c.rows().length).toBe(1);
      expect(c.loading()).toBeFalse();
    });

    it('empties the table and still clears loading when the request fails', () => {
      // finalize() owns the flag, so a failed load must not leave the page spinning.
      const c = create();
      gateway['list'].and.returnValue(throwError(() => ({ status: 500 })));
      enter('patients');
      expect(c.rows()).toEqual([]);
      expect(c.loading()).toBeFalse();
    });
  });

  describe('resolving invited portal status', () => {
    it('does not call the invitation endpoint without the invite permission', () => {
      // The B3 endpoint is permission-gated; calling it unprivileged 403s on every load.
      const c = create();
      gateway['list'].and.returnValue(of([row()]));
      enter('patients');
      expect(gateway['activeInvitedEmails']).not.toHaveBeenCalled();
      expect(c.rows()[0].portal).toBe('none');
    });

    it('does not call it when no row is a candidate', () => {
      // Linked rows and rows without an email can never resolve to "invited".
      const c = create({ policies: [INVITE] });
      gateway['list'].and.returnValue(
        of([row({ id: 'a', identityUserId: 'u-1' }), row({ id: 'b', email: null })]),
      );
      enter('patients');
      expect(gateway['activeInvitedEmails']).not.toHaveBeenCalled();
    });

    it('asks about each candidate email once, lowercased and trimmed', () => {
      const c = create({ policies: [INVITE] });
      gateway['list'].and.returnValue(
        of([
          row({ id: 'a', email: '  Ada@Example.test ' }),
          row({ id: 'b', email: 'ada@example.test' }),
        ]),
      );
      enter('patients');
      expect(gateway['activeInvitedEmails']).toHaveBeenCalledWith(['ada@example.test']);
      expect(c).toBeTruthy();
    });

    it('upgrades a matching row to invited and leaves a linked row linked', () => {
      const c = create({ policies: [INVITE] });
      gateway['list'].and.returnValue(
        of([
          row({ id: 'a', email: 'ada@example.test' }),
          row({ id: 'g', email: 'grace@example.test' }),
          row({ id: 'u', email: 'yuri@example.test', identityUserId: 'u-1', portal: 'linked' }),
        ]),
      );
      gateway['activeInvitedEmails'].and.returnValue(of(['ADA@EXAMPLE.TEST']));
      enter('patients');

      const byId = new Map(c.rows().map((r: PersonRow) => [r.id, r.portal]));
      expect(byId.get('a')).withContext('matched, case-insensitively').toBe('invited');
      expect(byId.get('g')).withContext('no active invitation').toBe('none');
      expect(byId.get('u')).withContext('an attached account always wins').toBe('linked');
    });

    it('leaves the rows as loaded when the invitation lookup fails', () => {
      const c = create({ policies: [INVITE] });
      gateway['list'].and.returnValue(of([row({ email: 'ada@example.test' })]));
      gateway['activeInvitedEmails'].and.returnValue(throwError(() => ({ status: 500 })));
      enter('patients');
      expect(c.rows().length).toBe(1);
      expect(c.rows()[0].portal).toBe('none');
    });
  });

  describe('the create and edit form', () => {
    it('opens a blank form for a new person', () => {
      const c = create();
      enter('patients');
      c.openNew();
      expect(c.form().id).toBeNull();
      expect(c.form().firstName).toBe('');
      expect(c.form().socialSecurityNumber).toBe('');
    });

    it('NEVER pre-fills the stored social-security number when editing', () => {
      /**
       * The field is write-only: the gateway omits a blank SSN so the backend preserves
       * what it holds. Pre-filling here -- with a masked value or anything else -- would
       * send that string back and overwrite the real number on the next save.
       */
      const c = create();
      enter('patients');
      c.openEdit(row({ socialSecurityNumber: '***-**-0000' }));
      expect(c.form().socialSecurityNumber).toBe('');
    });

    it('truncates a stored timestamp to the date the date input expects', () => {
      const c = create();
      enter('patients');
      c.openEdit(row({ dateOfBirth: '1980-01-01T00:00:00Z' }));
      expect(c.form().dateOfBirth).toBe('1980-01-01');
    });

    it('carries the concurrency stamp and the identity link through untouched', () => {
      // The modal never edits either; dropping them turns an update into a 409 or
      // silently unlinks a portal account.
      const c = create();
      enter('patients');
      c.openEdit(row({ concurrencyStamp: 'stamp-1', identityUserId: 'u-1' }));
      expect(c.form().concurrencyStamp).toBe('stamp-1');
      expect(c.form().identityUserId).toBe('u-1');
    });

    it('maps an absent optional field to an empty string rather than null', () => {
      const c = create();
      enter('patients');
      c.openEdit(row({ middleName: null, city: null, dateOfBirth: null }));
      expect(c.form().middleName).toBe('');
      expect(c.form().city).toBe('');
      expect(c.form().dateOfBirth).toBe('');
    });

    it('refuses to close the modal while a save is in flight', () => {
      const c = create();
      enter('patients');
      c.openNew();
      c.isBusy.set(true);
      c.closeModal();
      expect(c.form()).not.toBeNull();
    });

    it('closes the modal when nothing is in flight', () => {
      const c = create();
      enter('patients');
      c.openNew();
      c.closeModal();
      expect(c.form()).toBeNull();
    });
  });

  describe('saving', () => {
    function draft(over: Record<string, unknown> = {}) {
      return {
        id: null,
        firstName: 'Ada',
        lastName: 'Lovelace',
        socialSecurityNumber: '',
        ...over,
      };
    }

    it('warns and sends nothing when either name is blank', () => {
      const c = create();
      enter('patients');
      c.saveForm(draft({ firstName: '   ' }));
      expect(toaster.warn).toHaveBeenCalled();
      expect(gateway['create']).not.toHaveBeenCalled();

      c.saveForm(draft({ lastName: '' }));
      expect(gateway['create']).not.toHaveBeenCalled();
    });

    it('creates when the draft has no id', () => {
      const c = create();
      enter('aa');
      c.saveForm(draft());
      expect(gateway['create']).toHaveBeenCalled();
      expect(gateway['create'].calls.mostRecent().args[0]).toBe('aa');
      expect(gateway['update']).not.toHaveBeenCalled();
    });

    it('updates when the draft carries an id', () => {
      const c = create();
      enter('patients');
      c.saveForm(draft({ id: 'p-1' }));
      expect(gateway['update']).toHaveBeenCalled();
      expect(gateway['create']).not.toHaveBeenCalled();
    });

    it('closes the modal, toasts and reloads on success', () => {
      const c = create();
      enter('patients');
      gateway['list'].calls.reset();
      c.openNew();
      c.saveForm(draft());
      expect(c.form()).toBeNull();
      expect(toaster.success).toHaveBeenCalled();
      expect(gateway['list']).toHaveBeenCalled();
      expect(c.isBusy()).toBeFalse();
    });

    it('closes an open detail view for the person that was just edited', () => {
      // The detail panel holds a snapshot; leaving it open would show pre-edit values.
      const c = create();
      enter('patients');
      c.detailRow.set(row({ id: 'p-1' }));
      c.saveForm(draft({ id: 'p-1' }));
      expect(c.detailRow()).toBeNull();
    });

    it('leaves a detail view for a DIFFERENT person open', () => {
      const c = create();
      enter('patients');
      c.detailRow.set(row({ id: 'other' }));
      c.saveForm(draft({ id: 'p-1' }));
      expect(c.detailRow()).not.toBeNull();
    });

    it('keeps the modal open and clears busy when the save fails', () => {
      // ABP surfaces the server message; the form must stay so the user can retry.
      const c = create();
      enter('patients');
      gateway['create'].and.returnValue(throwError(() => ({ status: 400 })));
      c.openNew();
      c.saveForm(draft());
      expect(c.form()).not.toBeNull();
      expect(c.isBusy()).toBeFalse();
    });

    it('ignores a second save while the first is in flight', () => {
      const c = create();
      enter('patients');
      c.isBusy.set(true);
      c.saveForm(draft());
      expect(gateway['create']).not.toHaveBeenCalled();
    });
  });

  describe('deleting', () => {
    it('deletes through the active section and reloads', () => {
      const c = create();
      enter('ce');
      gateway['list'].calls.reset();
      c.tryDelete(row({ id: 'p-9' }));
      expect(gateway['delete']).toHaveBeenCalledWith('ce', 'p-9');
      expect(toaster.success).toHaveBeenCalled();
      expect(gateway['list']).toHaveBeenCalled();
    });

    it('ignores a delete while another request is in flight', () => {
      const c = create();
      enter('patients');
      c.isBusy.set(true);
      c.tryDelete(row());
      expect(gateway['delete']).not.toHaveBeenCalled();
    });

    it('clears busy when the delete is refused', () => {
      // The in-use guard is a 409 the server raises and ABP toasts; the button must
      // come back rather than stay disabled forever.
      const c = create();
      enter('patients');
      gateway['delete'].and.returnValue(throwError(() => ({ status: 409 })));
      c.tryDelete(row());
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('the invite deep-link', () => {
    it('carries the row email and the section user type', () => {
      const c = create();
      enter('aa');
      c.invitePerson(row({ email: 'ada@example.test' }));
      expect(router.navigate).toHaveBeenCalled();
      const [path, extras] = router.navigate.calls.mostRecent().args;
      expect(path).toEqual(['/users/invite']);
      expect(extras.queryParams.email).toBe('ada@example.test');
      expect(extras.queryParams.userType).toBe('ApplicantAttorney');
    });

    it('sends an empty email rather than null when the row has none', () => {
      const c = create();
      enter('patients');
      c.invitePerson(row({ email: null }));
      const [, extras] = router.navigate.calls.mostRecent().args;
      expect(extras.queryParams.email).toBe('');
      expect(extras.queryParams.userType).toBe('Patient');
    });
  });

  describe('display helpers', () => {
    it('names a gender by its enum key and falls back to a dash', () => {
      const c = create();
      enter('patients');
      expect(c.genderLabel(1)).toBe('Male');
      expect(c.genderLabel(2)).toBe('Female');
      expect(c.genderLabel(null)).toBe('--');
      expect(c.genderLabel(99)).toBe('--');
    });
  });
});
