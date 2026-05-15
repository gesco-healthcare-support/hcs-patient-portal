---
id: BUG-015
title: dynamic-env.json is documented but never read by the SPA
severity: medium
status: open
found: 2026-05-14
flow: angular-spa-bootstrap
component: angular/src/app/app.config.ts (missing APP_INITIALIZER)
---

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
