import { TestBed } from '@angular/core/testing';
import { Injector, runInInjectionContext } from '@angular/core';
import { Route, Routes } from '@angular/router';
import { ConfigStateService, PermissionService } from '@abp/ng.core';

import { APP_ROUTES } from './app.routes';

/**
 * The bare-`/admin` landing redirect (app.routes.ts, 2026-07-31): which admin section a caller
 * is sent to when they open /admin with no section named.
 *
 * <p>WHY ONLY THIS ONE ROUTE. app.routes.ts carries 60 uncovered lines. FIFTY-FIVE of them are
 * `loadComponent: () =&gt; import(...)` arrow bodies; covering one means invoking that dynamic
 * import, which pulls the feature component and its whole dependency graph into the test bundle.
 * Doing it for every route would load most of the application into a unit test to prove that an
 * import statement imports. The remaining FIVE are this redirect -- real logic, with a real bug
 * behind it -- and they are reachable without resolving a single component. So the arrows are
 * deliberately left alone and this is the part that gets a test.</p>
 *
 * <p>THE BUG IT FIXED, from the comment at the route: /admin used to redirect to a FIXED section
 * (`templates`), which 403s for any role lacking CaseEvaluation.NotificationTemplates -- a Staff
 * Supervisor opening /admin landed on "You don't have access" and could never reach the rail at
 * all. It now resolves to the first section the caller can actually see, through the SAME rule
 * the rail uses.</p>
 *
 * <p>WHAT THIS SPEC OWNS, and what it does not. The rule itself -- `isAdminSectionVisible` and
 * `firstVisibleAdminSection` -- is pure and is covered where it lives, in the admin-hub util.
 * What is only observable HERE is the WIRING: that the route actually calls that rule, that it
 * hands it the host-scope flag rather than a constant, and that it falls back to somewhere the
 * caller can use. Those are the four lines of the arrow body plus its fallback.</p>
 *
 * <p>No patient data is involved; the policy and route strings below are the real constants,
 * quoted so a rename has to come past this file.</p>
 */
describe('APP_ROUTES bare /admin redirect', () => {
  /** Policies gating the admin rail, as declared in ADMIN_SECTIONS. */
  const TEMPLATES = 'CaseEvaluation.NotificationTemplates';
  const PARAMETERS = 'CaseEvaluation.SystemParameters';
  const ROLES = 'AbpIdentity.Roles';
  const AUDIT = 'AuditLogging.AuditLogs';

  let granted: Set<string>;
  let hostScope: boolean;

  /** Mirrors the existing app.routes.spec.ts helper: match on the declared path. */
  function collect(routes: Routes, path: string, found: Route[] = []): Route[] {
    for (const route of routes) {
      if (route.path === path) {
        found.push(route);
      }
      if (route.children) {
        collect(route.children, path, found);
      }
    }
    return found;
  }

  /** The empty-path child of `admin` -- the one carrying the function redirect. */
  function landing(): Route | undefined {
    for (const admin of collect(APP_ROUTES, 'admin')) {
      const child = (admin.children ?? []).find(
        (c) => c.path === '' && typeof c.redirectTo === 'function',
      );
      if (child) {
        return child;
      }
    }
    return undefined;
  }

  /**
   * Run the route's redirectTo the way the router does: inside an injection context, so its
   * two `inject()` calls resolve to the stubs configured below.
   */
  function resolve(): string {
    const redirect = landing()?.redirectTo as () => string;
    return runInInjectionContext(TestBed.inject(Injector), () => redirect());
  }

  beforeEach(() => {
    granted = new Set<string>();
    hostScope = false;

    TestBed.configureTestingModule({
      providers: [
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (policy: string) => granted.has(policy) },
        },
        {
          provide: ConfigStateService,
          // isHostScope() reads currentTenant and asks whether it has an id.
          useValue: { getOne: () => (hostScope ? null : { id: 'tenant-1' }) },
        },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('declares the landing as a FUNCTION redirect on the exact empty path', () => {
    /**
     * The shape IS the fix. A string redirectTo cannot consult the caller's permissions,
     * which is precisely how every role ended up pointed at the same section.
     */
    const route = landing();

    expect(route).withContext('/admin must have an empty-path child').toBeDefined();
    expect(typeof route?.redirectTo).toBe('function');
    expect(route?.pathMatch)
      .withContext('a non-full match would swallow /admin/templates too')
      .toBe('full');
  });

  it('sends a Staff Supervisor to Notification Templates, the first section they can see', () => {
    granted = new Set([TEMPLATES, PARAMETERS]);

    expect(resolve()).toBe('/admin/templates');
  });

  it('sends a caller without templates to the first section they DO hold', () => {
    // The original bug, stated as a test: this caller has no templates permission and must
    // still land somewhere real rather than on the access-denied page.
    granted = new Set([ROLES, AUDIT]);

    expect(resolve()).toBe('/admin/roles');
  });

  it('respects rail order rather than the order permissions were granted', () => {
    // Audit comes after Users & Roles in the rail, so holding both lands on roles.
    granted = new Set([AUDIT, ROLES]);

    expect(resolve()).toBe('/admin/roles');
  });

  it('SKIPS tenant-scoped sections at host scope, where they would 403', () => {
    /**
     * This is the assertion that proves the route passes the REAL host flag rather than a
     * constant. An IT Admin at host scope holds the templates policy, but Notification
     * Templates is tenant-scoped and 403s with no tenant, so the redirect must step over
     * it to Users & Roles. Hand `false` in place of isHostScope(config) and this test --
     * and only this test -- fails.
     */
    hostScope = true;
    granted = new Set([TEMPLATES, PARAMETERS, ROLES, AUDIT]);

    expect(resolve()).toBe('/admin/roles');
  });

  it('sends the SAME caller to templates once they switch into a clinic', () => {
    // The positive control for the test above: identical permissions, tenant scope, and now
    // the tenant-scoped section is the right answer. Without this pair, the skip above could
    // be read as "roles always wins".
    hostScope = false;
    granted = new Set([TEMPLATES, PARAMETERS, ROLES, AUDIT]);

    expect(resolve()).toBe('/admin/templates');
  });

  it('falls back to the dashboard when no admin section is visible at all', () => {
    /**
     * The other half of the same bug. Somebody who reaches /admin by URL with no admin
     * permission must be sent somewhere usable, not to an error page with no way back.
     */
    granted = new Set<string>();

    expect(resolve()).toBe('/dashboard');
  });

  it('never resolves to an empty destination', () => {
    // An empty target redirects to the same URL, which is an infinite loop rather than an
    // error anybody can diagnose.
    granted = new Set<string>();
    expect(resolve().length).toBeGreaterThan(0);

    granted = new Set([TEMPLATES]);
    expect(resolve().length).toBeGreaterThan(0);
  });

  it('asks the permission service instead of deciding for itself', () => {
    // Keeps the redirect tied to the rail's rule. A redirect that stopped consulting
    // permissions would drift from the rail the moment either side changed, and the drift
    // would show up as a 403 for one role only.
    const asked: string[] = [];
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: PermissionService,
          useValue: {
            getGrantedPolicy: (policy: string) => {
              asked.push(policy);
              return false;
            },
          },
        },
        { provide: ConfigStateService, useValue: { getOne: () => ({ id: 'tenant-1' }) } },
      ],
    });

    resolve();

    expect(asked)
      .withContext('every rail policy is consulted before giving up')
      .toContain(TEMPLATES);
    expect(asked).toContain(ROLES);
  });
});
