---
feature: prod-remoteenv-office
date: 2026-07-17
status: draft
base-branch: development
related-issues: []
---

## Goal

Stop the production SPA from sending office-LESS `auth.`/`api.` URLs (which CORS-fail -> `/error`) by removing ABP's `remoteEnv` block from `environment.prod.ts`, so the per-office subdomain prefix applied at boot survives.

## Context

- Live symptom: in a real browser (any profile, cleared cache), `https://admin.appointment-portal.pfd.tbc.local/` -> `/error`; console shows CORS preflight failures fetching the BARE `https://auth.appointment-portal.pfd.tbc.local/.well-known/openid-configuration` and `https://api.appointment-portal.pfd.tbc.local/...` (no `admin.` office prefix). Never reproduced on localhost.
- The SPA is one build for all offices; `main.ts`'s pre-bootstrap IIFE fetches `dynamic-env.json` and `tenant-bootstrap.ts` prepends the office subdomain (`admin.`) to issuer/api/baseUrl BEFORE `bootstrapApplication`. That produces the correct `admin.auth.`/`admin.api.` origins.
- ROOT CAUSE: `environment.prod.ts` ALSO declares `remoteEnv: { url: '/getEnvConfig', mergeStrategy: 'deepmerge' }`. ABP fetches `/getEnvConfig` at init and deep-merges it into `environment` with the REMOTE values prioritized (ABP docs). `/getEnvConfig` serves the office-LESS `dynamic-env.json` (verified: `issuer: https://auth.appointment-portal.pfd.tbc.local/`), so ABP overwrites the office-prefixed issuer/api URLs back to the bare host AFTER the IIFE rewrite. The bare host has no nginx route to authserver/api (`{office}.auth`/`{office}.api` only) -> CORS -> `/error`.
- Prod-only because ONLY `environment.prod.ts` has `remoteEnv`; `environment.docker.ts` (localhost) and `environment.ts` do not -> localhost never hit it. The intermittent Playwright "pass" is a race between ABP's `/getEnvConfig` merge and the OAuth discovery init.
- Sources: ABP Angular Environment docs (deepmerge = remote prioritized; getEnvConfig is the remoteEnv feature; "you could remove the configuration if you didn't use it"); an ABP support thread reports the identical login-error symptom fixed by removing `remoteEnv` from `environment.prod.ts`.

## Approach

Remove the `remoteEnv` block from `environment.prod.ts`. The `main.ts` IIFE already loads the same `dynamic-env.json` at boot (BUG-015 design) AND adds the per-office prefix + the bare-host->admin redirect, which `remoteEnv` cannot do. `remoteEnv` is redundant for loading and actively conflicts with per-office routing. Add a comment so it is not re-added. Leave the `/getEnvConfig` nginx alias in place (now unused, harmless).

Alternatives rejected:
- Keep `remoteEnv`, make `/getEnvConfig` return office-prefixed URLs: nginx can't do per-office rewrite without duplicating `tenant-bootstrap.ts` server-side; brittle.
- `remoteEnv` `customMergeFn` that re-prepends: keeps two competing loaders; more code; still races.
- Drop the IIFE, rely on `remoteEnv`: the IIFE must run pre-bootstrap (so ABP captures the values) and also does the bare-host->admin redirect; `remoteEnv` can't replace it.

## Tasks

- T1: Remove the `remoteEnv` block from `angular/src/environments/environment.prod.ts` (+ explanatory comment).
  - approach: code
  - files-touched: [angular/src/environments/environment.prod.ts]
  - acceptance: prod build no longer requests `/getEnvConfig`; the OAuth discovery + ABP config requests go to `admin.auth.`/`admin.api.` (office-prefixed).
- T2: Commit, push, PR into `development`.
  - approach: code
  - files-touched: [git]
  - acceptance: PR green; conventional commit; no attribution.
  - STOP: confirm before merge + the live-server rebuild.
- T3: Merge (admin) + FULL docker stack rebuild on the box + restart.
  - approach: code
  - files-touched: [server]
  - acceptance: `git pull` reaches the merged commit; `docker compose ... build` (all services) + `up -d` -> all healthy.
- T4: Verify (below).
  - approach: code
  - files-touched: []
  - acceptance: all verification checks pass, incl. Adrian's real-browser confirmation.

## Risk / Rollback

- Blast radius: Angular SPA runtime config only. No backend/data change. Worst case: the SPA can't read config -> but the IIFE path is already the working loader (proven: it produces the office prefix), so removing the conflicting re-merge can only help.
- Rollback: previous images retained (do NOT prune) -> re-up prior image; or `git revert` + rebuild. Never `docker compose down -v`.

## Verification

1. Fresh Playwright Chrome: NO request to `/getEnvConfig`; discovery + app-config requests go to `admin.auth.`/`admin.api.` and return 200; reaches the Sign in page, 0 console errors. (Deterministic now -- the racing loader is gone.)
2. `curl -ks .../getEnvConfig` still 200 (alias intact) but the SPA no longer calls it.
3. THE decisive check: Adrian confirms in his own real Chrome (new profile) that he reaches the Sign in page, not `/error`. (Playwright alone is insufficient -- it masked this via the race.)
4. Full stack healthy after rebuild (`docker compose ps` all healthy).
