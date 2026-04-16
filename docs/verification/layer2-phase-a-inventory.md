# Layer 2 Phase A -- Baseline Inventory

**Captured:** 2026-04-16
**Source:** PR #62 CI run (run 24521574642) + SonarCloud dashboard + GitHub Security tab
**Branch:** All 4 branches aligned at `8cfda75`

## Summary

Phase A landed all Layer 2 checks as non-blocking (continue-on-error). This document captures
the baseline finding counts that Phase B must drive to zero before Phase C flips everything
to blocking.

**Surprise:** Format checks (both backend and frontend) are already green. The Prettier sweep
from PR #42 and dotnet format enforcement from Layer 1 hooks kept the codebase formatted.
This removes 2 items from the Phase B backlog.

## Baseline Findings

### Already Green (no Phase B work needed)

| Tool | Result | Notes |
|------|--------|-------|
| Backend: Format Check | 0 files with drift (415 checked) | dotnet format --verify-no-changes passes cleanly |
| Frontend: Format Check | 0 files with drift | Prettier format:check passes cleanly |
| Frontend: Lint (ESLint) | 0 warnings, 0 errors | "All files pass linting" |
| yamllint | 0 issues | All workflow YAML files clean |
| markdownlint | 0 issues | All markdown files clean |
| Commitlint | Pass | PR commits follow Conventional Commits |
| TruffleHog (secrets) | 0 findings | No secrets in PR commit range |
| Dependency Review | Pass | No critical vulnerabilities or denied licenses |

### Needs Phase B Work

| Tool | Finding Count | Severity Breakdown | Phase B PR |
|------|---------------|-------------------|------------|
| .NET warnings (CS*) | 428 | See breakdown below | #3 (TreatWarningsAsErrors) |
| NuGet vulnerability warnings (NU*) | 414 | 108 moderate, 270 high, 36 critical | #12 (Scriban bump) |
| CodeQL security alerts | 5 | 5 high (all same rule) | #6 |
| SonarCloud | TBD | Awaiting first main-branch scan | #5 |
| OpenSSF Scorecard | Failed to run | `ossf/scorecard-action@v2` unresolvable | #11 + workflow fix |
| Backend test coverage | Unknown (not collected) | No --collect in current CI | #8 |
| Frontend test coverage | 0% | No spec files exist | #8 |

### .NET Warning Breakdown (CS* codes)

| Code | Count | Category | Description |
|------|-------|----------|-------------|
| CS8604 | 192 | Nullable | Possible null reference argument |
| CS8602 | 98 | Nullable | Dereference of a possibly null reference |
| CS8618 | 78 | Nullable | Non-nullable property must contain non-null value |
| CS8601 | 30 | Nullable | Possible null reference assignment |
| CS8603 | 18 | Nullable | Possible null reference return |
| CS0105 | 10 | Code quality | Duplicate using directive |
| CS8073 | 2 | Code quality | Expression always true/false |
| **Total CS*** | **428** | | |

All nullable warnings (416/428 = 97%) come from the inherited codebase's lack of nullable
annotations. The remaining 12 are trivial code quality issues.

### NuGet Vulnerability Warnings (Scriban 6.3.0)

All 414 NU warnings come from a single transitive dependency: **Scriban 6.3.0** (used by ABP
framework's text templating). Every project that references ABP gets these warnings.

| Advisory | Severity | Count |
|----------|----------|-------|
| GHSA-5rpf-x9jg-8j5p | Moderate | 12 |
| GHSA-m2p3-hwv5-xpqw | Moderate | 12 |
| GHSA-xw6w-9jjh-p9cr | Moderate | 12 |
| GHSA-5wr9-m6jw-xx44 | Critical | 12 |
| GHSA-c875-h985-hvrc | High | 12 |
| GHSA-grr9-747v-xvcp | High | 12 |
| GHSA-p6q4-fgr8-vx4p | High | 12 |
| GHSA-v66j-x4hw-fv9g | High | 12 |
| GHSA-wgh7-7m3c-fx25 | High | 12 |
| GHSA-x6m9-38vm-2xhf | High | 12 |
| GHSA-xcx6-vp38-8hr5 | High | 12 |

**Fix:** Bump Scriban to >= 6.4.0. ABP 10.0.2 may pin the version; check if a
`<PackageVersion>` override in Directory.Build.props resolves it.

### CodeQL Security Alerts (5 findings)

All 5 alerts are the same rule: **Missing X-Frame-Options HTTP header** (high severity).

| File | Line |
|------|------|
| `src/.../HttpApi.Host/web.config` | 1 |
| `src/.../HttpApi.Host/bin/Release/net10.0/web.config` | 1 |
| `src/.../AuthServer/web.config` | 1 |
| `src/.../AuthServer/bin/Release/net10.0/web.config` | 1 |
| `angular/web.config` | 1 |

**Fix:** Add `<add name="X-Frame-Options" value="DENY" />` to each web.config's
`<customHeaders>` section. The `bin/` findings may also require a `.gitignore` exclusion
(build artifacts should not be in the repo).

**Note:** 2 of 5 findings are in `bin/Release/` directories -- build artifacts that should
not be committed. Adding `bin/` to `.gitignore` would eliminate those 2 findings and is
independently a good practice.

### OpenSSF Scorecard

The Scorecard workflow failed entirely: `ossf/scorecard-action@v2` could not be resolved.
This is a workflow bug, not a codebase finding.

**Fix:** Pin to a specific release tag (e.g., `ossf/scorecard-action@v2.4.1`) or use a
SHA pin. This is a Phase B item (#11).

### SonarCloud

The SonarCloud analysis ran successfully on PR #62 but the project dashboard data needs
to be captured from sonarcloud.io directly. The Quality Gate was not waited on
(`sonar.qualitygate.wait=false`).

**Action:** Visit the SonarCloud dashboard at
`sonarcloud.io/project/overview?id=gesco-healthcare-support_hcs-patient-portal`
and record: bugs, vulnerabilities, security hotspots, code smells, duplication %, coverage %,
and Quality Gate status. Update this section with those numbers.

### Test Coverage

| Stack | Coverage | Notes |
|-------|----------|-------|
| Backend (.NET) | Not collected | `--collect:"XPlat Code Coverage"` not in ci.yml yet |
| Frontend (Angular) | 0% | No `*.spec.ts` files exist; Karma errors on "no files" |

**Phase B #8** is the longest item: write tests for 13 untested entities to reach 70% coverage.

## Known Carry-Overs from Phase A

These items were identified during Phase A PR reviews and deferred to Phase B:

1. Scriban 6.3.0 CVEs (NU1902/NU1903/NU1904) -- bump to >= 6.4.0
2. `angular/karma.conf.js` missing `lcov` reporter -- SonarCloud reports 0% frontend coverage
   until Phase B #8 adds specs AND `{ type: 'lcovonly' }` to `coverageReporter.reporters`
3. `common.props` + `Directory.Build.props` dual source of truth for `LangVersion=latest` --
   consolidate during Phase B
4. `dependency-review.yml` uses step-level `continue-on-error` while everything else uses
   job-level -- normalize during Phase C flip
5. `dependency-review.yml` missing `timeout-minutes` -- add during Phase B
6. Root-level files (README.md etc.) fall through all 3 paths-filter rules, causing all ci.yml
   jobs to skip. `Lint: Markdown` still catches content but no build gates run. Decide if
   shared filter should include root-level docs.
7. `ossf/scorecard-action@v2` fails to resolve -- pin to specific version
8. `bin/Release/` directories committed to repo -- add to `.gitignore`
9. Node.js 20 deprecation warning in CI -- actions/cache@v4, actions/checkout@v4,
   actions/setup-dotnet@v4 need Node.js 24 compatible versions by September 2026

## Revised Phase B Backlog

Based on the inventory, the original 12-item Phase B list is revised:

| # | PR Title | Status | Notes |
|---|----------|--------|-------|
| ~~1~~ | ~~style: apply prettier to entire repo~~ | **NOT NEEDED** | Already clean (0 drift) |
| ~~2~~ | ~~style: apply dotnet format to entire solution~~ | **NOT NEEDED** | Already clean (0 drift) |
| 3 | fix: enable TreatWarningsAsErrors + resolve .NET warnings | **428 warnings** | 97% nullable, sub-PRs by category |
| 4 | ~~fix: resolve ESLint warnings across angular/~~ | **NOT NEEDED** | Already clean (0 warnings) |
| 5 | fix: address SonarCloud findings by severity | **TBD** | Awaiting dashboard data |
| 6 | fix: address CodeQL security findings | **5 findings** | All X-Frame-Options + bin/ cleanup |
| 7 | fix: license-compliance violations if any surface | **NONE** | Dependency review passes clean |
| 8 | chore: expand test coverage to >= 70% per entity | **13 entities** | Longest item |
| 9 | ~~chore: resolve yamllint warnings~~ | **NOT NEEDED** | Already clean |
| 10 | ~~chore: resolve markdownlint warnings~~ | **NOT NEEDED** | Already clean |
| 11 | chore: lift OpenSSF Scorecard score | **Workflow broken** | Fix action pin first |
| 12 | chore: bump Scriban >= 6.4.0 + consolidate build props | **414 NU warnings** | Single dependency fix |

**5 of 12 original items are already green.** Phase B reduces to 5 substantive items
(#3, #5, #6, #8, #12) plus #11 (Scorecard fix).

## Phase C Required Status Checks (for reference)

When Phase B drives all findings to zero, Phase C will make these checks required on
all 4 branches:

`Backend: Build`, `Backend: Format Check`, `Backend: Test`, `Frontend: Build`,
`Frontend: Format Check`, `Frontend: Lint`, `Frontend: Test`, `Docs: Structure Check`,
`SonarCloud: Analysis`, `CodeQL: csharp`, `CodeQL: javascript-typescript`,
`TruffleHog: PR commits`, `Commitlint: PR commits`, `PR Title: Conventional Commits`,
`Lint: YAML workflows`, `Lint: Markdown`, `dependency-review`
