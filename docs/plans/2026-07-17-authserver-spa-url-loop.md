---
feature: authserver-standard-logout
date: 2026-07-17
status: draft
base-branch: development
related-issues: []
---

## Goal

Replace the hand-rolled logout + root-redirect (which loop with `ERR_TOO_MANY_REDIRECTS` on the production subdomain layout) with the standard OpenID Connect end-session logout, landing the user on `/Account/Login` per the app's deliberate "AuthServer owns the auth UI" design -- eliminating every place that guesses the SPA URL from the AuthServer's own request host.

## Context

- Live symptom (both `admin` host and `falkinstein` tenant, reproduced from Adrian's HARs): first login works; **logout -> re-login loops** (`GET / -> 302 https://{office}.auth.<base>/` to itself). Server-side 302s only.
- Root cause: two AuthServer Razor pages build the SPA URL from `Request.Host.Host` (the AuthServer host) + a port swap -- `Index.cshtml.cs ResolveAngularUrl()` and `Logout.cshtml.cs BuildSpaLogoutUrl()`. That assumption (SPA and AuthServer share a host, differ by port) is true in dev but false on the server (subdomain-per-service), so both resolve to the AuthServer host itself -> self-loop, and the SPA's `?logout=true` token-cleanup handshake never runs.
- The current logout is fully custom and hand-rolled: SPA `full-logout.ts` calls `authService.logout({ noRedirectToLogoutUrl: true })` (revoke + clear, NO redirect) then manually navigates to `{issuer}/Account/Logout`; the custom `LogoutModel` signs out and redirects to the hand-built SPA URL `?logout=true`; `app.component.ts` catches `?logout=true` and re-runs cleanup, then navigates to `/Account/Login`. This bypasses the standard OIDC end-session entirely.
- Chesterton's Fence (confirmed intentional, must respect): `LoggedOut.cshtml.cs` documents a deliberate decision -- the AuthServer owns the auth UI end-to-end and the user "should always land on `/Account/Login` after sign-out for OLD parity" (memory `project_authserver-ui-not-spa`). So the fix must still land on `/Account/Login`, NOT bounce back into the SPA via `post_logout_redirect_uri`.
- Standard endpoints are available and verified live: discovery advertises `end_session_endpoint` (`/connect/endsession`) and `revocation_endpoint`; `GET /connect/logout` -> 302. `OpenIddictDataSeedContributor` registers the SPA client with `postLogoutRedirectUris` and `EnableWildcardDomainSupport = true` covers tenant subdomains.
- Shared correct rule already exists + is unit-tested: `Application/Notifications/TenantUrlComposer.ComposeForTenant(baseUrl, slug)` (matches `angular/src/tenant-bootstrap.ts`). AuthServer references the Application project.
- Constraints: deploy `development` only; do NOT touch/deploy `main`; full-stack rebuild + `--force-recreate reverse-proxy` on deploy (stale-upstream-IP lesson, see [[patient-portal-server-rollout]]); CI enforces SonarCloud new_coverage >= 80%; ASCII only; commit `<type>(patient-portal): ...`, no attribution.

## Approach

Standard OIDC RP-initiated logout via the end-session endpoint, landing on `/Account/Login`. Concretely:

1. SPA logout uses the library's standard end-session redirect (`OAuthService.revokeTokenAndLogout()` / `logOut()`), which clears the SPA token storage and redirects the browser to `end_session_endpoint` with `id_token_hint`. No `post_logout_redirect_uri` is passed (so we land on the AuthServer's own post-logout page, honoring "AuthServer owns the UI"). Keep the pre-redirect client-side cleanup of the non-OAuth cookies (`__tenant`, `XSRF-TOKEN`) and localStorage stragglers.
2. OpenIddict clears the SSO auth cookie and returns to the AuthServer post-logout page. `LoggedOutModel.OnGet()` changes from `Page()` to `RedirectToPage("./Login")` so the user lands on `/Account/Login` (relative redirect on the auth host -- no host guessing, tenant-safe).
3. Remove the hand-rolled machinery that guesses the SPA URL: the manual `/Account/Logout` navigation + `?logout=true` handshake (`app.component.ts handleAuthServerLogoutHandshake`) and `LogoutModel.BuildSpaLogoutUrl()`. For robustness on a direct `GET /Account/Logout`, keep the sign-out but end with a relative `RedirectToPage("./Login")` (still expiring `__tenant`/`XSRF`), never a hand-built SPA URL.
4. Fix the residual `IndexModel` self-loop for the "authenticated user hits the auth root directly" case: `ResolveAngularUrl()` uses the office slug (leftmost label of `Request.Host`) + `App:AngularUrl` via the shared `TenantUrlComposer` rule, not the request host. (Anonymous `/` -> `/Account/Login` unchanged.)
5. Make `TenantUrlComposer` public (method already public) so the AuthServer reuses the one tested rule; add tests for the new usage (coverage gate).

Result: anonymous -> `/Account/Login`; login -> role-based SPA landing (unchanged); logout -> standard end-session clears cookie + tokens -> `/Account/Login`. No request-host SPA-URL guessing anywhere.

Alternatives rejected:
- (a) Surgical URL-rule fix only (keep the custom logout hand-rolling): lowest risk and fixes the loop, but leaves the non-standard hand-rolled logout Adrian wants gone. Kept as the fallback if we need to de-risk before go-live.
- Bounce back to the SPA via `post_logout_redirect_uri`: contradicts the deliberate "land on `/Account/Login`" design; rejected.

## Tasks

- T1: Make `TenantUrlComposer` public; keep behavior; existing `TenantUrlComposerUnitTests` green.
  - approach: code
  - files-touched: [Application/Notifications/TenantUrlComposer.cs]
- T2: Fix `IndexModel.ResolveAngularUrl()` to use office-slug(leftmost label of request host) + `App:AngularUrl` via `ComposeForTenant`; add a tested pure helper for the composition.
  - approach: tdd
  - files-touched: [AuthServer/Pages/Index.cshtml.cs, AuthServer/**, test/**]
  - acceptance: unit tests cover prod tenant, prod host (`admin`), dev host:port, blank-config fallback.
- T3: `LoggedOutModel.OnGet()` -> `RedirectToPage("./Login")`.
  - approach: code
  - files-touched: [AuthServer/Pages/Account/LoggedOut.cshtml.cs]
- T4: Simplify `LogoutModel` -- keep sign-out + `__tenant`/`XSRF` expiry, replace `BuildSpaLogoutUrl()` redirect with `RedirectToPage("./Login")`; delete the dead `BuildSpaLogoutUrl` + `?logout=true`.
  - approach: code
  - files-touched: [AuthServer/Pages/Account/Logout.cshtml.cs]
- T5: SPA `full-logout.ts` -> standard end-session: `revokeTokenAndLogout()`/`logOut()` (redirects to `/connect/endsession`); keep pre-redirect `__tenant`/`XSRF`/localStorage cleanup; drop the manual `/Account/Logout` navigation.
  - approach: test-after
  - files-touched: [angular/src/app/shared/auth/full-logout.ts, .spec.ts]
- T6: Remove the now-dead `?logout=true` handshake in `app.component.ts`.
  - approach: test-after
  - files-touched: [angular/src/app/app.component.ts, .spec.ts]
- T7: Commit, push, PR into `development`.
  - approach: code
  - STOP: confirm before merge + live rebuild.
- T8: Merge (admin) + FULL docker rebuild + `--force-recreate reverse-proxy` + verify.
  - approach: code

## Risk / Rollback

- Blast radius: the logout flow (SPA + AuthServer) + the AuthServer root redirect. This is auth-path code -- higher risk than the cache/remoteEnv fixes. Login success path and token issuance are untouched. Bigger than option (a): ~6 files across SPA + AuthServer.
- Mitigations: keep the change behavior-preserving where possible (still land on `/Account/Login`); add unit tests for the URL rule; verify the full logout->re-login cycle on BOTH host and tenant before declaring done; keep the previous images for instant rollback (do NOT prune); never `docker compose down -v`.
- Verify-critical unknowns to check during build (flag if they misbehave): (1) `/connect/endsession` with `id_token_hint` and no `post_logout_redirect_uri` lands on our LoggedOut page without showing a logout-confirmation prompt; (2) `__tenant`/`XSRF` are actually gone after the standard logout (client expiry runs before the end-session redirect); (3) the one-off `POST /Account/Login -> /Error?400` seen on a first attempt in the tenant HAR -- recheck, file separately if reproducible.
- Fallback: if (b) proves too risky mid-implementation, ship option (a) (URL-rule fix in the two methods only) to unblock, and revisit (b).

## Verification

On the box after deploy (full rebuild + reverse-proxy force-recreate); Playwright for the flow, Adrian's own normal Chrome profile as the decisive check:
1. TENANT `falkinstein`: log in as `appatty1@gesco.com` -> office landing. **Logout -> log in again** -> lands on `/Account/Login` then the app; NO `ERR_TOO_MANY_REDIRECTS`. Repeat 3x.
2. HOST `admin`: same logout->re-login cycle as `adriang@gesco.com` -> dashboard; no loop.
3. Logout uses the standard endpoint: network shows a redirect to `/connect/endsession` (not the old `/Account/Logout` hand-off), then `/Account/Login`.
4. Tokens cleared: after logout, SPA localStorage has no `access_token`/`refresh_token`/`id_token`; `__tenant`/`XSRF` gone.
5. Direct visits: `GET /` (authenticated) and `GET /Account/Logout` server-side redirect chains point at `/Account/Login` or the SPA host (`{office}.appointment-portal...`), never the auth host.
6. Adrian confirms the full logout->re-login cycle in his own normal Chrome on both portals.
