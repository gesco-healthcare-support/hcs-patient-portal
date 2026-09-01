import { Injector } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';

/**
 * Centralised sign-out for every logout entry point in the SPA.
 *
 * Standard OIDC RP-initiated logout: revoke the access/refresh tokens (RFC 7009),
 * let angular-oauth2-oidc clear its own token storage, then redirect the browser to
 * the discovered `end_session_endpoint` (`/connect/endsession`), which clears the
 * AuthServer SSO cookie. No `post_logout_redirect_uri` is sent, so OpenIddict returns
 * to the AuthServer post-logout page, which redirects to `/Account/Login` (the
 * AuthServer owns the auth UI -- see `AuthServer/Pages/Account/LoggedOut.cshtml.cs`).
 *
 * 2026-07-17 -- replaced the previous hand-rolled logout (`AuthService.logout({
 * noRedirectToLogoutUrl: true })` + a manual navigation to `/Account/Logout` +
 * a `?logout=true` handshake). That path built the SPA/AuthServer URLs by reusing the
 * request host + a port swap, which is only valid in dev; on the production
 * subdomain layout ({office}.<base> SPA vs {office}.auth.<base> AuthServer) it
 * resolved to the AuthServer host itself and produced ERR_TOO_MANY_REDIRECTS.
 *
 * `__tenant` + `XSRF-TOKEN` are expired here (before the redirect) because they are
 * not OAuth tokens and the end-session flow does not clear them; a stale `__tenant`
 * can leak the prior user's tenant into a fresh registration on the same browser.
 * `LPX_THEME` / `abp_user_culture` are deliberately preserved (UI preferences, not
 * session state).
 *
 * @returns a Promise that resolves before the end-session navigation; it never
 * rejects -- if revocation fails (e.g. the session is already gone) it falls back to
 * a plain end-session redirect so the user still lands on the login page.
 */
export async function performFullLogout(injector: Injector): Promise<void> {
  if (typeof window !== 'undefined') {
    expireCookie('__tenant');
    expireCookie('XSRF-TOKEN');
  }

  const oAuthService = injector.get(OAuthService);
  try {
    // Revokes the tokens then calls logOut(), which clears local token storage and
    // redirects to the end_session_endpoint (from the discovery document).
    await oAuthService.revokeTokenAndLogout();
  } catch {
    // Revocation endpoint unreachable / session already gone: still drive the
    // end-session redirect so the user lands on the login page. logOut() clears the
    // local tokens, then redirects to the discovered end_session_endpoint.
    try {
      oAuthService.logOut();
    } catch {
      // No active/bootstrapped OAuthService (e.g. SSR) -- the cookie cleanup above suffices.
    }
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
