import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';

import { InternalUsersHubComponent } from './internal-users-hub.component';
import { UsersSectionGateway } from './users-section.gateway';
import { ExternalUserType } from '../proxy/external-signups/external-user-type.enum';
import { InvitationStatus } from '../proxy/invitations/invitation-status.enum';

/**
 * The Users & Access hub's four surfaces: Invite External, Pending Invites, Internal Users and
 * Practices.
 *
 * <p>`internal-users-hub.component.spec.ts` covers the Escape-to-close handler added in sweep
 * #648 and nothing else -- 112 lines against a 704-line component, which is why it sat at 58 of
 * 231 covered. Its provider block is a real head start and this spec mirrors it; what is new
 * here is everything the four surfaces actually do.</p>
 *
 * <p>Three rules get particular attention because each exists to prevent a specific wrong
 * outcome: a 200 from the invite endpoint no longer always means an invitation was issued, the
 * New Practice form validates a DNS-safe slug before it will submit, and every mutating action
 * is gated on `isBusy` so a double-click cannot fire it twice.</p>
 *
 * <p>All names, emails, firms and subdomains below are synthetic.</p>
 */
describe('InternalUsersHubComponent surfaces', () => {
  let gateway: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; warn: jasmine.Spy; info: jasmine.Spy; error: jasmine.Spy };
  let impersonateTenant: jasmine.Spy;
  let routeData: Subject<Record<string, unknown>>;
  let queryParams: Record<string, string>;
  let granted: Set<string>;

  interface Probe {
    [key: string]: any;
  }

  const page = () => of({ items: [], totalCount: 0 });

  function create(
    options: { section?: string; policies?: string[]; query?: Record<string, string> } = {},
  ): Probe {
    routeData = new Subject();
    queryParams = options.query ?? {};
    granted = new Set(options.policies ?? []);

    gateway = {
      sendInvite: jasmine
        .createSpy('sendInvite')
        .and.returnValue(of({ inviteUrl: 'https://x.test/i' })),
      sendPortalLink: jasmine.createSpy('sendPortalLink').and.returnValue(of(undefined)),
      getInviteTenantOptions: jasmine.createSpy('getInviteTenantOptions').and.returnValue(of([])),
      invitesPage: jasmine.createSpy('invitesPage').and.callFake(page),
      resendInvite: jasmine
        .createSpy('resendInvite')
        .and.returnValue(of({ inviteUrl: 'https://x.test/fresh' })),
      revokeInvite: jasmine.createSpy('revokeInvite').and.returnValue(of(undefined)),
      internalUsersPage: jasmine.createSpy('internalUsersPage').and.callFake(page),
      createInternalUser: jasmine
        .createSpy('createInternalUser')
        .and.returnValue(of({ id: 'u1', temporaryPassword: 'x' })),
      sendPasswordReset: jasmine.createSpy('sendPasswordReset').and.returnValue(of(undefined)),
      setUserActive: jasmine.createSpy('setUserActive').and.returnValue(of(undefined)),
      officesPage: jasmine.createSpy('officesPage').and.callFake(page),
      getEditionOptions: jasmine.createSpy('getEditionOptions').and.returnValue(of([])),
      createPractice: jasmine.createSpy('createPractice').and.returnValue(of({ id: 'office-1' })),
      uploadOfficeLogo: jasmine.createSpy('uploadOfficeLogo').and.returnValue(of({})),
      updateTenant: jasmine.createSpy('updateTenant').and.returnValue(of({})),
    };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      info: jasmine.createSpy('info'),
      error: jasmine.createSpy('error'),
    };
    impersonateTenant = jasmine.createSpy('impersonateTenant').and.returnValue(of({}));

    TestBed.configureTestingModule({
      imports: [InternalUsersHubComponent],
      providers: [
        { provide: UsersSectionGateway, useValue: gateway },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.has(p) },
        },
        { provide: ToasterService, useValue: toaster },
        { provide: ImpersonationService, useValue: { impersonateTenant } },
        {
          provide: ActivatedRoute,
          useValue: {
            data: routeData,
            snapshot: { queryParamMap: { get: (k: string) => queryParams[k] ?? null } },
          },
        },
      ],
    });

    const component = TestBed.createComponent(InternalUsersHubComponent)
      .componentInstance as unknown as Probe;
    if (options.section !== undefined) {
      routeData.next({ section: options.section });
    }
    return component;
  }

  /**
   * Stub the clipboard for EVERY test, not just the one that asserts on it.
   *
   * `resend()` calls `copy()` on success, so any test touching resend reaches the real
   * `navigator.clipboard.writeText`. In headless Chrome, with no user gesture and no granted
   * permission, that call can simply never settle -- which killed the browser here with
   * "Disconnected, because no message in 30000 ms" at test 40 of 72, reporting no failure at
   * all. A hang is invisible to jasmine: the spec neither passes nor fails, it just stops.
   */
  let clipboardWrite: jasmine.Spy | null;

  beforeEach(() => {
    clipboardWrite = null;
    if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
      clipboardWrite = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    }
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('section routing', () => {
    it('defaults to the invite section', () => {
      const c = create();
      routeData.next({});
      expect(c.section()).toBe('invite');
      expect(gateway['getInviteTenantOptions']).toHaveBeenCalled();
    });

    it('takes the section from route data', () => {
      const c = create({ section: 'tenants' });
      expect(c.section()).toBe('tenants');
    });

    it('resolves the section metadata', () => {
      const c = create({ section: 'staff' });
      expect(c.meta().key).toBe('staff');
    });

    it('falls back to the first section for an unknown key', () => {
      const c = create({ section: 'not-a-section' });
      expect(c.meta()).toBe(c.sections[0]);
    });

    it('only does invite-specific setup for the invite section', () => {
      // The three list sections fetch through the managed table's own data source, so there
      // is nothing for load() to do there.
      create({ section: 'staff' });
      expect(gateway['getInviteTenantOptions']).not.toHaveBeenCalled();
    });

    it('closes both modals when the section changes', () => {
      /**
       * A REMOVAL needing both modals open first. Without it, navigating from Practices to
       * Internal Users with the tenant modal open would leave that modal floating over a
       * completely different surface.
       */
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      c.openCreateUser();
      expect(c.tenantForm()).not.toBeNull();
      expect(c.createForm()).not.toBeNull();

      routeData.next({ section: 'staff' });

      expect(c.tenantForm()).toBeNull();
      expect(c.createForm()).toBeNull();
      expect(c.tenantLogo()).toBeNull();
      expect(c.tenantTriedSave()).toBeFalse();
    });
  });

  describe('rail visibility', () => {
    it('shows a section whose policy is granted', () => {
      const c = create({
        section: 'invite',
        policies: ['CaseEvaluation.UserManagement.InviteExternalUser'],
      });
      const invite = c.sections.find((s: any) => s.key === 'invite');
      expect(c.canSee(invite)).toBeTrue();
    });

    it('hides a section whose policy is not granted', () => {
      const c = create({ section: 'invite', policies: [] });
      const invite = c.sections.find((s: any) => s.key === 'invite');
      expect(c.canSee(invite)).toBeFalse();
    });

    it('gates practice create, edit and user deactivation independently', () => {
      // Each is a distinct ABP policy; collapsing them would hand a view-only operator a
      // button that 403s.
      const c = create({ section: 'tenants', policies: ['Saas.Tenants.Create'] });
      expect(c.canCreateTenant()).toBeTrue();
      expect(c.canEditTenant()).toBeFalse();
      expect(c.canDeactivateUser()).toBeFalse();
    });
  });

  describe('inviting an external user', () => {
    function ready(query: Record<string, string> = {}): Probe {
      return create({ section: 'invite', query });
    }

    it('starts from an empty draft', () => {
      const c = ready();
      expect(c.invite().email).toBe('');
      expect(c.invite().userType).toBe(ExternalUserType.Patient);
    });

    it('prefills from the query string', () => {
      // The pending-invite and people screens link here with the address already known.
      const c = ready({ email: 'ada@example.test', userType: 'ApplicantAttorney' });
      expect(c.invite().email).toBe('ada@example.test');
      expect(c.invite().userType).not.toBe(ExternalUserType.Patient);
    });

    it('leaves the draft alone when the query carries nothing', () => {
      const c = ready();
      expect(c.invite().email).toBe('');
      expect(c.inviteResult()).toBeNull();
    });

    it('merges a patch rather than replacing the draft', () => {
      const c = ready();
      c.patchInvite({ email: 'ada@example.test' });
      c.patchInvite({ firstName: 'Ada' });
      expect(c.invite().email).toBe('ada@example.test');
      expect(c.invite().firstName).toBe('Ada');
    });

    it('shows the firm field only for the attorney types', () => {
      const c = ready();
      expect(c.showFirm()).toBeFalse();
      c.patchInvite({ userType: ExternalUserType.ApplicantAttorney });
      expect(c.showFirm()).toBeTrue();
    });

    it('refuses an invalid email without calling the server', () => {
      const c = ready();
      c.patchInvite({ email: 'not-an-email' });

      c.sendInvite();

      expect(gateway['sendInvite']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalled();
    });

    it('rejects an address with an internal space or a second at-sign', () => {
      /**
       * The two ways the previous regex was wrong, and the reason it was replaced: the old
       * `/.+@.+\..+/` accepted both, and was genuinely quadratic because `.+` can match `@`
       * and `.`. These are invalid addresses, so rejecting them is a fix, not a tightening.
       */
      const c = ready();
      for (const bad of ['a b@example.test', 'a@b@example.test', 'ada@example', 'ada@.test']) {
        gateway['sendInvite'].calls.reset();
        c.patchInvite({ email: bad });
        c.sendInvite();
        expect(gateway['sendInvite']).withContext(bad).not.toHaveBeenCalled();
      }
    });

    it('accepts an ordinary address', () => {
      const c = ready();
      c.patchInvite({ email: '  ada.lovelace@example.test  ' });

      c.sendInvite();

      expect(gateway['sendInvite']).toHaveBeenCalled();
      expect(gateway['sendInvite'].calls.mostRecent().args[0].email).toBe(
        'ada.lovelace@example.test',
      );
    });

    it('sends the name for every role, and the firm only for attorneys', () => {
      // F-005: the invitation email greets the invitee by name whatever their role.
      const c = ready();
      c.patchInvite({
        email: 'ada@example.test',
        firstName: ' Ada ',
        lastName: ' Lovelace ',
        firmName: 'Lovelace LLP',
        userType: ExternalUserType.Patient,
      });

      c.sendInvite();

      const body = gateway['sendInvite'].calls.mostRecent().args[0];
      expect(body.firstName).toBe('Ada');
      expect(body.lastName).toBe('Lovelace');
      expect(body.firmName).toBeUndefined();
    });

    it('sends the firm name for an attorney', () => {
      const c = ready();
      c.patchInvite({
        email: 'ada@example.test',
        userType: ExternalUserType.ApplicantAttorney,
        firmName: ' Lovelace LLP ',
      });

      c.sendInvite();

      expect(gateway['sendInvite'].calls.mostRecent().args[0].firmName).toBe('Lovelace LLP');
    });

    it('requires an office once the picker is shown', () => {
      /**
       * The picker is non-empty only at host scope, and there the invitation has no ambient
       * tenant to fall back on -- so an invite without one would land in no office at all.
       */
      const c = ready();
      gateway['getInviteTenantOptions'].and.returnValue(of([{ id: 'office-a', displayName: 'A' }]));
      routeData.next({ section: 'invite' });
      expect(c.tenantPickerRequired()).toBeTrue();
      c.patchInvite({ email: 'ada@example.test', tenantId: '' });

      c.sendInvite();

      expect(gateway['sendInvite']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalled();
    });

    it('omits the office in-office, where the picker is empty', () => {
      const c = ready();
      c.patchInvite({ email: 'ada@example.test' });
      c.sendInvite();
      expect(gateway['sendInvite'].calls.mostRecent().args[0].tenantId).toBeUndefined();
    });

    it('leaves the picker list empty when the lookup fails', () => {
      const c = ready();
      gateway['getInviteTenantOptions'].and.returnValue(throwError(() => ({ status: 500 })));
      routeData.next({ section: 'invite' });
      expect(c.tenantOptions()).toEqual([]);
      expect(c.tenantPickerRequired()).toBeFalse();
    });

    it('does NOT claim an invite was sent when the address already has an account', () => {
      /**
       * Item F follow-up: a 200 no longer always means an invitation was issued. When the
       * address already has an account the server deliberately issues NOTHING. Announcing
       * "Invite sent" there would be a false success on the exact support call this feature
       * exists to help with.
       */
      const c = ready();
      gateway['sendInvite'].and.returnValue(of({ alreadyRegistered: true }));
      c.patchInvite({ email: 'ada@example.test' });

      c.sendInvite();

      expect(toaster.info).toHaveBeenCalled();
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('announces a genuine invite', () => {
      const c = ready();
      c.patchInvite({ email: 'ada@example.test' });
      c.sendInvite();
      expect(toaster.success).toHaveBeenCalledWith('Invite sent.');
    });

    it('refuses a second send while one is in flight', () => {
      const c = ready();
      c.isBusy.set(true);
      c.patchInvite({ email: 'ada@example.test' });
      c.sendInvite();
      expect(gateway['sendInvite']).not.toHaveBeenCalled();
    });

    it('releases the busy flag when the send fails', () => {
      const c = ready();
      gateway['sendInvite'].and.returnValue(throwError(() => ({ status: 500 })));
      c.patchInvite({ email: 'ada@example.test' });

      c.sendInvite();

      expect(c.isBusy()).toBeFalse();
    });

    it('resets the form and the result', () => {
      const c = ready();
      c.patchInvite({ email: 'ada@example.test' });
      c.inviteResult.set({ inviteUrl: 'x' });
      c.portalLinkSent.set(true);

      c.resetInvite();

      expect(c.invite().email).toBe('');
      expect(c.inviteResult()).toBeNull();
      expect(c.portalLinkSent()).toBeFalse();
    });
  });

  describe('sending an existing user their portal link', () => {
    it('does nothing without an address', () => {
      const c = create({ section: 'invite' });
      c.sendPortalLink(null);
      expect(gateway['sendPortalLink']).not.toHaveBeenCalled();
    });

    it('sends the link and marks it sent', () => {
      /**
       * `portalLinkSent` stops the operator inviting a second send for the same result --
       * the URL is composed server-side from the office subdomain, never assembled here,
       * because a hand-built URL has twice sent tenant users to the host portal.
       */
      const c = create({ section: 'invite' });
      c.sendPortalLink('ada@example.test');
      expect(gateway['sendPortalLink']).toHaveBeenCalledWith('ada@example.test', undefined);
      expect(c.portalLinkSent()).toBeTrue();
      expect(toaster.success).toHaveBeenCalled();
    });

    it('carries the chosen office for a host-scope caller', () => {
      const c = create({ section: 'invite' });
      c.patchInvite({ tenantId: 'office-a' });
      c.sendPortalLink('ada@example.test');
      expect(gateway['sendPortalLink']).toHaveBeenCalledWith('ada@example.test', 'office-a');
    });

    it('refuses while busy and releases the flag on failure', () => {
      const c = create({ section: 'invite' });
      c.isBusy.set(true);
      c.sendPortalLink('ada@example.test');
      expect(gateway['sendPortalLink']).not.toHaveBeenCalled();

      c.isBusy.set(false);
      gateway['sendPortalLink'].and.returnValue(throwError(() => ({ status: 500 })));
      c.sendPortalLink('ada@example.test');
      expect(c.isBusy()).toBeFalse();
      expect(c.portalLinkSent()).toBeFalse();
    });
  });

  describe('pending invites', () => {
    function invitation(over: Record<string, unknown> = {}) {
      return { id: 'inv-1', email: 'ada@example.test', status: InvitationStatus.Pending, ...over };
    }

    it('offers resend and revoke while the invite is unaccepted', () => {
      const c = create({ section: 'pending' });
      expect(c.canManageInvite(invitation())).toBeTrue();
      expect(c.isPending(invitation())).toBeTrue();
    });

    it('withdraws them once the invite is accepted', () => {
      // Resending an accepted invitation would email a link that no longer does anything.
      const c = create({ section: 'pending' });
      const accepted = invitation({ status: InvitationStatus.Accepted });
      expect(c.canManageInvite(accepted)).toBeFalse();
      expect(c.isPending(accepted)).toBeFalse();
    });

    it('treats a missing status as pending', () => {
      const c = create({ section: 'pending' });
      expect(c.canManageInvite(invitation({ status: undefined }))).toBeTrue();
    });

    it('resends, copies the fresh link and reloads the table', () => {
      const c = create({ section: 'pending' });
      const reloaded = spyOn(c.reload$, 'next');

      c.resend(invitation());

      expect(gateway['resendInvite']).toHaveBeenCalledWith('inv-1');
      expect(toaster.success).toHaveBeenCalled();
      expect(reloaded).toHaveBeenCalled();
    });

    it('revokes and reloads', () => {
      const c = create({ section: 'pending' });
      const reloaded = spyOn(c.reload$, 'next');

      c.revoke(invitation());

      expect(gateway['revokeInvite']).toHaveBeenCalledWith('inv-1');
      expect(reloaded).toHaveBeenCalled();
    });

    it('refuses both while busy', () => {
      const c = create({ section: 'pending' });
      c.isBusy.set(true);
      c.resend(invitation());
      c.revoke(invitation());
      expect(gateway['resendInvite']).not.toHaveBeenCalled();
      expect(gateway['revokeInvite']).not.toHaveBeenCalled();
    });

    it('releases the busy flag when either fails', () => {
      const c = create({ section: 'pending' });
      gateway['resendInvite'].and.returnValue(throwError(() => ({ status: 500 })));
      c.resend(invitation());
      expect(c.isBusy()).toBeFalse();
    });

    it('derives the expiry and status chips', () => {
      const c = create({ section: 'pending' });
      expect(c.expiry(invitation({ expiresAt: '2026-12-01T00:00:00' }))).toBeTruthy();
      expect(c.status(invitation())).toBeTruthy();
    });
  });

  describe('internal users', () => {
    function userRow(over: Record<string, unknown> = {}) {
      return { id: 'u1', fullName: 'Ada Lovelace', isActive: true, ...over };
    }

    it('opens the create form with a default role', () => {
      const c = create({ section: 'staff' });
      c.openCreateUser();
      expect(c.createForm()?.roleName).toBe('Intake Staff');
      expect(c.createResult()).toBeNull();
    });

    it('merges a patch into the create form', () => {
      const c = create({ section: 'staff' });
      c.openCreateUser();
      c.patchCreate({ firstName: 'Ada' });
      c.patchCreate({ lastName: 'Lovelace' });
      expect(c.createForm()?.firstName).toBe('Ada');
      expect(c.createForm()?.lastName).toBe('Lovelace');
    });

    it('ignores a patch when the form is closed', () => {
      const c = create({ section: 'staff' });
      expect(() => c.patchCreate({ firstName: 'Ada' })).not.toThrow();
      expect(c.createForm()).toBeNull();
    });

    it('refuses to save without both names and a valid email', () => {
      const c = create({ section: 'staff' });
      c.openCreateUser();
      c.patchCreate({ firstName: 'Ada', lastName: '', email: 'ada@example.test' });

      c.saveCreateUser();

      expect(gateway['createInternalUser']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalled();
    });

    it('creates the user, closes the form and reloads', () => {
      const c = create({ section: 'staff' });
      const reloaded = spyOn(c.reload$, 'next');
      c.openCreateUser();
      c.patchCreate({
        firstName: ' Ada ',
        lastName: ' Lovelace ',
        email: ' ada@example.test ',
        phoneNumber: ' 5555550100 ',
      });

      c.saveCreateUser();

      const body = gateway['createInternalUser'].calls.mostRecent().args[0];
      expect(body.firstName).toBe('Ada');
      expect(body.email).toBe('ada@example.test');
      expect(body.phoneNumber).toBe('5555550100');
      expect(c.createForm()).toBeNull();
      expect(c.createResult()).not.toBeNull();
      expect(reloaded).toHaveBeenCalled();
    });

    it('omits a blank phone number rather than sending an empty string', () => {
      const c = create({ section: 'staff' });
      c.openCreateUser();
      c.patchCreate({ firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test' });

      c.saveCreateUser();

      expect(gateway['createInternalUser'].calls.mostRecent().args[0].phoneNumber).toBeUndefined();
    });

    it('never sends a tenant id for an internal operator', () => {
      /**
       * Internal operators are HOST logins -- the app service forces
       * CurrentTenant.Change(null) -- and office access is granted later on the assignment
       * screen. Sending one here would create the operator inside a single office.
       */
      const c = create({ section: 'staff' });
      c.openCreateUser();
      c.patchCreate({ firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test' });

      c.saveCreateUser();

      expect(gateway['createInternalUser'].calls.mostRecent().args[0].tenantId).toBeUndefined();
    });

    it('refuses to save while busy, and releases the flag on failure', () => {
      const c = create({ section: 'staff' });
      c.openCreateUser();
      c.patchCreate({ firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test' });
      c.isBusy.set(true);
      c.saveCreateUser();
      expect(gateway['createInternalUser']).not.toHaveBeenCalled();

      c.isBusy.set(false);
      gateway['createInternalUser'].and.returnValue(throwError(() => ({ status: 500 })));
      c.saveCreateUser();
      expect(c.isBusy()).toBeFalse();
    });

    it('will not close the create form mid-save', () => {
      // Closing would discard the form while the request is still in flight.
      const c = create({ section: 'staff' });
      c.openCreateUser();
      c.isBusy.set(true);

      c.closeCreateUser();

      expect(c.createForm()).not.toBeNull();
    });

    it('toggles a user active and inactive with the right wording', () => {
      const c = create({ section: 'staff' });

      c.toggleActive(userRow({ isActive: true }));
      expect(gateway['setUserActive']).toHaveBeenCalledWith('u1', false);
      expect(toaster.success).toHaveBeenCalledWith('User deactivated.');

      c.toggleActive(userRow({ isActive: false }));
      expect(gateway['setUserActive']).toHaveBeenCalledWith('u1', true);
      expect(toaster.success).toHaveBeenCalledWith('User reactivated.');
    });

    it('queues a password reset', () => {
      const c = create({ section: 'staff' });
      c.sendReset(userRow());
      expect(gateway['sendPasswordReset']).toHaveBeenCalledWith('u1');
      expect(toaster.success).toHaveBeenCalled();
    });

    it('refuses both while busy', () => {
      const c = create({ section: 'staff' });
      c.isBusy.set(true);
      c.toggleActive(userRow());
      c.sendReset(userRow());
      expect(gateway['setUserActive']).not.toHaveBeenCalled();
      expect(gateway['sendPasswordReset']).not.toHaveBeenCalled();
    });
  });

  describe('practices', () => {
    function officeRow(over: Record<string, unknown> = {}) {
      return {
        id: 'office-1',
        name: 'valley',
        isActive: true,
        concurrencyStamp: 'stamp-1',
        ...over,
      };
    }

    function newPractice(c: Probe, over: Record<string, unknown> = {}): void {
      c.openNewTenant();
      c.patchTenant({
        name: 'valley',
        doctorFirstName: 'Yuri',
        doctorLastName: 'Falkinstein',
        doctorEmail: 'yuri@example.test',
        ...over,
      });
    }

    it('opens a blank create form', () => {
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      expect(c.tenantForm()?.id).toBeNull();
      expect(c.tenantForm()?.isActive).toBeTrue();
      expect(c.tenantTriedSave()).toBeFalse();
    });

    it('opens an edit form from the row', () => {
      const c = create({ section: 'tenants' });
      c.openEditTenant(officeRow());
      expect(c.tenantForm()?.id).toBe('office-1');
      expect(c.tenantForm()?.name).toBe('valley');
      expect(c.tenantForm()?.concurrencyStamp).toBe('stamp-1');
    });

    it('hides field errors until a save is attempted', () => {
      /**
       * The New Practice modal opens blank, so showing every required-field error
       * immediately would greet the operator with a form that looks broken before they
       * have typed anything.
       */
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      expect(c.slugError()).toBeFalse();
      expect(c.doctorFirstError()).toBeFalse();
      expect(c.doctorEmailError()).toBeFalse();

      c.saveTenant();

      expect(c.tenantTriedSave()).toBeTrue();
      expect(c.slugError()).toBeTrue();
      expect(c.doctorEmailError()).toBeTrue();
    });

    it('rejects a subdomain that is not DNS-safe', () => {
      /**
       * Mirrors the server's TenantNaming rule. Each of these becomes a hostname, so an
       * invalid one produces an office nobody can reach -- and `admin` is reserved because
       * it is the host portal's own subdomain.
       */
      const c = create({ section: 'tenants' });
      for (const bad of ['valley_clinic', '-valley', 'valley-', 'admin', '', 'a'.repeat(64)]) {
        gateway['createPractice'].calls.reset();
        newPractice(c, { name: bad });
        c.saveTenant();
        expect(gateway['createPractice']).withContext(bad).not.toHaveBeenCalled();
      }
    });

    it('accepts an ordinary subdomain', () => {
      const c = create({ section: 'tenants' });
      newPractice(c, { name: 'valley-ortho-2' });
      c.saveTenant();
      expect(gateway['createPractice']).toHaveBeenCalled();
    });

    it('creates the practice, closes the modal and reloads', () => {
      const c = create({ section: 'tenants' });
      const reloaded = spyOn(c.reload$, 'next');
      newPractice(c);

      c.saveTenant();

      expect(toaster.success).toHaveBeenCalledWith('Practice created.');
      expect(c.tenantForm()).toBeNull();
      expect(c.tenantLogo()).toBeNull();
      expect(c.tenantTriedSave()).toBeFalse();
      expect(reloaded).toHaveBeenCalled();
    });

    it('uploads the logo after the practice exists', () => {
      const c = create({ section: 'tenants' });
      newPractice(c);
      c.tenantLogo.set(new File([''], 'logo.png'));

      c.saveTenant();

      expect(gateway['uploadOfficeLogo']).toHaveBeenCalled();
      expect(gateway['uploadOfficeLogo'].calls.mostRecent().args[0]).toBe('office-1');
    });

    it('keeps the practice when the logo upload fails', () => {
      // Best-effort by design: the practice exists either way, and losing it over a logo
      // would be the worse outcome.
      const c = create({ section: 'tenants' });
      gateway['uploadOfficeLogo'].and.returnValue(throwError(() => ({ status: 500 })));
      newPractice(c);
      c.tenantLogo.set(new File([''], 'logo.png'));

      c.saveTenant();

      expect(toaster.success).toHaveBeenCalledWith('Practice created.');
      expect(toaster.warn).toHaveBeenCalled();
    });

    it('requires only a subdomain when editing', () => {
      // Doctor and branding are set at creation and edited on their own screens, so the
      // create-only validation must not run on an edit.
      const c = create({ section: 'tenants' });
      c.openEditTenant(officeRow());

      c.saveTenant();

      expect(gateway['updateTenant']).toHaveBeenCalled();
      expect(gateway['createPractice']).not.toHaveBeenCalled();
    });

    it('refuses an edit that clears the subdomain', () => {
      const c = create({ section: 'tenants' });
      c.openEditTenant(officeRow());
      c.patchTenant({ name: '  ' });

      c.saveTenant();

      expect(gateway['updateTenant']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalled();
    });

    it('previews the default display name from the doctor name', () => {
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      c.patchTenant({ doctorFirstName: ' Yuri ', doctorLastName: 'Falkinstein' });
      expect(c.displayNamePreview()).toBe('Dr. Yuri Falkinstein');
    });

    it('previews a bare prefix before a name is typed', () => {
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      expect(c.displayNamePreview()).toBe('Dr.');
    });

    it('records a chosen logo file', () => {
      const c = create({ section: 'tenants' });
      const file = new File([''], 'logo.png');
      c.onTenantLogoSelected({ target: { files: [file] } } as unknown as Event);
      expect(c.tenantLogo()).toBe(file);
    });

    it('clears the logo when the picker is cancelled', () => {
      const c = create({ section: 'tenants' });
      c.tenantLogo.set(new File([''], 'logo.png'));
      c.onTenantLogoSelected({ target: { files: null } } as unknown as Event);
      expect(c.tenantLogo()).toBeNull();
    });

    it('will not close the tenant modal mid-save', () => {
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      c.isBusy.set(true);

      c.closeTenant();

      expect(c.tenantForm()).not.toBeNull();
    });

    it('clears the logo and the attempt flag on close', () => {
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      c.tenantLogo.set(new File([''], 'logo.png'));
      c.tenantTriedSave.set(true);

      c.closeTenant();

      expect(c.tenantLogo()).toBeNull();
      expect(c.tenantTriedSave()).toBeFalse();
    });

    it('derives the base domain from the current host', () => {
      // Self-correcting per environment: admin.localhost -> localhost.
      const c = create({ section: 'tenants' });
      expect(c.tenantBaseDomain).toBeTruthy();
      expect(typeof c.tenantBaseDomain).toBe('string');
    });

    it('switches into a practice', () => {
      const c = create({ section: 'tenants' });
      c.switchTenant(officeRow());
      expect(impersonateTenant).toHaveBeenCalledWith('office-1', 'admin');
      expect(toaster.info).toHaveBeenCalled();
    });

    it('refuses to switch while busy, and releases the flag on failure', () => {
      const c = create({ section: 'tenants' });
      c.isBusy.set(true);
      c.switchTenant(officeRow());
      expect(impersonateTenant).not.toHaveBeenCalled();

      c.isBusy.set(false);
      impersonateTenant.and.returnValue(throwError(() => ({ status: 500 })));
      c.switchTenant(officeRow());
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('copy to clipboard', () => {
    it('does nothing without text', () => {
      const c = create({ section: 'pending' });
      c.copy(null);
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('writes the text and confirms when a clipboard exists', () => {
      const c = create({ section: 'pending' });
      if (!clipboardWrite) {
        pending('no clipboard API in this browser');
        return;
      }

      c.copy('https://x.test/i');

      expect(clipboardWrite).toHaveBeenCalledWith('https://x.test/i');
      expect(toaster.success).toHaveBeenCalled();
    });
  });

  /**
   * The three server-driven tables' data sources, what a failed mutation leaves behind, and the
   * New Practice guards the practices block above does not reach.
   *
   * <p>The failure handlers show nothing of their own. What they must do is release the busy
   * flag -- or every later action is refused -- and not report success or reload.</p>
   */
  describe('data sources, failed mutations and the remaining practice guards', () => {
    const fail = () => throwError(() => ({ status: 500 }));

    it('hands each table query straight to the gateway', () => {
      const c = create();
      const query = { search: 'ada', sorting: 'email asc', skipCount: 20, maxResultCount: 10 };
      for (const [source, method] of [
        ['invitesDataSource', 'invitesPage'],
        ['internalUsersDataSource', 'internalUsersPage'],
        ['officesDataSource', 'officesPage'],
      ]) {
        c[source](query);
        expect(gateway[method]).withContext(source).toHaveBeenCalledWith(query);
      }
    });

    for (const { label, method, act } of [
      {
        label: 'revoking an invite',
        method: 'revokeInvite',
        act: (c: Probe) => c.revoke({ id: 'inv-1' }),
      },
      {
        label: 'switching a user on or off',
        method: 'setUserActive',
        act: (c: Probe) => c.toggleActive({ id: 'u1', isActive: true }),
      },
      {
        label: 'queueing a password reset',
        method: 'sendPasswordReset',
        act: (c: Probe) => c.sendReset({ id: 'u1' }),
      },
    ]) {
      it(`releases the busy flag and reports nothing when ${label} fails`, () => {
        const c = create({ section: 'staff' });
        const reloaded = spyOn(c.reload$, 'next');
        gateway[method].and.returnValue(fail());

        act(c);

        expect(gateway[method]).toHaveBeenCalled();
        expect(c.isBusy()).toBeFalse();
        expect(toaster.success).not.toHaveBeenCalled();
        expect(reloaded).not.toHaveBeenCalled();
      });
    }

    it('keeps the edit form open when saving a practice fails', () => {
      const c = create({ section: 'tenants' });
      gateway['updateTenant'].and.returnValue(fail());
      c.openEditTenant({
        id: 'office-1',
        name: 'valley',
        isActive: true,
        concurrencyStamp: 'stamp-1',
      });

      c.saveTenant();

      expect(c.tenantForm()).withContext('the operator must be able to retry').not.toBeNull();
      expect(c.isBusy()).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('keeps the create form open, and uploads no logo, when creating a practice fails', () => {
      const c = create({ section: 'tenants' });
      gateway['createPractice'].and.returnValue(fail());
      c.openNewTenant();
      c.patchTenant({
        name: 'valley',
        doctorFirstName: 'Yuri',
        doctorLastName: 'Falkinstein',
        doctorEmail: 'yuri@example.test',
      });
      c.tenantLogo.set(new File([''], 'logo.png'));

      c.saveTenant();

      expect(c.tenantForm()).not.toBeNull();
      expect(c.isBusy()).toBeFalse();
      expect(gateway['uploadOfficeLogo']).not.toHaveBeenCalled();
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('does nothing when no practice form is open', () => {
      const c = create({ section: 'tenants' });
      c.saveTenant();
      expect(gateway['createPractice']).not.toHaveBeenCalled();
      expect(gateway['updateTenant']).not.toHaveBeenCalled();
      expect(toaster.warn).not.toHaveBeenCalled();
    });

    it('does not save a practice while another action is in flight', () => {
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      c.isBusy.set(true);

      c.saveTenant();

      expect(gateway['createPractice']).not.toHaveBeenCalled();
      expect(c.tenantTriedSave()).withContext('refused before the attempt is recorded').toBeFalse();
    });

    it('flags a blank doctor last name, but only once a save has been attempted', () => {
      const c = create({ section: 'tenants' });
      c.openNewTenant();
      c.patchTenant({
        name: 'valley',
        doctorFirstName: 'Yuri',
        doctorLastName: '  ',
        doctorEmail: 'yuri@example.test',
      });
      expect(c.doctorLastError()).toBeFalse();

      c.saveTenant();

      expect(c.doctorLastError()).toBeTrue();
      expect(c.doctorFirstError()).toBeFalse();
      expect(gateway['createPractice']).not.toHaveBeenCalled();
    });
  });
});
