[Home](../INDEX.md) > [Testing](./) > Coverage Status

# Backend Test Coverage Status

> Purpose: name the commands that report test counts and coverage, so every figure is read from a
> run rather than from this page. Audience: developers.

## This page stores no numbers, deliberately

It used to. It claimed **115 backend test methods across 17 files**, verified 2026-04-24, and
called itself "the single source of truth for backend test suite counts". By 2026-09-11 the
real figure was roughly twenty times that, and nothing in the file could notice. It is linked from
`docs/INDEX.md`, so the page that existed to stop drifted counts had itself become the drifted
count a new developer would read first.

That is not a maintenance lapse to apologise for and patch. **A stored count begins rotting the
day it lands**, because the suite keeps changing and the page does not. Replacing the stale
number with a fresh one only restarts the same clock.

So the figures are gone, and the commands that produce them are below.

**If you need a count in any document, paste the command that produced it beside it.** Do not
re-add standing figures here.

## Backend

Count and result, from the repository root:

```bash
dotnet test HealthcareSupport.CaseEvaluation.slnx
```

Read the totals off the run summary.

With a coverage report, the same collection CI performs in the `Backend: Test` job of
`.github/workflows/ci.yml`:

```bash
dotnet tool install --global dotnet-coverage --version 18.11.0

dotnet-coverage collect \
  'dotnet test HealthcareSupport.CaseEvaluation.slnx --no-restore -c Release' \
  -f cobertura -o coverage.cobertura.xml
```

CI additionally passes `--logger "trx;LogFileName=results.trx"` and `--results-directory
./test-results` inside the quoted inner command; both exist only so the run can be uploaded as an
artefact and neither changes the measurement.

Cobertura is the format `scripts/coverage-gate.py` parses. SonarCloud is fed a _different_ format
from a _different_ workflow -- `sonarcloud.yml` collects `-f xml` for
`sonar.cs.vscoveragexml.reportsPaths` -- so the two artefacts are not interchangeable.

## Frontend

From `angular/`:

```bash
yarn test
```

With coverage, as CI runs it:

```bash
yarn test --watch=false --browsers=ChromeHeadless --code-coverage
```

`--browsers=ChromeHeadless` is load-bearing rather than decorative: `karma.conf.js` defaults to
`browsers: ['Chrome']`, so without it the run tries to open a real browser.

## The project-wide percentage

The coverage figure worth quoting comes from SonarCloud rather than any local run:

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=gesco-healthcare-support_hcs-patient-portal&metricKeys=coverage,uncovered_lines,lines_to_cover"
```

Read all three metrics, never the headline alone. Branch coverage in this repository is well under
half its line coverage, so the two move at very different rates.

## Related

- [docs/devops/TESTING-STRATEGY.md](../devops/TESTING-STRATEGY.md) -- structure of the test projects.
- [docs/devops/CI-TESTS-AND-CHECKS.md](../devops/CI-TESTS-AND-CHECKS.md) -- which workflow runs which suite.

`docs/issues/TEST-EVIDENCE.md` used to be listed here as the frozen E2E baseline. It was deleted in
`83f75e82` (2026-05-05), so the reference is dropped rather than carried forward. Worth recording
that `check-links.py` never flagged it: the entry was bare text rather than a markdown link, so the
checker structurally could not see it. Existence was confirmed with `git ls-tree` instead.

The per-entity rollup, the E2E harness totals and the 2026-04 history were removed along with the
counts, for the reason above. `git log --follow` on this file still has every word of them.
