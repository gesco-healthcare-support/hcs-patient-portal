# Phase 6 -- Dependency advisories

**Change class:** behaviour preservation with unknown failure modes. **Broad coverage FIRST --
phase 3 is a hard prerequisite.** This is the highest-regression-risk phase in the epic and the
direct reason phase 3 exists.

**97 open advisories, 96 with a patch available, all npm, zero NuGet.** The .NET side is clean.

> **Re-measured 2026-09-08.** The figures below replace an earlier count of 88 / 87-patched taken
> when this phase was written. Two corrections matter beyond the arithmetic: the advisories span
> **two manifests, not one**, and the lane scoping in this document was derived from a
> direct-versus-transitive split that was wrong in three places. Details in
> [10-research-corrections.md](10-research-corrections.md) and on the lane issues themselves.

| Severity | Count |
| -------- | ----- |
| Critical | 1     |
| High     | 50    |
| Medium   | 39    |
| Low      | 7     |

**Two manifests carry them:**

| Manifest                                                    | Advisories | Owned by                |
| ----------------------------------------------------------- | ---------- | ----------------------- |
| `angular/yarn.lock`                                         | 88         | lanes 6.1 / 6.2 / 6.3   |
| `src/HealthcareSupport.CaseEvaluation.AuthServer/yarn.lock` | 9          | **no lane** -- see #699 |

The AuthServer manifest was absent from this document entirely. It is a real manifest with its own
`package.json` (ABP LeptonX theme + `@volo/account`), and its 9 advisories -- 7 high, 2 medium --
were outside the programme's scope rather than miscounted.

Most-affected packages: `hono` (19), `undici` (7), `fast-uri` (7), `brace-expansion` (7), `tar` (6),
`@angular/common` (5), `nanoid` (4), `@angular/core` (4), `qs` (3), `js-yaml` (3),
`@angular/compiler` (3), `minimatch` (3).

**Only four packages are direct dependencies of `angular/package.json`:** `@angular/common`,
`@angular/core`, `@angular/compiler` and `quill`. Everything else -- `hono`, `undici`, `fast-uri`,
`tar`, `vite`, all of it -- is transitive. That fact is what re-scopes the three lanes below.

---

## Why this is riskier than it looks

There is no targeted test for "this bump broke nothing". A dependency upgrade changes behaviour you
did not write, in paths you did not choose, and the failure is usually silent -- a changed default,
a stricter parser, a dropped polyfill. The only instrument that detects it is a suite broad enough
to exercise the affected paths.

Frontend coverage is the weaker half of this codebase. Running 96 bumps against it without phase 3
is precisely the scenario Adrian named: _"I don't want the app to regress or cause new issues
because we did not have enough tests to guard against that."_

---

## Sequencing within the phase

Do **not** do one bulk bump. Group by risk and land each group separately so a regression is
attributable.

### 6.1 Transitive-only, no direct import (lowest risk) -- issue #585

**75 advisories: 1 critical, 35 high, 33 medium, 6 low.** Everything except the four direct
packages. The suspicion recorded here originally -- that these are build tooling rather than
application imports -- held for all of them.

**The `hono` question is answered.** It resolves through a single parent chain:

```text
@angular/cli@~20.3.24  ->  @modelcontextprotocol/sdk@1.26.0  ->  hono + @hono/node-server
```

Angular 20's CLI ships an MCP server for AI-assisted development. So 20 of the 75 come from
build-time tooling that is never bundled and never shipped, and they clear by bumping one
**devDependency**. That makes it the highest-leverage and lowest-risk action in the whole phase --
do it first, ahead of everything else in this document.

The critical advisory is `tar` GHSA-23hp-3jrh-7fpw (parse DoS), also build-chain: this repo's `tar`
is used by package tooling against a lockfile it already trusts. Severity here reflects the
upstream advisory, not reachability in this application, so **do not let the critical badge reorder
the phase.**

### 6.2 Direct runtime dependency -- `quill` (1, unpatched) -- issue #586

**Rescoped 2026-09-08.** This lane's original named member was `undici`, which is wrong twice over:
it is transitive rather than direct, and it is a Node HTTP client that cannot execute in a browser
SPA. Measured -- there is no server-side rendering path at all: no `@angular/ssr`, no
`platform-server`, no `server.ts` under `angular/`, and no `ssr` or `prerender` key in
`angular.json`. `undici` is unreachable and belongs in 6.1.

The lane's true member is **`quill`**, pinned at `2.0.3` in `dependencies` and genuinely imported
by `internal-admin-hub.component.ts`. It is the **one advisory of 97 with no patch available**:

```text
GHSA-v3m3-f69x-jf25   low   XSS via HTML export feature
vulnerable range: = 2.0.3     first_patched_version: null
```

So this lane cannot close on a version bump. It needs a written decision -- accept with
justification, sanitise the export output through the existing `HtmlSanitizer`, remove the editor,
or replace it.

### 6.3 Angular framework packages (highest risk) -- issue #587

**12 advisories across three packages, not 9 across two.** `@angular/compiler` (3) was missing from
this document; the full set is `@angular/common` (5), `@angular/core` (4), `@angular/compiler` (3)
-- 8 high, 4 medium -- all pinned to the same `~20.3.19`, so they move as one unit rather than
three serial bumps.

These are point upgrades **within v20** -- do not let this phase turn into a major-version
migration. A major upgrade is a separate epic with its own plan; it is explicitly out of scope here.

Land these last and alone, with the full frontend suite behind them. This lane is **held pending
phase 3**, and the hold is deliberate: it is the only lane whose packages compile into the shipped
browser bundle.

---

## Method

- One group per branch, per PR. Never all 96 in one diff -- an attributable regression is worth far
  more than a tidy changelog.
- After each group: full frontend suite plus `ng build`, and re-check the advisory count.
- Where a bump has no patch (1 of 97 -- `quill`, see 6.2), record the decision and the compensating
  control, if any, on the lane issue rather than in a triage log.
- **Watch for lockfile churn.** A known artefact already exists:
  `test/...ConsoleTestApp/packages.lock.json` drifts on restore (logged in `docs/backlog.md`).
  Resolve that separately so it does not muddy these diffs.

---

## Validation loop

```bash
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
```

Plus the backend loop if anything touches the .NET side (it should not -- zero NuGet advisories).

Re-measure after each group. Query only open alerts and break them down **by manifest**, because a
single total is what hid the AuthServer set in the first place:

```bash
gh api "repos/gesco-healthcare-support/hcs-patient-portal/dependabot/alerts?state=open&per_page=100" \
  --paginate --jq '.[] | .dependency.manifest_path' | sort | uniq -c
```

Baseline 2026-09-08: **88 `angular/yarn.lock` + 9 AuthServer = 97.**

Two traps when re-measuring, both hit while producing these figures:

- Patch availability lives under `security_vulnerability.first_patched_version`, **not**
  `security_advisory.first_patched_version`. Reading the latter returns a well-formed, confident
  "0 of 97 patched". The correct answer is 96.
- Prefix the whole `gh api` call with `MSYS_NO_PATHCONV=1` in Git Bash, and pipe JSON via stdin
  rather than through a temp file -- a POSIX path handed between two Windows binaries resolves to
  two different locations.

**Done bar for the phase:** advisory count materially reduced AND the full frontend suite green AND
the app manually exercised on the critical paths from phase 3. A green unit suite after a
dependency bump is necessary but not sufficient -- run the app.
