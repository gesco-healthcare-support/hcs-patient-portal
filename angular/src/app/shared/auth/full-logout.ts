import { AuthService } from '@abp/ng.core';
import { clearOAuthStorage } from '@abp/ng.oauth';
import { Injector } from '@angular/core';
import { lastValueFrom } from 'rxjs';

/**
 * Issue #106a (2026-05-13) -- centralised "drop everything the prior
 * user left in this browser" helper.
 *
 * The library-side {@link AuthService.logout} clears the OAuth keys
 * that angular-oauth2-oidc owns (access_token, refresh_token, id_token,
 * nonce, expires_at, session_state, granted_scopes, id_token_claims_obj)
 * but leaves the rest of the per-session detritus in place:
 *
 *   * `__tenant` cookie (ABP tenant resolver) -- leaks the prior user's
 *     tenant into a fresh registration on the same browser.
 *   * `XSRF-TOKEN` cookie -- a scraped value could be replayed against
 *     the next session if not rotated.
 *   * Any `oauth_*` / `id_token_*` localStorage scraps that pre-date
 *     the current angular-oauth2-oidc release; harmless but pollutes
 *     debugging.
 *
 * `LPX_THEME` and `abp_user_culture` are deliberately preserved -- they
 * are UI preferences, not session tokens; surviving across sign-outs
 * matches user expectation. Any future "totally fresh state" reset
 * should be a separate, explicit user action.
 *
 * Server-side mirror of this cleanup lives in
 * `AuthServer/Pages/Account/Logout.cshtml.cs` (also Issue #106a) -- the
 * AuthServer expires `__tenant` + `XSRF-TOKEN` cookies via
 * Response.Cookies.Delete during sign-out. Both sides clear the same
 * cookies defensively; either alone would not be sufficient because the
 * cookies can be re-set by intermediate requests.
 *
 * @returns a Promise that resolves once {@link AuthService.logout} emits
 * either complete or error. The promise never rejects -- if the library
 * call fails (e.g. the session was already torn down server-side), the
 * cookie + storage cleanup has still run.
 */
export async function performFullLogout(injector: Injector): Promise<void> {
  if (typeof window !== 'undefined') {
    // Step 1: kill cookies the AuthServer mirror can't reach (different
    // host:port combo from the SPA, so AuthServer-side Set-Cookie
    // doesn't always overwrite the SPA-side store, e.g. when a user
    // never made it back to the AuthServer between sign-in and logout).
    // Set-Cookie with past Expires is the RFC 6265 incantation;
    // Path="/" matches what ABP uses when setting these.
    expireCookie('__tenant');
    expireCookie('XSRF-TOKEN');

    // Step 2: localStorage scraps angular-oauth2-oidc may not own. The
    // library is responsible for the OAuth keys, but defensive removal
    // here closes any future drift between library versions.
    const stragglerKeys = [
      'access_token',
      'access_token_stored_at',
      'expires_at',
      'granted_scopes',
      'id_token',
      'id_token_claims_obj',
      'id_token_expires_at',
      'id_token_stored_at',
      'nonce',
      'PKCE_verifier',
      'refresh_token',
      'session_state',
    ];
    for (const key of stragglerKeys) {
      window.localStorage.removeItem(key);
    }

    // Step 3: belt-and-suspenders -- ABP's own OAuth-storage helper
    // walks the OAuthService.tokenStorage and clears. Safe to call even
    // if AuthService.logout below will repeat the same work.
    try {
      clearOAuthStorage(injector);
    } catch {
      // No-op: clearOAuthStorage throws when there is no active
      // OAuthService instance (e.g. server-side render). Cleanup of
      // cookies + localStorage above is sufficient.
    }
  }

  // Step 4: library-side logout. Drives angular-oauth2-oidc to clear
  // its remaining state + (depending on ConfigFlags) bounce to the
  // AuthServer's /Account/Logout endpoint. The caller of
  // performFullLogout decides what to do with the resulting completion;
  // we just await the observable here.
  const authService = injector.get(AuthService);
  try {
    await lastValueFrom(authService.logout());
  } catch {
    // Swallow: AuthService.logout can fail when the session is already
    // gone server-side. We have already cleared client state.
  }
}

/**
 * Set a cookie to expired. Matches the ABP default of Path="/" without
 * an explicit Domain, which deletes the cookie on the current host
 * (cross-subdomain cookies would need Domain= matching the original
 * Set-Cookie -- ABP does not set those today).
 */
function expireCookie(name: string): void {
  document.cookie = `${name}=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
}
