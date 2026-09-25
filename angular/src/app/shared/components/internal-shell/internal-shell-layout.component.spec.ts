import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import {
  buildAuthServerUrl,
  InternalShellLayoutComponent,
} from './internal-shell-layout.component';
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
 * F Half 2 office -> office two-hop orchestration. We exercise switchInto() and
 * maybeResumePendingSwitch() directly (cast past `protected`) without calling
 * detectChanges(), so ngOnInit's config/router/badge subscriptions never run --
 * keeping the test focused on the hop logic with stubbed services.
 */
describe('InternalShellLayoutComponent office-to-office switch', () => {
  let impersonation: {
    impersonateTenant: jasmine.Spy;
    impersonate: jasmine.Spy;
    isImpersonatorVisible: jasmine.Spy;
  };

  function createComponent(): any {
    impersonation = {
      impersonateTenant: jasmine.createSpy('impersonateTenant').and.returnValue(of({})),
      impersonate: jasmine.createSpy('impersonate').and.returnValue(of({})),
      isImpersonatorVisible: jasmine.createSpy('isImpersonatorVisible').and.returnValue(false),
    };

    TestBed.configureTestingModule({
      imports: [InternalShellLayoutComponent],
      providers: [
        {
          provide: ConfigStateService,
          useValue: { createOnUpdateStream: () => new Subject(), getOne: () => null },
        },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => false } },
        { provide: ToasterService, useValue: { info: () => undefined, error: () => undefined } },
        { provide: Router, useValue: { url: '/', events: new Subject() } },
        { provide: Title, useValue: { setTitle: () => undefined } },
        { provide: ImpersonationService, useValue: impersonation },
        {
          provide: InternalNavBadgeService,
          useValue: {
            start: () => undefined,
            pendingAppointments: () => 0,
            pendingChangeRequests: () => 0,
          },
        },
        { provide: BrandingService, useValue: { displayName: () => null } },
        { provide: InternalUsersService, useValue: { getTenantOptions: () => of({ items: [] }) } },
        {
          provide: IntakeAssignmentsService,
          useValue: { getSwitchableOffices: () => of({ items: [] }) },
        },
      ],
    });

    // No detectChanges() -> ngOnInit is not invoked.
    return TestBed.createComponent(InternalShellLayoutComponent).componentInstance;
  }

  afterEach(() => {
    clearPendingOfficeSwitch();
    TestBed.resetTestingModule();
  });

  it('host-scope switchInto does a direct impersonateTenant and stores nothing', () => {
    const c = createComponent();
    c.hostScope.set(true);
    c.impersonating.set(false);
    c.user.set({ roles: ['Staff Supervisor'] });

    c.switchInto('office-b');

    // task_2e8e4dc2: every tier now sends an empty username -> the grant lands the operator as
    // their own per-office shadow (Supervisor role here), never the shared office admin account.
    expect(impersonation.impersonateTenant).toHaveBeenCalledWith('office-b', '');
    expect(impersonation.impersonate).not.toHaveBeenCalled();
    expect(readPendingOfficeSwitch()).toBeNull();
  });

  it('in-office switchInto stashes the target and de-impersonates to host', () => {
    const c = createComponent();
    c.hostScope.set(false);
    c.impersonating.set(true);
    c.user.set({ roles: ['admin'] });
    c.tenant.set({ id: 'office-a' });

    c.switchInto('office-b');

    expect(impersonation.impersonate).toHaveBeenCalledWith({});
    expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
    // task_2e8e4dc2: the stashed username is empty for every tier (own-shadow target).
    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: '' });
  });

  it('in-office intake switchInto stashes an empty username (shadow-user target)', () => {
    const c = createComponent();
    c.hostScope.set(false);
    c.impersonating.set(true);
    c.user.set({ roles: ['Intake Staff'] });

    c.switchInto('office-b');

    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: '' });
  });

  it('resumes a pending switch once back at host scope, clearing the record first', () => {
    const c = createComponent();
    storePendingOfficeSwitch({ officeId: 'office-b', userName: 'admin' });
    c.hostScope.set(true);
    c.impersonating.set(false);

    c.maybeResumePendingSwitch();

    expect(impersonation.impersonateTenant).toHaveBeenCalledWith('office-b', 'admin');
    expect(readPendingOfficeSwitch()).toBeNull();
  });

  it('does not resume while still impersonating (in office A)', () => {
    const c = createComponent();
    storePendingOfficeSwitch({ officeId: 'office-b', userName: 'admin' });
    c.hostScope.set(false);
    c.impersonating.set(true);

    c.maybeResumePendingSwitch();

    expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
    expect(readPendingOfficeSwitch()).toEqual({ officeId: 'office-b', userName: 'admin' });
  });

  it('does nothing at host scope when no switch is pending', () => {
    const c = createComponent();
    c.hostScope.set(true);
    c.impersonating.set(false);

    c.maybeResumePendingSwitch();

    expect(impersonation.impersonateTenant).not.toHaveBeenCalled();
  });

  // QA item 5 (2026-07-10): the header shows the operator's OWN role while impersonating.
  it('labels an impersonated session with the operator own role, not the office role', () => {
    const c = createComponent();
    c.user.set({ roles: ['admin'] }); // session runs as the impersonated office 'admin'
    c.impersonating.set(true);
    c.operatorRoleKey.set('supervisor'); // resolved from the impersonation claim

    expect(c.roleLabel()).toBe('Staff Supervisor');
  });

  it('labels a non-impersonated session with the session role', () => {
    const c = createComponent();
    c.user.set({ roles: ['admin'] });
    c.impersonating.set(false);

    expect(c.roleLabel()).toBe('Administrator');
  });
});

/**
 * Sweep #640. `buildAuthServerUrl` composes the AuthServer Razor URL from the runtime OAuth
 * issuer, which carries the correct host:port for the current tenant subdomain. Getting the
 * join wrong sends a user to the wrong office's login, so it is worth pinning.
 *
 * <p>The trailing-slash trim used to be `replace(/\/+$/, '')`. Sonar flagged it (S8786) and
 * was right: `+` retries every split before `$` can fail, so `"/".repeat(n) + "a"` backtracks
 * quadratically. Scanning back from the end is linear. Exported so the replacement has a test
 * rather than only a comment.</p>
 */
describe('buildAuthServerUrl (sweep #640)', () => {
  it('joins the issuer and the path', () => {
    expect(buildAuthServerUrl('https://admin.auth.example.test', '/Account/Manage')).toBe(
      'https://admin.auth.example.test/Account/Manage',
    );
  });

  it('trims a single trailing slash so the path is not doubled', () => {
    expect(buildAuthServerUrl('https://admin.auth.example.test/', '/Account/Manage')).toBe(
      'https://admin.auth.example.test/Account/Manage',
    );
  });

  it('trims a run of trailing slashes', () => {
    expect(buildAuthServerUrl('https://admin.auth.example.test///', '/Account/Manage')).toBe(
      'https://admin.auth.example.test/Account/Manage',
    );
  });

  it('keeps slashes that are not at the end', () => {
    // The scan must stop at the first non-slash from the right, not strip every slash.
    expect(buildAuthServerUrl('https://host.test/base/', '/x')).toBe('https://host.test/base/x');
  });

  it('falls back to the bare path when the issuer is empty', () => {
    expect(buildAuthServerUrl('', '/Account/Manage')).toBe('/Account/Manage');
  });

  it('falls back to the bare path when the issuer is only slashes', () => {
    // Trimming leaves an empty base, which must take the fallback rather than emit "//x".
    expect(buildAuthServerUrl('///', '/Account/Manage')).toBe('/Account/Manage');
  });

  it('falls back when the issuer is null at runtime', () => {
    expect(buildAuthServerUrl(null as unknown as string, '/Account/Manage')).toBe(
      '/Account/Manage',
    );
  });

  it('is linear on the input that made the old regex quadratic', () => {
    // "/".repeat(n) + "a" is the adversarial shape for /\/+$/. The assertion is the result;
    // the point of the case is that it returns at all rather than stalling.
    const issuer = '/'.repeat(20000) + 'a';
    const started = Date.now();
    expect(buildAuthServerUrl(issuer, '/x')).toBe(issuer + '/x');
    expect(Date.now() - started).toBeLessThan(1000);
  });
});
