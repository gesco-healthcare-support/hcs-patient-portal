import { TestBed } from '@angular/core/testing';
import { NavigationEnd, Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { Subject, of, throwError } from 'rxjs';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { OAuthService } from 'angular-oauth2-oidc';

import { InternalShellLayoutComponent } from './internal-shell-layout.component';
import { InternalNavBadgeService } from '../../services/internal-nav-badge.service';
import { BrandingService } from '../../branding/branding.service';
import { InternalUsersService } from '../../../proxy/internal-users/internal-users.service';
import { IntakeAssignmentsService } from '../../../proxy/host-operators/intake-assignments.service';
import {
  clearPendingOfficeSwitch,
  readPendingOfficeSwitch,
  storePendingOfficeSwitch,
} from './pending-office-switch';

/**
 * The shell's DERIVED STATE -- the computed signals and the small handlers around them.
 *
 * <p>`internal-shell-layout.component.spec.ts` already covers the office-to-office hop and
 * `buildAuthServerUrl`. What it does not reach is everything the chrome is actually made
 * of: which nav item is active, what the breadcrumb says, which sections default open,
 * whose initials are in the avatar, and who is allowed to open the switcher. Those were
 * 116 uncovered lines across 49 fragments.</p>
 *
 * <p>The two biggest of those fragments carry real rules rather than plumbing, and get
 * the most attention here: `activeId` resolves the nav item by LONGEST route prefix, and
 * `initials` has to survive names that are one word, three words, or absent.</p>
 *
 * <p>Built with `createComponent` but deliberately never change-detected, so `ngOnInit`
 * runs only where a test calls it. That is the same arrangement the sibling spec uses and
 * the reason these tests can drive signals directly without a template.</p>
 *
 * <p>All names and offices below are synthetic.</p>
 */
describe('InternalShellLayoutComponent derived state', () => {
  let impersonation: {
    impersonateTenant: jasmine.Spy;
    impersonate: jasmine.Spy;
    isImpersonatorVisible: jasmine.Spy;
  };
  let toaster: { info: jasmine.Spy; error: jasmine.Spy };
  let badge: {
    start: jasmine.Spy;
    pendingAppointments: jasmine.Spy;
    pendingChangeRequests: jasmine.Spy;
  };
  let title: { setTitle: jasmine.Spy };
  let configUpdates: Subject<unknown>;
  let routerEvents: Subject<unknown>;
  let tenantOptions: jasmine.Spy;
  let switchableOffices: jasmine.Spy;
  let impersonatorInfo: jasmine.Spy;
  let displayName: jasmine.Spy;
  let configValues: Record<string, unknown>;

  interface Probe {
    [key: string]: any;
  }

  function createComponent(
    options: { granted?: boolean; withOAuth?: boolean; issuer?: string } = {},
  ): Probe {
    configUpdates = new Subject();
    routerEvents = new Subject();
    configValues = {};
    impersonation = {
      impersonateTenant: jasmine.createSpy('impersonateTenant').and.returnValue(of({})),
      impersonate: jasmine.createSpy('impersonate').and.returnValue(of({})),
      isImpersonatorVisible: jasmine.createSpy('isImpersonatorVisible').and.returnValue(false),
    };
    toaster = {
      info: jasmine.createSpy('info'),
      error: jasmine.createSpy('error'),
    };
    badge = {
      start: jasmine.createSpy('start'),
      pendingAppointments: jasmine.createSpy('pendingAppointments').and.returnValue(0),
      pendingChangeRequests: jasmine.createSpy('pendingChangeRequests').and.returnValue(0),
    };
    title = { setTitle: jasmine.createSpy('setTitle') };
    tenantOptions = jasmine.createSpy('getTenantOptions').and.returnValue(of({ items: [] }));
    switchableOffices = jasmine
      .createSpy('getSwitchableOffices')
      .and.returnValue(of({ items: [] }));
    impersonatorInfo = jasmine.createSpy('getImpersonatorInfo').and.returnValue(of(null));
    displayName = jasmine.createSpy('displayName').and.returnValue(null);

    const providers: any[] = [
      {
        provide: ConfigStateService,
        useValue: {
          createOnUpdateStream: () => configUpdates,
          getOne: (key: string) => configValues[key] ?? null,
          getAll: () => ({}),
          getDeep: () => null,
        },
      },
      {
        provide: PermissionService,
        useValue: { getGrantedPolicy: () => options.granted !== false },
      },
      { provide: ToasterService, useValue: toaster },
      { provide: Router, useValue: { url: '/', events: routerEvents } },
      { provide: Title, useValue: title },
      { provide: ImpersonationService, useValue: impersonation },
      { provide: InternalNavBadgeService, useValue: badge },
      { provide: BrandingService, useValue: { displayName } },
      { provide: InternalUsersService, useValue: { getTenantOptions: tenantOptions } },
      {
        provide: IntakeAssignmentsService,
        useValue: {
          getSwitchableOffices: switchableOffices,
          getImpersonatorInfo: impersonatorInfo,
        },
      },
    ];
    if (options.withOAuth) {
      providers.push({ provide: OAuthService, useValue: { issuer: options.issuer } });
    }

    TestBed.configureTestingModule({ imports: [InternalShellLayoutComponent], providers });
    // No detectChanges() -> ngOnInit is not invoked.
    return TestBed.createComponent(InternalShellLayoutComponent)
      .componentInstance as unknown as Probe;
  }

  /** A supervisor inside an office: the tenant nav, which is where the nested routes live. */
  function inOfficeSupervisor(): Probe {
    const c = createComponent();
    c.user.set({ name: 'Ada', surname: 'Lovelace', roles: ['Staff Supervisor'] });
    c.hostScope.set(false);
    return c;
  }

  afterEach(() => {
    clearPendingOfficeSwitch();
    TestBed.resetTestingModule();
  });

  describe('activeId -- longest route prefix wins', () => {
    /**
     * The tenant nav carries `/appointments` AND `/appointments/change-requests`, so the
     * nested route is a strict extension of its sibling. That pair is the only reason the
     * rule is "longest prefix" rather than "first match", and every case below is built
     * on it.
     */
    it('matches an item on its exact route', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments');
      expect(c.activeId()).toBe('appointments');
    });

    it('prefers the deeper item when one route extends another', () => {
      // First-match order would answer 'appointments' here: it appears earlier in the
      // group and IS a prefix of this URL. The length comparison is what makes the
      // Change Requests item highlight on its own page.
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments/change-requests');
      expect(c.activeId()).toBe('change-requests');
    });

    it('keeps the parent active on an unregistered child route', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments/a1b2c3');
      expect(c.activeId()).toBe('appointments');
    });

    it('does not match a sibling route that merely starts with the same text', () => {
      /**
       * The guard is `url === r || url.startsWith(r + '/')`. A bare `startsWith(r)` would
       * light up Appointments for `/appointments-archive`, which is a different page.
       */
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments-archive');
      expect(c.activeId()).toBeNull();
    });

    it('ignores the query string', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments/change-requests?tab=cancellations');
      expect(c.activeId()).toBe('change-requests');
    });

    it('ignores the fragment', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments#top');
      expect(c.activeId()).toBe('appointments');
    });

    it('ignores both at once', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments?page=2#row-5');
      expect(c.activeId()).toBe('appointments');
    });

    it('is null on a route no nav item owns', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/account/manage');
      expect(c.activeId()).toBeNull();
    });

    it('is null while the role has not resolved, because there are no groups yet', () => {
      const c = createComponent();
      c.user.set(null);
      c.currentUrl.set('/appointments');
      expect(c.groups()).toEqual([]);
      expect(c.activeId()).toBeNull();
    });
  });

  describe('groups', () => {
    it('is empty until the role key resolves', () => {
      const c = createComponent();
      c.user.set({ roles: ['Patient'] });
      expect(c.groups()).toEqual([]);
    });

    it('gives a host supervisor the platform nav', () => {
      const c = createComponent();
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);
      expect(c.groups().map((g: any) => g.sect)).toContain('Practice Management');
    });

    it('gives an in-office supervisor the tenant nav', () => {
      const c = inOfficeSupervisor();
      expect(c.groups().map((g: any) => g.sect)).toContain('Workspace');
      expect(c.groups().map((g: any) => g.sect)).not.toContain('Practice Management');
    });

    it('hides an item whose ABP policy is not granted', () => {
      /**
       * The nav must never show a link the route guard would 403. Both halves are
       * asserted against the SAME role so the difference is the permission, not the role.
       */
      const granted = inOfficeSupervisor();
      const grantedIds = granted.groups().flatMap((g: any) => g.items.map((i: any) => i.id));
      expect(grantedIds).toContain('reports');

      TestBed.resetTestingModule();
      const refused = createComponent({ granted: false });
      refused.user.set({ roles: ['Staff Supervisor'] });
      refused.hostScope.set(false);
      const refusedIds = refused.groups().flatMap((g: any) => g.items.map((i: any) => i.id));
      expect(refusedIds).not.toContain('reports');
      // Dashboard carries no requiredPolicy, so it survives the refusal -- proving the
      // filter is per-item rather than an all-or-nothing wipe.
      expect(refusedIds).toContain('dashboard');
    });
  });

  describe('activeSect and crumb', () => {
    it('names the section holding the active item', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments');
      expect(c.activeSect()).toBe('Workspace');
    });

    it('has no active section off-nav', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/account/manage');
      expect(c.activeSect()).toBeNull();
    });

    it('shows the active item label as the breadcrumb', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments/change-requests');
      expect(c.crumb()).toBe('Change Requests');
    });

    it('falls back to Dashboard off-nav inside an office', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/account/manage');
      expect(c.crumb()).toBe('Dashboard');
    });

    it('falls back to Overview off-nav on the platform', () => {
      // The two fallbacks differ because the host landing page is called Overview; using
      // one string for both would mislabel one of the two shells.
      const c = createComponent();
      c.user.set({ roles: ['IT Admin'] });
      c.hostScope.set(true);
      c.currentUrl.set('/account/manage');
      expect(c.crumb()).toBe('Overview');
    });
  });

  describe('platform', () => {
    it('is true for an IT Admin at host scope', () => {
      const c = createComponent();
      c.user.set({ roles: ['IT Admin'] });
      c.hostScope.set(true);
      expect(c.platform()).toBeTrue();
    });

    it('is false for an IT Admin who has switched into an office', () => {
      const c = createComponent();
      c.user.set({ roles: ['IT Admin'] });
      c.hostScope.set(false);
      expect(c.platform()).toBeFalse();
    });

    it('is false for a host supervisor', () => {
      const c = createComponent();
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);
      expect(c.platform()).toBeFalse();
    });
  });

  describe('userName', () => {
    it('joins the given name and surname', () => {
      const c = createComponent();
      c.user.set({ name: 'Ada', surname: 'Lovelace' });
      expect(c.userName()).toBe('Ada Lovelace');
    });

    it('falls back to the login when no name is set', () => {
      const c = createComponent();
      c.user.set({ userName: 'a.lovelace' });
      expect(c.userName()).toBe('a.lovelace');
    });

    it('uses whichever name part exists', () => {
      const c = createComponent();
      c.user.set({ surname: 'Lovelace', userName: 'a.lovelace' });
      expect(c.userName()).toBe('Lovelace');
    });

    it('is empty when there is no user at all', () => {
      const c = createComponent();
      c.user.set(null);
      expect(c.userName()).toBe('');
    });

    it('prefers the operator own name while impersonating', () => {
      // Issue #5: the session user is the impersonated OFFICE account, so without this
      // the chip reads "admin" while a named supervisor is driving.
      const c = createComponent();
      c.user.set({ name: 'Office', surname: 'Admin' });
      c.impersonating.set(true);
      c.operatorName.set('Grace Hopper');
      expect(c.userName()).toBe('Grace Hopper');
    });

    it('falls back to the session user until the operator name resolves', () => {
      const c = createComponent();
      c.user.set({ name: 'Office', surname: 'Admin' });
      c.impersonating.set(true);
      c.operatorName.set(null);
      expect(c.userName()).toBe('Office Admin');
    });

    it('ignores a stale operator name once impersonation ends', () => {
      const c = createComponent();
      c.user.set({ name: 'Office', surname: 'Admin' });
      c.impersonating.set(false);
      c.operatorName.set('Grace Hopper');
      expect(c.userName()).toBe('Office Admin');
    });
  });

  describe('initials', () => {
    it('takes the first letter of the first and last name', () => {
      const c = createComponent();
      c.user.set({ name: 'Ada', surname: 'Lovelace' });
      expect(c.initials()).toBe('AL');
    });

    it('takes a single letter from a one-word name', () => {
      const c = createComponent();
      c.user.set({ userName: 'ada' });
      expect(c.initials()).toBe('A');
    });

    it('skips the middle name rather than producing three letters', () => {
      // `parts.at(-1)` is the LAST part, not the second. An avatar with three letters
      // overflows the chip.
      const c = createComponent();
      c.user.set({ name: 'Ada Byron', surname: 'Lovelace' });
      expect(c.initials()).toBe('AL');
    });

    it('collapses repeated whitespace rather than reading a blank part', () => {
      const c = createComponent();
      c.user.set({ name: 'Ada', surname: '  Lovelace' });
      expect(c.initials()).toBe('AL');
    });

    it('uppercases a lowercase login', () => {
      const c = createComponent();
      c.user.set({ userName: 'ada.lovelace' });
      expect(c.initials()).toBe('A');
    });

    it('falls back to a question mark when there is no name', () => {
      // An empty avatar reads as a rendering bug; '?' reads as "not loaded yet".
      const c = createComponent();
      c.user.set(null);
      expect(c.initials()).toBe('?');
    });

    it('falls back to a question mark for a whitespace-only name', () => {
      const c = createComponent();
      c.user.set({ userName: '   ' });
      expect(c.initials()).toBe('?');
    });
  });

  describe('avatar', () => {
    it('derives a colour from the displayed name', () => {
      const c = createComponent();
      c.user.set({ name: 'Ada', surname: 'Lovelace' });
      expect(c.avatar()).toBeTruthy();
    });

    it('is stable for the same name', () => {
      const c = createComponent();
      c.user.set({ name: 'Ada', surname: 'Lovelace' });
      const first = c.avatar();
      c.user.set({ name: 'Ada', surname: 'Lovelace' });
      expect(c.avatar()).toBe(first);
    });

    it('still resolves with no user', () => {
      const c = createComponent();
      c.user.set(null);
      expect(c.avatar()).toBeTruthy();
    });
  });

  describe('brand and tenant labels', () => {
    it('uses the constant app brand everywhere', () => {
      const c = createComponent();
      expect(c.brandName()).toBe('Appointment Portal');
    });

    it('shows the Evaluators crest at host scope only', () => {
      const c = createComponent();
      c.hostScope.set(true);
      expect(c.brandLogo()).toBe('assets/branding/evaluators-logo.png');
      c.hostScope.set(false);
      expect(c.brandLogo()).toBeNull();
    });

    it('prefers the office branded display name', () => {
      const c = createComponent();
      displayName.and.returnValue('Valley Orthopaedics');
      c.tenant.set({ id: 'office-a', name: 'Falkinstein' });
      expect(c.tenantName()).toBe('Valley Orthopaedics');
    });

    it('falls back to a Dr. prefix when branding is unresolved', () => {
      // The impersonation path: on the host subdomain branding resolves to null even
      // though a tenant is active, so the office would otherwise read "Appointment Portal".
      const c = createComponent();
      displayName.and.returnValue(null);
      c.tenant.set({ id: 'office-a', name: 'Falkinstein' });
      expect(c.tenantName()).toBe('Dr. Falkinstein');
    });

    it('treats a whitespace-only display name as unset', () => {
      const c = createComponent();
      displayName.and.returnValue('   ');
      c.tenant.set({ id: 'office-a', name: 'Falkinstein' });
      expect(c.tenantName()).toBe('Dr. Falkinstein');
    });

    it('falls back to the app brand at true host scope', () => {
      const c = createComponent();
      displayName.and.returnValue(null);
      c.tenant.set(null);
      expect(c.tenantName()).toBe('Appointment Portal');
    });

    it('takes the tenant initial from the resolved name', () => {
      const c = createComponent();
      displayName.and.returnValue('valley orthopaedics');
      expect(c.tenantInitial()).toBe('V');
    });

    it('defaults the tenant initial to A when the name is blank', () => {
      const c = createComponent();
      displayName.and.returnValue(' ');
      c.tenant.set({ id: null, name: '' });
      expect(c.tenantInitial()).toBe('A');
    });

    it('reads Management as the subtitle at host scope', () => {
      const c = createComponent();
      c.hostScope.set(true);
      expect(c.brandSubtitle()).toBe('Management');
    });

    it('reads the office name as the subtitle inside an office', () => {
      const c = createComponent();
      displayName.and.returnValue('Valley Orthopaedics');
      c.hostScope.set(false);
      expect(c.brandSubtitle()).toBe('Valley Orthopaedics');
    });
  });

  describe('badgeCount', () => {
    it('reads the pending appointment count', () => {
      const c = createComponent();
      badge.pendingAppointments.and.returnValue(4);
      expect(c.badgeCount('appointments')).toBe(4);
    });

    it('reads the pending change-request count', () => {
      const c = createComponent();
      badge.pendingChangeRequests.and.returnValue(7);
      expect(c.badgeCount('changeRequests')).toBe(7);
    });

    it('is zero for an item that carries no badge', () => {
      const c = createComponent();
      expect(c.badgeCount(undefined)).toBe(0);
      expect(badge.pendingAppointments).not.toHaveBeenCalled();
    });
  });

  describe('sidebar and menu toggles', () => {
    it('toggles the rail collapse', () => {
      const c = createComponent();
      expect(c.collapsed()).toBeFalse();
      c.toggleCollapse();
      expect(c.collapsed()).toBeTrue();
      c.toggleCollapse();
      expect(c.collapsed()).toBeFalse();
    });

    it('opens the section holding the active route by default', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments');
      expect(c.isSectionOpen('Workspace')).toBeTrue();
    });

    it('leaves other sections collapsed by default', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments');
      expect(c.isSectionOpen('Configuration')).toBeFalse();
    });

    it('lets an explicit toggle override the default', () => {
      // The override is the point: an operator who opens Configuration expects it to
      // stay open while they work, even though the active route is elsewhere.
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments');
      c.toggleSection('Configuration');
      expect(c.isSectionOpen('Configuration')).toBeTrue();
    });

    it('lets an explicit toggle close the active section', () => {
      const c = inOfficeSupervisor();
      c.currentUrl.set('/appointments');
      c.toggleSection('Workspace');
      expect(c.isSectionOpen('Workspace')).toBeFalse();
    });

    it('toggles and closes the account menu', () => {
      const c = createComponent();
      c.toggleAcct();
      expect(c.acctOpen()).toBeTrue();
      c.closeAcct();
      expect(c.acctOpen()).toBeFalse();
    });

    it('closes both menus on Escape', () => {
      const c = createComponent();
      c.acctOpen.set(true);
      c.switcherOpen.set(true);
      c.onEscape();
      expect(c.acctOpen()).toBeFalse();
      expect(c.switcherOpen()).toBeFalse();
    });
  });

  describe('canSwitch', () => {
    it('lets a host supervisor open the switcher', () => {
      const c = createComponent();
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);
      expect(c.canSwitch()).toBeTrue();
    });

    it('refuses a host Intake operator, who is tenant-locked', () => {
      const c = createComponent();
      c.user.set({ roles: ['Intake Staff'] });
      c.hostScope.set(true);
      expect(c.canSwitch()).toBeFalse();
    });

    it('lets any impersonating operator inside an office open it', () => {
      const c = createComponent();
      c.user.set({ roles: ['Intake Staff'] });
      c.hostScope.set(false);
      c.impersonating.set(true);
      expect(c.canSwitch()).toBeTrue();
    });

    it('refuses a non-impersonating user inside an office', () => {
      const c = createComponent();
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(false);
      c.impersonating.set(false);
      expect(c.canSwitch()).toBeFalse();
    });
  });

  describe('the office switcher', () => {
    it('does nothing when the caller may not switch', () => {
      const c = createComponent();
      c.user.set({ roles: ['Intake Staff'] });
      c.hostScope.set(true);
      c.toggleSwitcher();
      expect(c.switcherOpen()).toBeFalse();
      expect(tenantOptions).not.toHaveBeenCalled();
    });

    it('loads the office list on first open', () => {
      const c = createComponent();
      tenantOptions.and.returnValue(of({ items: [{ id: 'office-b', displayName: 'Office B' }] }));
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);

      c.toggleSwitcher();

      expect(c.switcherOpen()).toBeTrue();
      expect(c.offices().map((o: any) => o.id)).toEqual(['office-b']);
    });

    it('does not refetch the list on a later open', () => {
      // The list is loaded lazily but only once -- reopening the menu must not put a
      // request on the wire per click.
      const c = createComponent();
      tenantOptions.and.returnValue(of({ items: [{ id: 'office-b', displayName: 'Office B' }] }));
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);

      c.toggleSwitcher();
      c.toggleSwitcher();
      c.toggleSwitcher();

      expect(tenantOptions).toHaveBeenCalledTimes(1);
    });

    it('asks the assignments service for an in-office Intake operator', () => {
      /**
       * The in-office shadow Intake user does not hold IntakeImpersonation, so the
       * host-scoped tenant lookup would refuse it. Its targets come from the assignment
       * list resolved server-side from the impersonation claim instead.
       */
      const c = createComponent();
      c.user.set({ roles: ['Intake Staff'] });
      c.hostScope.set(false);
      c.impersonating.set(true);

      c.toggleSwitcher();

      expect(switchableOffices).toHaveBeenCalled();
      expect(tenantOptions).not.toHaveBeenCalled();
    });

    it('asks the tenant lookup for an in-office supervisor', () => {
      const c = createComponent();
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(false);
      c.impersonating.set(true);

      c.toggleSwitcher();

      expect(tenantOptions).toHaveBeenCalled();
      expect(switchableOffices).not.toHaveBeenCalled();
    });

    it('drops the current office from the target list', () => {
      /**
       * A REMOVAL, so the fixture includes the current office in the response. Without it
       * the filter is a no-op and this passes with `excludeCurrentOffice` deleted.
       */
      const c = createComponent();
      tenantOptions.and.returnValue(
        of({
          items: [
            { id: 'office-a', displayName: 'Office A' },
            { id: 'office-b', displayName: 'Office B' },
          ],
        }),
      );
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(false);
      c.impersonating.set(true);
      c.tenant.set({ id: 'office-a', name: 'A' });

      c.toggleSwitcher();

      expect(c.offices().map((o: any) => o.id)).toEqual(['office-b']);
    });

    it('keeps every office when there is no current one', () => {
      const c = createComponent();
      tenantOptions.and.returnValue(of({ items: [{ id: 'office-a' }, { id: 'office-b' }] }));
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);
      c.tenant.set(null);

      c.toggleSwitcher();

      expect(c.offices().length).toBe(2);
    });

    it('leaves the list empty when the lookup fails', () => {
      // A failed lookup must not strand a half-populated menu.
      const c = createComponent();
      tenantOptions.and.returnValue(throwError(() => ({ status: 500 })));
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);

      c.toggleSwitcher();

      expect(c.offices()).toEqual([]);
      expect(c.switcherOpen()).toBeTrue();
    });

    it('tolerates a response with no items array', () => {
      const c = createComponent();
      tenantOptions.and.returnValue(of({}));
      c.user.set({ roles: ['Staff Supervisor'] });
      c.hostScope.set(true);
      c.toggleSwitcher();
      expect(c.offices()).toEqual([]);
    });

    it('closes the switcher', () => {
      const c = createComponent();
      c.switcherOpen.set(true);
      c.closeSwitcher();
      expect(c.switcherOpen()).toBeFalse();
    });
  });

  describe('switchInto failure paths', () => {
    it('ignores a click with no office id', () => {
      const c = createComponent();
      c.switchInto(undefined);
      expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
      expect(c.switching()).toBeFalse();
    });

    it('ignores a second click while one switch is in flight', () => {
      const c = createComponent();
      c.switching.set(true);
      c.switchInto('office-b');
      expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
    });

    it('reports a failed host-scope switch and releases the spinner', () => {
      /**
       * Impersonation runs through the OAuth token grant, NOT RestService, so ABP's
       * global HTTP error dialog never fires. Without this handler the spinner simply
       * stops and the UI looks frozen.
       */
      const c = createComponent();
      impersonation.impersonateTenant.and.returnValue(throwError(() => ({ status: 500 })));
      c.hostScope.set(true);
      c.impersonating.set(false);

      c.switchInto('office-b');

      expect(c.switching()).toBeFalse();
      expect(toaster.error).toHaveBeenCalled();
    });

    it('clears the pending record when the de-impersonation leg fails', () => {
      /**
       * Office -> office stashes the target BEFORE de-impersonating. If that leg fails
       * the record has to go, or the operator is auto-bounced into an office they never
       * reached on their next load.
       */
      const c = createComponent();
      impersonation.impersonate.and.returnValue(throwError(() => ({ status: 500 })));
      c.hostScope.set(false);
      c.impersonating.set(true);

      c.switchInto('office-b');

      expect(c.switching()).toBeFalse();
      expect(toaster.error).toHaveBeenCalled();
      expect(readPendingOfficeSwitch()).toBeNull();
    });

    it('closes the menu as the switch starts', () => {
      const c = createComponent();
      c.switcherOpen.set(true);
      c.hostScope.set(true);
      c.switchInto('office-b');
      expect(c.switcherOpen()).toBeFalse();
    });
  });

  describe('backToHost', () => {
    it('exits impersonation with an empty target', () => {
      const c = createComponent();
      c.backToHost();
      expect(impersonation.impersonate).toHaveBeenCalledWith({});
      expect(c.switching()).toBeTrue();
    });

    it('ignores a second click while one exit is in flight', () => {
      const c = createComponent();
      c.switching.set(true);
      c.backToHost();
      expect(impersonation.impersonate).not.toHaveBeenCalled();
    });

    it('reports a failed exit and releases the spinner', () => {
      const c = createComponent();
      impersonation.impersonate.and.returnValue(throwError(() => ({ status: 500 })));
      c.backToHost();
      expect(c.switching()).toBeFalse();
      expect(toaster.error).toHaveBeenCalled();
    });
  });

  describe('maybeResumePendingSwitch failure path', () => {
    it('reports a failed second leg and releases the spinner', () => {
      const c = createComponent();
      impersonation.impersonateTenant.and.returnValue(throwError(() => ({ status: 500 })));
      storePendingOfficeSwitch({ officeId: 'office-b', userName: '' });
      c.hostScope.set(true);
      c.impersonating.set(false);

      c.maybeResumePendingSwitch();

      expect(c.switching()).toBeFalse();
      expect(toaster.error).toHaveBeenCalled();
    });
  });

  describe('refreshIdentity', () => {
    it('reads the user and tenant from the config state', () => {
      const c = createComponent();
      configValues['currentUser'] = {
        name: 'Ada',
        surname: 'Lovelace',
        roles: ['Staff Supervisor'],
      };
      configValues['currentTenant'] = { id: 'office-a', name: 'Falkinstein' };

      c.refreshIdentity();

      expect(c.userName()).toBe('Ada Lovelace');
      expect(c.tenant().name).toBe('Falkinstein');
    });

    it('resolves the operator own role while impersonating', () => {
      // QA item 5: the session token carries the OFFICE account's role, so the header
      // would otherwise label a supervisor "Administrator".
      const c = createComponent();
      impersonation.isImpersonatorVisible.and.returnValue(true);
      impersonatorInfo.and.returnValue(
        of({ isImpersonating: true, roles: ['Staff Supervisor'], name: 'Grace Hopper' }),
      );

      c.refreshIdentity();

      expect(c.operatorRoleKey()).toBe('supervisor');
      expect(c.operatorName()).toBe('Grace Hopper');
    });

    it('fetches the operator info only once per impersonation', () => {
      // The operator is stable across office -> office hops, and refreshIdentity runs on
      // every config emission -- one request per emission would be a request storm.
      const c = createComponent();
      impersonation.isImpersonatorVisible.and.returnValue(true);
      impersonatorInfo.and.returnValue(
        of({ isImpersonating: true, roles: ['IT Admin'], name: 'X' }),
      );

      c.refreshIdentity();
      c.refreshIdentity();
      c.refreshIdentity();

      expect(impersonatorInfo).toHaveBeenCalledTimes(1);
    });

    it('clears the operator identity when the server says it is not impersonating', () => {
      /**
       * MY FIRST FIXTURE DEFEATED THIS BRANCH, and the reason is worth keeping.
       *
       * The fetch is guarded by `operatorRoleKey() === null`. Seeding operatorRoleKey to
       * 'supervisor' -- which is what "clear the identity" seemed to call for -- meant the
       * guard was false, no request was made, and nothing was cleared. The test failed
       * against correct code.
       *
       * So the seed goes on operatorName INSTEAD: it is not part of the guard, it can
       * legitimately be non-null while the role key is null, and clearing it is
       * observable. Asserting operatorRoleKey alone here would be vacuous -- it starts
       * null and ends null whatever the branch does.
       */
      const c = createComponent();
      impersonation.isImpersonatorVisible.and.returnValue(true);
      impersonatorInfo.and.returnValue(of({ isImpersonating: false }));
      c.operatorName.set('Grace Hopper');
      expect(c.operatorRoleKey()).toBeNull(); // load-bearing precondition: lets the fetch run

      c.refreshIdentity();

      expect(impersonatorInfo).toHaveBeenCalled();
      expect(c.operatorName()).toBeNull();
      expect(c.operatorRoleKey()).toBeNull();
    });

    it('clears the operator identity when the lookup fails', () => {
      const c = createComponent();
      impersonation.isImpersonatorVisible.and.returnValue(true);
      impersonatorInfo.and.returnValue(throwError(() => ({ status: 500 })));

      c.refreshIdentity();

      expect(c.operatorRoleKey()).toBeNull();
      expect(c.operatorName()).toBeNull();
    });

    it('clears a stale operator identity on de-impersonation', () => {
      /**
       * A REMOVAL, so the fixture seeds the operator identity first. Left behind, the
       * header would keep showing the impersonated session's operator after it ended.
       */
      const c = createComponent();
      c.operatorRoleKey.set('supervisor');
      c.operatorName.set('Grace Hopper');
      impersonation.isImpersonatorVisible.and.returnValue(false);

      c.refreshIdentity();

      expect(c.operatorRoleKey()).toBeNull();
      expect(c.operatorName()).toBeNull();
      expect(impersonatorInfo).not.toHaveBeenCalled();
    });

    it('sets the host tab title when no office branding applies', () => {
      const c = createComponent();
      displayName.and.returnValue(null);
      c.refreshIdentity();
      expect(title.setTitle).toHaveBeenCalledWith('Appointment Portal');
    });

    it('leaves the tab title alone when an office has branded it', () => {
      // Offices keep their per-office tab title so several office tabs stay tellable
      // apart; overwriting it here would make them all read the same.
      const c = createComponent();
      displayName.and.returnValue('Valley Orthopaedics');
      c.refreshIdentity();
      expect(title.setTitle).not.toHaveBeenCalled();
    });
  });

  describe('lifecycle', () => {
    it('starts the badge poller and seeds identity on init', () => {
      const c = createComponent();
      configValues['currentUser'] = { name: 'Ada', surname: 'Lovelace' };

      c.ngOnInit();

      expect(badge.start).toHaveBeenCalled();
      expect(c.userName()).toBe('Ada Lovelace');
    });

    it('re-reads identity when the config state emits', () => {
      const c = createComponent();
      c.ngOnInit();
      configValues['currentUser'] = { name: 'Grace', surname: 'Hopper' };

      configUpdates.next({});

      expect(c.userName()).toBe('Grace Hopper');
    });

    it('follows the router to the redirected URL', () => {
      /**
       * `urlAfterRedirects`, not `url`: a guard that redirects would otherwise leave the
       * nav highlighting the route the user asked for rather than the one they landed on.
       */
      const c = createComponent();
      c.ngOnInit();

      routerEvents.next(new NavigationEnd(1, '/appointments', '/appointments/change-requests'));

      expect(c.currentUrl()).toBe('/appointments/change-requests');
    });

    it('ignores router events that are not navigation ends', () => {
      const c = createComponent();
      c.ngOnInit();
      const before = c.currentUrl();

      routerEvents.next({ id: 2, url: '/somewhere' });

      expect(c.currentUrl()).toBe(before);
    });

    it('stops listening on destroy', () => {
      // Both subscriptions live for the component's lifetime, so a missed unsubscribe
      // leaks a listener per shell mount.
      const c = createComponent();
      c.ngOnInit();
      c.ngOnDestroy();
      configValues['currentUser'] = { name: 'Grace', surname: 'Hopper' };

      configUpdates.next({});
      routerEvents.next(new NavigationEnd(1, '/x', '/x'));

      expect(c.userName()).not.toBe('Grace Hopper');
      expect(c.currentUrl()).not.toBe('/x');
    });

    it('survives a destroy that follows no init', () => {
      const c = createComponent();
      expect(() => c.ngOnDestroy()).not.toThrow();
    });
  });

  describe('manageAccountUrl', () => {
    it('builds the account page from the runtime issuer', () => {
      const c = createComponent({ withOAuth: true, issuer: 'https://admin.auth.example.test' });
      expect(c.manageAccountUrl).toBe('https://admin.auth.example.test/Account/Manage');
    });

    it('falls back to the bare path when the issuer is unset', () => {
      const c = createComponent({ withOAuth: true, issuer: undefined });
      expect(c.manageAccountUrl).toBe('/Account/Manage');
    });

    it('falls back to the bare path when OAuth is not available at all', () => {
      /**
       * The try/catch exists because `injector.get(OAuthService)` THROWS when the
       * provider is absent. Omitting the provider here is what reaches the catch -- a
       * stub returning undefined would take the other branch entirely.
       */
      const c = createComponent({ withOAuth: false });
      expect(c.manageAccountUrl).toBe('/Account/Manage');
    });
  });
});
