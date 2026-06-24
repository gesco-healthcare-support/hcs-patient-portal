---
title: Unify Docker dev workflow + migrate to Yarn 4 (Berry)
status: in-progress
date: 2026-06-12
base-branch: feat/redesign-foundation
work-branch: chore/docker-dev-workflow
deadline-context: frontend redesign due Monday 2026-06-15; this is the enabling infra
---

# Unify Docker dev workflow + migrate to Yarn 4

## Goal

One multi-stage Dockerfile per service (dev + prod targets), latest Yarn (4.16,
node-modules linker), lean images, and a fast "edit -> `docker compose restart
<svc>`" loop for both frontend and backend. Deletes the vestigial `.dev`/`.local`
Dockerfile sprawl. Fixes the original failure (adding a dep no longer forces a
cold full image rebuild -- deps live in named volumes).

## Non-goals

- No live hot-reload (Windows + Docker bind-mount file-watching is unreliable --
  OBS-22; restart-to-iterate is the accepted loop).
- No change to AuthServer Razor auth pages (only how the image is built/run).
- No production deployment stack (separate effort; the `prod` target stays the
  deployable image).
- No package-manager change beyond yarn 1 -> 4 (not pnpm/npm).

## Proven (spike, 2026-06-12, throwaway worktree)

- yarn 4.16 + `nodeLinker: node-modules`: `yarn install` + `ng build` succeed on the
  host AND in a cold `node:20-alpine` docker build (the exact scenario that failed
  under yarn 1). Native deps (esbuild, @parcel/watcher, lmdb, msgpackr-extract)
  build. Only pre-existing NG8113 warnings.
- `corepack enable` EPERMs on the Windows host (Program Files) but `yarn set version`
  falls back to a committed `.yarn/releases` binary; corepack works in Linux containers.

## Decisions (grounded)

### D1 - Yarn config (angular/)
- `.yarnrc.yml`: `yarnPath: .yarn/releases/yarn-4.16.0.cjs`, `nodeLinker:
  node-modules`, `enableScripts: true` (explicit -- guarantees the native-dep builds
  the spike showed; matches yarn-1 parity), `httpTimeout: 600000`, `httpRetry: 5`
  (cheap install-resilience hardening; defaults worked in the spike but this removes
  doubt). `networkConcurrency` left at default 50 (spike used it).
- `package.json`: add `"packageManager": "yarn@4.16.0"` (corepack reads this in
  CI/containers).
- Commit `.yarn/releases/yarn-4.16.0.cjs`. Regenerate `yarn.lock` (Berry format).
- Root `.gitignore` append the canonical Berry block:
  `.yarn/*` + negations for `!.yarn/patches !.yarn/plugins !.yarn/releases
  !.yarn/sdks !.yarn/versions`, plus `.pnp.*`. (`.yarn/cache`,
  `.yarn/install-state.gz`, `.yarn/unplugged` thus ignored.)
- `.gitattributes`: `.yarn/releases/** binary linguist-vendored` and
  `.yarn/releases/** -diff` so the committed binary is not diffed or counted as code.

### D2 - Angular Dockerfile (one file, multi-stage)
Stages:
- `base`: `node:20-alpine`, `corepack enable`, copy `.yarnrc.yml package.json
  yarn.lock` + `.yarn/releases`.
- `deps`: `yarn install --immutable`.
- `build` (from deps): copy source, `ARG NG_CONFIG=docker`, `yarn ng build
  --configuration "$NG_CONFIG"`.
- `prod` (default, `nginx:alpine`): copy `nginx.conf`, `dynamic-env.json`, built
  `dist/.../browser`. (= current prod Dockerfile.)
- `dev` (from base): copy `dev-entrypoint.sh`; node_modules + source arrive at
  runtime (named volume + bind-mount). Entrypoint: `yarn install` (no-op when
  install-state matches) -> `yarn ng build --configuration docker` -> regenerate
  dynamic-env.json -> serve on :80 (browser-sync or a static server).
- Add `.yarn/cache` to `angular/.dockerignore` (keep `.yarn/releases`).

### D3 - Backend Dockerfiles (api, authserver, db-migrator: one file each)
- `build` (sdk): restore + publish (= current prod). **AuthServer build keeps the
  Node + ABP CLI + `abp install-libs` step.**
- `prod` (default, aspnet runtime): copy publish (= current prod).
- `dev` (sdk): restore + build baked; entrypoint wipes Windows-stale
  obj/project.assets.json, re-restores, `dotnet run` (= current Dockerfile.dev
  pattern). **AuthServer dev target must also produce `wwwroot/libs`** (run
  `abp install-libs`, or derive the dev stage from a stage that has it) or the Razor
  login UI loses its theme/jQuery/Bootstrap.
- db-migrator: prod target (one-shot publish->run); optional dev target (sdk
  `dotnet run`) so new migrations/seeds re-run from bind-mounted source.

### D4 - docker-compose.yml
- angular/api/authserver: add `build.target: dev`, bind-mount source, named volumes
  for deps (`angular_node_modules:/app/node_modules`,
  `nuget_packages:/root/.nuget/packages`) + optional yarn-cache volume.
- Keep all env/ports/healthchecks/depends_on as-is.
- Rewrite the stale header comment (it claims `dotnet watch` + `ng build --watch`,
  which OBS-22 removed).
- CI/prod still build the default `prod` target (no target override).

### D5 - CI (.github/workflows) -- validated on first PR run (no local runner)
- `commitlint.yml`: `yarn install --frozen-lockfile` -> `yarn install --immutable`.
- `security.yml`: `yarn audit --level moderate || true` -> `yarn npm audit
  --severity moderate || true` (Berry renamed the command + flag).
- `ci.yml` frontend jobs: add `corepack enable` before yarn; confirm `setup-node`
  `cache: 'yarn'` resolves the Berry cache folder (if flaky, switch to `actions/cache`
  on `yarn config get cacheFolder`). `packageManager` field lets corepack pick 4.16.
- `sonarcloud.yml`/`release.yml`: add `corepack enable` if they run yarn.

### D6 - Delete vestigial files (after the new setup is verified)
Delete `angular/Dockerfile.dev`, `angular/Dockerfile.local`, and the `.dev`/`.local`
variants under each `src/.../{HttpApi.Host,AuthServer,DbMigrator}`. Update the
doc references (OBS-22 finding, ENGINEERING-ROADMAP, the 2026-05-27 investigations)
to point at the unified Dockerfiles.

## Tasks (ordered; angular-first so frontend iteration unblocks early)

| # | Task | Verify | Approach |
|---|---|---|---|
| 1 | Yarn 4 config (D1): `.yarnrc.yml`, `packageManager`, regenerate `yarn.lock`, commit `.yarn/releases`, `.gitignore`/`.gitattributes` | `yarn install` + `yarn ng build` clean on host | code |
| 2 | Unified angular Dockerfile (D2) + dev-entrypoint + `.dockerignore`; (don't delete old yet) | `docker build --target prod` and `--target dev` succeed | code |
| 3 | Compose angular dev (D4 angular only) | `docker compose up -d --build angular`; `/foundation-preview` loads at :4250; edit a `.ts` -> `restart angular` reflects it | code |
| 4 | Unified backend Dockerfiles (D3) incl. AuthServer install-libs in dev | `docker build --target prod`/`--target dev` each | code |
| 5 | Compose backend dev (D4 backend) + rewrite header comment | full `docker compose up` healthy; edit a `.cs` -> `restart api` reflects it | code |
| 6 | CI edits (D5) | edits per Berry conventions; green on PR | code |
| 7 | Delete vestigial `.dev`/`.local` + fix doc refs (D6) | full `docker compose up --build` from clean still green | code |
| 8 | Final verification + PR into feat/redesign-foundation | image sizes lean; restart loops work both sides; foundation preview signed off | code |

**Milestone after Task 3:** frontend iteration + foundation-preview sign-off are
unblocked, even before backend/CI are converted.

## Risk / rollback
- Blast radius: build/dev infra only; running app behavior unchanged (same env,
  ports, healthchecks). The `prod` targets reproduce today's images.
- Each task is its own commit. Old `.dev`/`.local` Dockerfiles stay until Task 7, so
  there's a fallback during transition.
- Rollback: revert the branch / specific commits.
- Biggest residual unknown: CI Berry caching (can't run runners locally) -> validated
  on first PR; `yarn npm audit` flag exact form -> confirm against `yarn npm audit -h`.

## Verification (end state)
- `docker compose up -d --build` from a clean tree: all services healthy.
- Edit `angular/src/...` -> `docker compose restart angular` -> change visible at :4250.
- Edit a `.cs` -> `docker compose restart api` -> change visible.
- One Dockerfile per service; `.dev`/`.local` gone.
- Image sizes: angular ~0.15 GB, api/auth ~0.6 GB (unchanged from today's prod).
