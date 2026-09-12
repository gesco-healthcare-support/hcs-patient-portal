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
