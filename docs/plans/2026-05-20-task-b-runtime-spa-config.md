---
feature: task-b-runtime-spa-config
date: 2026-05-20
status: in-progress
base-branch: main
working-branch: feat/parallel-worktree-stacks
related-issues: []
closes: [BUG-015]
spec: docs/superpowers/specs/2026-05-20-task-b-runtime-spa-config.md
task-of: parallel-worktree-stacks (B of 6)
---

# Plan: Task B -- runtime SPA config load (BUG-015)

## Goal

Make the Angular SPA load its URL configuration at RUNTIME by writing
`dynamic-env.json` from container env vars in `dev-entrypoint.sh` and
fetching+merging it in `main.ts` before `bootstrapApplication`, so the
same built Angular image can be re-pointed at different backend ports
per Docker stack.

## Context

Task B is the 2nd of six commits on `feat/parallel-worktree-stacks`
(off `main`). Task A landed at commit `5cdae28` (BUG-014 fix --
config-driven email URLs). All six tasks ship as ONE pull request at
the end -- no per-task PR. Per-task smoke gates are verification
checkpoints inside the branch, not merge gates.

BUG-015 is the second of two technical blockers (the other was
BUG-014, fixed in Task A) for reversing OBS-9 (2026-05-14 decision to
abandon parallel Docker stacks). Without Task B, the SPA bakes
canonical URLs into the bundle at build time regardless of port
shifting, so a second worktree's stack on offset ports would still
hijack the main stack's API.

Authoritative design context:
`docs/superpowers/specs/2026-05-20-task-b-runtime-spa-config.md`.

## Approach

**Chosen: pre-bootstrap async IIFE in `main.ts` + inline heredoc in
`dev-entrypoint.sh`.**

Two layers:

1. **Entrypoint emit (Layer 1).** `dev-entrypoint.sh` writes
   `dynamic-env.json` to `$DIST` from container env vars `$NG_PORT`,
   `$AUTH_PORT`, `$API_PORT` (with `${VAR:-default}` fallback) using a
   bash heredoc, ONCE at script startup before `ng build --watch` is
   spawned. The existing `ensure_dynamic_env()` 3-second copy loop is
   removed; the `docker/dynamic-env.json` source file is deleted; the
   bind-mount line is removed from compose; the `angular/dynamic-env.json`
   placeholder + its `angular.json` asset entry are removed so ng-watch
   rebuilds don't clobber the heredoc-written file.

2. **Pre-bootstrap fetch + merge (Layer 2).** `angular/src/main.ts`
   becomes an async IIFE. Fetches `dynamic-env.json` (relative path,
   `cache: 'no-store'`), `Object.assign`-merges the JSON into the
   imported `environment` constant, then runs the existing
   `tenant-bootstrap` chain unchanged, then `bootstrapApplication`. On
   fetch/parse failure: `console.warn` + silent fallback to baked-in
   `environment.docker.ts` values.

**Alternatives rejected:**

- `provideAppInitializer` -- runs after `provideAbpCore`
  constructs, so mutation is too late. Verified via Angular 20 source
  (`application_init.ts`).
- Build-time bake (Dockerfile ARG) -- defeats port-agnostic image
  property.
- `envsubst` template substitution -- needs `apk add gettext` in
  Dockerfile.dev (new dep, image bloat), and `envsubst` doesn't
  support `${VAR:-default}` syntax.
- Separate node renderer script -- unnecessary complexity vs an
  inline heredoc that has every var already in scope.

## Tasks

- T1: Edit `angular/dev-entrypoint.sh` -- add startup heredoc (minimal-corrected scope)
  - approach: code
  - files-touched: [angular/dev-entrypoint.sh]
  - acceptance: Inline heredoc added near top of script (after the
    DIST/INDEX/ENV_SRC variable definitions, before `ensure_dynamic_env()`
    function). Heredoc writes to `$ENV_SRC` (= `/app/dynamic-env.json`)
    using `${NG_PORT:-4200}`, `${AUTH_PORT:-44368}`, `${API_PORT:-44327}`
    with literal-fallback defaults. Existing `ensure_dynamic_env()` loop,
    `exec npx concurrently` block, and inline cp inside the second
    concurrently process are ALL preserved unchanged.
    `bash -n angular/dev-entrypoint.sh` parses without error. Heredoc
    body matches spec section 2 Layer 1 verbatim.

- T2: Edit `docker-compose.yml` angular service block
  - approach: code
  - files-touched: [docker-compose.yml]
  - acceptance: angular service has new `environment:` block with
    `NG_PORT: "${NG_PORT:-4200}"`, `AUTH_PORT: "${AUTH_PORT:-44368}"`,
    `API_PORT: "${API_PORT:-44327}"`. The bind-mount line
    `- ./docker/dynamic-env.json:/app/dynamic-env.json:ro` is removed.
    `docker compose config` parses without error and shows the three
    new env vars when grepping the angular service section.

- T3: Delete `docker/dynamic-env.json` only (minimal-corrected scope)
  - approach: code
  - files-touched: [docker/dynamic-env.json (delete)]
  - acceptance: `git status --short` shows `docker/dynamic-env.json`
    with status `D`. `angular/dynamic-env.json` is intentionally
    PRESERVED as the `{}` placeholder for ng build's asset glob;
    the entrypoint cp overwrites the placeholder output with the
    heredoc-written content at runtime.

- T4: SKIPPED (minimal-corrected scope)
  - approach: code
  - files-touched: []
  - acceptance: `angular/angular.json` is intentionally UNCHANGED.
    The `dynamic-env.json` asset entry stays because the placeholder
    behavior is harmless (overwritten by entrypoint cp). Reason for
    scope reduction documented in 2026-05-20 chat: removing the asset
    entry was a non-essential cleanup; the goal is "make dynamic-env.json
    reflect runtime URLs," not "modernize the entrypoint structure."

- T5: Edit `angular/src/main.ts` -- async IIFE with fetch+merge
  - approach: code
  - files-touched: [angular/src/main.ts]
  - acceptance: File contains an async IIFE wrapping the existing
    bootstrap logic. Inside the IIFE: (a) `try/catch` block fetching
    `dynamic-env.json` with `{ cache: 'no-store' }`; (b)
    `Object.assign(environment, await res.json())` on success; (c)
    `console.warn` on non-OK response or thrown error; (d) existing
    `detectTenantSlugAndMaybeRedirect()` + `rewriteEnvironmentForTenantSubdomain`
    invocation order preserved AFTER the merge; (e) `enableProdMode()`
    guard + `bootstrapApplication(AppComponent, appConfig)` call
    unchanged. Code matches spec section 2 Layer 2 verbatim.

- T6: Smoke gate Phase 1 -- entrypoint emits offset-port JSON
  - approach: code
  - files-touched: [] (verification, temporary edit to compose if needed)
  - acceptance: `cd /w/patient-portal/main && NG_PORT=4299 AUTH_PORT=44399 API_PORT=44329 docker compose up -d --build --force-recreate angular`
    succeeds. `curl -sS http://localhost:4299/dynamic-env.json` returns
    JSON containing `"http://localhost:4299"` (baseUrl + redirectUri),
    `"http://localhost:44399/"` (issuer), `"http://localhost:44329"`
    (apis.default.url), `"http://localhost:44399"` (AbpAccountPublic.url).
    **STOP if FAIL.** Likely failure causes if FAIL: heredoc syntax
    error, env vars not propagated to container, or `$DIST` directory
    doesn't exist when heredoc runs (need to `mkdir -p`).

- T7: Smoke gate Phase 2 -- SPA's first XHR targets offset-port API
  - approach: code
  - files-touched: [] (verification)
  - acceptance: With T6 stack still running on offset ports, open
    `http://localhost:4299/` in a browser with DevTools Network tab
    open. The first backend XHR (typically
    `/api/abp/application-configuration`) should be requested against
    `http://localhost:44329` OR `http://falkinstein.localhost:44329`
    (after subdomain redirect). NOT `http://localhost:44327` (the
    baked default). **STOP if FAIL.** Failure causes if FAIL:
    `Object.assign` didn't propagate to ABP's providers (would
    contradict tenant-bootstrap.ts evidence), or the fetch is
    completing after `bootstrapApplication`. Restore canonical ports
    in compose after Phase 2 passes.

- T8: Commit Task B as a single combined commit (no push, no PR)
  - approach: code
  - files-touched: [] (git only)
  - acceptance: After T1-T7 all pass, ALL 8 Task B files (4 edits + 2
    deletes + spec + plan) staged and committed together via
    `git commit -m "..."` using the message in the "Task B commit
    message" section below. **DO commit.** **DO NOT run `git push`.**
    **DO NOT run `gh pr create`.** `git log --oneline -2` shows Task
    B commit at HEAD followed by Task A commit (5cdae28). PR opens
    only after all six tasks + cross-worktree verification.

## Risk / Rollback

**Blast radius:** SPA bootstrap path -- every page load goes through
`main.ts`. A bug surfaces immediately on next reload. Frontend-only:
no DB migration, no schema change, no setting-value seed. Backend
unchanged from post-Task-A state.

**Rollback:** Trivial. `git restore` working-tree changes (nothing
committed until T8). If committed: revert the single commit; no data
cleanup. Containers pick up the reverted `main.ts` on next ng-watch
cycle.

**Smoke-gate behavior on FAIL:**

- T6 FAIL -> entrypoint debugging. Likely culprits: missing `mkdir -p`
  for `$DIST`, env vars not in container (check `docker exec ... env`),
  heredoc syntax error. Do NOT proceed to T7.
- T7 FAIL -> the mutation pattern is the broken link. The `tenant-bootstrap.ts`
  empirical evidence (working subdomain rewrite via the same mutation
  surface) becomes load-bearing. If `provideAbpCore` is cloning the
  options object, the design needs restructuring (e.g. pass URLs
  explicitly via a setter on `ConfigStateService`). Do NOT proceed to
  T8.

## Verification

End-to-end Task B verification (after T1-T7 all pass):

1. `npx ng build --configuration docker` completes without error
   (asset entry removal verified).
2. With offset-port stack running per T6: `curl
   http://localhost:4299/dynamic-env.json` shows offset-port URLs.
3. With offset-port stack running per T7: browser opens correctly,
   first XHR targets offset-port API.
4. Restore canonical ports in compose; `docker compose up -d
   --force-recreate angular`. `curl
   http://localhost:4200/dynamic-env.json` shows canonical URLs.
5. Browser open `http://localhost:4200/`; existing test flows
   (login, etc.) work as before. No regression in baseline behavior.

Cross-task verification (after ALL of A-F complete, before single PR):
see Task A plan's "Cross-task verification" section. Same procedure.

## Task B commit message

After T1-T7 acceptance criteria all pass, commit Task B's deliverables
as ONE commit on `feat/parallel-worktree-stacks`:

```
feat(angular): runtime dynamic-env.json load via pre-bootstrap fetch

- main.ts becomes async IIFE: fetches dynamic-env.json (cache:no-store)
  + Object.assign-merges into environment before tenant-bootstrap +
  bootstrapApplication; console.warn fallback on fetch failure
- dev-entrypoint.sh emits dynamic-env.json from container env vars via
  inline heredoc at startup; removes dead 3-sec re-copy loop
- docker-compose.yml: angular service gains NG_PORT/AUTH_PORT/API_PORT
  env block; removes docker/dynamic-env.json bind-mount
- Deletes docker/dynamic-env.json + angular/dynamic-env.json (`{}`
  placeholder); removes asset entry from angular.json so ng-watch
  rebuilds don't clobber the heredoc output
- Plan + spec docs under docs/plans/ + docs/superpowers/specs/

Closes BUG-015.
```

Header length: 60 chars (under 72 hard cap). Body bullets each <= 20
words per `commit-format.md`.

## Do-not-push directive

`git push` is NOT run as part of Task B. `gh pr create` is NOT run.
The PR opens only after all six tasks (A done, B-F pending) pass
their smoke gates AND the cross-worktree verification (parallel
stacks both running cleanly) succeeds.

## Out of scope

- Production Angular Dockerfile (deferred; current scope is
  `Dockerfile.dev`).
- Service-worker compatibility (no service worker today; future PR
  that adds `provideServiceWorker` must exclude `dynamic-env.json`
  from precache).
- OBS-9 documentation reversal (handled in Task F).
- BUG-014 fix (Task A; already shipped).
- Compose hygiene for MinIO/Gotenberg ports (Task C).
- `replicate-old-app` retrofit (Task D).
- Worktree role markers (Task E).
