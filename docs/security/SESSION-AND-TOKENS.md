# Session and Tokens

> Purpose: Inventory of every auth artifact stored client-side, its lifetime, and its invalidation path. Audience: backend and frontend engineers.

Inventory of every authentication-related artifact this app stores
client-side, where it lives, what writes it, what reads it, and how it
is invalidated. Written 2026-05-13 as part of the Issue #106 auth /
session polish pass.

## Identity boundary

The app is two cooperating ASP.NET Core processes plus an Angular SPA:

| Surface | Process | Default host |
| --- | --- | --- |
| AuthServer (OpenIddict) | `HealthcareSupport.CaseEvaluation.AuthServer` | `office-a.localhost:44368` |
| API | `HealthcareSupport.CaseEvaluation.HttpApi.Host` | `localhost:44327` |
| SPA | static Angular bundle | `office-a.localhost:4200` |

The SPA holds Bearer tokens issued by the AuthServer and presents them
to the API. The AuthServer also keeps a cookie session at its own
host so the OAuth `/connect/authorize?prompt=none` and Razor-page
flows can identify the user without re-prompting.

## What we store

### Cookies (AuthServer host)

| Name | Type | Write site | Read site | Lifetime | Cleared on logout? |
| --- | --- | --- | --- | --- | --- |
| `.AspNetCore.Identity.Application` | HttpOnly, Secure (prod), SameSite=Lax | `SignInManager.PasswordSignInAsync` on AuthServer | AuthServer Identity middleware on every request | 14 days sliding | YES -- `LogoutModel.OnGetAsync` calls `SignOutAsync(IdentityConstants.ApplicationScheme)` |
| `.AspNetCore.Identity.External` | HttpOnly | external login providers (none configured since 2026-05-19, so not written in practice) | external sign-in handler | session | YES (signed out in same Logout) |
| `.AspNetCore.Identity.TwoFactorUserId` | HttpOnly | 2FA challenge handler | 2FA verify handler | 5 minutes | YES (signed out in same Logout) |
| `.AspNetCore.Identity.TwoFactorRememberMe` | HttpOnly | 2FA verify when "remember me" checked | 2FA bypass logic | 30 days | YES (signed out in same Logout) |
| `__tenant` | NOT HttpOnly | ABP `DomainTenantResolveContributor` on tenant-subdomain hit | ABP tenant pipeline on every request | 14 days | YES -- `Response.Cookies.Delete("__tenant")` in `LogoutModel.OnGetAsync` |
| `XSRF-TOKEN` | NOT HttpOnly | ASP.NET Core antiforgery middleware | razor `@Antiforgery.GetAndStoreTokens` plus client-side fetch | Per-form | YES -- expired in `LogoutModel.OnGetAsync` |
| `abp_user_culture` | NOT HttpOnly | culture-switcher Razor handler | ABP localization middleware | 1 year | NO (UI preference, persisted across users) |

### localStorage (SPA host)

| Key | Write site | Read site | Lifetime | Cleared on logout? |
| --- | --- | --- | --- | --- |
| `access_token` | `angular-oauth2-oidc.OAuthService.tryLogin` | every `HttpClient` request via the Bearer interceptor | Until refresh-token rotation (15-minute lifetime, set by `SetAccessTokenLifetime` in `CaseEvaluationAuthServerModule`) | YES -- `angular-oauth2-oidc` clears its storage when `performFullLogout` signs out (see below) |
| `refresh_token` | same | `OAuthService.refreshToken` (silent renewal) | 14 days, rotated on each refresh | YES (same) |
| `id_token` | same | `OAuthService.getIdentityClaims` -> `currentUser.id`, profile menu | matches access_token | YES (same) |
| `id_token_claims_obj` | same | profile rendering helpers | matches | YES (same) |
| `id_token_expires_at`, `expires_at` | same | renewal scheduler | matches | YES (same) |
| `nonce`, `PKCE_verifier`, `session_state` | same (during sign-in dance) | OAuth state-validation | per-flow (cleared once the flow completes) | YES |
| `granted_scopes` | same | scope-aware UI gating (currently unused) | matches access_token | YES |
| `LPX_THEME` | The `Issue 1.5` provider initializer in `app.config.ts`, which backfills `'light'` for a missing or stale `'system'` value (the SPA renders no theme picker) | LeptonX theme bootstrap | manual | NO (UI preference) |

### sessionStorage (SPA host)

None today. Considered as a future hardening if we move away from
`localStorage` for tokens.

## Renewal mechanism

`angular-oauth2-oidc` is configured for **OAuth code flow + PKCE +
refresh-token rotation**:

1. SPA bootstrap: `tryLogin` exchanges the auth code returned by the
   AuthServer redirect for an `access_token` + `refresh_token`. Both
   land in localStorage.
2. Background: a scheduled timer wakes up at ~75% of the access_token
   TTL and POSTs `/connect/token` with `grant_type=refresh_token`. The
   AuthServer rotates the refresh_token (RFC 6749 sec 6) and returns a
   fresh access_token / refresh_token pair. Old refresh_token is
   server-side invalidated.
3. If refresh fails (refresh_token expired, revoked, replay rotation
   broken), the next API call returns 401 and the SPA shows its
   session-timeout screen. Its **Sign in again** button runs the same
   sign-out as the Sign out action (below), which ends on the AuthServer
   sign-in page and starts a new code flow.

Silent-refresh via hidden iframe at `/connect/authorize?prompt=none`
was tried (Bug D, May 2026) and ripped in Issue #107 -- the
@abp/ng.oauth interceptor threw on the token exchange, and the iframe
path's cross-origin / CSP overhead wasn't justified given refresh-token
rotation does the same work via a clean POST.

## Threat model

| Threat | Mitigation today | Residual risk |
| --- | --- | --- |
| XSS on the SPA reads `access_token` from `localStorage` and exfiltrates | Standard Angular template escaping; ABP-supplied DomSanitizer; no `dangerouslySetInnerHTML` patterns. CSP is liberal in dev; tighten before hosting. | If an attacker lands a script execution on the SPA, they own the session for the rest of the access_token lifetime. Acceptable for internal app today; revisit at production launch (consider `sessionStorage` or in-memory token + refresh-on-tab-focus). |
| Cookie theft via network sniffing | `Secure` flag on cookies in prod (TLS termination at the load balancer). Dev uses HTTP. | Dev-only concern. |
| CSRF on AuthServer Razor forms | ASP.NET Core antiforgery middleware: `XSRF-TOKEN` cookie + hidden form field validated on POST. | None for the live Razor surface. |
| CSRF on API endpoints | Bearer-token-based; no cookie auth on `/api/*` so CSRF is not applicable. | None. |
| Session replay after logout | `LogoutModel` expires all 4 Identity schemes plus `__tenant` plus `XSRF-TOKEN`; SPA `performFullLogout` expires `__tenant` and `XSRF-TOKEN`, revokes the tokens and ends the AuthServer session; `angular-oauth2-oidc` clears its token storage. | If a user does not click logout (just closes the tab), tokens stay until expiry. Accepted; matches industry norm. |
| Prior-user residue leaking into a new session | Logout clears every cookie and localStorage key listed above. Register flow fires a fire-and-forget GET to `/Account/Logout` so a brand-new registration cannot be silently auto-signed-in as the prior user when they click "Sign In". | Defense-in-depth, not a known live attack. |
| Multi-tab session-account swap | `SessionIdentityWatcherService` listens to `OAuthService.events` and forces `location.reload` when the `sub` claim changes on token rotation. | Detection latency is "next token rotation" -- worst case ~1 hour. Accepted (was iframe-driven in May, now passive after Issue #107). |
| Tenant-cookie leak across registrations | `__tenant` cookie is expired on logout AND fire-and-forget `/Account/Logout` runs after a successful register, so the prior user's `__tenant` cookie is gone before the new user does anything tenant-scoped. | None. |

## Logout invalidation flow

Every sign-out entry point in the SPA calls one helper, `performFullLogout`
(`angular/src/app/shared/auth/full-logout.ts`): the Sign out action in the staff shell and in the
external pages' navbar, and the **Sign in again** button on the session-timeout screen. It is a
standard OIDC RP-initiated logout:

1. `performFullLogout` expires the `__tenant` and `XSRF-TOKEN` cookies on the SPA host. They are not
   OAuth tokens, so the end-session flow does not clear them.
2. If an access token is held, it calls `OAuthService.revokeTokenAndLogout()`, which revokes the
   access and refresh tokens (RFC 7009) and then redirects; otherwise it calls `OAuthService.logOut()`.
   Either way `angular-oauth2-oidc` clears its own token storage and sends the browser to the
   discovered `end_session_endpoint` (`/connect/endsession`). If revocation fails, it falls back to
   `logOut()`, so the redirect always happens.
3. OpenIddict ends the AuthServer SSO session. No `post_logout_redirect_uri` is sent, so it lands on
   the AuthServer's post-logout page (`Pages/Account/LoggedOut.cshtml.cs`), which redirects to
   `/Account/Login`. The next sign-in starts a new code flow, and the next office visit resolves the
   tenant from the URL rather than a stale cookie.

`LPX_THEME` and `abp_user_culture` are kept on purpose: they are UI preferences, not session state.
The AuthServer's own `/Account/Logout` page (`LogoutModel`) signs out all four Identity schemes,
deletes `__tenant` and `XSRF-TOKEN`, and redirects to `/Account/Login`; the sign-up page calls it
(below).

## Register-success isolation

After a successful register POST (`global-scripts.js -> showSignupSuccess`)
the AuthServer's sign-up page script fires:

```js
fetch('/Account/Logout', { method: 'GET', credentials: 'same-origin', redirect: 'manual' })
```

Side effect: even if the user was previously signed in (e.g., shared
browser scenario), the prior `.AspNetCore.Identity.Application` cookie
plus `__tenant` plus `XSRF-TOKEN` are expired before they click
"Verify Email" or "Sign In". The next `/connect/authorize` therefore
prompts for credentials instead of silently issuing tokens for the
prior user.

## Open follow-ups (out of scope for #106)

1. **Token storage location.** localStorage is XSS-readable. A move to
   in-memory access_token + sessionStorage refresh_token + cookie-based
   refresh would shrink the XSS blast radius. Substantial Angular
   refactor; defer until a production-hardening pass.
2. **CSP tightening.** Dev CSP is permissive (`unsafe-inline` for
   styles, scripts inlined). Production needs strict CSP with hashes
   or nonces for every inline.
3. **Cookie attributes in prod.** Set `Secure`, `SameSite=Strict`
   where appropriate, and verify `Domain` semantics across the
   tenant-subdomain layout.
4. **Audit log on logout / register.** Today we log to ILogger; long
   term these should also write to ABP's AuditLog table so
   compliance can trace identity transitions.

## Related

- `Logout.cshtml.cs` -- server-side cookie cleanup
- `angular/src/app/shared/auth/full-logout.ts` -- SPA-side cookie + localStorage cleanup helper
- `angular/src/app/shared/auth/session-identity-watcher.service.ts` -- passive sub-change detector
- `src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js` -- register success fires `/Account/Logout`
- `docs/security/THREAT-MODEL.md` -- broader threat model
- `docs/security/AUTHORIZATION.md` -- per-feature authorization
