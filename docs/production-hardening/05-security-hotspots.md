# Phase 5 -- Security hotspots

**Change class:** review-and-decide. A hotspot is not a defect -- it is a location Sonar cannot
judge without knowing intent. The deliverable is a **decision with evidence** for each, and a code
change only where the review finds a real problem.

## COMPLETE as of 2026-09-08 -- 0 TO_REVIEW, 53 REVIEWED/SAFE

Every hotspot in this phase now carries a recorded decision. Re-measure before quoting:

```bash
curl -s "https://sonarcloud.io/api/hotspots/search?projectKey=gesco-healthcare-support_hcs-patient-portal&status=TO_REVIEW&ps=1" | head -c 200
```

| Category      | Opened with | Outcome                                                        | Lane       |
| ------------- | ----------- | -------------------------------------------------------------- | ---------- |
| csrf          | 6           | 6 Safe -- JWT-bearer only, no cookie scheme; 3 are anonymous   | 5.1 (#581) |
| auth          | 3           | 3 Safe -- dev-stage ARG, key never reaches a shipped image     | 5.1 (#581) |
| dos           | 6           | 6 **fixed in code** -- match timeouts, PR #722 (`770e4478`)    | 5.2 (#582) |
| permission    | 1           | 1 Safe -- dev stage; **see #701**, all five images run as root | 5.3 (#583) |
| encrypt-data  | 6           | 6 Safe -- dev URLs, in-cluster sidecar, TLS at nginx           | 5.4 (#584) |
| others        | 3           | 2 Safe (cookie deletion) + 1 **fixed** -- PR #724 (`0a8a9917`) | 5.4 (#584) |
| insecure-conf | 2           | 2 Safe -- cookie deletion, not creation                        | 5.4 (#584) |

**This file opened claiming 31, and the real number was 27** -- kept here rather than silently
corrected. The entire 4-hotspot gap is the `permission` row, recorded as 5 and measured as 1; the
other six rows were exact. (#583's title carried the same stale 5 and was corrected to 1 on
2026-09-08. Whether the 5 was wrong when written or 4 were resolved earlier by unrelated work was
not established, so no cause is claimed here.)

The 7 code-fixed hotspots do not appear as REVIEWED at all -- they stopped matching their rules,
so nobody had to attest to them. 53 REVIEWED/SAFE = 33 resolved before this phase + 20 marked
during it.

**Three open issues sit adjacent to findings marked Safe and are NOT closed by this phase:**
**#701** (no Dockerfile sets `USER`), **#702** (no per-token throttle on the public endpoints --
the real risk on those, not CSRF), **#703** (move the ABP NuGet key to a BuildKit secret). One
marking is conditional: `CaseEvaluationDomainModule.cs:119` is safe under single-host topology
only, because that leg carries packet HTML (PHI) unencrypted on the docker network.

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

**RESOLVED 2026-09-08. The prediction below was wrong -- recorded rather than deleted so nobody
re-derives it.** These are not rate-limiting findings at all. All six are `csharpsquid:S6444`
(_"Pass a timeout to limit the execution time"_) on `new Regex(...)` constructions. S6444 is a
blanket rule that fires on every timeout-less constructor regardless of the pattern, so the count
measured nothing about exposure.

Adjudicated per site in [#582](https://github.com/gesco-healthcare-support/hcs-patient-portal/issues/582):
not one of the six contains a nested quantifier, and the only attacker-reachable pattern
(`TenantNaming.SlugPattern`, fed by a request subdomain) is additionally bounded by the
`MaxSlugLength = 63` check that runs before it. All six were resolved by passing
`TimeSpan.FromSeconds(1)`, matching the existing precedent at `LocationManager.cs:33` -- satisfying
the rule uniformly rather than re-arguing it each time a `new Regex` is added.

The rate-limiting gap is real and still open, but it belongs to the edge design, not here:

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

## 5.3 Permission (1, MEDIUM)

**REVIEWED 2026-09-08 -- and the count was 1, not the 5 this heading carried.** Issue #583 was
retitled `5 -> 1`; this heading had not been. The single hotspot is `angular/Dockerfile:52`, which
is `FROM base AS dev` -- the development stage. Production is the digest-pinned nginx stage at
`:37`, so the flagged stage is never deployed. Marked Safe.

**That marking is not a statement that containers run as non-root.** They do not: no Dockerfile in
this repository sets `USER`, so all five images -- production stages included -- run as root. The
scanner under-reports it and flags only this dev stage. Tracked as **#701**.

The anticipated angle below did not apply -- no permission hotspot pointed at an app service:

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

**MET 2026-09-08: 0 TO_REVIEW, 53 REVIEWED/SAFE.** This phase's **27** hotspots were adjudicated at
source and dispositioned per site in #581-#584: **20** marked through the SonarCloud UI by Adrian
across two batches, and **7** resolved by code so they no longer match their rules. All four lanes
are closed. The marking is the sign-off, which is why it stayed on his account rather than being
automated behind a token.

**Phase 5 only. Phases 4 and 6 are NOT closed by this, and neither is a SonarCloud phase.**

- **Phase 4** (`04-codeql-sensitive-info.md`, #578-#580) -- 19 CodeQL alerts. Every one is
  **adjudicated** at source, with per-alert verdicts in the issue bodies: 18 safe, **1 real**. Do
  not re-read them. What remains is acting on that record -- dismissing the 18 in GitHub code
  scanning, and fixing the real one: a **full email address reaching a Warning log** at
  `CaseEvaluationAccountEmailer.cs:205`, which breaches the repo's own redaction rule. **Open.**
- **Phase 6** (`06-dependencies.md`, #585-#587) -- 97 Dependabot advisories. **Open**, and only
  scoped, not adjudicated.

Adjudicated is not resolved. A hotspot count at zero says nothing about either phase.
