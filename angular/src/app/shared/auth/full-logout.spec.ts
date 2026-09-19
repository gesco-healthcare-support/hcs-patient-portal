import { Injector } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { performFullLogout } from './full-logout';

/**
 * 2026-07-17 -- performFullLogout now drives the standard OIDC end-session logout
 * (revoke + redirect to /connect/endsession), replacing the old hand-rolled
 * /Account/Logout + ?logout=true handshake that looped on the prod subdomain layout.
 */
describe('performFullLogout', () => {
  function injectorFor(oauth: Partial<OAuthService>): Injector {
    return {
      get: (token: unknown) => {
        if (token === OAuthService) {
          return oauth;
        }
        throw new Error('unexpected injection token');
      },
    } as unknown as Injector;
  }

  /** Synthetic; only its truthiness matters to the code under test. */
  const STORED_ACCESS_TOKEN = 'synthetic-access-token';

  it('revokes tokens and drives the standard end-session logout', async () => {
    const oauth = {
      getAccessToken: jasmine.createSpy('getAccessToken').and.returnValue(STORED_ACCESS_TOKEN),
      revokeTokenAndLogout: jasmine.createSpy('revokeTokenAndLogout').and.resolveTo(true),
      logOut: jasmine.createSpy('logOut'),
    } as unknown as OAuthService;

    await performFullLogout(injectorFor(oauth));

    expect(oauth.revokeTokenAndLogout).toHaveBeenCalledTimes(1);
    expect(oauth.logOut).not.toHaveBeenCalled();
  });

  it('falls back to logOut() when revocation fails so the user still reaches login', async () => {
    const oauth = {
      getAccessToken: jasmine.createSpy('getAccessToken').and.returnValue(STORED_ACCESS_TOKEN),
      revokeTokenAndLogout: jasmine
        .createSpy('revokeTokenAndLogout')
        .and.rejectWith(new Error('boom')),
      logOut: jasmine.createSpy('logOut'),
    } as unknown as OAuthService;

    await performFullLogout(injectorFor(oauth));

    expect(oauth.logOut).toHaveBeenCalledTimes(1);
  });

  it('never rejects even if both revocation and the fallback throw', async () => {
    const oauth = {
      getAccessToken: jasmine.createSpy('getAccessToken').and.returnValue(STORED_ACCESS_TOKEN),
      revokeTokenAndLogout: jasmine
        .createSpy('revokeTokenAndLogout')
        .and.rejectWith(new Error('boom')),
      logOut: jasmine.createSpy('logOut').and.throwError('gone'),
    } as unknown as OAuthService;

    await expectAsync(performFullLogout(injectorFor(oauth))).toBeResolved();
  });

  /**
   * Production hardening 1.8b (2026-09-01) -- the defect these exist to prevent recurring.
   *
   * `revokeTokenAndLogout()` early-returns a RESOLVED promise when local storage holds no access
   * token (angular-oauth2-oidc@20.0.2 fesm2022 :2958), so the old code's catch never fired, the
   * fallback never ran, no end-session redirect happened, and sign-out reported success while the
   * AuthServer SSO cookie survived -- for 14 days on a sliding window.
   *
   * These are ALSO the drift guard. `performFullLogout` mirrors the library's own `!accessToken`
   * condition, which is a library internal. If a future version changes that condition, these fail
   * rather than the silent sign-out quietly returning.
   */
  describe('when local storage holds no access token', () => {
    function noTokenOAuth(): OAuthService {
      return {
        getAccessToken: jasmine.createSpy('getAccessToken').and.returnValue(null),
        revokeTokenAndLogout: jasmine.createSpy('revokeTokenAndLogout').and.resolveTo(true),
        logOut: jasmine.createSpy('logOut'),
      } as unknown as OAuthService;
    }

    it('still drives the end-session redirect, and does not call revoke with nothing to revoke', async () => {
      const oauth = noTokenOAuth();

      await performFullLogout(injectorFor(oauth));

      expect(oauth.logOut).toHaveBeenCalledTimes(1);
      expect(oauth.revokeTokenAndLogout).not.toHaveBeenCalled();
    });

    it('still resolves', async () => {
      await expectAsync(performFullLogout(injectorFor(noTokenOAuth()))).toBeResolved();
    });

    it('still expires the tenant cookie', async () => {
      document.cookie = '__tenant=44444444-4444-4444-4444-444444444444; Path=/';
      expect(document.cookie).toContain('__tenant=');

      await performFullLogout(injectorFor(noTokenOAuth()));

      expect(document.cookie).not.toContain('__tenant=');
    });
  });

  /**
   * Production hardening (task 1.3, 2026-09-01) -- characterization tests, behaviour PRESERVED.
   *
   * Nothing in the Angular test tree touched either cookie before this block
   * (`grep -rn "__tenant\|XSRF-TOKEN" src --include=*.spec.ts` returned no matches), and
   * performFullLogout is the only place `__tenant` is cleared. That makes this the
   * security-relevant half of sign-out and it was entirely unguarded.
   *
   * Why it matters, from full-logout.ts:21-23: the end-session flow does not clear these two,
   * and "a stale `__tenant` can leak the prior user's tenant into a fresh registration on the
   * same browser". On a shared machine in a medical office that is a patient-data boundary,
   * not a cosmetic detail.
   *
   * These assert against real `document.cookie` rather than a spy, because the point is that
   * the cookie is actually gone, not that a helper was called. All values are synthetic.
   */
  describe('cookie clearing on sign-out', () => {
    /** Cookies these tests create, removed again in afterEach so specs stay independent. */
    const TOUCHED = ['__tenant', 'XSRF-TOKEN', 'LPX_THEME', 'abp_user_culture'];

    function setCookie(name: string, value: string): void {
      document.cookie = `${name}=${value}; Path=/`;
    }

    function resolvingOAuth(): OAuthService {
      return {
        getAccessToken: jasmine.createSpy('getAccessToken').and.returnValue(STORED_ACCESS_TOKEN),
        revokeTokenAndLogout: jasmine.createSpy('revokeTokenAndLogout').and.resolveTo(true),
        logOut: jasmine.createSpy('logOut'),
      } as unknown as OAuthService;
    }

    afterEach(() => {
      for (const name of TOUCHED) {
        document.cookie = `${name}=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
      }
    });

    it('expires the tenant cookie so it cannot leak into the next registration', async () => {
      setCookie('__tenant', '11111111-1111-1111-1111-111111111111');
      expect(document.cookie).toContain('__tenant=');

      await performFullLogout(injectorFor(resolvingOAuth()));

      expect(document.cookie).not.toContain('__tenant=');
    });

    it('expires the CSRF cookie', async () => {
      setCookie('XSRF-TOKEN', 'synthetic-csrf-value');
      expect(document.cookie).toContain('XSRF-TOKEN=');

      await performFullLogout(injectorFor(resolvingOAuth()));

      expect(document.cookie).not.toContain('XSRF-TOKEN=');
    });

    it('expires both cookies in the same sign-out', async () => {
      setCookie('__tenant', '22222222-2222-2222-2222-222222222222');
      setCookie('XSRF-TOKEN', 'another-synthetic-value');

      await performFullLogout(injectorFor(resolvingOAuth()));

      expect(document.cookie).not.toContain('__tenant=');
      expect(document.cookie).not.toContain('XSRF-TOKEN=');
    });

    /**
     * The inverse assertion. full-logout.ts:24-25 keeps these deliberately -- they are UI
     * preferences, not session state -- so a future "clear everything on logout" change should
     * fail here rather than silently resetting every user's theme.
     */
    it('preserves the theme and culture cookies, which are UI preferences not session state', async () => {
      setCookie('LPX_THEME', 'Dim');
      setCookie('abp_user_culture', 'en');

      await performFullLogout(injectorFor(resolvingOAuth()));

      expect(document.cookie).toContain('LPX_THEME=');
      expect(document.cookie).toContain('abp_user_culture=');
    });

    it('still expires the cookies when revocation fails', async () => {
      setCookie('__tenant', '33333333-3333-3333-3333-333333333333');
      const oauth = {
        revokeTokenAndLogout: jasmine
          .createSpy('revokeTokenAndLogout')
          .and.rejectWith(new Error('boom')),
        logOut: jasmine.createSpy('logOut'),
      } as unknown as OAuthService;

      await performFullLogout(injectorFor(oauth));

      expect(document.cookie).not.toContain('__tenant=');
    });
  });
});
