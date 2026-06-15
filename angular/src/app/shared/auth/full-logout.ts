import { AuthService } from '@abp/ng.core';
import { clearOAuthStorage } from '@abp/ng.oauth';
import { Injector } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
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
    // Kill cookies the AuthServer mirror can't reach (different host:port from
    // the SPA). Done first because they don't affect the token revocation below.
    expireCookie('__tenant');
    expireCookie('XSRF-TOKEN');
  }

  // Step 1: revoke the tokens server-side + clear ABP's OAuth state, WITHOUT
  // ABP's own redirect (strategy-dependent: one strategy redirects, the other
  // is revoke-only). We drive the end-session ourselves below so the flow is
  // deterministic. The access/refresh tokens are still present here, so the
  // revoke actually hits the AuthServer. (The earlier version cleared the
  // tokens BEFORE this call, leaving nothing to revoke + no id_token for the
  // redirect -- so Sign out appeared to do nothing.)
  const authService = injector.get(AuthService);
  try {
    await lastValueFrom(authService.logout({ noRedirectToLogoutUrl: true }));
  } catch {
    // Session already gone server-side; client cleanup below still runs.
  }

  let issuer = '';
  if (typeof window !== 'undefined') {
    // Step 2: defensive localStorage cleanup for any OAuth scraps a library
    // version might leave behind (revokeTokenAndLogout clears the canonical keys).
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
    try {
      clearOAuthStorage(injector);
    } catch {
      // No active OAuthService (e.g. SSR); cookie + localStorage cleanup suffices.
    }
    try {
      issuer = injector.get(OAuthService).issuer ?? '';
    } catch {
      issuer = '';
    }
  }

  // Step 3: drive the AuthServer end-session so the OpenIddict SSO cookie is
  // cleared and the user lands back on the login screen. The runtime issuer is
  // authoritative for the AuthServer host:port (correct even on shifted worktree
  // ports, where the build-time environment is overridden). /Account/Logout
  // clears the cookie and bounces to the post-logout redirect.
  if (typeof window !== 'undefined') {
    const base = issuer.replace(/\/+$/, '');
    window.location.href = base ? `${base}/Account/Logout` : '/';
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
