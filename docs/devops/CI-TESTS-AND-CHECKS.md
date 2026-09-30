# CI, tests and checks: what actually exists today

> Factual baseline of every automated check in this repository -- local hooks, CI workflows,
> the merge gate, the test suites, static analysis, and the dependency pipeline.
>
> **This document describes what is, not what should be.** It makes no recommendations. It exists
> to be verified against an industry standard for a public-facing healthcare scheduling
> application, which is a separate exercise.

| Field      | Value                                                                                                                                                                                                                  |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Verified   | Sections 1-4, 5.1 (test database), 5.2, 9 and 11 re-checked 2026-09-30 against main `2df2f2d9`; 5.3 and 7 on 2026-09-28 against `51723e39`. Measurements marked **snapshot 2026-08-26** (test counts, SonarCloud, CodeQL, Scorecard, Dependabot, reliability) were taken on `bc4f2029` and not re-measured |
| Method     | Direct file reads of `.github/workflows/`, `.husky/`, test projects and config; `gh api` for branch protection and Dependabot; SonarCloud public API                                                                   |
| Supersedes | Nothing |

---

## 1. At a glance

| Layer              | What runs                                              | Can it block?                               |
| ------------------ | ------------------------------------------------------ | ------------------------------------------- |
| Local pre-commit   | gitleaks, lint-staged, `dotnet format` on staged `.cs` | Yes, locally; bypassable with `--no-verify` |
| Local commit-msg   | commitlint (Conventional Commits)                      | Yes, locally                                |
| Local pre-push     | gitleaks full scan, backend Debug build                | Yes, locally                                |
| CI on pull request | 9 workflows, plus 2 that run only for their own paths  | **18 required checks** (section 4)          |
| CI after merge     | 6 workflows on push, plus the same 2 path-filtered     | No                                          |
| Scheduled          | 2 workflows (weekly)                                   | No                                          |

**The most consequential fact in this document: `main` requires 18 checks,** including the backend and
frontend tests, both format checks, the frontend lint, the coverage floors, markdown and YAML lint,
CodeQL for both languages, TruffleHog, dependency review, commitlint, the PR title and the packet
golden-output check. SonarCloud is the notable check that is **not** required.

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

18 workflow files (`ls .github/workflows | wc -l`), grouped by trigger. `doc-check.yml` and `sonarcloud.yml`
no longer exist; SonarCloud now runs as a job inside `ci.yml`.

### 3.1 On pull request (9)

| Workflow                | Job(s)                                                                                                                                                                                             | `continue-on-error`                    |
| ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------- |
| `ci.yml`                | Meta: Changed paths, Backend: Build (includes four migration checks), Backend: Test (4 shards plus a merge job), Backend: Format Check, Frontend: Build, Frontend: Lint, Frontend: Test, Frontend: Format Check, Python: Test, Docs: Structure Check, Tools: Packet Golden Output, Coverage: Floors, SonarCloud: Analysis | SonarCloud job, and the three coverage-download steps inside Coverage: Floors |
| `codeql-pr.yml`         | CodeQL: csharp, CodeQL: javascript-typescript (`queries: security-extended`)                                                                                                                       | job level                              |
| `trufflehog-pr.yml`     | TruffleHog: PR commits (`--only-verified`)                                                                                                                                                         | job level                              |
| `dependency-review.yml` | Dependency Review (`fail-on-severity: critical`)                                                                                                                                                   | --                                     |
| `commitlint.yml`        | Commitlint: PR commits                                                                                                                                                                             | job level                              |
| `lint-meta.yml`         | Lint: YAML workflows, Lint: Markdown                                                                                                                                                               | --                                     |
| `pr-title.yml`          | PR Title: Conventional Commits (`pull_request_target`)                                                                                                                                             | job level                              |
| `pr-size.yml`           | PR size label                                                                                                                                                                                      | --                                     |
| `labeler.yml`           | Path-based labels (`pull_request_target`)                                                                                                                                                          | --                                     |

Five required checks (both CodeQL checks, TruffleHog, Commitlint and PR Title) sit in jobs that set
`continue-on-error: true` at job level. GitHub documents that setting as keeping the **workflow run**
from failing; whether a failed job still fails its own required check was not tested for this
re-baseline. `ci.yml` also runs on push to `main`.

### 3.2 After merge (6)

| Workflow              | Trigger               | What it does                                                                                         |
| --------------------- | --------------------- | ---------------------------------------------------------------------------------------------------- |
| `auto-pr-dev.yml`     | push to `main`        | Opens the `main -> development` cascade PR. Requires `AUTO_PR_TOKEN`                                 |
| `cascade-guard.yml`   | push to `development` or `main` | Fails when a cascade into `development` arrived as a squash rather than a merge commit, or when `main`'s tip merges `development` (the update-branch trap below) |
| `deploy-dev.yml`      | push to `development` | **Does not deploy.** Runs `dotnet build` + `dotnet test`, then opens the `development -> staging` PR |
| `promote-staging.yml` | push to `staging`     | `dotnet build` + `dotnet test`. Notes that staging -> production PRs are always manual               |
| `release.yml`         | push to `production`  | `npx semantic-release`                                                                               |
| `scorecard.yml`       | push to `main` (and weekly) | OpenSSF Scorecard, uploads SARIF                                                               |

### 3.3 Scheduled (2)

| Workflow        | Schedule                           | Jobs                                                                 |
| --------------- | ---------------------------------- | -------------------------------------------------------------------- |
| `security.yml`  | Mondays 06:00 UTC                  | .NET vulnerability audit, npm audit, TruffleHog full history, CodeQL |
| `scorecard.yml` | Mondays 07:00 UTC + push to `main` | OpenSSF Scorecard, uploads SARIF                                     |

`security.yml` has no push trigger; besides the schedule it runs only on a manual `workflow_dispatch`.

### 3.4 Path-filtered (2)

These run on a pull request only when it touches their paths, and neither is a required check.

| Workflow        | Runs on                                                                                                                                              | Jobs                                        |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------- |
| `docs-site.yml` | Pull request or push to `main` touching `docs/**`, `mkdocs.yml`, `.github/docs-requirements.*`, `scripts/docs/**` or the workflow; manual dispatch | Docs: Build site, Docs: Publish to Pages    |
| `infra.yml`     | Pull request touching `infra/azure/**`, `scripts/infra-ci.py` or the workflow; push to `production` touching `infra/azure/**`; manual dispatch      | Infra: Bicep, Infra: What-If, Infra: Deploy |

- **The documentation site is built and checked, not published.** The publish job runs only on `main` when the
  repository variable `DOCS_SITE_PUBLISH` is `true`, and the repository defines no variables
  (`gh api repos/<org>/hcs-patient-portal/actions/variables`, 2026-09-30). The deployed stack serves the same site
  at `/docs` instead; see [RUNTIME-AND-DATA-PROFILE.md](RUNTIME-AND-DATA-PROFILE.md).
- **The Azure preview and deploy are not configured.** Infra: What-If reads its Azure identity from repository
  secrets that do not exist yet (the repository holds `ABP_LICENSE_CODE`, `ABP_NUGET_API_KEY`, `AUTO_PR_TOKEN` and
  `SONAR_TOKEN`), so it reports that the preview is not configured. Infra: Deploy runs only on `production`. The
  Bicep job runs on every matching pull request. Setup is in
  [infra/azure/README.md](https://github.com/gesco-healthcare-support/hcs-patient-portal/blob/main/infra/azure/README.md).

---

## 4. The merge gate

`gh api repos/<org>/hcs-patient-portal/branches/main/protection` on 2026-09-30 (the checks are unchanged from
2026-09-28; the review count is not):

```text
strict (branch must be up to date): true
REQUIRED CHECKS (18):
   - Meta: Changed paths          - Backend: Build            - Backend: Test
   - Backend: Format Check        - Frontend: Build           - Frontend: Test
   - Frontend: Lint               - Frontend: Format Check    - Coverage: Floors
   - Lint: Markdown               - Lint: YAML workflows      - Tools: Packet Golden Output
   - CodeQL: csharp               - CodeQL: javascript-typescript
   - TruffleHog: PR commits       - Dependency Review
   - Commitlint: PR commits       - PR Title: Conventional Commits
required_approving_review_count: 0
enforce_admins: false
required_linear_history: false
allow_force_pushes: false
required_conversation_resolution: false
```

A repository ruleset, `development-merge-only` (active), applies to `development`: pull requests only, merge
commits only (no squash or rebase), no deletion and no force push.

Consequences:

- A failing backend or frontend test suite, a format or lint failure, or a coverage drop below the floors
  blocks a merge to `main`.
- A job that is skipped reports success. `Coverage: Floors` runs unconditionally for that reason and treats
  missing coverage input as a failure.
- `main` requires no approving review. The downstream branches do: `development` and `staging` require one and
  `production` two (the same command with the branch name; each also requires 17 checks, strict). With
  `enforce_admins: false`, an administrator can merge past those requirements.
- SonarCloud (`SonarCloud: Analysis`, and the separate `SonarCloud Code Analysis` check its app posts) is
  advisory.

### Cascade PRs report BEHIND permanently -- do not "fix" it

Every environment promotion PR (`main -> development`, and each one downstream of it) shows
**BEHIND**, always, and it is not a state to correct. Measured 2026-09-08:

```text
main-only commits:          5
development-only commits:  13   <- all 13 are ci(sync) promote merge commits, 2 parents each
```

Each cascade merge leaves a merge commit on the DOWNSTREAM branch that the upstream branch never
receives. So `main` is permanently behind `development` in git terms, and `development` carries
`strict = true`, which means the up-to-date requirement can never be satisfied by any amount of
updating.

**The trap.** Update-branch merges the BASE into the HEAD. On the `main -> development` PR the base is
`development` and the head is `main`, so updating it would **merge `development` into `main`** and
drag every accumulated sync commit backwards into the trunk -- permanently, and visibly in `main`'s
history. There is no clean undo.

`cascade-guard.yml`'s "Cascade: no backwards merge into main" job fails such a commit. It judges
main's tip only, so the red also shows on the cascade PR (whose head is main) and stays until the next
reviewed commit lands on `main`. Resetting `main` to remove the commit would rewrite public history;
moving the tip forward is the fix.

**BEHIND here is a true statement that is not a problem.** The tooling renders "stale, update me" and
"structurally impossible, ignore me" identically, and nothing on the PR distinguishes them, so anyone
reading branch state alone will reach the wrong action. It looks most attractive under deadline
pressure, which is exactly when cascade PRs get merged.

**Cascade PRs merge with `--merge --admin`: never `--squash`, and never after an update-branch.**
`--admin` is structurally required rather than a convenience -- it is the only way past `strict`, and
the same account cannot approve a PR it authored. That the established shape is a merge commit is
checkable rather than asserted: all 13 existing promotes have two parents.

---

## 5. Tests

### 5.1 Backend

Five projects under `test/`. Counts are the **snapshot 2026-08-26**; current coverage and the commands to
re-measure are in [coverage-status.md](../testing/coverage-status.md).

| Project                         | Files   | `[Fact]`/`[Theory]` | Executed  | Result                                |
| ------------------------------- | ------- | ------------------- | --------- | ------------------------------------- |
| `Application.Tests`             | 124     | 1,058               | 1,106     | all pass                              |
| `Domain.Tests`                  | 89      | 549                 | 667       | 663 pass, 4 skipped                   |
| `EntityFrameworkCore.Tests`     | 82      | 133                 | 488       | 475 pass, 12 skipped, **1 failing**   |
| `TestBase`                      | 25      | 0                   | --        | shared infrastructure                 |
| `HttpApi.Client.ConsoleTestApp` | --      | --                  | --        | console harness, not in the CI run    |
| **Total**                       | **271** | **1,740**           | **2,261** | **2,244 pass, 16 skipped, 1 failing** |

Attribute count and executed count differ because `[Theory]` expands per data row.

**Almost every backend test runs against SQLite in-memory, not SQL Server.**
`CaseEvaluationEntityFrameworkCoreTestModule.cs:152` opens
`Data Source=:memory:;Foreign Keys=True`, and the multi-office harness uses SQLite too
(`CaseEvaluationMultiOfficeTestModule.cs:133`, `MultiOfficeTestDatabase.cs`). The production database is
SQL Server. Behaviours that differ between the two -- filtered unique indexes, collation, `datetime2`
semantics, concurrency tokens, computed columns -- are therefore not exercised by those tests.

**The exception is two classes that start a real SQL Server container**, from the image
`docker-compose.yml` pins (`SqlServerFeedFixture.Image`): `CaseTrackerFeedSqlServerTests` (the feed's
`rowversion` reads, and the tenant migrations applied on SQL Server) and `SqlAppLockTests` (the application
lock behind the admin-password store). Both join `SqlServerCollection`, so one container serves them and they
run one after the other (`git grep -l "SqlServerCollection" -- test`). Docker must be running for them;
CI's Linux runners have it, and the `Backend: Test` shards include them.

### 5.2 The multi-office (tenant isolation) suite

26 files under `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/`
(`git ls-files <that folder> | wc -l`, 2026-09-30),
including `MultiOfficeIsolationMatrixTests.cs`, `MultiOfficeAppointmentsAppServiceTests.cs`,
`MultiOfficeCatalogResolutionTests.cs`, `MultiOfficeConsentTokenResolutionTests.cs`,
`MultiOfficeImpersonationRoleTests.cs` and a self-validating harness
(`MultiOfficeHarnessSelfValidationTests.cs`).

Across the whole `test/` tree: 9 files reference tenant isolation, 10 reference cross-tenant
behaviour, 14 reference authorization, 16 reference permissions.

For a database-per-tenant system holding PHI this is the highest-consequence area, and it is the
best-covered area in the suite.

### 5.3 Frontend

- 184 `*.spec.ts` files (`find angular/src -name '*.spec.ts' | wc -l`, 2026-09-30). The CI run on main
  `2df2f2d9` (run 36755133682, 2026-09-30) executed **3,515 specs, all passing**, on Chrome Headless.
- Run via `yarn test --watch=false --browsers=ChromeHeadless --code-coverage`.
- Coverage is uploaded (`Upload frontend coverage`, unconditionally) and checked by the required
  `Coverage: Floors` job against a whole-number floor, through the shared `.coverage-exclusions` list
  that the SonarCloud job also reads.
- Spec concentration: 61 files under `app/appointments/` and 44 under `app/shared/`; no other folder
  has more than 8.

`Frontend: Test` still skips the test step, with a warning, when no `*.spec.ts` file exists. Because the
coverage upload runs regardless and `Coverage: Floors` fails on missing coverage, a pull request that
deleted every spec would still be blocked -- by the coverage gate rather than by the test job.

---

## 6. Static analysis and quality gates

### 6.1 SonarCloud

Project `gesco-healthcare-support_hcs-patient-portal`. Runs as the `SonarCloud: Analysis` job in `ci.yml`
(per-PR and on push to `main`), job-level `continue-on-error: true`, not a required check.

The figures below are the **snapshot 2026-08-26**, when the quality gate was **ERROR**.

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

The scanner configuration (then in `sonarcloud.yml`, now in the `ci.yml` job) carried 19 `sonar.issue.ignore.multicriteria` suppressions, mostly for
ABP framework patterns (dependency-injection parameter counts, permission-string duplication,
email-template HTML rules).

The 253 unlabelled inputs are concentrated: 47 in `internal-appointment-detail.component.html`,
26 in `people-edit-modal.component.html`, 17 in `patient-profile-redesign.component.html`, 13 in
`appointment-add-claim-parties-section.component.html`, then a long tail. Ten files hold roughly
70 percent of them.

### 6.2 CodeQL

Two runs: per-PR (`codeql-pr.yml`, matrix `csharp` + `javascript-typescript`,
`queries: security-extended`; both are required checks, with job-level `continue-on-error`) and weekly
inside `security.yml`.

**Snapshot 2026-08-26:** 23 open alerts (3 high, 20 medium), all in C#, none in JavaScript/TypeScript.
Maintainers see the live list, with rules and locations, in the repository's **Security** tab under
code scanning.

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
covers that separately in `Backend: Format Check`, which is a required check. (The separate
`-warnaserror` build step was removed on 2026-09-02 as redundant.)

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

Formatting is Prettier (`yarn format:check`) in `Frontend: Format Check`, a required check, and is
enforced locally by `lint-staged`.

---

## 8. The dependency pipeline

**Snapshot 2026-08-26.** This was the most misleading area of the repository, and the mechanism is worth
stating precisely.

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

**Since the snapshot (checked 2026-09-30).** `.github/dependabot.yml` now has five entries, all still targeting
`chore/dependency-updates`: `nuget` and the two `npm` directories keep `open-pull-requests-limit: 0`, while
`github-actions` and `docker` allow 5 (`grep -n "open-pull-requests-limit" .github/dependabot.yml`). Dependabot pull
requests can still reach `main`: #1154 (a `moment` bump in the AuthServer) merged there on 2026-09-29.

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
| **API contract testing**                  | Only the generated ABP proxies, plus `CaseTrackerWireContractTests`, which pins the Case Tracker feed's route, header and parameter names as literals |
| **Tests against real SQL Server**         | Two classes only (`CaseTrackerFeedSqlServerTests`, `SqlAppLockTests`); every other test uses SQLite in-memory (section 5.1)          |
| **Automated deployment**                  | `deploy-dev.yml` validates and opens a PR; the application server is updated by hand. `infra.yml` has an Azure deploy job with no credentials configured (section 3.4) |
| **Staging environment**                   | `staging` and `production` branches last moved 2026-05-01                                                                             |

---

## 10. Known reliability issues

**Snapshot 2026-08-26.**

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
| CI workflows              | `.github/workflows/` (18 files)                                                |
| Local hooks               | `angular/.husky/{pre-commit,commit-msg,pre-push}`                              |
| Commit message rules      | `angular/commitlint.config.js`, `angular/commitlint.config.mjs`                |
| Backend compiler settings | `Directory.Build.props`                                                        |
| Frontend lint             | `angular/.eslintrc.json`                                                       |
| Sonar configuration       | `.github/workflows/ci.yml` (`sonarcloud` job) and `.coverage-exclusions`        |
| Dependabot                | `.github/dependabot.yml`                                                       |
| Secret scanning           | `.gitleaks.toml`, `trufflehog-pr.yml`, `security.yml`                          |
| Test infrastructure       | `test/HealthcareSupport.CaseEvaluation.TestBase/`                              |
| Tenant isolation tests    | `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/MultiOffice/` |
| Test layout and harnesses | `docs/devops/TESTING-STRATEGY.md`                                              |
