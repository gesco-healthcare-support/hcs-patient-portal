import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';

import { postLoginRedirectGuard } from './post-login-redirect.guard';

/**
 * Sweep #640 rewrote the anonymous check as an optional chain (S6582). That rewrite is only
 * safe because `currentUser` being absent and `isAuthenticated` being false must behave
 * identically -- both mean "not logged in" -- and this guard is what stands between an
 * anonymous visitor and the internal shell.
 *
 * <p>It had no spec, so the equivalence had nothing holding it.</p>
 */
describe('postLoginRedirectGuard anonymous handling (sweep #640)', () => {
  let navigateToLogin: jasmine.Spy;

  function run(currentUser: unknown) {
    navigateToLogin = jasmine.createSpy('navigateToLogin');
    TestBed.configureTestingModule({
      providers: [
        { provide: ConfigStateService, useValue: { getOne: () => currentUser } },
        { provide: AuthService, useValue: { navigateToLogin } },
        { provide: Router, useValue: { parseUrl: (u: string) => u, createUrlTree: () => ({}) } },
      ],
    });
    return TestBed.runInInjectionContext(() => postLoginRedirectGuard({} as never, [] as never));
  }

  afterEach(() => TestBed.resetTestingModule());

  it('challenges when there is no current user at all', () => {
    expect(run(null)).toBeFalse();
    expect(navigateToLogin).toHaveBeenCalled();
  });

  it('challenges when the user object exists but is not authenticated', () => {
    // The half the optional chain must keep: `{}` is not null, so `!currentUser` alone
    // would have let it through.
    expect(run({ isAuthenticated: false })).toBeFalse();
    expect(navigateToLogin).toHaveBeenCalled();
  });

  it('challenges when isAuthenticated is simply absent', () => {
    expect(run({})).toBeFalse();
    expect(navigateToLogin).toHaveBeenCalled();
  });

  it('does not challenge an authenticated user', () => {
    run({ isAuthenticated: true, roles: ['Patient'] });
    expect(navigateToLogin).not.toHaveBeenCalled();
  });
});

/**
 * Where an AUTHENTICATED caller is sent. The block above owns the anonymous half; this one owns
 * the three destinations, which had nothing holding them.
 *
 * <p>WHY A SEPARATE HARNESS. The stub above answers `getOne` for any key, which is fine for a
 * question that only reads `currentUser`. These specs also need `currentTenant`, because the
 * Intake Staff redirect is scope-gated -- so the stub below keys on its argument, and a guard
 * reading the wrong key fails instead of silently receiving the user object.</p>
 *
 * <p>THE SCOPE GATE IS THE LOAD-BEARING PART, and it is why two nearly identical Intake Staff
 * specs exist rather than one. At host scope an Intake Staff user has no Dashboard.Host grant, so
 * `/dashboard` renders a nav-less "you don't have access" page and they must land on the office
 * switcher. INSIDE an office a current tenant is set, the office dashboard IS reachable through
 * Dashboard.Tenant, and `/host/my-offices` would 403 because the office shadow user lacks
 * IntakeImpersonation. Same role, two destinations: a spec covering only the host case would stay
 * green with the gate deleted.</p>
 *
 * <p>Role names are the real seeded ABP names; the tenant id is a synthetic placeholder whose
 * value never matters, only its presence. No patient data is involved.</p>
 */
describe('postLoginRedirectGuard internal destinations', () => {
  /** Synthetic. Only its presence is read, via isHostScope. */
  const IN_OFFICE_TENANT = { id: 'a0000000-0000-0000-0000-000000000001' };

  let parsedUrls: string[];
  let navigateToLogin: jasmine.Spy;

  beforeEach(() => {
    parsedUrls = [];
    navigateToLogin = jasmine.createSpy('navigateToLogin');
  });

  afterEach(() => TestBed.resetTestingModule());

  /**
   * The declared return type narrows the guard's broad `GuardResult` to the two shapes this
   * guard can actually produce, so a destination can be asserted directly. The Router stub
   * returns `{ url }` in place of a real UrlTree; only the parsed path is ever read.
   */
  function run(roles: string[], currentTenant: unknown): boolean | { url: string } {
    // Reset first: the "other internal staff" spec runs this twice, and TestBed refuses to be
    // reconfigured once instantiated.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (key: string) => {
              if (key === 'currentUser') {
                return { isAuthenticated: true, roles };
              }
              return key === 'currentTenant' ? currentTenant : null;
            },
          },
        },
        { provide: AuthService, useValue: { navigateToLogin } },
        {
          provide: Router,
          useValue: {
            parseUrl: (url: string) => {
              parsedUrls.push(url);
              return { url };
            },
          },
        },
      ],
    });
    return TestBed.runInInjectionContext(() =>
      postLoginRedirectGuard({} as never, [] as never),
    ) as unknown as boolean | { url: string };
  }

  it('keeps a pure-external user at the external home', () => {
    // Positive control for every redirect below: this input must reach no parseUrl at all, so
    // the assertions on parsedUrls are not satisfiable by any authenticated caller.
    expect(run(['Patient'], null)).toBeTrue();
    expect(parsedUrls).toEqual([]);
    expect(navigateToLogin).not.toHaveBeenCalled();
  });

  it('sends Intake Staff at HOST scope to the office switcher', () => {
    expect(run(['Intake Staff'], null)).toEqual({ url: '/host/my-offices' });
    expect(parsedUrls).toEqual(['/host/my-offices']);
  });

  it('sends Intake Staff INSIDE an office to the dashboard instead', () => {
    expect(run(['Intake Staff'], IN_OFFICE_TENANT)).toEqual({ url: '/dashboard' });
    expect(parsedUrls).toEqual(['/dashboard']);
  });

  it('sends other internal staff to the dashboard', () => {
    expect(run(['IT Admin'], null)).toEqual({ url: '/dashboard' });
    expect(run(['Staff Supervisor'], null)).toEqual({ url: '/dashboard' });
  });

  it('sends the host superuser to the dashboard even alongside an intake role', () => {
    // resolveInternalRoleKey resolves 'admin' ahead of every other role, so a stray Intake Staff
    // grant must not divert the superuser to the office switcher.
    expect(run(['admin', 'Intake Staff'], null)).toEqual({ url: '/dashboard' });
  });
});
