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

  it('revokes tokens and drives the standard end-session logout', async () => {
    const oauth = {
      revokeTokenAndLogout: jasmine.createSpy('revokeTokenAndLogout').and.resolveTo(true),
      logOut: jasmine.createSpy('logOut'),
    } as unknown as OAuthService;

    await performFullLogout(injectorFor(oauth));

    expect(oauth.revokeTokenAndLogout).toHaveBeenCalledTimes(1);
    expect(oauth.logOut).not.toHaveBeenCalled();
  });

  it('falls back to logOut() when revocation fails so the user still reaches login', async () => {
    const oauth = {
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
      revokeTokenAndLogout: jasmine
        .createSpy('revokeTokenAndLogout')
        .and.rejectWith(new Error('boom')),
      logOut: jasmine.createSpy('logOut').and.throwError('gone'),
    } as unknown as OAuthService;

    await expectAsync(performFullLogout(injectorFor(oauth))).toBeResolved();
  });
});
