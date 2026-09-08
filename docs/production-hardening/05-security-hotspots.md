# Phase 5 -- Security hotspots

**Change class:** review-and-decide. A hotspot is not a defect -- it is a location Sonar cannot
judge without knowing intent. The deliverable is a **decision with evidence** for each, and a code
change only where the review finds a real problem.

**31 hotspots, all TO_REVIEW.** None has ever been reviewed, which is why the count equals the
total.

| Probability | Category      | Count | Note                          |
| ----------- | ------------- | ----- | ----------------------------- |
| HIGH        | csrf          | 6     | Review first                  |
| HIGH        | auth          | 3     | Review first                  |
| MEDIUM      | dos           | 6     | Ties to the rate-limiting gap |
| MEDIUM      | permission    | 5     | Ties to deny-by-default       |
| LOW         | encrypt-data  | 6     |                               |
| LOW         | others        | 3     |                               |
| LOW         | insecure-conf | 2     |                               |

---

## 5.1 CSRF (6, HIGH) and auth (3, HIGH)

Review these together -- they are the same trust boundary.

**Context that matters.** One CSRF-adjacent decision is already known and deliberate: the Hangfire
dashboard is registered with `IgnoreAntiforgeryToken = true`. That is being handled separately by
the bug-fix session. If a hotspot points at it, cross-reference rather than duplicating the work.

**What to establish per site:** is the endpoint state-changing, is it reachable by a browser
carrying ambient credentials, and does ABP's antiforgery apply to it. ABP applies antiforgery
selectively depending on the controller style, and this codebase uses manual controllers
(ADR 002), so the framework default cannot be assumed.

**The structural fact that shapes the auth ones:** sign-in is served by the AuthServer, a separate
process. Its Razor pages call application services in-process, so API middleware never runs for
them. Any conclusion of the form "the API handles this" is wrong for the auth flows.

---

## 5.2 DoS (6, MEDIUM)

These will almost certainly resolve to the known rate-limiting gap rather than to individual code
defects.

**Established facts, already verified -- do not re-derive:**

- There is exactly one `AddRateLimiter` in the solution
  (`CaseEvaluationHttpApiHostModule.cs:628`). It covers password reset, the public document
  upload, the API registration endpoint, and the partner integration path.
- Every other business endpoint falls through to no limiter.
- **Sign-in is not covered at all**, and cannot be by that limiter, because it lives in the
  AuthServer process.
- `docker/nginx-proxy/default.conf.template` contains zero `limit_req` directives, so there is no
  edge throttling either.

So the honest review outcome for most of these is "accepted for LAN, must be closed before public
hosting", with the fix belonging to the edge design that the system-design research is meant to
produce. Record that reasoning rather than marking them safe.

---

## 5.3 Permission (5, MEDIUM)

Cross-reference phase 3.2. If critical-path authorization coverage lands first, these reviews get
much cheaper -- there will be tests demonstrating what the permission actually does.

Known constraint: inherited ABP identity app services cannot be re-gated, so some of these may be
"accepted, framework-imposed" with the compensating control named.

---

## 5.4 encrypt-data (6, LOW), others (3), insecure-conf (2)

**REVIEWED 2026-09-08. The SSN overlap this section anticipated does not exist** -- kept rather than
deleted so nobody goes looking for it. Not one `encrypt-data` hotspot points at SSN storage; all six
are `http://` URLs (three dev-fallback localhost URLs, one in-cluster sidecar default, two
`--urls http://+:8080` ENTRYPOINT lines behind the nginx TLS terminator). SSN-at-rest encryption
remains a separate deferred item and is untouched by this lane.

Per-site verdicts are recorded in
[#584](https://github.com/gesco-healthcare-support/hcs-patient-portal/issues/584): **10 of 11
safe-intentional, 1 real.**

### The one real finding, fixed here

`scripts/worktrees/add-worktree.sh:105` ran `yarn install --mutex network`. Two defects in one line:

- **`--mutex` is a Yarn 1 flag.** The repo moved to Yarn 4.16.0 on 2026-06-13 (#310) while the flag
  dates from 2026-04-23 (#116). Yarn 4 rejects unknown options (`Unsupported option name`) instead
  of ignoring them, and the script runs under `set -euo pipefail`, so **it aborted at this line**
  and never reached the port summary it ends with.
- **Lifecycle scripts (`shell:S6505`).** Note that `--ignore-scripts`, which #584 originally
  prescribed, **does not exist in Yarn 4** -- `yarn install` accepts only `--json`,
  `--immutable`, `--immutable-cache`, `--refresh-lockfile`, `--check-cache`, `--check-resolutions`,
  `--inline-builds` and `--mode`. The Yarn 4 equivalent is the `enableScripts` setting, overridden
  per invocation with `YARN_ENABLE_SCRIPTS=false`.

The precedent #584 cited does not extend here: `angular/Dockerfile:63`'s `--ignore-scripts` is on
`npm install -g serve@14.2.6`, an npm global tool install. The project's own dependency install at
`Dockerfile:23` is `yarn install --immutable`, with scripts enabled.

### The repo-wide setting is out of scope for this lane

`angular/.yarnrc.yml` carries `enableScripts: true`, which applies to CI, the Docker build,
`dev-entrypoint.sh` and every developer machine -- a much larger surface than one worktree helper.
Flipping it can break any package that needs a postinstall build, so it is tracked separately rather
than ridden in on a shell-script fix.

---

## Method

For each hotspot, record in the phase notes:

1. The file and line, and what the code actually does.
2. Whether it is reachable, by whom, and under what authentication.
3. The decision: **Safe** (with why), **Fixed** (with the commit), or **Accepted risk** (with the
   condition that would change it -- almost always "before public hosting").
4. Mark it in SonarCloud so the count reflects reality.

**Do not bulk-mark hotspots Safe to clear the number.** The count is the only signal anyone has
that a review happened; zeroing it without review destroys that signal permanently and is worse
than leaving them untouched.

---

## Validation loop

Mostly review, so the loop is the record itself. Where a fix lands, the normal backend or frontend
loop applies. Re-measure at the end:

```bash
curl -s "https://sonarcloud.io/api/hotspots/search?projectKey=gesco-healthcare-support_hcs-patient-portal&status=TO_REVIEW&ps=1" | head -c 200
```

Baseline: 31 TO_REVIEW. Target: 0 TO_REVIEW, every one carrying a recorded decision.
