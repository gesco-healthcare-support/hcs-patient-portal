---
id: BUG-015
title: dynamic-env.json is documented but never read by the SPA
severity: medium
status: fixed-by-redesign
last-replayed: 2026-05-21
found: 2026-05-14
flow: angular-spa-bootstrap
component: angular/src/main.ts (pre-bootstrap async IIFE -- different fix path than the original APP_INITIALIZER recommendation)
---

> **2026-05-21 replay outcome: fixed.** Task B (2026-05-20, commit
> 76348fd on `feat/parallel-worktree-stacks`) implemented the runtime-
> config load via a pre-bootstrap async IIFE in `main.ts` (NOT the
> originally-recommended `APP_INITIALIZER` in `app.config.ts` --
> the IIFE approach was chosen because `provideAppInitializer` runs
> AFTER `provideAbpCore({ environment })` captures the environment by
> reference, making mutation too late).
>
> Verified 2026-05-21 against main stack:
> 1. `performance.getEntriesByType('resource')` shows 2 fetches of
>    `http://falkinstein.localhost:4200/dynamic-env.json` during SPA
>    bootstrap (durations 70ms + 6ms; HTTP 200, content-type
>    `application/json`).
> 2. Response body contains the runtime config:
>    `oAuthConfig.issuer = "http://localhost:44368/"`,
>    `apis.default.url = "http://localhost:44327"`,
>    `application.baseUrl = "http://localhost:4200"`.
> 3. These values are env-var-driven via the `dev-entrypoint.sh`
>    heredoc that writes `dynamic-env.json` from `NG_PORT` /
>    `AUTH_PORT` / `API_PORT` container env at startup, so different
>    worktrees get different runtime URLs from the same built Angular
>    image.
>
> Side observation: the URLs in the loaded config have NO tenant
> subdomain prefix (`localhost:44368` not `falkinstein.localhost:44368`).
> That is the same root cause as [[BUG-029]] (URL composition expects
> `TenantUrlComposer` to prepend the tenant at runtime). The fact
> that `dynamic-env.json` IS loaded means BUG-015 itself is closed;
> the URL-composition concern lives in [[BUG-029]].

# BUG-015 — dynamic-env.json never loaded

## Severity
medium (blocks any-environment SPA testing; makes runtime config impossible)

## Status
**Open** — for fix session.

## Symptom
The Angular SPA always uses the URLs baked into `environment.docker.ts` at `ng build` time. The bind-mounted `/app/dynamic-env.json` (also exposed at the SPA's root via `/dynamic-env.json` HTTP path) is NEVER read by the SPA. The docker entrypoint copies the file into `dist/CaseEvaluation/browser/` after each rebuild — suggesting an intent to load it at runtime — but no Angular code does so.

## Root cause
`grep` across `angular/src/` for `dynamic-env` or `DynamicEnv` returns **zero matches**. No `APP_INITIALIZER`, no `HTTP_INTERCEPTOR`, no service reads `/dynamic-env.json`. The `environment.docker.ts` file is the sole config source, and Angular compiles it as a static module at build time.

## Recommended fix
1. Add an `APP_INITIALIZER` provider in `app.config.ts` that fetches `/dynamic-env.json` via `fetch()` before app bootstrap.
2. Merge the loaded JSON into a mutable in-memory `environment` singleton; refactor existing `environment.*` consumers to read through it.
3. Keep `environment.docker.ts` as the fallback so a missing/404 `dynamic-env.json` doesn't break the app.
4. Update `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md` to accurately describe what `dynamic-env.json` does (right now it documents a phantom feature).

## Sample implementation
```typescript
// app.config.ts
function loadRuntimeEnv(): Promise<void> {
  return fetch('/dynamic-env.json')
    .then(r => r.ok ? r.json() : Promise.resolve({}))
    .then(json => { Object.assign(environment, json); })
    .catch(() => { /* fall back to build-time env */ });
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideAppInitializer(loadRuntimeEnv),
    // ...rest
  ],
};
```

## Related
- [[BUG-014]] (hardcoded URLs in email templates) — these two together would make multi-env config work end to end.
