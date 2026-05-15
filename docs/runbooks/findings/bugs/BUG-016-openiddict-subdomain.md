---
id: BUG-016
title: OpenIddict client RedirectUris missing subdomain wildcards
severity: medium
status: open
found: 2026-05-14
flow: oauth-login
component: DbMigrator (OpenIddict client seed)
---

# BUG-016 — OpenIddict missing subdomain RedirectUris

## Severity
medium (blocks SPA login from any tenant subdomain; affects multi-tenant Phase 2)

## Status
**Open** — for fix session.

## Symptom
Navigating to the SPA at `http://falkinstein.localhost:4200/` (any tenant subdomain) redirects to `/connect/authorize`. The OpenIddict authorization endpoint then returns:
```
error:invalid_request
error_description:The specified 'redirect_uri' is not valid for this client application.
error_uri:https://documentation.openiddict.com/errors/ID2043
```

## Root cause
The seeded `CaseEvaluation_App` OpenIddict client has `RedirectUris` set to the literal value of `OpenIddict:Applications:CaseEvaluation_App:RootUrl` (= `http://localhost:${NG_PORT:-4200}`). When the SPA is accessed at `http://falkinstein.localhost:4200/` (the tenant-subdomain entry per ADR-006), the SPA's `oAuthConfig.redirectUri` resolves to `http://falkinstein.localhost:4200`, which does NOT match the literal `http://localhost:4200` in the seeded RedirectUris. OpenIddict performs exact-string redirect-URI matching with no wildcard support out of the box.

## Recommended fix
1. In the OpenIddict client seed (likely `DbMigrator` / `IDataSeedContributor` impl that creates `CaseEvaluation_App`), set `RedirectUris` as a multi-entry list:
   - `http://localhost:${NG_PORT}` (host-scope flow)
   - `http://*.localhost:${NG_PORT}` (subdomain-tenant flow)
2. OpenIddict 5+ supports prefix matching via the `Permissions` model; alternatively register an `OnApplyRedirectUriResponse` event handler that allows subdomain matches.
3. Apply the same change to `PostLogoutRedirectUris`.
4. Verify canonical-port (4200) path still works post-change so the fixes-session worktree isn't broken.

## Workaround (DB-direct)
```sql
UPDATE OpenIddictApplications
SET RedirectUris = '["http://localhost:4200","http://*.localhost:4200"]',
    PostLogoutRedirectUris = '["http://localhost:4200","http://*.localhost:4200"]'
WHERE ClientId = 'CaseEvaluation_App';
```
Wildcard subdomain support depends on whether the deployed OpenIddict version honors `*` patterns. If not, enumerate known tenant subdomains explicitly (e.g. `http://admin.localhost:4200`, `http://falkinstein.localhost:4200`).

## Related
- [[BUG-013]] (CORS missed AuthServer self-port) + [[BUG-014]] (hardcoded email URLs) + [[BUG-015]] (dynamic-env.json unused) form the multi-environment URL handling family. All four need to be config-driven end to end.
