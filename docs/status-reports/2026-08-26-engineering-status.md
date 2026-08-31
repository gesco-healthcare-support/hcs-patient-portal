# Patient Portal -- Engineering Status Audit

> Point-in-time audit of the whole repository: merged PRs, commit history, CI, and the
> automated quality signals, across backend, frontend, and both database scopes.
> Audience: Adrian (sole dev) and whoever picks up the hosting/production-deployment work.

| Field                         | Value                                                                                                                                          |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| Audit date                    | 2026-08-26                                                                                                                                     |
| Repository                    | `gesco-healthcare-support/hcs-patient-portal` (**public**)                                                                                        |
| `origin/main` at audit        | `bc4f2029` -- `docs(requirements): close two open requirement questions (#487)`                                                                |
| `origin/development` at audit | `8695cd72` -- `ci(sync): promote main to development (#467)`, 2026-08-25                                                                       |
| Deployed (LAN box)            | `development @ 8695cd72`, containers rebuilt 2026-08-25. **Not re-verified in this pass** -- see [Unverified](#what-this-audit-did-not-verify) |
| Method                        | `git`, `gh` (PRs, runs, alerts, branch protection), SonarCloud public API, direct file reads                                                   |

---

## 1. Headline numbers

| Dimension                          | Value                                                               |
| ---------------------------------- | ------------------------------------------------------------------- |
| Merged PRs (all time)              | **405** (first `#12`, 2026-04-08; last `#487`, 2026-08-26)          |
| Commits on `main`                  | **922**                                                             |
| Tracked files                      | 3,335                                                               |
| Lines of code (SonarCloud `ncloc`) | **115,709**                                                         |
| Backend tests                      | **2,261 executed** -- 2,244 pass, 16 skipped, **1 failing (flaky)** |
| Frontend tests                     | **581 executed, 581 pass**                                          |
| SonarCloud quality gate            | **ERROR (failing)**                                                 |
| Coverage (SonarCloud)              | **52.2%** -- backend only, see [4.1](#41-sonarcloud)                |
| CodeQL open alerts                 | **23** (3 high, 20 medium)                                          |
| OpenSSF Scorecard open alerts      | **109**                                                             |
| Dependabot open alerts             | **88** (1 critical, 43 high, 37 medium, 7 low) -- all npm           |
| Required PR checks on `main`       | **2** (`Backend: Build`, `Frontend: Build`)                         |

---

## 2. Delivery state

### 2.1 Branch cascade

The documented flow is `main -> development -> staging -> production`. Only the first hop
is actually moving.

| Branch        | Head       | Date           | Position                             |
| ------------- | ---------- | -------------- | ------------------------------------ |
| `main`        | `bc4f2029` | 2026-08-26     | trunk, active                        |
| `development` | `8695cd72` | 2026-08-25     | **3 commits behind `main`**          |
| `staging`     | `6e9ee0f9` | **2026-05-01** | **633 commits behind `development`** |
| `production`  | `23245ab9` | **2026-05-01** | **627 commits behind `main`**        |

**`staging` and `production` have not moved since 2026-05-01.** Nearly four months of work
(and every commit in this audit) sits outside them. The cascade PR `#384`
(`development -> staging`) has been open since 2026-07-22 and unmerged.

Practical read: there is one live environment (the LAN box on `development`) and one trunk.
The `staging`/`production` branches are naming, not environments. Any production-hosting plan
starts from `main`/`development`, not from the `production` branch.

### 2.2 In flight

| PR     | Direction                | Opened     | State                          |
| ------ | ------------------------ | ---------- | ------------------------------ |
| `#485` | `main -> development`    | 2026-08-26 | open, CI running at audit time |
| `#384` | `development -> staging` | 2026-07-22 | open, stale                    |

The 3 commits `main` holds over `development` are `#484` (compose wrapper), `#486` (lockout
copy), `#487` (requirements docs). None is live.

### 2.3 Worktrees

`C:/src/patient-portal/{main, development, spa-cache-headers}`. `main` is currently parked on
branch `docs/close-deadletter-and-reschedule-questions` (`4a0d96e1`), with two untracked
files under `docs/integration/`.

---

## 3. History: PRs and commits

### 3.1 Volume over time

| Month   | Merged PRs | Commits on `main` |
| ------- | ---------- | ----------------- |
| 2026-04 | 129        | 260               |
| 2026-05 | 96         | 372               |
| 2026-06 | 51         | 167               |
| 2026-07 | 70         | 72                |
| 2026-08 | 59         | 51                |

Throughput per PR has risen while raw commit count has fallen -- consistent with the move to
squash-merged feature PRs and away from direct commits.

### 3.2 Merge targets

| Base branch                | Merged PRs |
| -------------------------- | ---------- |
| `main`                     | 242        |
| `feat/replicate-old-app`   | 57         |
| `development`              | 53         |
| `staging`                  | 18         |
| `production`               | 10         |
| `feat/db-per-tenant`       | 10         |
| `chore/dependency-updates` | 6          |
| others (5 branches)        | 9          |

The 57 PRs into `feat/replicate-old-app` and 10 into `feat/db-per-tenant` are the two long-lived
epic branches; both are closed out.

### 3.3 Change type

`feat` 108, `fix` 94, `ci` 62, `chore` 52, `docs` 45, `test` 20, `refactor` 8, `build` 1,
plus 15 cascade/promotion PRs. Cumulative churn across merged PRs: **+2,658,250 / -488,313
lines over 17,464 file-changes** (inflated by generated ABP proxy code and lockfiles).

### 3.4 Authorship

`main` is effectively single-author across four git identities:

```
483  Adrian <adriang@gesco.com>
311  AdrianG <arajeev@gesco.com>
 91  Adriang <arajeev@gesco.com>
 35  Adrian <adrian@gesco.com>
  2  dependabot[bot]
```

Worth normalising via `.mailmap` so contribution history reads as one person.

---

## 4. CI/CD

17 workflows under `.github/workflows/`.

| Workflow                                                                                         | Trigger                            | Blocking?                                                           |
| ------------------------------------------------------------------------------------------------ | ---------------------------------- | ------------------------------------------------------------------- |
| `ci.yml`                                                                                         | PR                                 | Only `Backend: Build` + `Frontend: Build` are required              |
| `sonarcloud.yml`                                                                                 | PR + push to `main`                | No (`continue-on-error`, `qualitygate.wait=false`)                  |
| `codeql-pr.yml`                                                                                  | PR                                 | No                                                                  |
| `security.yml`                                                                                   | weekly cron (Mon 06:00) + dispatch | No                                                                  |
| `trufflehog-pr.yml`                                                                              | PR                                 | No                                                                  |
| `dependency-review.yml`                                                                          | PR                                 | No (`continue-on-error`)                                            |
| `commitlint.yml`, `pr-title.yml`, `pr-size.yml`, `lint-meta.yml`, `labeler.yml`, `doc-check.yml` | PR                                 | No                                                                  |
| `scorecard.yml`                                                                                  | push/cron                          | No                                                                  |
| `auto-pr-dev.yml`                                                                                | push to `main`                     | Opens the `main -> development` cascade PR                          |
| `deploy-dev.yml`                                                                                 | push to `development`              | Builds + runs `dotnet test`, then opens `development -> staging` PR |
| `promote-staging.yml`, `release.yml`                                                             | promotion                          | No                                                                  |

### 4.0 Two structural observations

**`deploy-dev.yml` does not deploy.** Despite the name, it validates (`dotnet build`,
`dotnet test`) and opens the next cascade PR. Nothing pushes an image or touches the LAN box;
the box is updated by hand over SSH. There is **no CD**.

**Branch protection on `main` is thin.** `gh api .../branches/main/protection`:

```
strict (up-to-date required): true
REQUIRED CHECKS (2):
   - Backend: Build
   - Frontend: Build
required approving reviews: 1
enforce_admins: false
linear_history: false
allow_force_pushes: false
```

Tests, lint, format, SonarCloud, CodeQL, TruffleHog and dependency review all run but **none
gates a merge**. `enforce_admins: false` plus the single-dev workflow means the one required
review is routinely satisfied by admin merge. Net: **the only thing that can block a merge to
`main` is a compile failure.**

`ci.yml` also carries three deliberate `continue-on-error` steps -- `Backend: Format Check`,
`Frontend: Format Check`, and the `-warnaserror` build -- annotated as Phase A/B scaffolding
awaiting a "Phase C" hardening pass that has not happened. Note `Directory.Build.props` has
since set `TreatWarningsAsErrors=true` repo-wide, so the separate informational
`-warnaserror` step is now redundant.

### 4.1 CI reliability

| Workflow         | Sample         | Result                                                         |
| ---------------- | -------------- | -------------------------------------------------------------- |
| `ci.yml`         | last 100 runs  | 89 success, 6 failure, 4 cancelled, 1 running                  |
| `sonarcloud.yml` | last 30        | 25 success, 4 cancelled, 1 running                             |
| `codeql-pr.yml`  | last 30        | 29 success, 1 cancelled                                        |
| `security.yml`   | last 10 weekly | green since 2026-07-27; failed 2026-06-22, 06-29, 07-06, 07-20 |

Of the 6 `ci.yml` failures, **2 were on `main`** (the cascade PRs of 2026-08-23 and 2026-08-25).
Both failed the same way -- see [6.1](#61-one-flaky-backend-test).

---

## 5. Quality signals

### 5.1 SonarCloud

Project `gesco-healthcare-support_hcs-patient-portal`. **Quality gate: ERROR.**

| Metric                        | Value             | Rating                      |
| ----------------------------- | ----------------- | --------------------------- |
| Coverage                      | 52.2%             | --                          |
| Bugs                          | 338               | Reliability **D** (4.0)     |
| Vulnerabilities               | 20                | Security **E** (5.0)        |
| Security hotspots (to review) | 31                | Security review **C** (3.0) |
| Code smells                   | 920               | Maintainability **A** (1.0) |
| Technical debt                | 3,346 min (~56 h) | --                          |
| Duplicated lines              | 3.0%              | --                          |
| Cognitive complexity          | 7,570             | --                          |
| `ncloc`                       | 115,709           | --                          |

Failing gate conditions (new-code period = `previous_version`, dated **2026-04-15**, so
"new code" is effectively the whole project):

| Condition                        | Threshold | Actual | Status |
| -------------------------------- | --------- | ------ | ------ |
| `new_reliability_rating`         | <= A      | D      | ERROR  |
| `new_security_rating`            | <= A      | E      | ERROR  |
| `new_coverage`                   | >= 80%    | 51.9%  | ERROR  |
| `new_security_hotspots_reviewed` | 100%      | 35.4%  | ERROR  |
| `new_maintainability_rating`     | <= A      | A      | OK     |
| `new_duplicated_lines_density`   | <= 3%     | 1.8%   | OK     |

**The 338 "bugs" are misleading.** 330 of them are HTML accessibility rules:
`Web:InputWithoutLabelCheck` (253) and `Web:MouseEventWithoutKeyboardEquivalentCheck` (77).
Only **3 are C# and 5 are TypeScript**. The reliability D is an accessibility debt signal, not
a runtime-defect signal. It is still worth fixing -- a public-facing portal used by patients has
a real accessibility obligation -- but it should not be read as 338 latent crashes.

**Coverage is backend-only.** `sonarcloud.yml:98` puts `angular/src/**/*.ts` and
`angular/src/**/*.html` in `sonar.coverage.exclusions`, alongside `Program.cs`, `*Module.cs`,
`*DbContext*.cs`, `Migrations/**` and `TenantMigrations/**`. Angular LCOV is generated and
uploaded but excluded from the metric. So 52.2% describes the .NET code
(`lines_to_cover` 17,669, `uncovered_lines` 8,445); the 26,694 lines of TypeScript and 15,015
of `web` are **not measured at all**.

`README.md` states "SonarCloud is live and gates new-code coverage on PRs". That is
**inaccurate** -- the job is `continue-on-error: true`, sets `sonar.qualitygate.wait=false`,
and is not a required check.

Language distribution (`ncloc`): `cs` 53,596 | `ts` 26,694 | `web` 15,015 | `css` 8,060 |
`powershell` 6,595 | `py` 3,526 | `yaml` 1,319 | `shell` 527 | `docker` 189 | `js` 121 | `xml` 67.

**The 20 vulnerabilities** (5 BLOCKER, 14 MAJOR, 1 MINOR) are mostly hygiene flags, but three
deserve a look before public exposure:

| Severity   | Rule                | Location                                                                                        | Note                                                                                                          |
| ---------- | ------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| BLOCKER    | `tssecurity:S6105`  | `angular/src/tenant-bootstrap.ts:65`                                                            | client-side redirect from user-controlled data -- open-redirect shape, and this is the tenant resolution path |
| BLOCKER    | `typescript:S6268`  | `angular/src/app/shared/ui/icon/icon.component.ts:58`                                           | Angular built-in sanitisation disabled                                                                        |
| BLOCKER    | `python:S8392`      | `docker/packet-renderer/app.py:116`                                                             | binds all interfaces (contained, but the container is on the app network)                                     |
| BLOCKER x2 | `secrets:S7539`     | `scripts/dev/dev-api.ps1:34`, `dev-authserver.ps1:37`                                           | SQL Server passwords in dev scripts                                                                           |
| MAJOR x5   | `csharpsquid:S2068` | seed contributors, `appsettings.json:34` (both hosts), `CaseEvaluationHttpApiHostModule.cs:783` | hardcoded-credential pattern; expected to be dev placeholders, **worth confirming individually**              |

**31 hotspots awaiting review**: csrf 6 (HIGH), auth 3 (HIGH), dos 6 (MEDIUM), permission 5
(MEDIUM), encrypt-data 6 (LOW), insecure-conf 2 (LOW), others 3 (LOW). The 9 HIGH-probability
csrf/auth hotspots are the ones that matter for a public deployment; none has been triaged.

### 5.2 CodeQL

23 open alerts (`csharp`, `javascript-typescript`). No JS/TS findings -- all C#.

| Severity | Rule                                            | Count | Where                                                                                                                                                                                                                                                               |
| -------- | ----------------------------------------------- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| high     | `cs/cleartext-storage-of-sensitive-information` | 3     | `CaseEvaluationAccountEmailer.cs` (2), `NotificationDispatcher.cs` (1)                                                                                                                                                                                              |
| medium   | `cs/exposure-of-sensitive-information`          | 18    | `ExternalUsersDataSeedContributor.cs` (5), `DemoExternalUsersDataSeedContributor.cs` (5), `DemoPatientDataSeedContributor.cs` (3), `ExternalAccountAppService.cs` (2), `CaseEvaluationAccountEmailer.cs` (2), `AppointmentChangeRequestsAppService.Approval.cs` (1) |
| medium   | `cs/log-forging`                                | 2     | `ExternalAccountAppService.cs`                                                                                                                                                                                                                                      |

The 13 alerts in seed contributors are almost certainly synthetic demo credentials and will
triage away. The **emailer and dispatcher findings are the ones to actually read** -- they sit
on the path that composes messages containing patient and appointment data, which is exactly
where a HIPAA-relevant leak would live. `cs/log-forging` in `ExternalAccountAppService` means
unsanitised user input reaches a log sink.

### 5.3 OpenSSF Scorecard

109 open alerts, dominated by `PinnedDependenciesID` (68) -- GitHub Actions referenced by tag
rather than commit SHA. Also `TokenPermissionsID` (4, high), `VulnerabilitiesID`,
`CodeReviewID`, `SASTID`, `FuzzingID`, `CIIBestPracticesID`. These are supply-chain posture
signals, not application defects. Pinning actions to SHAs would clear the bulk of them.

### 5.4 Dependabot

**88 open alerts, every one npm. Zero NuGet.** The .NET side is clean; the JavaScript side is
not.

| Scope       | Critical | High | Medium | Low |
| ----------- | -------- | ---- | ------ | --- |
| runtime     | 1        | 27   | 15     | 4   |
| development | 0        | 16   | 22     | 3   |

Two lockfiles are affected: `angular/yarn.lock` and
`src/HealthcareSupport.CaseEvaluation.AuthServer/yarn.lock` (the ABP MVC UI assets).

The six that ship to a browser and are not merely build tooling:

| CVE                             | Package                              | Impact                                                               |
| ------------------------------- | ------------------------------------ | -------------------------------------------------------------------- |
| CVE-2026-69151                  | `@angular/core`, `@angular/compiler` | **XSS** via i18n event-handler attributes                            |
| CVE-2026-54267                  | `@angular/core`                      | client hydration DOM clobbering + response-cache poisoning           |
| CVE-2026-68945                  | `@angular/common`                    | `HttpTransferCache` cache-key ambiguity -> **cross-request leakage** |
| CVE-2026-50170                  | `@angular/common`                    | information leak via default caching of credentialed responses       |
| CVE-2026-54266                  | `@angular/common`                    | weak 32-bit `HttpTransferCache` key hashing                          |
| CVE-2026-54268 / CVE-2026-50171 | `@angular/common`                    | DoS via OOM in date/number formatting                                |

Angular is pinned at `~20.3.19`. Three of these are cross-request/cross-user data-exposure
shapes in a **multi-tenant app handling PHI**, which raises them above routine dependency
noise. The remaining high-severity runtime entries (`tar` critical, `brace-expansion`,
`minimatch`, `undici`, `vite`, `immutable`, `nanoid`, `form-data`, `fast-xml-builder`) are
mostly DoS/ReDoS in build-time or server-side-tooling packages; the `scope: runtime`
classification overstates how many actually reach a user's browser.

### 5.5 Secret scanning

TruffleHog runs per-PR (`--only-verified`) and weekly. Green throughout. Gitleaks config
present (`.gitleaks.toml`). No verified leaked secret found.

---

## 6. Test suite

### 6.1 Backend

From the CI run of 2026-08-25 (`dotnet test` across the solution):

| Project                     | Total     | Passed    | Skipped | Failed |
| --------------------------- | --------- | --------- | ------- | ------ |
| `Domain.Tests`              | 667       | 663       | 4       | 0      |
| `Application.Tests`         | 1,106     | 1,106     | 0       | 0      |
| `EntityFrameworkCore.Tests` | 488       | 475       | 12      | **1**  |
| **Total**                   | **2,261** | **2,244** | **16**  | **1**  |

Source declares 1,707 `[Fact]`/`[Theory]` attributes across 271 test files (Application 1,049,
Domain 528, EFCore 130); `[Theory]` expansion accounts for the difference.

#### One flaky backend test

`MultiOfficeAppointmentChildCascadeTests.Copies_custom_field_values` failed on **both** the
2026-08-23 and 2026-08-25 cascade PRs, identically:

```
Microsoft.Data.Sqlite.SqliteException : SQLite Error 19:
'UNIQUE constraint failed: AppAppointments.TenantId, AppAppointments.RequestConfirmationNumber'
  at ...MultiOfficeAppointmentChildCascadeTests.RunCopyAsync(...):line 286
```

It **passed** on the 2026-08-26 runs, so it is intermittent rather than a standing regression.
The constraint it trips is the one narrowed by migration
`20260821165915_Fix_UniqueIndexesExcludeSoftDeleted` (2026-08-21), and the first observed
failure is two days later -- so the most likely reading is a confirmation-number collision the
old wider index tolerated and the new narrower one does not. Not proven; worth reproducing
with a seeded loop before concluding.

Because `Backend: Test` is not a required check, **both failing runs merged anyway.**

### 6.2 Frontend

581 specs across 66 `*.spec.ts` files, 206 `describe` blocks. Latest run:
`TOTAL: 581 SUCCESS` on Chrome Headless 151. No failures, no skips.

Coverage is collected (`--code-coverage`) but, per [5.1](#41-sonarcloud), excluded from the
Sonar metric and not published anywhere else. **The frontend has no coverage number.**

`ci.yml:248` still contains a `find src -name "*.spec.ts"` guard that skips the test step
entirely when no specs exist, emitting a warning instead of failing. With 66 spec files that
branch is dead, but the guard remains and would silently green a PR that deleted every spec.

---

## 7. Backend

**Stack**: .NET 10, C#, ABP Commercial **10.0.2**, EF Core (SQL Server), OpenIddict,
Riok.Mapperly, Stateless (state machine), QuestPDF, MailKit/MimeKit, MinIO blob storing.

**Projects** (10 src, 5 test):

```
src/  Application, Application.Contracts, AuthServer, DbMigrator, Domain,
      Domain.Shared, EntityFrameworkCore, HttpApi, HttpApi.Client, HttpApi.Host
test/ Application.Tests, Domain.Tests, EntityFrameworkCore.Tests,
      TestBase, HttpApi.Client.ConsoleTestApp
```

1,282 `.cs` files outside migrations/obj/bin; 1,496 tracked `.cs` overall.

**Build discipline**: `Directory.Build.props` sets `Nullable=enable`,
`TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `RestorePackagesWithLockFile=true`
(`RestoreLockedMode=false`). A prior 480-warning nullability cleanup is recorded as closed.
Scriban is pinned to 7.2.5 to clear NU1902/NU1903 above ABP's shipped 6.3.0.

**Observability**: Serilog (console + file + async sinks) and `AspNetCore.HealthChecks.UI`
with **in-memory storage**. No OpenTelemetry, no metrics export, no APM, no centralised log
aggregation. Health check UI state does not survive a restart.

**Rate limiting** is narrow, not global: `ConfigurePasswordResetRateLimiter` in
`CaseEvaluationHttpApiHostModule.cs:625` partitions per-email/per-IP fixed windows, applied to
password reset, public document upload, and external account endpoints. Everything else is
unthrottled at the app tier, and nginx adds none (see [8](#8-database)).

---

## 8. Frontend

**Stack**: Angular **20.3.19**, Angular CLI 20.3.24, ABP `@abp/ng.*` 10.0.2, TypeScript 5.8,
RxJS 7.8, zone.js 0.15, Yarn **4.16.0** (Berry, via corepack). 37 deps, 27 devDeps.

562 tracked `.ts`; 339 under `angular/src` excluding the generated `proxy/` tree.

**Bundle budgets** (`angular.json`, production): initial warn 2 MB / error 2.5 MB;
`anyComponentStyle` warn 20 KB / error 100 KB. No budget on lazy chunks.

**Lint/format**: `yarn lint` blocking-ish (runs, not required); `yarn format:check` is
`continue-on-error`.

Note `README.md` badges Node 20.x while `ci.yml` uses Node 22 and `security.yml`'s npm-audit
job uses Node 20 -- three versions across the repo.

---

## 9. Database

Two DbContexts, two migration sets, **db-per-office** multi-tenancy.

|                    | Host                                                 | Tenant (per office)                                  |
| ------------------ | ---------------------------------------------------- | ---------------------------------------------------- |
| Context            | `CaseEvaluationDbContext`                            | `CaseEvaluationTenantDbContext`                      |
| `DbSet<>` declared | 46                                                   | 44                                                   |
| Migration folder   | `Migrations/`                                        | `TenantMigrations/`                                  |
| Migrations         | **90**                                               | **15**                                               |
| First migration    | `20260131164316_Initial`                             | `20260624205034_Initial`                             |
| Latest migration   | `20260821165915_Fix_UniqueIndexesExcludeSoftDeleted` | `20260821170002_Fix_UniqueIndexesExcludeSoftDeleted` |

Both derive from `CaseEvaluationDbContextBase<T>` and share
`CaseEvaluationSharedModelConfiguration` (`ConfigureCaseEvaluationShared()`), so an entity
mapped in both must receive a migration in **both** sets. The 2026-08-21 index fix landed in
both, correctly and 47 seconds apart.

The tenant context sets `MultiTenancySides.Tenant` and unconditionally configures `Doctor`,
`Patient`, `Location`, `WcabOffice` -- entities the host context gates behind
`IsHostDatabase()`. Comments in `CaseEvaluationTenantDbContext.cs:87` and `:95` record why:
FKs must resolve inside the office DB, and `IntegrationOutboxItems` "must be configured here
as well as in the host context, or office databases get no table at all".

Physical layout: one `CaseEvaluation` host DB plus one `CaseEvaluation_<office>` DB per office,
all in a **single SQL Server container**, discovered at backup time via `sys.databases`.

Known gap already logged in `docs/backlog.md`: `CaseEvaluationDbMigrationService` loops tenants
with **no per-tenant error handling**, so one office failing mid-loop leaves the fleet on two
schema versions with no report of which succeeded. That is a real operational risk once there
is more than a handful of offices.

Also logged: `Down()` on `Fix_UniqueIndexesExcludeSoftDeleted` becomes non-executable once a
row exists that the narrow index permits and the wide one would not. Rollback of that migration
is one-way in practice.

---

## 10. Findings that matter, ranked

1. **Nothing but a compile failure can block a merge to `main`.** Tests, lint, format, Sonar,
   CodeQL and secret scanning all run and none is required. Two PRs with a failing backend test
   merged in the last week. (Section [4.0](#40-two-structural-observations))
2. **Six Angular CVEs ship to the browser**, three of them cross-request/cross-user data
   exposure in a multi-tenant PHI app. ([5.4](#54-dependabot))
3. **`staging` and `production` branches are dead** since 2026-05-01, 627+ commits behind.
   There is no path to production that has ever been exercised. ([2.1](#21-branch-cascade))
4. **No CD, no deploy automation.** `deploy-dev.yml` does not deploy. The only environment is
   updated by hand over SSH. ([4.0](#40-two-structural-observations))
5. **31 security hotspots untriaged**, including 9 HIGH csrf/auth. ([5.1](#41-sonarcloud))
6. **Frontend has no coverage measurement at all**, and backend sits at 52.2% against an
   80% gate that does not enforce. ([5.1](#41-sonarcloud), [6.2](#62-frontend))
7. **253 form inputs without labels.** An accessibility obligation for a public patient-facing
   portal, not just a lint score. ([5.1](#41-sonarcloud))
8. **Per-tenant migration has no error handling** -- a partial migration leaves offices on
   split schema versions silently. ([9](#9-database))
9. **One flaky backend test** tied to the 2026-08-21 unique-index change. ([6.1](#61-one-flaky-backend-test))
10. **`README.md` overstates the gate** ("SonarCloud ... gates new-code coverage on PRs") and
    badges the wrong Node version.

---

## 11. What this audit did NOT verify

Stated explicitly so nothing here is mistaken for confirmed:

- **Live box state.** The deployed SHA and container health are quoted from the 2026-08-25
  check, not re-checked today. No SSH was performed in this pass.
- **Whether the 5 `csharpsquid:S2068` hardcoded-credential flags are all dev placeholders.**
  Expected, not confirmed file by file.
- **Whether the flaky test is a collision or a genuine regression.** Both readings fit the
  evidence; reproducing it was out of scope.
- **Runtime behaviour of any kind.** This is a repository and CI audit. No application was
  started, no endpoint exercised, no query run.
- **The apex-domain routing question** raised in the hosting input document -- flagged there as
  needing confirmation, not asserted.
- **`docs/` accuracy.** `docs/runbooks/ENGINEERING-ROADMAP.md` self-reports "Last verified
  2026-06-01" and still describes `feat/replicate-old-app` as the working branch; it is stale,
  but a full documentation audit was not attempted.

---

## 12. Source map

| Topic                         | Where                                                                                                                |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| CI definitions                | `.github/workflows/` (17 files)                                                                                      |
| Sonar config incl. exclusions | `.github/workflows/sonarcloud.yml:97-99`                                                                             |
| Build defaults                | `Directory.Build.props`                                                                                              |
| DbContexts                    | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/`                                      |
| Migrations                    | `.../Migrations/` (host), `.../TenantMigrations/` (tenant)                                                           |
| Rate limiter                  | `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:625`                           |
| Prod compose                  | `docker-compose.prod.yml`                                                                                            |
| Reverse proxy                 | `docker/nginx-proxy/default.conf.template`                                                                           |
| Hosting scripts               | `scripts/hosting/`                                                                                                   |
| Security docs                 | `docs/security/` (THREAT-MODEL, HIPAA-COMPLIANCE, SECRETS-MANAGEMENT, DATA-FLOWS, AUTHORIZATION, SESSION-AND-TOKENS) |
| Database docs                 | `docs/database/`                                                                                                     |
| Hosting runbooks              | `docs/runbooks/hosting-backup-restore.md`, `hosting-local-verification.md`, `DOCKER-DEV.md`, `LOCAL-DEV.md`          |
| Open follow-ups               | `docs/backlog.md` (633 lines, gitignored)                                                                            |
