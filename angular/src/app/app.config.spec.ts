import { Injector } from '@angular/core';
import { CHECK_AUTHENTICATION_STATE_FN_KEY, ConfigStateService } from '@abp/ng.core';
import { OAuthService, OAuthStorage } from 'angular-oauth2-oidc';
import { appConfig } from './app.config';

/**
 * The one piece of the root ApplicationConfig this spec covers: the session-safety function
 * registered under CHECK_AUTHENTICATION_STATE_FN_KEY, which replaces ABP 10.0.2's own (the comment
 * above it in app.config.ts says why).
 *
 * The rule it enforces: a browser holding a valid OAuth token but NO signed-in user is in an
 * inconsistent state, so the stored tokens are cleared. A browser whose token belongs to a signed-in
 * user must keep them -- clearing there would sign every user out on every boot.
 *
 * Storage is observed through the OAuthStorage the fake injector hands out, because that is what
 * ABP's clearOAuthStorage removes the token keys from (@abp/ng.oauth 10.0.2).
 *
 * The rest of appConfig -- the two app initializers and the address-provider factory -- is
 * deliberately not covered: reaching it means booting the root config or reading Angular's private
 * EnvironmentProviders shape (tranche 12 plan, D1, Adrian 2026-09-22).
 */
describe('appConfig session-safety check (CHECK_AUTHENTICATION_STATE_FN_KEY)', () => {
  type CheckFn = (injector: Injector) => void;

  /** The registered function, found by its token rather than by position in the array. */
  function checkFn(): CheckFn {
    const entry = (appConfig.providers as unknown[]).find(
      (p) =>
        !!p &&
        typeof p === 'object' &&
        (p as { provide?: unknown }).provide === CHECK_AUTHENTICATION_STATE_FN_KEY,
    ) as { useValue: CheckFn } | undefined;
    if (!entry) {
      throw new Error('CHECK_AUTHENTICATION_STATE_FN_KEY is no longer registered in appConfig');
    }
    return entry.useValue;
  }

  function fakeInjector(state: { validToken: boolean; userId: string | null }) {
    const storage = { removeItem: jasmine.createSpy('removeItem') };
    const injector = {
      get: (token: unknown) => {
        if (token === ConfigStateService) {
          return {
            getDeep: (path: string) => (path === 'currentUser.id' ? state.userId : undefined),
          };
        }
        if (token === OAuthService) {
          return { hasValidAccessToken: () => state.validToken };
        }
        if (token === OAuthStorage) {
          return storage;
        }
        throw new Error('unexpected injection token');
      },
    } as unknown as Injector;
    return { injector, storage };
  }

  it('clears the stored tokens when a valid token has no signed-in user behind it', () => {
    const { injector, storage } = fakeInjector({ validToken: true, userId: null });

    checkFn()(injector);

    expect(storage.removeItem).toHaveBeenCalledWith('access_token');
    expect(storage.removeItem).toHaveBeenCalledWith('refresh_token');
  });

  it('keeps the tokens of a signed-in user', () => {
    // The negative guarantee, so the fixture carries exactly what must NOT be acted on: a valid
    // token AND a real user id. Without both, "nothing was cleared" would prove nothing.
    const { injector, storage } = fakeInjector({ validToken: true, userId: 'user-1' });

    checkFn()(injector);

    expect(storage.removeItem).not.toHaveBeenCalled();
  });

  it('leaves storage alone when there is no valid token to clear', () => {
    const { injector, storage } = fakeInjector({ validToken: false, userId: null });

    checkFn()(injector);

    expect(storage.removeItem).not.toHaveBeenCalled();
  });
});
