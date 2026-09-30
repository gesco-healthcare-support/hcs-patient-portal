import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ConfigStateService, ListService, RestService } from '@abp/ng.core';

import { ExternalHomeComponent } from './external-home.component';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';

/**
 * The external role home -- one screen serving all four external roles (Patient, Applicant
 * Attorney, Defense Attorney, Claim Examiner).
 *
 * <p>It sat at 73 of 110 lines uncovered. An existing spec (sweep #660) covers the type and
 * location filter dropdowns; neither is repeated here.</p>
 *
 * <p>The role config is what earns the most attention: one component renders four different
 * screens from it, and picking the wrong one shows an attorney the patient layout (no patient
 * column, no date of birth) or greets a patient with case-file wording. The other half is the
 * client-side list -- the server returns the caller's involvement-filtered appointments once,
 * and every segment, search and filter after that is computed here.</p>
 *
 * <p>The component declares ListService and AppointmentService as its OWN providers, which beat
 * anything registered on the testing module, so both are replaced with `overrideComponent`.</p>
 *
 * <p>`signOut` is deliberately not exercised: it delegates to the real full-logout helper, which
 * reaches the OAuth stack. All names, claim numbers and identifiers below are synthetic.</p>
 */
describe('ExternalHomeComponent surfaces', () => {
  let appointments: { getList: jasmine.Spy };
  let rest: { request: jasmine.Spy };
  let router: { navigate: jasmine.Spy; navigateByUrl: jasmine.Spy };
  let currentUser: Record<string, unknown> | null;
  let currentTenant: Record<string, unknown> | null;

  interface Probe {
    [key: string]: any;
  }

  /** The component's placeholder for an absent value, built to keep this source ASCII-only. */
  const DASH = String.fromCodePoint(0x2014);

  function appt(over: Record<string, unknown> = {}) {
    return {
      appointment: {
        id: 'a-1',
        requestConfirmationNumber: 'C0001',
        appointmentStatus: AppointmentStatusType.Pending,
        appointmentDate: '2026-10-01T09:00:00',
        ...((over['appointment'] as Record<string, unknown>) ?? {}),
      },
      patient: { firstName: 'Ada', lastName: 'Lovelace' },
      appointmentType: { name: 'AME' },
      location: { name: 'Encino' },
      appointmentInjuryDetails: [
        { appointmentInjuryDetail: { claimNumber: 'CLM-1', wcabAdj: 'ADJ-1' } },
      ],
      ...over,
    };
  }

  function create(options: { roles?: string[]; items?: unknown[] } = {}): Probe {
    currentUser = {
      roles: options.roles ?? ['Patient'],
      email: 'ada@example.test',
      userName: 'ada',
      name: 'Ada',
      surname: 'Lovelace',
    };
    currentTenant = { name: 'Example Practice' };

    appointments = {
      getList: jasmine
        .createSpy('getList')
        .and.returnValue(of({ items: options.items ?? [], totalCount: 0 })),
    };
    rest = { request: jasmine.createSpy('request').and.returnValue(of({ firmName: '' })) };
    router = {
      navigate: jasmine.createSpy('navigate'),
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    };

    const listStub = {
      maxResultCount: 10,
      hookToQuery: (fn: (q: Record<string, unknown>) => unknown) => fn({}),
    };

    TestBed.configureTestingModule({
      providers: [
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (k: string) => (k === 'currentUser' ? currentUser : currentTenant),
          },
        },
        { provide: RestService, useValue: rest },
        { provide: Router, useValue: router },
      ],
    });

    // The component's own providers win over the module's, so replace them at source.
    TestBed.overrideComponent(ExternalHomeComponent, {
      set: {
        providers: [
          { provide: ListService, useValue: listStub },
          { provide: AppointmentService, useValue: appointments },
        ],
      },
    });

    return TestBed.createComponent(ExternalHomeComponent).componentInstance as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('resolving the role', () => {
    const cases: [string, string, 'cards' | 'table', boolean][] = [
      ['Patient', 'Patient', 'cards', false],
      ['Applicant Attorney', 'Applicant Attorney', 'table', true],
      ['Defense Attorney', 'Defense Attorney', 'table', true],
      ['Claim Examiner', 'Claim Examiner', 'table', true],
    ];

    cases.forEach(([role, label, view, showsPatient]) => {
      it(`renders the ${label} layout`, () => {
        const c = create({ roles: [role] });
        c.ngOnInit();
        expect(c.role.label).toBe(label);
        expect(c.view()).withContext('each role has its own default view').toBe(view);
        expect(c.role.showPatientCol).toBe(showsPatient);
        expect(c.navRoleLabel).toBe(label);
      });
    });

    it('matches the role whatever its casing', () => {
      const c = create({ roles: ['DEFENSE ATTORNEY'] });
      c.ngOnInit();
      expect(c.role.label).toBe('Defense Attorney');
    });

    it('falls back to the patient layout for an unrecognised role', () => {
      const c = create({ roles: ['Some Other Role'] });
      c.ngOnInit();
      expect(c.role.label).toBe('Patient');
    });

    it('falls back to the patient layout when there are no roles at all', () => {
      const c = create({ roles: [] });
      c.ngOnInit();
      expect(c.role.label).toBe('Patient');
    });

    it('gives all four roles the same booking capability', () => {
      // R2-4: the four external roles differ in the data they see, not in what they may do.
      ['Patient', 'Applicant Attorney', 'Defense Attorney', 'Claim Examiner'].forEach((role) => {
        TestBed.resetTestingModule();
        const c = create({ roles: [role] });
        c.ngOnInit();
        expect(c.role.canBook).toBeTrue();
        expect(c.role.canReeval).toBeTrue();
      });
    });
  });

  describe('the navbar', () => {
    it('names the office from the tenant', () => {
      const c = create();
      c.ngOnInit();
      expect(c.clinicName).toBe('Example Practice');
    });

    it('accepts either tenant name field', () => {
      const c = create();
      currentTenant = { tenantName: 'Second Practice' };
      c.ngOnInit();
      expect(c.clinicName).toBe('Second Practice');
    });

    it('falls back to a generic title with no tenant', () => {
      const c = create();
      currentTenant = null;
      c.ngOnInit();
      expect(c.clinicName).toBe('Appointment Portal');
    });

    it('shows the email, falling back to the user name', () => {
      const c = create();
      c.ngOnInit();
      expect(c.navUserEmail).toBe('ada@example.test');

      currentUser = { roles: ['Patient'], userName: 'ada' };
      c.ngOnInit();
      expect(c.navUserEmail).toBe('ada');
    });

    it('shows nothing rather than undefined when neither exists', () => {
      const c = create();
      currentUser = { roles: ['Patient'] };
      c.ngOnInit();
      expect(c.navUserEmail).toBe('');
    });
  });

  describe('the display name', () => {
    it('prefers the first and last name', () => {
      const c = create();
      c.ngOnInit();
      expect(c.navUserName()).toBe('Ada Lovelace');
      expect(c.heroName()).toBe('Ada Lovelace');
    });

    it('falls back to the firm name once it arrives', () => {
      // The firm lookup is async, which is why these are signals: OnPush has to repaint
      // when the name resolves.
      const c = create();
      currentUser = { roles: ['Applicant Attorney'], userName: 'aa' };
      rest.request.and.returnValue(of({ firmName: 'Analytical Engines LLP' }));

      c.ngOnInit();

      expect(c.navUserName()).toBe('Analytical Engines LLP');
      expect(c.navOrgName()).toBe('Analytical Engines LLP');
    });

    it('greets a user it cannot name at all', () => {
      const c = create();
      currentUser = { roles: ['Patient'] };
      c.ngOnInit();
      expect(c.heroName()).toBe('there');
    });

    it('leaves the organisation blank when the firm lookup fails', () => {
      const c = create();
      rest.request.and.returnValue(throwError(() => ({ status: 404 })));
      c.ngOnInit();
      expect(c.navOrgName()).toBeNull();
    });
  });

  describe('mapping a row', () => {
    it('flattens the nav properties the cards and table render', () => {
      const c = create({ items: [appt()] });
      c.ngOnInit();

      const r = c.rows()[0];
      expect(r.id).toBe('a-1');
      expect(r.type).toBe('AME');
      expect(r.location).toBe('Encino');
      expect(r.patientName).toBe('Ada Lovelace');
      expect(r.confirmation).toBe('C0001');
      expect(r.claimNumber).toBe('CLM-1');
      expect(r.adjNumber).toBe('ADJ-1');
      expect(c.loading()).toBeFalse();
    });

    it('substitutes the placeholder for every absent field', () => {
      const c = create({
        items: [
          {
            appointment: { id: 'a-1' },
            patient: undefined,
            appointmentType: undefined,
            location: undefined,
            appointmentInjuryDetails: undefined,
          },
        ],
      });
      c.ngOnInit();

      const r = c.rows()[0];
      expect(r.type).toBe(DASH);
      expect(r.location).toBe(DASH);
      expect(r.patientName).toBe(DASH);
      expect(r.claimNumber).toBe(DASH);
      expect(r.adjNumber).toBe(DASH);
      expect(r.confirmation).toBe(DASH);
    });

    it('reads the claim details from the first injury row', () => {
      const c = create({
        items: [
          appt({
            appointmentInjuryDetails: [
              { appointmentInjuryDetail: { claimNumber: 'FIRST', wcabAdj: 'ADJ-FIRST' } },
              { appointmentInjuryDetail: { claimNumber: 'SECOND', wcabAdj: 'ADJ-SECOND' } },
            ],
          }),
        ],
      });
      c.ngOnInit();
      expect(c.rows()[0].claimNumber).toBe('FIRST');
    });

    it('derives the avatar initials and a stable colour from the patient name', () => {
      // The shared avatar helper returns a hex colour, not hsl; what matters here is that
      // it is derived from the name and is deterministic.
      const c = create({ items: [appt()] });
      c.ngOnInit();
      expect(c.rows()[0].patientInitials).toBe('AL');
      expect(c.rows()[0].patientColor).toMatch(/^#[0-9a-f]{6}$/i);
    });

    it('treats an appointment with no status as pending', () => {
      const c = create({ items: [appt({ appointment: { id: 'a-1', appointmentStatus: null } })] });
      c.ngOnInit();
      expect(c.rows()[0].segment).toBe('pending');
    });
  });

  describe('the segment counts', () => {
    it('counts each segment and the total', () => {
      const c = create({
        items: [
          appt({ appointment: { id: '1', appointmentStatus: AppointmentStatusType.Pending } }),
          appt({ appointment: { id: '2', appointmentStatus: AppointmentStatusType.Pending } }),
          appt({ appointment: { id: '3', appointmentStatus: AppointmentStatusType.Approved } }),
        ],
      });
      c.ngOnInit();

      expect(c.counts().all).toBe(3);
      expect(c.counts().pending).toBe(2);
      expect(c.counts().approved).toBe(1);
      expect(c.counts().cancelled).toBe(0);
    });
  });

  describe('the client-side list', () => {
    function seeded(): Probe {
      const c = create({
        items: [
          appt({
            appointment: {
              id: '1',
              requestConfirmationNumber: 'C0001',
              appointmentStatus: AppointmentStatusType.Pending,
            },
          }),
          appt({
            appointment: {
              id: '2',
              requestConfirmationNumber: 'C0002',
              appointmentStatus: AppointmentStatusType.Approved,
            },
            appointmentType: { name: 'QME' },
            location: { name: 'Glendale' },
            patient: { firstName: 'Grace', lastName: 'Hopper' },
            appointmentInjuryDetails: [
              { appointmentInjuryDetail: { claimNumber: 'CLM-2', wcabAdj: 'ADJ-2' } },
            ],
          }),
        ],
      });
      c.ngOnInit();
      return c;
    }

    it('shows everything on the all segment', () => {
      const c = seeded();
      expect(c.filtered().length).toBe(2);
    });

    it('narrows to one segment', () => {
      const c = seeded();
      c.activeSeg.set('approved');
      expect(c.filtered().map((r: { id: string }) => r.id)).toEqual(['2']);
    });

    it('searches across name, confirmation, claim, ADJ, type and location', () => {
      const c = seeded();
      const hits: [string, string][] = [
        ['hopper', '2'],
        ['C0001', '1'],
        ['CLM-2', '2'],
        ['ADJ-1', '1'],
        ['QME', '2'],
        ['glendale', '2'],
      ];
      hits.forEach(([term, id]) => {
        c.q.set(term);
        expect(c.filtered().map((r: { id: string }) => r.id))
          .withContext(`searching for ${term}`)
          .toEqual([id]);
      });
    });

    it('ignores surrounding whitespace in the search', () => {
      const c = seeded();
      c.q.set('   hopper   ');
      expect(c.filtered().length).toBe(1);
    });

    it('matches type and location exactly, not by substring', () => {
      // These come from a dropdown of values already present, so a substring match would
      // let "QME" also select "AME" rows.
      const c = seeded();
      c.filters.set({ type: 'QME' });
      expect(c.filtered().map((r: { id: string }) => r.id)).toEqual(['2']);

      c.filters.set({ type: 'ME' });
      expect(c.filtered()).toEqual([]);
    });

    it('matches the reference numbers on a case-insensitive substring', () => {
      const c = seeded();
      c.filters.set({ conf: 'c000' });
      expect(c.filtered().length).toBe(2);

      c.filters.set({ claim: 'clm-2' });
      expect(c.filtered().map((r: { id: string }) => r.id)).toEqual(['2']);

      c.filters.set({ adj: 'adj-1' });
      expect(c.filtered().map((r: { id: string }) => r.id)).toEqual(['1']);
    });

    it('applies the segment, the search and the filters together', () => {
      const c = seeded();
      c.activeSeg.set('approved');
      c.q.set('hopper');
      c.filters.set({ location: 'Glendale' });
      expect(c.filtered().map((r: { id: string }) => r.id)).toEqual(['2']);

      c.filters.set({ location: 'Encino' });
      expect(c.filtered()).toEqual([]);
    });
  });

  describe('the filter panel', () => {
    it('lists only the filters that carry a value', () => {
      const c = create();
      c.filters.set({ type: 'AME', conf: '', claim: 'CLM-1' });
      expect(c.activeFilterKeys()).toEqual(['type', 'claim']);
    });

    it('copies the applied filters into the draft when it opens', () => {
      // Opening and cancelling must not lose what is already applied.
      const c = create();
      c.filters.set({ type: 'AME' });
      c.openFilters();
      expect(c.draft()).toEqual({ type: 'AME' });
      expect(c.showFilters()).toBeTrue();
    });

    it('toggles closed again', () => {
      const c = create();
      c.openFilters();
      c.openFilters();
      expect(c.showFilters()).toBeFalse();
    });

    it('commits the draft and closes on apply', () => {
      const c = create();
      c.openFilters();
      c.setDraft('type', 'AME');
      c.applyFilters();
      expect(c.filters()).toEqual({ type: 'AME' });
      expect(c.showFilters()).toBeFalse();
    });

    it('sets one draft key without disturbing the others', () => {
      const c = create();
      c.setDraft('type', 'AME');
      c.setDraft('conf', 'C0001');
      expect(c.draft()).toEqual({ type: 'AME', conf: 'C0001' });
    });

    it('clears both the draft and the applied filters on reset', () => {
      const c = create();
      c.filters.set({ type: 'AME' });
      c.draft.set({ type: 'AME' });
      c.resetFilters();
      expect(c.filters()).toEqual({});
      expect(c.draft()).toEqual({});
    });

    it('removes a single chip from both the applied set and the draft', () => {
      // Leaving it in the draft would resurrect the chip the next time the panel opened.
      const c = create();
      c.filters.set({ type: 'AME', conf: 'C0001' });

      c.clearChip('type');

      expect(c.filters()).toEqual({ conf: 'C0001' });
      expect(c.draft()).toEqual({ conf: 'C0001' });
    });

    it('labels each chip, falling back to the raw key', () => {
      const c = create();
      expect(c.chipLabel('type')).toBe('Type');
      expect(c.chipLabel('conf')).toBe('Conf #');
      expect(c.chipLabel('location')).toBe('Location');
      expect(c.chipLabel('claim')).toBe('Claim #');
      expect(c.chipLabel('adj')).toBe('ADJ #');
      expect(c.chipLabel('unknown')).toBe('unknown');
    });
  });

  describe('navigation', () => {
    it('books a new evaluation and a re-evaluation through different types', () => {
      // type=1 is a new booking and type=2 a re-evaluation; the wizard resolves them.
      const c = create();
      c.requestAppointment();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments/request?type=1');

      c.requestReEvaluation();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments/request?type=2');
    });

    it('opens an appointment, and its documents, at the detail route', () => {
      const c = create();
      c.viewAppointment('a-1');
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/view', 'a-1']);

      router.navigate.calls.reset();
      c.viewDocuments('a-1');
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/view', 'a-1']);
    });

    it('opens the profile for both the profile and the documents entries', () => {
      const c = create();
      c.openProfile();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/user-management/patients/my-profile');

      router.navigateByUrl.calls.reset();
      c.openMyDocuments();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/user-management/patients/my-profile');
    });

    it('opens the query modal', () => {
      const c = create();
      expect(c.submitQueryVisible).toBeFalse();
      c.openQuery();
      expect(c.submitQueryVisible).toBeTrue();
    });
  });
});
