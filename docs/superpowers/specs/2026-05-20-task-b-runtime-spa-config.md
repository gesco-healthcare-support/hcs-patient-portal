---
title: "Task B: Runtime SPA Config Load (BUG-015 Fix)"
date: 2026-05-20
status: draft
branch: feat/parallel-worktree-stacks
task-of: parallel-worktree-stacks (B of 6)
closes:
  - BUG-015
related:
  - OBS-9 (will be reversed once Task D retrofit also lands)
  - Task A (5cdae28) -- BUG-014 backend half of the parallel-stack fix
---

# Task B: Runtime SPA Config Load (BUG-015 Fix)

Single-PR plan: this task is the second of six commits on
`feat/parallel-worktree-stacks`. All six tasks ship together as one PR
to `main` at the end. Per-task smoke tests are verification gates, not
merge gates.

## 1. Problem

The Angular SPA bakes URLs into the bundle at build time via
`environment.docker.ts:3,6,27` (`http://localhost:4200`,
`http://localhost:44368/`, `http://localhost:44327`). The
`docker/dynamic-env.json` file (tracked, 27 lines, same hardcoded URLs)
LOOKS like a runtime config but `BUG-015-dynamic-env-unused.md` reports
that no SPA code reads it -- `grep` for `dynamic-env` / `DynamicEnv`
across `angular/src/` returns zero matches. The Dockerfile's
`dev-entrypoint.sh` copies the file into `dist/` after each rebuild,
suggesting an intent that was never wired through.

Effect on parallel Docker stacks: even with port-shifted backends (Task
A's premise), each Angular image still ships with canonical URLs baked
in. The SPA at `localhost:4230` (a hypothetical second worktree on
offset ports) would still issue XHRs to `localhost:44327` (Main's API).
Cross-stack hijack on every request. This is the second of two
technical blockers OBS-9 cited for the abandoned 2026-05-14 parallel-
stack attempt. Task A fixed the backend half; this task fixes the SPA
half.

## 2. Solution -- two layers

### Layer 1: Entrypoint heredoc (runtime config emit)

**Minimal-corrected scope (2026-05-20):** `angular/dev-entrypoint.sh`
writes `dynamic-env.json` from container env vars to `$ENV_SRC`
(= `/app/dynamic-env.json`) via inline heredoc at script startup,
BEFORE `exec npx concurrently`. The existing concurrently
second-process's `cp "$ENV_SRC" "$DIST/dynamic-env.json"` is preserved
(timing-wise it fires after `ng build`'s first iteration places the
SPA bundle in `$DIST`). The existing `ensure_dynamic_env()` background
loop is also preserved (defensive guard against any future ng-watch
clobber). The `docker/dynamic-env.json` bind-mount is removed from
compose; the file is deleted from the repo.

Why this shape: the current entrypoint already orchestrates the
"write to /app, cp to $DIST after first build, defensive re-cp loop"
flow. The only thing actually broken is the SOURCE content
(hardcoded canonical URLs in `docker/dynamic-env.json`). Swapping the
bind-mounted source for an entrypoint-written source is the smallest
change that achieves the fix. The `angular/dynamic-env.json` `{}`
placeholder and the `angular.json:48` asset entry remain unchanged --
they're harmless because the entrypoint cp overwrites the
placeholder's output after ng build's first iteration.

```bash
# Near the top of dev-entrypoint.sh, after DIST/INDEX/ENV_SRC defs:
ng_port="${NG_PORT:-4200}"
auth_port="${AUTH_PORT:-44368}"
api_port="${API_PORT:-44327}"

# BUG-015 (Task B, 2026-05-20): write runtime config from container env
# vars. Replaces the previous bind-mount of docker/dynamic-env.json
# (which was tracked-in-repo with hardcoded canonical URLs). The
# existing concurrently second-process copies this into $DIST after
# ng build's first iteration; the existing ensure_dynamic_env() loop
# re-copies if ng watch ever clobbers it.
cat > "$ENV_SRC" <<EOF
{
  "production": false,
  "application": {
    "baseUrl": "http://localhost:${ng_port}",
    "name": "CaseEvaluation",
    "logoUrl": ""
  },
  "oAuthConfig": {
    "issuer": "http://localhost:${auth_port}/",
    "redirectUri": "http://localhost:${ng_port}",
    "clientId": "CaseEvaluation_App",
    "responseType": "code",
    "scope": "offline_access openid profile email phone CaseEvaluation",
    "requireHttps": false
  },
  "apis": {
    "default": {
      "url": "http://localhost:${api_port}",
      "rootNamespace": "HealthcareSupport.CaseEvaluation"
    },
    "AbpAccountPublic": {
      "url": "http://localhost:${auth_port}",
      "rootNamespace": "AbpAccountPublic"
    }
  }
}
EOF
```

Container needs `NG_PORT` / `AUTH_PORT` / `API_PORT` in its env. The
Angular service block in `docker-compose.yml` doesn't currently pass
these (they're used only for compose-level interpolation in the
host-port mapping). Add an `environment:` block to the angular service.

### Layer 2: Pre-bootstrap fetch + merge in main.ts

`angular/src/main.ts` becomes an async IIFE. Fetches
`dynamic-env.json` (relative path, `cache: 'no-store'`), merges the JSON
into the imported `environment` constant via `Object.assign`, then runs
the existing `tenant-bootstrap` chain unchanged.

```typescript
import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './environments/environment';
import {
  detectTenantSlugAndMaybeRedirect,
  rewriteEnvironmentForTenantSubdomain,
} from './tenant-bootstrap';

// BUG-015 (Task B, 2026-05-20) -- runtime-load dynamic-env.json so the
// same built Angular image can be re-pointed at different backend ports
// per Docker stack. ABP's provideAbpCore({ environment }) captures the
// imported reference, so mutating environment here (before bootstrap)
// propagates. Same pattern proven by tenant-bootstrap.ts's subdomain
// rewrite. On fetch failure: console.warn + silent fallback to the
// baked environment.docker.ts URLs.
(async () => {
  try {
    const res = await fetch('dynamic-env.json', { cache: 'no-store' });
    if (res.ok) {
      Object.assign(environment, await res.json());
    } else {
      console.warn(
        '[bootstrap] dynamic-env.json returned',
        res.status,
        '-- using baked defaults',
      );
    }
  } catch (err) {
    console.warn(
      '[bootstrap] dynamic-env.json fetch failed:',
      err,
      '-- using baked defaults',
    );
  }

  const tenantSlug = detectTenantSlugAndMaybeRedirect();
  if (tenantSlug !== null) {
    rewriteEnvironmentForTenantSubdomain(environment, tenantSlug);

    if (environment.production) {
      enableProdMode();
    }

    bootstrapApplication(AppComponent, appConfig).catch((err) =>
      console.error(err),
    );
  }
})();
```

Ordering rationale: dynamic-env merge writes bare-localhost URLs into
`environment`. `tenant-bootstrap` then rewrites bare-localhost ->
`<tenant>.localhost`. Idempotent because the rewrite regex
(`tenant-bootstrap.ts:99`) only matches bare-localhost tokens.

## 3. Why this is safe end-to-end

- **`provideAbpCore({ environment })` captures by reference.** Verified
  empirically: `tenant-bootstrap.ts` mutates the same imported
  `environment` constant and ABP's downstream providers see the rewrite.
  Same mutation surface for our merge.
- **`fetch` runs before Zone.js patches it.** Top-level `main.ts` runs
  before Angular's NgZone setup. Errors won't enter Angular's error
  handler -- that's why the `try/catch` is explicit and `console.warn`
  is the surface.
- **Relative path + `cache: 'no-store'`.** Path is `'dynamic-env.json'`
  (no leading slash) so it respects any future `<base href>`. No-store
  prevents stale browser cache from masking entrypoint updates.
- **Service-worker interception.** No service worker currently
  configured (no `provideServiceWorker` in `app.config.ts`). If one
  lands later, that PR must exclude `dynamic-env.json` from precache.
  Flagged in `docs/runbooks/findings/bugs/BUG-015-dynamic-env-unused.md`
  recommended-fix step 1.
- **Phase 1B multi-tenant.** Subdomain rewrite happens AFTER the merge.
  Multi-tenant access (e.g. `pelton.localhost:NG_PORT`) gets
  Pelton-prefixed URLs without code change here.

## 4. Architecture impact

- **Domain / Application / EF Core / HttpApi:** unchanged.
- **DbMigrator:** unchanged.
- **Angular bundle:** identical bytes regardless of which Docker stack
  it runs in. Port-agnostic image.
- **Tests:** no new unit tests. `main.ts` is the bootstrap shell;
  behavior is verified by smoke test, not isolated unit tests. The
  tenant-bootstrap rewrite already has working-in-production evidence.
- **Compose:** Angular service block gains `environment:` block.
  `docker/dynamic-env.json` bind-mount line removed.

## 5. Error handling

| Scenario | Behavior |
|---|---|
| Entrypoint writes JSON successfully | SPA fetches it; merges into env; subdomain rewrite applies; bootstrap with correct URLs |
| Entrypoint fails to write (disk full, etc.) | `dist/.../browser/dynamic-env.json` doesn't exist; fetch returns 404; `console.warn` triggered; SPA bootstraps with baked env.docker.ts URLs |
| Fetch returns 404 | `console.warn('[bootstrap] dynamic-env.json returned 404 -- using baked defaults')`; SPA bootstraps with baked URLs |
| Fetch network error | `console.warn('[bootstrap] dynamic-env.json fetch failed: ... -- using baked defaults')`; SPA bootstraps with baked URLs |
| Malformed JSON | Same as fetch-failed (the `await res.json()` throws into the catch) |

No UI alert, no app hang on any failure. Worst case: SPA loads with
baked URLs; user sees the app working against canonical ports
regardless of what the entrypoint did. That's the safest fallback when
config infrastructure has a hiccup.

## 6. Testing

### Smoke test gate (manual, gating Task B commit)

1. **Build the Angular image once on this branch.**
   `cd /w/patient-portal/main && docker compose build angular`
   (Should rebuild fast since base layers cache.)

2. **Override the ports in the running stack.** Temp-set
   `NG_PORT=4299`, `AUTH_PORT=44399`, `API_PORT=44329` in the angular
   service block of `docker-compose.yml` (or via shell-exported
   variables before `docker compose up -d --force-recreate angular`).

3. **Verify entrypoint output.**
   `curl -sS http://localhost:4299/dynamic-env.json`
   Expected: JSON containing `"baseUrl": "http://localhost:4299"`,
   `"issuer": "http://localhost:44399/"`, `"url":
   "http://localhost:44329"`. If canonical ports appear instead:
   entrypoint didn't write -- STOP and check container env + script
   logs.

4. **Verify SPA picks up runtime URLs.**
   Open `http://localhost:4299/` in a browser with DevTools Network
   tab open. Observe the first XHR to the backend (e.g.
   `application-configuration`). Target should be
   `localhost:44329` (or `falkinstein.localhost:44329` post-subdomain-
   redirect). If `localhost:44327` (canonical API): the
   `Object.assign(environment, ...)` didn't propagate to ABP's
   providers -- STOP and investigate whether ABP is cloning the
   options object (would be a v3 confidence-claim failure analogous to
   the env-var-dots blunder from Task A).

5. **Restore canonical values** in `docker-compose.yml` after smoke
   passes.

### Verification gate

- Pass both steps 3 and 4 -> Task B commit proceeds.
- Fail step 3 -> entrypoint debugging; do not touch main.ts further
  until heredoc emits correct values.
- Fail step 4 (entrypoint OK, SPA still on canonical) -> the
  pre-bootstrap mutation pattern is the broken link. Either
  `provideAbpCore` clones the options object (unlikely, contradicts
  tenant-bootstrap evidence) or the fetch isn't completing before
  Angular's provider injector is built. Most likely culprit if it
  fails: misplaced `await` or `bootstrapApplication` called outside the
  async IIFE scope.

## 7. HIPAA / PHI impact

None. No PHI in `dynamic-env.json` (it's URL configuration only).
Synthetic ports + URLs throughout. Logging redacts identifiers where
applicable -- `console.warn` only logs HTTP status codes and error
objects, no user data.

## 8. Blast radius

- **SPA bootstrap path:** every page load goes through `main.ts`. A
  bug surfaces immediately on next reload.
- **Frontend-only:** no DB migration, no schema change, no setting-
  value seed. Backend behavior identical to pre-Task-B state.
- **Reversibility:** trivial. Revert the commit. No data cleanup.
  Containers pick up reverted `main.ts` on next ng-watch cycle.
- **Production:** unaffected; this is dev-stack-only (`Dockerfile.dev`
  + dev-entrypoint.sh). A production Angular Dockerfile, when it
  lands, must apply the same pattern OR bake URLs into its image at
  build time using build-args.

## 9. Dependencies

None added. `fetch` is a standard Web API. `Object.assign` is ES2015.
Bash heredoc + `${VAR:-default}` is POSIX.

## 10. Out of scope

- Dead-const cleanup in unrelated Angular code (not relevant to this
  fix).
- Production Angular Dockerfile (deferred; current scope is
  `Dockerfile.dev`).
- Service-worker compatibility (no service worker today; future PR
  must handle).
- OBS-9 reversal documentation (handled in Task F).
- `replicate-old-app` retrofit (Task D).

## 11. Files changed

| File | Type | Approx lines | Why |
|---|---|---:|---|
| `angular/src/main.ts` | edit | +25 | Async IIFE wrap; fetch+merge dynamic-env before tenant-bootstrap |
| `angular/dev-entrypoint.sh` | edit | +25/-1 | Add heredoc that writes `$ENV_SRC` from container env vars near top of script; keep existing concurrently structure + cp + ensure_dynamic_env loop |
| `docker-compose.yml` | edit | +4/-1 | Angular service `environment:` block (NG_PORT/AUTH_PORT/API_PORT); remove `docker/dynamic-env.json` bind-mount |
| `docker/dynamic-env.json` | delete | -27 | Replaced by entrypoint heredoc |
| `angular/dynamic-env.json` | unchanged | -- | `{}` placeholder stays; ng build's asset copy is harmless because entrypoint cp overwrites with real content |
| `angular/angular.json` | unchanged | -- | Asset entry stays for the placeholder (same rationale) |
| `docs/superpowers/specs/2026-05-20-task-b-runtime-spa-config.md` | new | +~250 | This spec |
| `docs/plans/2026-05-20-task-b-runtime-spa-config.md` | new | +~120 | Plan |
| **Total** | | **~340 lines / 5 files** | (incl. docs; 1 deletion + 3 edits + 2 new docs / minimal-corrected scope) |

## 12. Acceptance criteria

- [ ] `dotnet test` whole solution still green (no regressions vs Task
  A baseline -- expected since Task B is frontend-only).
- [ ] `docker compose config` parses without error.
- [ ] Smoke step 3 PASS (`dynamic-env.json` contains offset-port URLs).
- [ ] Smoke step 4 PASS (SPA's first backend XHR targets offset-port
  API).
- [ ] No debug `console.log` left in `main.ts` or `dev-entrypoint.sh`.
- [ ] Commit message follows `commit-format.md`:
  `feat(angular): runtime dynamic-env.json load via pre-bootstrap fetch`
- [ ] Task B commit lands on `feat/parallel-worktree-stacks`; not
  pushed.

## 13. Open questions

None after the 2026-05-20 brainstorming pass resolved fetch-failure
behavior (console.warn + silent fallback).

## 14. Confidence

HIGH on the pre-bootstrap mutation pattern (proven empirically by
`tenant-bootstrap.ts`). HIGH on the bash heredoc + env-var
substitution (standard POSIX). MEDIUM on the smoke test step 4 outcome
-- if it fails, the pattern's empirical evidence (tenant-bootstrap
working in production) becomes load-bearing; we'd need to verify
`provideAbpCore` doesn't clone the options object. The smoke catches
this if my confidence is wrong.

Confidence-calibration note: Task A's v3 confidence claim on the
Settings:* env-var mechanism was wrong because I extrapolated from a
JSON-source evidence base to env-var-source without verifying the
Docker layer. Task B's claim is grounded in directly-equivalent
empirical evidence (the same mutation pattern, on the same imported
object, in the same codebase). If it fails the smoke step 4, the
remediation is well-defined (switch to a different mutation surface;
see step 4 failure-mode notes).
