# CI, tests and checks: what actually exists today

> Factual baseline of every automated check in this repository -- local hooks, CI workflows,
> the merge gate, the test suites, static analysis, and the dependency pipeline.
>
> **This document describes what is, not what should be.** It makes no recommendations. It exists
> to be verified against an industry standard for a public-facing healthcare scheduling
> application, which is a separate exercise.

| Field      | Value                                                                                                                                                                                                                  |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Verified   | 2026-08-26, against `origin/main` `bc4f2029`                                                                                                                                                                           |
| Method     | Direct file reads of `.github/workflows/`, `.husky/`, test projects and config; `gh api` for branch protection and Dependabot; SonarCloud public API                                                                   |
| Supersedes | Nothing. `docs/devops/TESTING-STRATEGY.md` self-reports "last verified 2026-06-01" and describes a test layout that no longer matches the code (it still documents `BookAppService_Tests` and the ABP "Books" sample). |

---

## 1. At a glance

| Layer              | What runs                                              | Can it block?                               |
| ------------------ | ------------------------------------------------------ | ------------------------------------------- |
| Local pre-commit   | gitleaks, lint-staged, `dotnet format` on staged `.cs` | Yes, locally; bypassable with `--no-verify` |
| Local commit-msg   | commitlint (Conventional Commits)                      | Yes, locally                                |
| Local pre-push     | gitleaks full scan, backend Debug build                | Yes, locally                                |
| CI on pull request | 12 workflows                                           | **Only 2 checks are required**              |
| CI post-merge      | 3 workflows                                            | No                                          |
| Scheduled          | 2 workflows (weekly)                                   | No                                          |

**The single most consequential fact in this document: of everything below, only `Backend: Build`
and `Frontend: Build` can prevent a merge to `main`.** Every test, lint, scan and analysis runs
and reports, and none of them gates.

---

## 2. Layer 1 -- local git hooks

Husky, installed from `angular/.husky/`, wired by `yarn prepare` (`cd .. && husky angular/.husky`).

### `pre-commit`

1. **Secret scan** -- `gitleaks protect --staged`. **Degrades silently:** if `gitleaks` is not on
   `PATH` the hook prints a warning and continues. The gate is therefore only as present as the
   developer's local tooling.
2. **Angular lint + format** -- `lint-staged` on staged files, only if `angular/node_modules`
   exists.
3. **C# filename guard** -- rejects `.cs` filenames containing spaces (they break `dotnet format`
   argument passing).
4. **C# format** -- `dotnet format --verify-no-changes` scoped to staged `.cs` files.

### `commit-msg`

`commitlint --edit`, against `angular/commitlint.config.js`. Handles the worktree path difference
(`$1` is relative in a normal checkout, absolute in a linked worktree) and WSL path conversion.

### `pre-push`

1. `gitleaks detect` across the whole working tree (again, skipped with a warning if absent).
2. `dotnet build -c Debug` of the full solution.

**Note:** the pre-push hook builds but does **not** run tests.

---

## 3. Layer 2 -- CI workflows

17 workflow files. Grouped by trigger.

### 3.1 On pull request (12)

| Workflow                | Job(s)                                                                                                                                    | `continue-on-error`                                 | Notes                                                        |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- | ------------------------------------------------------------ |
| `ci.yml`                | Changed paths, Backend Build, Backend Test, Backend Format, Frontend Build, Frontend Lint, Frontend Test, Frontend Format, Docs Structure | Format checks and the `-warnaserror` build: **yes** | The main pipeline. Path-filtered via `dorny/paths-filter@v4` |
| `sonarcloud.yml`        | SonarCloud Analysis                                                                                                                       | **yes**                                             | Also sets `sonar.qualitygate.wait=false`                     |
| `codeql-pr.yml`         | CodeQL csharp + javascript-typescript                                                                                                     | **yes**                                             | Uses `queries: security-extended` (broader than default)     |
| `trufflehog-pr.yml`     | Secret scan of the PR commit range                                                                                                        | **yes**                                             | `--only-verified`                                            |
| `dependency-review.yml` | Dependency Review                                                                                                                         | **yes**                                             | `fail-on-severity: critical` only                            |
| `commitlint.yml`        | PR commit messages                                                                                                                        | **yes**                                             | Catches commits that bypassed the local hook                 |
| `lint-meta.yml`         | yamllint, markdownlint                                                                                                                    | **yes** (both)                                      | Comment says "Phase C flips them to blocking"                |
| `pr-title.yml`          | PR title format                                                                                                                           | --                                                  |                                                              |
| `pr-size.yml`           | PR size label                                                                                                                             | --                                                  |                                                              |
| `labeler.yml`           | Path-based labels                                                                                                                         | --                                                  | `pull_request_target`                                        |
| `doc-check.yml`         | **Nothing**                                                                                                                               | --                                                  | See below                                                    |
| `dependency-review.yml` | (listed above)                                                                                                                            |                                                     |                                                              |

**`doc-check.yml` is a no-op that reports success.** Its entire job body is commented out; the
only live step is `run: echo "Doc check workflow ready. Uncomment steps when ANTHROPIC_API_KEY is
configured."` It appears in the PR checks list as a green tick and verifies nothing.

### 3.2 Post-merge (3)

| Workflow              | Trigger               | What it does                                                                                         |
| --------------------- | --------------------- | ---------------------------------------------------------------------------------------------------- |
| `auto-pr-dev.yml`     | push to `main`        | Opens the `main -> development` cascade PR. Requires `AUTO_PR_TOKEN`                                 |
| `deploy-dev.yml`      | push to `development` | **Does not deploy.** Runs `dotnet build` + `dotnet test`, then opens the `development -> staging` PR |
| `promote-staging.yml` | push to `staging`     | `dotnet build` + `dotnet test`. Notes that staging -> production PRs are always manual               |
| `release.yml`         | push to `production`  | `npx semantic-release`                                                                               |

`deploy-dev.yml` and `promote-staging.yml` are the only places `dotnet test` runs as a
post-merge gate. Neither has run meaningfully since `staging` and `production` stopped moving on
2026-05-01.

### 3.3 Scheduled (2)

| Workflow        | Schedule                           | Jobs                                                                 |
| --------------- | ---------------------------------- | -------------------------------------------------------------------- |
| `security.yml`  | Mondays 06:00 UTC                  | .NET vulnerability audit, npm audit, TruffleHog full history, CodeQL |
| `scorecard.yml` | Mondays 07:00 UTC + push to `main` | OpenSSF Scorecard, uploads SARIF                                     |

---

## 4. The merge gate

`gh api repos/.../branches/main/protection`:

```text
strict (branch must be up to date): true
REQUIRED CHECKS (2):
   - Backend: Build
   - Frontend: Build
required_approving_review_count: 1
enforce_admins: false
required_linear_history: false
allow_force_pushes: false
required_conversation_resolution: false
```

Consequences, stated plainly:

- **A failing test suite does not block a merge.** `Backend: Test` failed on the cascade PRs of
  2026-08-23 and 2026-08-25 and both merged.
- `enforce_admins: false` plus a single-maintainer repository means the one required review is
  routinely satisfied by admin merge.
- Lint, format, SonarCloud, CodeQL, TruffleHog and dependency review are advisory.

**Net: only a compile failure can stop a change reaching `main`.**

---

## 5. Tests

### 5.1 Backend

Five projects under `test/`.

| Project                         | Files   | `[Fact]`/`[Theory]` | Executed  | Result                                |
| ------------------------------- | ------- | ------------------- | --------- | ------------------------------------- |
| `Application.Tests`             | 124     | 1,058               | 1,106     | all pass                              |
| `Domain.Tests`                  | 89      | 549                 | 667       | 663 pass, 4 skipped                   |
| `EntityFrameworkCore.Tests`     | 82      | 133                 | 488       | 475 pass, 12 skipped, **1 failing**   |
| `TestBase`                      | 25      | 0                   | --        | shared infrastructure                 |
| `HttpApi.Client.ConsoleTestApp` | --      | --                  | --        | console harness, not in the CI run    |
| **Total**                       | **271** | **1,740**           | **2,261** | **2,244 pass, 16 skipped, 1 failing** |

Attribute count and executed count differ because `[Theory]` expands per data row.

**Every backend test runs against SQLite in-memory, not SQL Server.**
`CaseEvaluationEntityFrameworkCoreTestModule.cs:123` opens
`Data Source=:memory:;Foreign Keys=True`, and the multi-office harness does the same
(`CaseEvaluationMultiOfficeTestModule.cs:122`). The production database is SQL Server. Behaviours
that differ between the two -- filtered unique indexes, collation, `datetime2` semantics,
concurrency tokens, computed columns -- are therefore not exercised by any test.

### 5.2 The multi-office (tenant isolation) suite

20 files under `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/`,
including `MultiOfficeIsolationMatrixTests.cs`, `MultiOfficeAppointmentsAppServiceTests.cs`,
`MultiOfficeCatalogResolutionTests.cs`, `MultiOfficeConsentTokenResolutionTests.cs`,
`MultiOfficeImpersonationRoleTests.cs` and a self-validating harness
(`MultiOfficeHarnessSelfValidationTests.cs`).

Across the whole `test/` tree: 9 files reference tenant isolation, 10 reference cross-tenant
behaviour, 14 reference authorization, 16 reference permissions.

For a database-per-tenant system holding PHI this is the highest-consequence area, and it is the
best-covered area in the suite.

### 5.3 Frontend

- 66 `*.spec.ts` files, 206 `describe` blocks, **581 specs, all passing** on Chrome Headless.
- Run via `yarn test --watch=false --browsers=ChromeHeadless --code-coverage`.
- Coverage is collected and then **discarded**: `sonarcloud.yml:98` lists `angular/src/**/*.ts`
  and `angular/src/**/*.html` under `sonar.coverage.exclusions`. There is no other coverage
  publisher. **The frontend has no coverage figure anywhere.**
- Spec concentration is in `appointments/` (9 in `appointment/components`, 7 in
  `appointments/shared`, 3 each in `availability-calendar` and `appointment-documents`) and
  `shared/`.

**`ci.yml:248` still contains a guard that skips the test step entirely if no `*.spec.ts` file is
found**, emitting a warning instead of failing. With 66 spec files the branch is dead, but it
would silently green a pull request that deleted every spec.

---

## 6. Static analysis and quality gates

### 6.1 SonarCloud

Project `gesco-healthcare-support_hcs-patient-portal`. Runs per-PR and on push to `main`.
`continue-on-error: true`, `sonar.qualitygate.wait=false`, not a required check.

Current state: **quality gate ERROR**.

| Metric            | Value                                                                                                                                                          |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Coverage          | 52.2% (backend only)                                                                                                                                           |
| Bugs              | 338 -- of which **330 are HTML accessibility rules** (`Web:InputWithoutLabelCheck` 253, `Web:MouseEventWithoutKeyboardEquivalentCheck` 77); 3 C#, 5 TypeScript |
| Vulnerabilities   | 20 (5 BLOCKER, 14 MAJOR, 1 MINOR)                                                                                                                              |
| Security hotspots | 31 TO_REVIEW (6 csrf HIGH, 3 auth HIGH, 6 dos MEDIUM, 5 permission MEDIUM, 6 encrypt-data LOW, 2 insecure-conf LOW, 3 other LOW)                               |
| Code smells       | 920                                                                                                                                                            |
| Technical debt    | 3,346 minutes (~56 hours)                                                                                                                                      |
| Duplication       | 3.0%                                                                                                                                                           |
| `ncloc`           | 115,709                                                                                                                                                        |

Gate conditions failing: `new_reliability_rating` (D), `new_security_rating` (E), `new_coverage`
(51.9% against an 80% threshold), `new_security_hotspots_reviewed` (35.4% against 100%). The
"new code" period is `previous_version` dated **2026-04-15**, so "new code" is effectively the
whole project.

The scanner configuration carries 19 `sonar.issue.ignore.multicriteria` suppressions, mostly for
ABP framework patterns (dependency-injection parameter counts, permission-string duplication,
email-template HTML rules).

The 253 unlabelled inputs are concentrated: 47 in `internal-appointment-detail.component.html`,
26 in `people-edit-modal.component.html`, 17 in `patient-profile-redesign.component.html`, 13 in
`appointment-add-claim-parties-section.component.html`, then a long tail. Ten files hold roughly
70 percent of them.

### 6.2 CodeQL

Two runs: per-PR (`codeql-pr.yml`, matrix `csharp` + `javascript-typescript`,
`queries: security-extended`) and weekly inside `security.yml`. Both `continue-on-error`.

23 open alerts, all C#, none JavaScript/TypeScript:

| Severity | Rule                                            | Count | Location                                                                                                                                                 |
| -------- | ----------------------------------------------- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| high     | `cs/cleartext-storage-of-sensitive-information` | 3     | `CaseEvaluationAccountEmailer.cs` (2), `NotificationDispatcher.cs` (1)                                                                                   |
| medium   | `cs/exposure-of-sensitive-information`          | 18    | seed contributors (13), `ExternalAccountAppService.cs` (2), `CaseEvaluationAccountEmailer.cs` (2), `AppointmentChangeRequestsAppService.Approval.cs` (1) |
| medium   | `cs/log-forging`                                | 2     | `ExternalAccountAppService.cs`                                                                                                                           |

### 6.3 OpenSSF Scorecard

109 open alerts, dominated by `PinnedDependenciesID` (68 -- GitHub Actions referenced by tag
rather than commit SHA), plus `TokenPermissionsID` (4, high), `VulnerabilitiesID`, `CodeReviewID`,
`SASTID`, `FuzzingID`, `CIIBestPracticesID`.

### 6.4 Secret scanning

Three layers: `gitleaks` locally (pre-commit staged, pre-push full), TruffleHog per-PR
(`--only-verified`, PR commit range), TruffleHog weekly (full history, no `--only-verified`).
`.gitleaks.toml` present. No verified leak has been found.

---

## 7. Lint, format and compiler enforcement

### Backend

`Directory.Build.props` applies repo-wide:

```text
LangVersion            latest
Nullable               enable
ImplicitUsings         enable
AnalysisLevel          latest
TreatWarningsAsErrors  true
EnforceCodeStyleInBuild false
RestorePackagesWithLockFile true   (RestoreLockedMode false)
NoWarn                 CS1591; NU1510
```

`TreatWarningsAsErrors=true` is the strongest single quality control in the repository. Note
`EnforceCodeStyleInBuild=false`, so IDE style rules do not fail the build; `dotnet format`
covers that separately and is `continue-on-error` in CI.

The separate informational `-warnaserror` step in `ci.yml:100-106` is now redundant, since
`Directory.Build.props` already sets it.

### Frontend

`angular/.eslintrc.json` (legacy `.eslintrc` format, not flat config). It extends
`@angular-eslint/recommended`, `@angular-eslint/template/process-inline-templates` and
`@angular-eslint/template/recommended`, plus `prettier`.

It defines exactly three rules of its own:

- `@angular-eslint/directive-selector` and `@angular-eslint/component-selector` (naming).
- One custom `no-restricted-syntax` rule banning Angular's built-in `date` pipe in templates, in
  favour of the project's `pacificDate` / `calendarDate` pipes, with a detailed rationale about
  timezone correctness.

**`@angular-eslint/template/accessibility` is not extended.** That ruleset is where the
label-for-control, keyboard-event and ARIA rules live. Its absence is why 253 unlabelled inputs
exist in the codebase without any lint failure.

Formatting is Prettier (`yarn format:check`), `continue-on-error` in CI, enforced locally by
`lint-staged`.

---

## 8. The dependency pipeline

This is the most misleading area of the repository, and the mechanism is worth stating precisely.

**Dependabot is enabled and working.** `automated-security-fixes` returns
`{"enabled":true,"paused":false}`, and Dependabot has opened at least 15 pull requests since
2026-07-23.

**Every one of them was closed, not merged.** Including `#417`, `#416` and `#415` on 2026-08-03,
which would have moved `@angular/core`, `@angular/compiler` and `@angular/common` from 20.3.19 to
20.3.27.

The reason is structural. `.github/dependabot.yml` routes **all** ecosystems to
`target-branch: "chore/dependency-updates"` with `open-pull-requests-limit: 0`. The stated intent
was an integration branch so bumps could be batched and tested together. That branch:

- last received a commit on **2026-05-22**
- is **465 commits behind `main`**
- has never been merged into `main`

So the loop is: Dependabot raises a PR against a branch that is three months stale and goes
nowhere, and the PR is closed. The 88 open alerts (1 critical, 43 high, 37 medium, 7 low, **all
npm, zero NuGet**) are the accumulated result.

The config comment gives the original reason for the zero limit: _"until ABP Commercial supports
Angular 20.3+"_. Whether that constraint still binds against Angular 20.3.27 is not recorded
anywhere and has not been retested.

---

## 9. What does not exist

Checked for and absent. Listed without judgement; a separate exercise decides which of these a
public deployment requires.

| Absent                                    | Verified by                                                                                                                           |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| **End-to-end browser tests**              | No Playwright, Cypress, Puppeteer, WebdriverIO or TestCafe in `angular/package.json`. The 9-step booking wizard is exercised by hand  |
| **Accessibility testing**                 | No `axe-core`, `pa11y` or Lighthouse in `package.json` or any workflow                                                                |
| **Performance testing / Core Web Vitals** | No Lighthouse CI. Bundle budgets exist (`initial` 2 MB warn / 2.5 MB error) but no measurement of LCP, INP or CLS has ever been taken |
| **Container image scanning**              | No Trivy, Grype, Snyk, Docker Scout or Anchore in any workflow. Base images are never scanned                                         |
| **SBOM generation**                       | No CycloneDX or SPDX step                                                                                                             |
| **DAST**                                  | No OWASP ZAP, Nuclei or equivalent                                                                                                    |
| **Load / stress testing**                 | No k6, JMeter, NBomber or Gatling. Every sizing estimate is unmeasured                                                                |
| **Mutation testing**                      | No Stryker                                                                                                                            |
| **API contract testing**                  | None beyond the generated ABP proxies                                                                                                 |
| **Tests against real SQL Server**         | Every test uses SQLite in-memory (section 5.1)                                                                                        |
| **Frontend coverage reporting**           | Collected then excluded (section 5.3)                                                                                                 |
| **Automated deployment**                  | `deploy-dev.yml` validates and opens a PR; the server is updated by hand over SSH                                                     |
| **Staging environment**                   | `staging` and `production` branches last moved 2026-05-01                                                                             |

---

## 10. Known reliability issues

1. **One flaky backend test.** `MultiOfficeAppointmentChildCascadeTests.Copies_custom_field_values`
   failed on 2026-08-23 and 2026-08-25 with
   `SQLite Error 19: UNIQUE constraint failed: AppAppointments.TenantId, AppAppointments.RequestConfirmationNumber`,
   and passed on 2026-08-26. The constraint is the one narrowed by migration
   `20260821165915_Fix_UniqueIndexesExcludeSoftDeleted`; the first failure is two days after it
   landed. Not diagnosed.
2. **CI reliability**: last 100 `ci.yml` runs -- 89 success, 6 failure, 4 cancelled. Two of the
   six failures were on `main`.
3. **`security.yml`** failed on 2026-06-22, 06-29, 07-06 and 07-20; green since 2026-07-27.
4. **`AUTO_PR_TOKEN` expiry** has previously broken the cascade automation.
5. **The `SonarCloud Code Analysis` check on branch `main`** (distinct from the per-PR gate) has
   been red since at least 2026-07-08 with accumulated new-code findings, so every cascade merge
   is a named bypass of it.

---

## 11. Source map

| Topic                     | Location                                                                       |
| ------------------------- | ------------------------------------------------------------------------------ |
| CI workflows              | `.github/workflows/` (17 files)                                                |
| Local hooks               | `angular/.husky/{pre-commit,commit-msg,pre-push}`                              |
| Commit message rules      | `angular/commitlint.config.js`, `angular/commitlint.config.mjs`                |
| Backend compiler settings | `Directory.Build.props`                                                        |
| Frontend lint             | `angular/.eslintrc.json`                                                       |
| Sonar configuration       | `.github/workflows/sonarcloud.yml:85-136`                                      |
| Dependabot                | `.github/dependabot.yml`                                                       |
| Secret scanning           | `.gitleaks.toml`, `trufflehog-pr.yml`, `security.yml`                          |
| Test infrastructure       | `test/HealthcareSupport.CaseEvaluation.TestBase/`                              |
| Tenant isolation tests    | `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/` |
| Stale predecessor doc     | `docs/devops/TESTING-STRATEGY.md` (last verified 2026-06-01)                   |
