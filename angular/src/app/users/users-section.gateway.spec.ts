import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { EditionService, TenantService } from '@volo/abp.ng.saas/proxy';

import { UsersSectionGateway, TenantFormState } from './users-section.gateway';
import { ExternalUserService } from '../proxy/external-users/external-user.service';
import { ExternalSignupService } from '../proxy/external-signups/external-signup.service';
import { InternalUsersService } from '../proxy/internal-users/internal-users.service';
import { UserExtendedService } from '../proxy/users/user-extended.service';
import { DashboardService } from '../proxy/dashboards/dashboard.service';
import { DoctorTenantService } from '../proxy/doctors/doctor-tenant.service';
import { BrandingService } from '../shared/branding/branding.service';

/**
 * The Users hub's section gateway: invitations, internal users, and practices, each routed to
 * its own proxy so the hub component carries no per-section wiring.
 *
 * <p>It had NO spec and sat at 1 of 33 lines covered.</p>
 *
 * <p>It is the same `@Injectable`-plus-proxies shape as the people, config and admin gateways,
 * so it needs no component harness -- `TestBed.inject` and one spy object per service. It is
 * the heaviest of the four, though: NINE collaborators, a `switchMap` round trip, and four
 * server-paged table sources.</p>
 *
 * <p>THE ONE THING IN HERE THAT CAN LOSE DATA is `setUserActive`. ABP's update endpoint
 * REPLACES the whole user, so toggling a checkbox means fetching the current record and
 * resending every field with only `isActive` changed. Any field dropped from that resend is
 * erased -- silently, and most visibly for `roleNames`, where a staff member would come back
 * from a toggle with no roles at all. That round trip gets the most attention below.</p>
 *
 * <p>All names, emails and identifiers below are synthetic.</p>
 */
describe('UsersSectionGateway', () => {
  let externalUsers: Record<string, jasmine.Spy>;
  let externalSignup: Record<string, jasmine.Spy>;
  let internalUsers: Record<string, jasmine.Spy>;
  let userExtended: Record<string, jasmine.Spy>;
  let dashboard: Record<string, jasmine.Spy>;
  let tenants: Record<string, jasmine.Spy>;
  let editions: Record<string, jasmine.Spy>;
  let doctorTenant: Record<string, jasmine.Spy>;
  let branding: Record<string, jasmine.Spy>;

  /** A fully populated identity user, as the update round trip would read one back. */
  const STORED_USER = {
    userName: 'sam.intake',
    name: 'Sam',
    surname: 'Reed',
    email: 'sam.reed@example.test',
    phoneNumber: '555-0100',
    isActive: true,
    lockoutEnabled: true,
    roleNames: ['Intake Staff'],
    shouldChangePasswordOnNextLogin: false,
    emailConfirmed: true,
    concurrencyStamp: 'stamp-1',
  };

  /** A managed-table query, as the table passes one in. */
  function query(overrides: Partial<Record<string, unknown>> = {}) {
    return {
      search: '',
      sorting: '',
      skipCount: 0,
      maxResultCount: 10,
      ...overrides,
    } as never;
  }

  function gateway(): UsersSectionGateway {
    externalUsers = {
      inviteExternalUser: jasmine.createSpy('inviteExternalUser').and.returnValue(of({})),
      getInvites: jasmine.createSpy('getInvites').and.returnValue(of({ items: [], totalCount: 0 })),
      resendInvite: jasmine.createSpy('resendInvite').and.returnValue(of({})),
      revokeInvite: jasmine.createSpy('revokeInvite').and.returnValue(of(undefined)),
    };
    externalSignup = {
      sendPortalLink: jasmine.createSpy('sendPortalLink').and.returnValue(of(undefined)),
      getTenantOptions: jasmine.createSpy('getTenantOptions').and.returnValue(of({ items: [] })),
    };
    internalUsers = {
      getInternalUsers: jasmine
        .createSpy('getInternalUsers')
        .and.returnValue(of({ items: [], totalCount: 0 })),
      create: jasmine.createSpy('create').and.returnValue(of({})),
      sendPasswordResetEmail: jasmine
        .createSpy('sendPasswordResetEmail')
        .and.returnValue(of(undefined)),
    };
    userExtended = {
      get: jasmine.createSpy('get').and.returnValue(of(STORED_USER)),
      update: jasmine.createSpy('update').and.returnValue(of(undefined)),
    };
    dashboard = {
      getOffices: jasmine.createSpy('getOffices').and.returnValue(of({ items: [], totalCount: 0 })),
    };
    tenants = { update: jasmine.createSpy('update').and.returnValue(of({})) };
    editions = { getList: jasmine.createSpy('getList').and.returnValue(of({ items: [] })) };
    doctorTenant = {
      createPractice: jasmine.createSpy('createPractice').and.returnValue(of({ id: 'office-1' })),
    };
    branding = { uploadLogo: jasmine.createSpy('uploadLogo').and.returnValue(of({})) };

    TestBed.configureTestingModule({
      providers: [
        { provide: ExternalUserService, useValue: externalUsers },
        { provide: ExternalSignupService, useValue: externalSignup },
        { provide: InternalUsersService, useValue: internalUsers },
        { provide: UserExtendedService, useValue: userExtended },
        { provide: DashboardService, useValue: dashboard },
        { provide: TenantService, useValue: tenants },
        { provide: EditionService, useValue: editions },
        { provide: DoctorTenantService, useValue: doctorTenant },
        { provide: BrandingService, useValue: branding },
      ],
    });

    return TestBed.inject(UsersSectionGateway);
  }

  function emitted<T>(source: { subscribe: (fn: (value: T) => void) => unknown }): T {
    let captured: T | undefined;
    source.subscribe((value) => (captured = value));
    return captured as T;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the internal users page', () => {
    it('sends the trimmed search, the sort and the page window', () => {
      gateway().internalUsersPage(
        query({ search: '  sam ', sorting: 'name asc', skipCount: 20, maxResultCount: 10 }),
      );

      expect(internalUsers['getInternalUsers']).toHaveBeenCalledWith({
        filter: 'sam',
        sorting: 'name asc',
        skipCount: 20,
        maxResultCount: 10,
      });
    });

    it('sends no filter or sort when they are blank', () => {
      gateway().internalUsersPage(query({ search: '   ' }));

      const sent = internalUsers['getInternalUsers'].calls.mostRecent().args[0];
      expect(sent.filter).toBeUndefined();
      expect(sent.sorting).toBeUndefined();
    });

    it('maps a response with no items to an empty page', () => {
      const g = gateway();
      internalUsers['getInternalUsers'].and.returnValue(of({ items: null, totalCount: null }));

      expect(emitted(g.internalUsersPage(query()))).toEqual({ items: [], totalCount: 0 });
    });
  });

  describe('toggling a user active', () => {
    it('READS the user first, then writes -- it does not blind-write a partial dto', () => {
      const g = gateway();

      g.setUserActive('u-1', false).subscribe();

      expect(userExtended['get']).toHaveBeenCalledWith('u-1');
      expect(userExtended['update']).toHaveBeenCalled();
    });

    it('PRESERVES the roles it did not come to change', () => {
      /**
       * The highest-stakes line in this file. ABP's update replaces the whole user, so a
       * resend that omits roleNames strips every role from a staff member who was merely
       * deactivated -- and the UI shows only the checkbox they clicked.
       */
      const g = gateway();

      g.setUserActive('u-1', false).subscribe();

      const [, body] = userExtended['update'].calls.mostRecent().args;
      expect(body.roleNames).toEqual(['Intake Staff']);
    });

    it('preserves the concurrency stamp and every other stored field', () => {
      const g = gateway();

      g.setUserActive('u-1', false).subscribe();

      const [, body] = userExtended['update'].calls.mostRecent().args;
      expect(body.concurrencyStamp).toBe('stamp-1');
      expect(body.name).toBe('Sam');
      expect(body.surname).toBe('Reed');
      expect(body.phoneNumber).toBe('555-0100');
      expect(body.lockoutEnabled).toBeTrue();
      expect(body.emailConfirmed).toBeTrue();
      expect(body.shouldChangePasswordOnNextLogin).toBeFalse();
    });

    it('changes ONLY the active flag', () => {
      const g = gateway();

      g.setUserActive('u-1', false).subscribe();

      const [, body] = userExtended['update'].calls.mostRecent().args;
      expect(body.isActive).withContext('the one field being changed').toBeFalse();
      expect(body.userName).withContext('unchanged').toBe('sam.intake');
    });

    it('turns a user back on with the same round trip', () => {
      // The positive control for the test above: the flag follows the argument rather than
      // being pinned to one value.
      const g = gateway();
      userExtended['get'].and.returnValue(of({ ...STORED_USER, isActive: false }));

      g.setUserActive('u-1', true).subscribe();

      expect(userExtended['update'].calls.mostRecent().args[1].isActive).toBeTrue();
    });

    it('falls back to the email when the stored record has no user name', () => {
      // userName is required by the update dto; sending undefined fails the whole toggle.
      const g = gateway();
      userExtended['get'].and.returnValue(of({ ...STORED_USER, userName: null }));

      g.setUserActive('u-1', false).subscribe();

      expect(userExtended['update'].calls.mostRecent().args[1].userName).toBe(
        'sam.reed@example.test',
      );
    });

    it('sends empty strings rather than undefined when the record has neither', () => {
      const g = gateway();
      userExtended['get'].and.returnValue(of({}));

      g.setUserActive('u-1', true).subscribe();

      const [, body] = userExtended['update'].calls.mostRecent().args;
      expect(body.userName).toBe('');
      expect(body.email).toBe('');
    });

    it('resolves to nothing, so callers cannot accidentally depend on the raw response', () => {
      const g = gateway();
      expect(emitted(g.setUserActive('u-1', false))).toBeUndefined();
    });
  });

  describe('creating a practice', () => {
    const FORM: TenantFormState = {
      id: null,
      name: '  Example Practice  ',
      editionId: null,
      isActive: true,
      doctorFirstName: '  Ada  ',
      doctorLastName: '  Lovelace  ',
      doctorEmail: '  ada@example.test  ',
      displayName: '  Example  ',
    };

    it('goes through the provisioning endpoint, NOT the stock SaaS create', () => {
      // The stock create writes a bare tenant row: no office database, no owner doctor, no
      // branding. An office created that way looks present and cannot be used.
      const g = gateway();

      g.createPractice(FORM);

      expect(doctorTenant['createPractice']).toHaveBeenCalled();
      expect(tenants['update']).not.toHaveBeenCalled();
    });

    it('lowercases and trims the name into the slug', () => {
      const g = gateway();
      g.createPractice(FORM);
      expect(doctorTenant['createPractice'].calls.mostRecent().args[0].slug).toBe(
        'example practice',
      );
    });

    it('trims every doctor field, since the email doubles as the admin login', () => {
      const g = gateway();
      g.createPractice(FORM);

      const [input] = doctorTenant['createPractice'].calls.mostRecent().args;
      expect(input.doctorFirstName).toBe('Ada');
      expect(input.doctorLastName).toBe('Lovelace');
      expect(input.doctorEmail).toBe('ada@example.test');
    });

    it('sends UNDEFINED for a blank display name, so the server default applies', () => {
      // An empty string would be stored as the display name and the office would render
      // unnamed; undefined lets the server fall back to "Dr. {first} {last}".
      const g = gateway();
      g.createPractice({ ...FORM, displayName: '   ' });

      expect(doctorTenant['createPractice'].calls.mostRecent().args[0].displayName).toBeUndefined();
    });

    it('substitutes empty strings when the doctor fields are absent entirely', () => {
      const g = gateway();
      g.createPractice({ id: null, name: 'x', editionId: null, isActive: true });

      const [input] = doctorTenant['createPractice'].calls.mostRecent().args;
      expect(input.doctorFirstName).toBe('');
      expect(input.doctorEmail).toBe('');
    });

    it('uploads a logo against the office id, not the caller scope', () => {
      const g = gateway();
      const file = new File(['x'], 'logo.png', { type: 'image/png' });

      g.uploadOfficeLogo('office-1', file);

      expect(branding['uploadLogo']).toHaveBeenCalledWith(file, 'office-1');
    });
  });

  describe('updating a practice', () => {
    it('maps the active flag onto the activation-state enum', () => {
      /**
       * 0 is active and 2 is disabled; they are not booleans and they are not 0/1. Sending
       * 1 would set "active with limited time", which is a different state that nothing in
       * this UI offers.
       */
      const g = gateway();
      const form: TenantFormState = {
        id: 't-1',
        name: 'Example',
        editionId: 'e-1',
        isActive: true,
        concurrencyStamp: 'stamp-1',
      };

      g.updateTenant(form);
      expect(tenants['update'].calls.mostRecent().args[1].activationState).toBe(0);

      g.updateTenant({ ...form, isActive: false });
      expect(tenants['update'].calls.mostRecent().args[1].activationState).toBe(2);
    });

    it('updates by id and carries the concurrency stamp', () => {
      const g = gateway();
      g.updateTenant({
        id: 't-1',
        name: 'Example',
        editionId: 'e-1',
        isActive: true,
        concurrencyStamp: 'stamp-1',
      });

      const [id, body] = tenants['update'].calls.mostRecent().args;
      expect(id).toBe('t-1');
      expect(body.concurrencyStamp).toBe('stamp-1');
      expect(body.editionId).toBe('e-1');
    });

    it('sends UNDEFINED rather than null when no edition is chosen', () => {
      const g = gateway();
      g.updateTenant({ id: 't-1', name: 'Example', editionId: null, isActive: true });
      expect(tenants['update'].calls.mostRecent().args[1].editionId).toBeUndefined();
    });

    it('lowercases the name here too, so the slug stays stable across an edit', () => {
      const g = gateway();
      g.updateTenant({ id: 't-1', name: '  Example Practice ', editionId: null, isActive: true });
      expect(tenants['update'].calls.mostRecent().args[1].name).toBe('example practice');
    });
  });

  describe('the server-paged tables', () => {
    it('omits a blank search rather than filtering on an empty string', () => {
      const g = gateway();

      g.invitesPage(query({ search: '   ' }));
      expect(externalUsers['getInvites'].calls.mostRecent().args[0].filter).toBeUndefined();

      g.internalUsersPage(query({ search: '  ' }));
      expect(internalUsers['getInternalUsers'].calls.mostRecent().args[0].filter).toBeUndefined();

      g.officesPage(query({ search: '' }));
      expect(dashboard['getOffices'].calls.mostRecent().args[0].filter).toBeUndefined();
    });

    it('passes a real search trimmed, with the paging window', () => {
      const g = gateway();

      g.internalUsersPage(query({ search: '  sam  ', skipCount: 20, maxResultCount: 10 }));

      const [sent] = internalUsers['getInternalUsers'].calls.mostRecent().args;
      expect(sent.filter).toBe('sam');
      expect(sent.skipCount).toBe(20);
      expect(sent.maxResultCount).toBe(10);
    });

    it('omits empty sorting, so the server applies its own default order', () => {
      const g = gateway();
      g.officesPage(query({ sorting: '' }));
      expect(dashboard['getOffices'].calls.mostRecent().args[0].sorting).toBeUndefined();
    });

    it('normalises every page into items plus a total count', () => {
      const g = gateway();
      externalUsers['getInvites'].and.returnValue(of({ items: [{ id: 'i-1' }], totalCount: 37 }));

      expect(emitted(g.invitesPage(query()))).toEqual({ items: [{ id: 'i-1' }], totalCount: 37 });
    });

    it('reports ZERO rather than undefined when the server omits the count', () => {
      // The table divides by the total to size its pager; undefined renders NaN pages.
      const g = gateway();
      dashboard['getOffices'].and.returnValue(of({}));

      expect(emitted(g.officesPage(query()))).toEqual({ items: [], totalCount: 0 });
    });
  });

  describe('invitations', () => {
    it('sends an invite straight through', () => {
      const g = gateway();
      const input = { email: 'new@example.test', role: 1 } as never;

      g.sendInvite(input);

      expect(externalUsers['inviteExternalUser']).toHaveBeenCalledWith(input);
    });

    it('sends a portal link through the SIGNUP service, which owns that route', () => {
      // There is no custom external-users route for it; calling the other service 404s.
      const g = gateway();

      g.sendPortalLink('someone@example.test', 't-1');

      expect(externalSignup['sendPortalLink']).toHaveBeenCalledWith({
        email: 'someone@example.test',
        tenantId: 't-1',
      });
    });

    it('omits the office when none is given', () => {
      const g = gateway();
      g.sendPortalLink('someone@example.test');
      expect(externalSignup['sendPortalLink']).toHaveBeenCalledWith({
        email: 'someone@example.test',
        tenantId: undefined,
      });
    });

    it('unwraps the office options and treats an absent list as empty', () => {
      /**
       * The list is populated only at host scope -- inside an office the tenant is implicit
       * and the backend returns nothing. A non-empty result is what tells the hub to show
       * the office picker, so an undefined list must read as "no picker", not crash it.
       */
      const g = gateway();
      externalSignup['getTenantOptions'].and.returnValue(of({ items: [{ id: 't-1' }] }));
      expect(emitted(g.getInviteTenantOptions())).toEqual([{ id: 't-1' }]);

      externalSignup['getTenantOptions'].and.returnValue(of({}));
      expect(emitted(g.getInviteTenantOptions())).toEqual([]);
    });

    it('resends and revokes by id', () => {
      const g = gateway();
      g.resendInvite('i-1');
      expect(externalUsers['resendInvite']).toHaveBeenCalledWith('i-1');

      g.revokeInvite('i-1');
      expect(externalUsers['revokeInvite']).toHaveBeenCalledWith('i-1');
    });
  });

  describe('internal users and editions', () => {
    it('creates an internal user through the custom endpoint', () => {
      const g = gateway();
      const input = { email: 'staff@example.test', roleName: 'Intake Staff' } as never;

      g.createInternalUser(input);

      expect(internalUsers['create']).toHaveBeenCalledWith(input);
    });

    it('sends a password reset by id', () => {
      const g = gateway();
      g.sendPasswordReset('u-1');
      expect(internalUsers['sendPasswordResetEmail']).toHaveBeenCalledWith('u-1');
    });

    it('maps editions to id and name, reading the DISPLAY name', () => {
      const g = gateway();
      editions['getList'].and.returnValue(
        of({ items: [{ id: 'e-1', displayName: 'Standard' }, {}] }),
      );

      expect(emitted(g.getEditionOptions())).toEqual([
        { id: 'e-1', name: 'Standard' },
        { id: '', name: '' },
      ]);
    });
  });
});
