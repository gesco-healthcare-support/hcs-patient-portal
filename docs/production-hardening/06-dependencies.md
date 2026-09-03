# Phase 6 -- Dependency advisories

**Change class:** behaviour preservation with unknown failure modes. **Broad coverage FIRST --
phase 3 is a hard prerequisite.** This is the highest-regression-risk phase in the epic and the
direct reason phase 3 exists.

**88 open advisories, 87 with a patch available, all npm, zero NuGet.** The .NET side is clean.

| Severity | Count |
| -------- | ----- |
| Critical | 1     |
| High     | 43    |
| Medium   | 37    |
| Low      | 7     |

Most-affected packages: `hono` (19), `undici` (7), `brace-expansion` (7), `tar` (6), `fast-uri` (5),
`@angular/common` (5), `@angular/core` (4), `js-yaml` (3).

---

## Why this is riskier than it looks

There is no targeted test for "this bump broke nothing". A dependency upgrade changes behaviour you
did not write, in paths you did not choose, and the failure is usually silent -- a changed default,
a stricter parser, a dropped polyfill. The only instrument that detects it is a suite broad enough
to exercise the affected paths.

Frontend coverage is the weaker half of this codebase. Running 87 bumps against it without phase 3
is precisely the scenario Adrian named: _"I don't want the app to regress or cause new issues
because we did not have enough tests to guard against that."_

---

## Sequencing within the phase

Do **not** do one bulk bump. Group by risk and land each group separately so a regression is
attributable.

### 6.1 Transitive-only, no direct import (lowest risk)

`brace-expansion`, `fast-uri`, `tar`, `js-yaml`, most of `hono`. These are almost certainly pulled
in by build tooling rather than imported by application code.

**Check before assuming:** confirm nothing in `angular/src` imports them directly. If they are
build-time only, a resolution bump is low risk and the existing suite plus a successful `ng build`
is adequate proof.

`hono` at 19 advisories is worth a moment's thought -- it is a server framework and it is unlikely
to be a genuine runtime dependency of an Angular SPA. Establish what pulls it in. If it arrives via
a dev-only toolchain package, the advisories may be technically real but practically unreachable,
which is a triage-log entry rather than an upgrade.

### 6.2 Runtime libraries used by application code

`undici` (7) is an HTTP client and may be reachable at runtime. Bump, then exercise the paths that
make outbound calls.

### 6.3 Angular framework packages (highest risk)

`@angular/common` (5) and `@angular/core` (4). These are point upgrades **within v20** -- do not
let this phase turn into a major-version migration. A major upgrade is a separate epic with its own
plan; it is explicitly out of scope here.

Land these last and alone, with the full frontend suite behind them.

---

## Method

- One group per branch, per PR. Never all 87 in one diff -- an attributable regression is worth far
  more than a tidy changelog.
- After each group: full frontend suite plus `ng build`, and re-check the advisory count.
- Where a bump has no patch (1 of 88), record it in the triage log with the reason and the
  compensating control, if any.
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

Re-measure after each group:

```bash
gh api repos/gesco-healthcare-support/hcs-patient-portal/dependabot/alerts --paginate \
  -q '.[] | select(.state=="open") | .number' | wc -l
```

Baseline: 88.

**Done bar for the phase:** advisory count materially reduced AND the full frontend suite green AND
the app manually exercised on the critical paths from phase 3. A green unit suite after a
dependency bump is necessary but not sufficient -- run the app.
