---
id: BUG-015
title: dynamic-env.json is documented but never read by the SPA
severity: medium
status: fixed-by-redesign
fixed: 2026-05-22
last-replayed: 2026-05-22
fixed-on: feat/parallel-worktree-stacks (merged via #208)
found: 2026-05-14
flow: angular-spa-bootstrap
component: angular/src/app/app.config.ts (missing APP_INITIALIZER)
---

> **Fixed-by-redesign 2026-05-22** -- commit `76348fd`
> (`feat(angular): runtime dynamic-env.json load via pre-bootstrap fetch`,
> merged via #208 on `feat/parallel-worktree-stacks`) landed the
> runtime-config load with a **better approach than the recommended
> APP_INITIALIZER**: a pre-bootstrap async IIFE in `angular/src/main.ts`.
>
> **Why async IIFE instead of `APP_INITIALIZER` / `provideAppInitializer`?**
> ABP's `provideAbpCore({ environment })` captures the imported
> `environment` reference at provider-array construction time -- which
> happens *before* `bootstrapApplication()` starts running, so any
> APP_INITIALIZER mutation would fire too late to influence ABP's
> captured copy. The IIFE mutates `environment` BEFORE
> `bootstrapApplication()` is even called, guaranteeing ABP (and the
> OAuth client, and the HTTP clients) see the merged values. This is
> the canonical solution for runtime-config-into-provider-factories
> scenarios per the Angular ecosystem -- see Lucas Arcuri / ITNEXT
> writeups and Angular issue #45970 ("lazy init for standalone").
>
> **Recommended-fix audit** (every item from the original report below):
>
> | Item | Status |
> |---|---|
> | 1. `APP_INITIALIZER` that fetches `/dynamic-env.json` before bootstrap | Done **with smarter approach**: pre-bootstrap async IIFE in `angular/src/main.ts:19-46`. Same `Object.assign(environment, json)` mutation, but runs before provider-array construction so ABP-captured references pick up the merged values |
> | 2. Merge loaded JSON into mutable `environment` singleton | Done -- `Object.assign(environment, await res.json())` |
> | 3. Keep `environment.docker.ts` as fallback for missing/404 | Done -- `console.warn` fallback on non-2xx + try/catch on fetch failure; bootstrap continues regardless |
> | 4. Update `MAIN-WORKTREE-USERFLOW-TESTING.md` to describe the feature | Moot -- the doc has no `dynamic-env` mention currently (the "phantom feature" description was already removed in a prior commit); nothing to correct |
>
> **Infra wiring** (also part of the redesign, not in the original report):
>
> - `angular/dev-entrypoint.sh` writes `/app/dynamic-env.json` from
>   container env vars (`NG_PORT`, `AUTH_PORT`, `API_PORT`) via heredoc
>   at startup. The `ensure_dynamic_env` background loop keeps the file
>   present in `dist/CaseEvaluation/browser/` even if `ng watch` clobbers
>   it on a rebuild.
> - `docker-compose.yml` angular service gained `NG_PORT`, `AUTH_PORT`,
>   `API_PORT` env-var block; the prior `docker/dynamic-env.json`
>   bind-mount (with hardcoded canonical URLs) was removed.
>
> **Live-verified 2026-05-22** against `main-angular-1` on the running stack:
>
> | Probe | Result |
> |---|---|
> | Container has `/app/dynamic-env.json` heredoc-templated | OK (NG=4200, AUTH=44368, API=44327) |
> | `GET http://falkinstein.localhost:4200/dynamic-env.json` | HTTP 200, `Content-Type: application/json`, 663 bytes |
> | `GET http://admin.localhost:4200/dynamic-env.json` | HTTP 200 (tenant-agnostic file, served at SPA root) |
> | SPA fetches it at boot (Playwright network log) | Entry #39 in the session: `GET .../dynamic-env.json => 200 OK` |
> | **Merged values reach the OIDC client** | Served `issuer: "http://localhost:44368/"` -> runtime `iss` claim is `"http://falkinstein.localhost:44368/"`. The **port 44368** matches the heredoc-generated JSON (not anything in `environment.docker.ts`), proving the merge actually mutated the live env. The `falkinstein.` subdomain was prepended by `tenant-bootstrap.ts` post-merge. |
>
> The last row is the strongest evidence: the runtime OIDC port came
> from the JSON file, not from the build-time `environment.docker.ts`
> baked-in default. If the IIFE merge weren't working, the runtime
> issuer would have shown whatever port `environment.docker.ts` carries.
>
> Architecture references:
> - `angular/src/main.ts:19-46` (pre-bootstrap async IIFE with try/catch + console.warn fallback)
> - `angular/dev-entrypoint.sh:13-43` (heredoc from container env vars + `ensure_dynamic_env` background loop)
> - `docker-compose.yml` angular service env block (`NG_PORT`, `AUTH_PORT`, `API_PORT`)

# BUG-015 — dynamic-env.json never loaded

## Severity
medium (blocks any-environment SPA testing; makes runtime config impossible)

## Status
**Fixed-by-redesign** — see top quote block.

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
