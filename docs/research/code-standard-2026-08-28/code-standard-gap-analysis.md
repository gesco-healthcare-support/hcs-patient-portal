# Code standard gap analysis

> What an industry-standard CI pipeline, test suite and automated check set looks like in 2026 for a
> public-facing, HIPAA-regulated, multi-tenant .NET 10 / ABP Commercial / Angular 20 / SQL Server
> scheduling application, and where this codebase stands against it.
>
> Prepared 2026-08-28. Inputs: `00-RESEARCH-BRIEF.md`, `01-ci-tests-and-checks.md` (baseline verified
> 2026-08-26), `02-project-status.md`.
>
> Companion document: `remediation-plan.md` (the ordered backlog).
>
> ## REPOSITORY VERIFICATION, 2026-08-28 -- read before acting on this document
>
> This analysis was produced by a session with **web access but no repository access**, so every
> statement about the codebase's current state was inherited from baseline documents rather than
> read from source. The repository checks it asked for have since been run. Its research into
> external standards holds up; several of its conclusions about *this* codebase do not.
>
> **Refuted or materially changed:**
>
> | Where | Claim | Verified reality |
> | --- | --- | --- |
> | Contradiction 5; section 12 | The flaky cascade test is a true-positive model/migration drift signal, because `HasFilter` probably went only into the migration | **`HasFilter` IS in the model configuration** -- `CaseEvaluationDbContext.cs:151,170` plus 8+ sites in `CaseEvaluationSharedModelConfiguration.cs`; 19 occurrences in the migration, 20 in the snapshot. The drift theory is dead. Live hypothesis: SQLite vs SQL Server *filtered-index fidelity* -- the failing index at `CaseEvaluationSharedModelConfiguration.cs:446-448` IS filtered, yet SQLite reports a bare unique violation. That makes the flake evidence FOR the SQL Server test project (remediation item 20), not a separate defect |
> | Contradiction 1; rows 5.1, R2 | `target-branch` is the kill switch routing security PRs to a stale branch; deleting it is "the highest value-per-minute change available" | **It did not suppress them.** PR #417 and #416 both carry `baseRefName: main` and were closed **by the maintainer** on 2026-08-07 with every check green. Dependabot then replied it would not raise that release again, so 20.3.x is now silently untracked. Deleting `target-branch` fixes version-update routing and **nothing about the CVEs** -- those need a manual bump |
> | Row 4.4; contradiction 9; R6 | CodeQL's JS/TS analysis is "PARTIAL / suspect"; zero alerts is not credible and probably means it never ran | **It runs.** The `javascript-typescript` analysis completes and uploads SARIF. Zero alerts is a real result. Removing `continue-on-error` is still correct; the urgency is lower |
> | Remediation items 12, 14 | Two BLOCKER-rated frontend findings requiring urgent code reading | **Both are false positives**, verified by reading the source. See `remediation-plan.md` items 12 and 14 |
> | Remediation item 13 | `Demo*` seed contributors may execute outside Development | **All four user seeders are runtime-gated on `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT` and fail closed.** Only 2 databases exist, not 11 |
> | Remediation item 8 | `dotnet test` must be moved into `ci.yml`; `backend-test` / `frontend-test` jobs must be added | **They already exist and already run** (`ci.yml:127` and `:216`; `dotnet test` at `:161`), unmasked. They are simply not *required checks*. The gap is the gate, not the tests |
> | Remediation item 29 | Delete `labeler.yml` because it uses `pull_request_target` | Both `pull_request_target` workflows do **not** check out PR code -- the safe pattern. Not warranted |
>
> **Not found by this analysis, and launch-blocking:** the Hangfire dashboard at `/hangfire` is
> mounted with an authorisation filter that returns `true` for everyone, gated only on an ABP Studio
> flag rather than on environment. See `remediation-plan.md` item 0.
>
> **Confirmed against the repository:** the reliability D is driven by 8 non-accessibility bugs, of
> which 4 are CRITICAL `typescript:S2871` (`.sort()` without a comparator) and **two are in slot
> generation**; `AnalysisModeSecurity` is absent; CI never passes `--locked-mode`; `ncloc` is
> 115,754. Note also that **the repository is public**, not private, which raises the stakes on
> full-history secret scanning.

---

## 0. Evidence status, and one limitation you must know about

Web access was available and was used heavily. 35 research agents ran across 14 standards areas, four
targeted questions and three triage passes, with **every cited claim re-opened and re-checked by a
second adversarial agent** instructed to default to "does not support the claim" where a page did not
clearly settle it. That verification pass caught a fabricated WCAG quotation, a wrong reading of
California statute, several thresholds attributed to pages that do not contain them, and a handful of
wrong version numbers. Those corrections are folded into what follows; the errors are not reproduced
here.

**The limitation: I did not have the repository.** The working directory available to this session
contains a personal profile README and three resume PDFs, not the scheduling portal. Every statement
about what this codebase currently does is therefore taken from `01-ci-tests-and-checks.md` and
`02-project-status.md`, not read from source.

Consequences, stated plainly rather than buried:

- The "Current state" column of the verification table is **inherited from the baseline, not
  independently verified**. Where the baseline is wrong, this document is wrong in the same place.
- Nine findings below are cases where **research contradicts the baseline**. Those are flagged in
  section 2 and each names the check that settles it. Several take under ten minutes.
- Several recommendations end in "verify X first". That is not hedging; it is the honest shape of the
  advice given that I could not run the grep myself.

The standard itself does not depend on the repository. It was researched independently and
deliberately: the 14 research agents were given the application and team context but **not** the
baseline's list of known gaps, precisely so they would not come back confirming only the gaps already
suspected.

Anything that could not be verified is marked `UNVERIFIED` inline with what was tried.

---

## 1. One page, for Monday morning

### The three things that matter most

**1. Your merge gate does not run tests, and has not for four months.**

Only `Backend: Build` and `Frontend: Build` are required. `dotnet test` runs in `deploy-dev.yml` and
`promote-staging.yml`, and neither has run meaningfully since `staging` and `production` stopped
moving on 2026-05-01. Two PRs with a failing backend test merged in the last week. Strip out every
check that is structurally incapable of failing and what remains guarding `main` is two compile jobs.

This is not primarily an "add more checks" problem. Roughly fifteen of the things in this pipeline
report success without checking anything, and they are why the gap is invisible: seventeen workflows
and a dozen green ticks read as thorough. **The fix starts with deletion.**

**2. One line of configuration would turn on the .NET security analyzers, and it is currently off.**

`Directory.Build.props` sets `AnalysisLevel=latest`. That looks like it enables the analyzers. It does
not. `AnalysisLevel` selects which *vintage* of rules ships; `AnalysisMode` decides how many are *on*,
and its default is `Default`, in which "only a small number of rules are enabled as build warnings".
Microsoft's own .NET 10 default-enabled table contains rules from Interoperability, Performance,
Reliability and Usage, and **not one rule from the Security category**. CA2100 (review SQL queries for
injection) and CA3001 (SQL injection taint) both state "Enabled by default in .NET 10: **No**".

So the entire CA2100 / CA3xxx / CA5xxx security band is dead code in your build today. The fix is
`<AnalysisModeSecurity>All</AnalysisModeSecurity>`, which turns on security rules without the
Design/Naming/Documentation noise of `AnalysisMode=All`.
([Microsoft Learn, code analysis overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview);
[CA2100](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2100);
[AnalysisMode<Category>](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#analysismodecategory);
all accessed 2026-08-28.)

**3. The Dependabot fix is a one-line deletion, and the reason is not what the baseline says.**

The baseline attributes the closed-PR loop to `target-branch` plus `open-pull-requests-limit: 0`.
Only half of that is right, and the wrong half matters. GitHub's options reference states that with
`open-pull-requests-limit`, "**Security update pull requests are not subject to this limit and do not
count toward it**". So `limit: 0` was actually a correct way to get security-only updates. The kill
switch is `target-branch`, and GitHub's security-updates guidance says explicitly that "**you should
not specify a target-branch**".

Delete the `target-branch` key and security PRs land on `main`. That is the highest
value-per-minute change available anywhere in this analysis.
([Dependabot options reference](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference);
[configuring Dependabot security updates](https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates);
accessed 2026-08-28.)

### The shape of the answer

| | |
| --- | --- |
| Workflows today | 17, of which ~15 cannot fail a merge |
| Workflows recommended | 6 to 8 PR checks |
| Required check names today | 2 (`Backend: Build`, `Frontend: Build`) |
| Required check names recommended | **1** (`ci / gate`, an aggregator over 8 jobs) |
| Launch-blocking work | **141 developer-hours** (see `remediation-plan.md`) |
| Available at one dev-week per month | ~40 hours per month |

**The honest capacity answer: the launch-blocking set is about three and a half months of the stated
budget.**
That is a finding for the business, not something to absorb by trimming the list. It is worked
through in `remediation-plan.md` section 1.

---

## 2. Where this research contradicts the baseline

The brief asked for these to be explicit. Each names the check that settles it.

| # | Baseline says | Research finds | Settle it by |
| --- | --- | --- | --- |
| 1 | **[CORRECTED 2026-08-28 -- `target-branch` did not suppress the security PRs; #417/#416 targeted `main` and were closed by the maintainer. This fixes routing, not the CVEs.]** The Dependabot loop is caused by `target-branch` **and** `open-pull-requests-limit: 0` | `limit: 0` does not affect security updates -- they are exempt by documented design. `target-branch` alone is the kill switch | Read the two GitHub docs linked above; delete one key |
| 2 | The `-warnaserror` step is "now redundant" since `Directory.Build.props` sets `TreatWarningsAsErrors=true` | Not redundant. Microsoft: `TreatWarningsAsErrors` "only impacts the C# compiler, not any other MSBuild tasks"; the `warnaserror` switch "impacts all tasks". The step is **broader** than the property | Delete the step but move `-warnaserror` onto the real build, or you lose coverage |
| 3 | Reliability D is "an accessibility debt signal" from the 330 HTML findings | Sonar's rating is set by the **single worst issue**, not volume. `Web:InputWithoutLabelCheck` is MAJOR/MEDIUM (caps at C); `MouseEventWithoutKeyboardEquivalent` is MINOR/LOW (caps at B). Neither can produce D at any count. The D comes **entirely from the 8 unexamined C#/TS bugs** | One SonarCloud filter: Reliability + High/Blocker. 20 minutes |
| 4 | The 13 CodeQL seed-contributor alerts "are almost certainly synthetic demo credentials and will triage away" | `cs/exposure-of-sensitive-information` does not detect credentials at all. It is a name heuristic (`%email%`, `%medical%`, `%social%security%number%`, ...) reaching a logger/Trace/cookie/file sink. These are near-certainly **email addresses reaching log lines** | Open one alert and read the source node name |
| 5 | **[REFUTED 2026-08-28 -- see the verification block at the top of this document. `HasFilter` is in the model config; the drift theory is dead.]** The flaky test is probably a confirmation-number collision the narrower index no longer tolerates | SQLite has supported UNIQUE **partial** indexes since 3.8.0 (2013) and EF Core's SQLite provider emits `WHERE <filter>`. So SQLite is not the limitation. ABP's test module builds schema via `IRelationalDatabaseCreator.CreateTables()` from the **model**, never from migrations -- the flake is a true-positive signal of **model/migration drift** | Check whether `Fix_UniqueIndexesExcludeSoftDeleted` put `HasFilter` in `OnModelCreating` or only in the migration file. Ten minutes |
| 6 | `AnalysisLevel=latest` is part of "the strongest single quality control in the repository" | `AnalysisLevel` governs rule vintage, not rule count. No Security-category rule is enabled by default in .NET 10 | Read the CA2100 doc page: "Enabled by default in .NET 10: No" |
| 7 | Six Angular CVEs ship to the browser | Seven in the baseline's own list (`CVE-2026-54268` and `CVE-2026-50171` are distinct CVEs with different fix versions), **plus** `CVE-2026-52725` which the baseline misses, **minus** `CVE-2026-27970` which was already fixed at 20.3.17. OSV returns 10 distinct CVEs affecting 20.3.19 | `yarn npm audit`; the count discrepancy between sources is noted in section 7 |
| 8 | Four of those CVEs are cross-request/cross-user data exposure raising them "above routine dependency noise" | Four of them (`54267`, `68945`, `50170`, `54266`) are **contingent on Angular SSR**. GHSA-39pv-4j6c-2g6v: "Applications that do not employ SSR with hydration are unaffected." If this SPA is client-rendered, they are not exploitable here | Grep for `@angular/ssr`, `server.ts`, `provideClientHydration`, and an `ssr`/`prerender` builder target. Fifteen minutes |
| 9 | CodeQL runs a matrix of `csharp` + `javascript-typescript` and has zero JS/TS findings | Zero JS/TS alerts across 27k lines, while Sonar finds 5 TS bugs and 2 TS BLOCKER vulnerabilities in the same code, is not credible as cleanliness. Because `codeql-pr.yml` is `continue-on-error`, a **failed** analysis is indistinguishable from a clean one -- no SARIF uploads and the alert count simply does not change | Open the last CodeQL run and confirm the `javascript-typescript` job actually completed and uploaded |

Item 1 changes a config edit. Items 3, 4, 5 and 8 change what the team should spend its next week on.
Item 6 is a free security win. Item 9 may mean the only real SAST in the pipeline has not been
running.

---

## 3. Verification table

The proposed standard, with this codebase's state against each item.

`PRESENT` = exists and works. `PARTIAL` = exists but incomplete. `ABSENT` = does not exist.
`MISCONFIGURED` = exists but is configured so it cannot do its job, or reports success without
checking.

### 3.1 Merge gates and branch protection

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 1.1 | Single aggregated required status check (`if: always()` over `needs`) | ABSENT | Two separate required names, both build-only |
| 1.2 | Test suite required to merge | **MISCONFIGURED** | `Backend: Test` runs and reports; it cannot block. Two failing PRs merged |
| 1.3 | Pull request required before merging | PRESENT | |
| 1.4 | At least 1 approving review | PARTIAL | Set to 1, but `enforce_admins: false` makes it optional in practice |
| 1.5 | Dismiss stale approvals on new push | UNVERIFIED | Not stated in the baseline; check the ruleset |
| 1.6 | Require approval of the most recent reviewable push | ABSENT | The only mechanical separation of duties available to a two-person team |
| 1.7 | Bypass list empty / `enforce_admins: true` | **MISCONFIGURED** | `enforce_admins: false` on an effectively single-author repo. Bypass habit already established: Sonar red on `main` since 2026-07-08, every cascade merge a named bypass |
| 1.8 | Require conversation resolution | ABSENT | `required_conversation_resolution: false` |
| 1.9 | Block force pushes | PRESENT | `allow_force_pushes: false` |
| 1.10 | Branch must be up to date before merging | PRESENT | `strict: true` |
| 1.11 | Ruleset config version-controlled and drift-detected | ABSENT | No committed baseline, no drift check |
| 1.12 | Periodic bypass review with a durable record | ABSENT | GitHub's audit log retains 180 days; HIPAA 164.316(b)(2)(i) requires six years |

### 3.2 Test strategy and depth

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 2.1 | Unit tests, backend | PRESENT | 2,261 executed, strong volume |
| 2.2 | Unit tests, frontend | PRESENT | 581 specs, all passing |
| 2.3 | Tests run as a merge gate | **MISCONFIGURED** | See 1.2 |
| 2.4 | Integration tests against the real database engine | **ABSENT** | Every test on SQLite in-memory. See question 2, section 7 |
| 2.5 | Migrations applied end-to-end in CI | **ABSENT** | ABP's harness builds schema from the model via `CreateTables()`. All 90 host + 15 tenant migrations have zero automated coverage |
| 2.6 | Migration/model drift check (`HasPendingModelChanges`) | ABSENT | One test file. Highest value per hour in the whole test area |
| 2.7 | HTTP-pipeline authorization tests (real middleware, no `AddAlwaysAllowAuthorization`) | ABSENT | ABP's template pre-wires `AddAlwaysAllowAuthorization()` and resolves services from DI, so filters, model binding, routing and multi-tenancy middleware never execute in tests |
| 2.8 | End-to-end browser tests | **ABSENT** | Nine-step booking wizard exercised only by hand |
| 2.9 | Flaky-test detection and quarantine | ABSENT | One known flake, undiagnosed, merged past twice |
| 2.10 | Mutation testing on critical paths | ABSENT | Appropriate as scheduled/advisory, not a gate |

### 3.3 Coverage

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 3.1 | Backend coverage measured | PRESENT | 52.2% |
| 3.2 | Frontend coverage measured | **MISCONFIGURED** | LCOV generated on every run, then discarded by `sonar.coverage.exclusions`. CI pays to produce a report configured away |
| 3.3 | Patch/diff coverage on changed lines | ABSENT | The only coverage metric a credible source gates on |
| 3.4 | Coverage reported on the PR | ABSENT | |
| 3.5 | Absolute coverage tracked as a trend, not a gate | ABSENT | |
| 3.6 | Coverage gate calibrated to observed reality | **MISCONFIGURED** | 80% threshold against 52.2%, non-enforcing. A gate nobody can pass teaches the team to ignore gates |

### 3.4 Static analysis

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 4.1 | Compiler warnings as errors | PRESENT | `TreatWarningsAsErrors=true` repo-wide. Genuinely strong |
| 4.2 | .NET **security** analyzers enabled | **MISCONFIGURED** | See section 1, item 2. `AnalysisMode` default enables no Security rules |
| 4.3 | Banned-API analyzer for tenancy-disabling calls | ABSENT | No SAST product has a rule for host-header cross-tenant leakage; a banned-API list is the substitute |
| 4.4 | CodeQL running and uploading | **CONFIRMED RUNNING (2026-08-28)** -- was PARTIAL / suspect | `continue-on-error` masks run failures; zero JS/TS alerts is not credible. See contradiction 9 |
| 4.5 | CodeQL as a merge gate | ABSENT | `continue-on-error: true` |
| 4.6 | SonarCloud quality gate enforcing | **MISCONFIGURED** | Disabled twice over: `continue-on-error: true` **and** `sonar.qualitygate.wait=false`. README claims it gates; it does not |
| 4.7 | New-code period usable | **MISCONFIGURED** | `previous_version` frozen at 2026-04-15, so "new code" is the whole project and all four gate conditions apply to 80k lines at once. Unpassable by construction |
| 4.8 | Suppressions inventoried and reviewed | PARTIAL | 19 `multicriteria` entries exist; no counter, no review cadence, no justification requirement |
| 4.9 | Frontend lint covering templates | PARTIAL | ESLint runs; `@angular-eslint/template/accessibility` not extended |

### 3.5 Supply chain and dependencies

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 5.1 | Dependabot security updates reaching `main` | **CORRECTED 2026-08-28: they DID reach `main`** -- was MISCONFIGURED | `target-branch` routes them to a branch 465 commits stale that has never merged. 88 alerts accumulated |
| 5.2 | Dependency audit failing the build on high/critical | **MISCONFIGURED** | `dependency-review.yml` is `continue-on-error` **and** `fail-on-severity: critical` (action default is `low`). Two safety catches on a gate that was already advisory |
| 5.3 | NuGet audit as an error in CI | PARTIAL | `NuGetAuditMode` defaults to `all` on net10.0+, so restore already computes transitive vulnerability data -- and discards it as warnings |
| 5.4 | Lockfile enforcement in CI | PARTIAL | `RestorePackagesWithLockFile=true` but `RestoreLockedMode=false`. Fine **if** CI passes `--locked-mode`; unverified |
| 5.5 | Yarn immutable install | UNVERIFIED | Implicit default in CI; assert it explicitly |
| 5.6 | Yarn install scripts disabled | UNVERIFIED | `enableScripts: false` is the Yarn 4.14+ default but upgraded projects are migrated with an explicit `true`. Check `.yarnrc.yml` -- this is the exact propagation vector every Shai-Hulud variant used |
| 5.7 | Actions pinned to full commit SHA | **ABSENT** | 68 OpenSSF `PinnedDependenciesID` alerts |
| 5.8 | Workflow security linting | ABSENT | No zizmor, no actionlint |
| 5.9 | SBOM generation | ABSENT | Not legally required here; see section 6.5 |
| 5.10 | Container image scanning | ABSENT | Base images never scanned |
| 5.11 | Dockerfile linting | ABSENT | |

### 3.6 Application security testing

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 6.1 | Secret scanning that can block | **MISCONFIGURED** | Three scanners, none blocking. Both husky hooks **fail open** when `gitleaks` is not on PATH -- the machine most likely to lack the binary is the new laptop whose commits then go unscanned |
| 6.2 | Response-header and cookie contract tests | ABSENT | Ten ASVS requirements for half a day; best ratio in the whole security area |
| 6.3 | File-upload contract tests | ABSENT | Anonymous upload is the most dangerous new surface being exposed |
| 6.4 | DAST baseline scan | ABSENT | |
| 6.5 | Authenticated API scan | ABSENT | |
| 6.6 | Security hotspots triaged | **ABSENT** | 0 of 31 reviewed. Drives `security_review_rating` to E |
| 6.7 | Rate limiting on anonymous surfaces | PARTIAL | Password reset, public upload and external-account endpoints only. Login and registration unthrottled |

### 3.7 Performance

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 7.1 | Bundle budgets | **MISCONFIGURED** | 2 MB warn / 2.5 MB error is the Angular scaffold default, not a measured number. No lazy-chunk budget. An unowned number guarding nothing |
| 7.2 | Core Web Vitals measured | ABSENT | No measurement of any kind has ever been taken |
| 7.3 | Lighthouse CI on anonymous routes | ABSENT | |
| 7.4 | Bundle analysis documented for diagnosis | ABSENT | A size gate without a diagnosis tool is a gate people disable |

### 3.8 Accessibility

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 8.1 | `@angular-eslint` template accessibility rules | **ABSENT** | The single reason 253 unlabelled inputs exist without any lint failure |
| 8.2 | axe on anonymous flows | ABSENT | |
| 8.3 | Manual keyboard + screen-reader pass per release | ABSENT | Catches what no scanner reports |
| 8.4 | Published accessibility statement / VPAT | ABSENT | The artefact procurement actually asks for |

### 3.9 Multi-tenant isolation

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 9.1 | Tenant isolation test suite exists | **PRESENT** | 20 files under `MultiOffice/` incl. an isolation matrix and a self-validating harness. Genuinely the strongest area |
| 9.2 | Isolation proven against real separate databases | **ABSENT** | Proven against one in-memory SQLite database. The mechanism the whole model rests on is tested against a fake |
| 9.3 | Host-header spoofing / resolver-precedence tests | ABSENT | ABP registers query-string, route, header and cookie `__tenant` resolvers by default. "Host header only" must be **proven**, not assumed |
| 9.4 | Connection-string routing tests | ABSENT | |
| 9.5 | Background-job tenant context | ABSENT | ABP derives job tenant from args only when the args type implements `IMultiTenant`; otherwise it falls back to ambient, which in a worker is null -- the host database |
| 9.6 | Redis cache key tenant-prefix audit | ABSENT | Only ABP's typed `IDistributedCache<T>` adds the prefix; raw `IDistributedCache` produces keys shared across all 11 offices |
| 9.7 | Static deny-list for isolation-disabling constructs | ABSENT | `DataFilter.Disable<IMultiTenant>()`, `[IgnoreMultiTenancy]`, `IsMultiTenant = false`. One added line, no reviewer to catch it |
| 9.8 | Per-tenant data fingerprint check | ABSENT | Catches the failure mode that reports success: a silent connection-string fallback writes to the wrong database with no exception |

### 3.10 CI/CD pipeline security (added; not in the brief)

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 10.1 | Least-privilege `permissions:` in every workflow | ABSENT | 4 high-severity `TokenPermissionsID` Scorecard alerts |
| 10.2 | No `pull_request_target` with untrusted content | **MISCONFIGURED** | `labeler.yml`. Write permission to the target repo plus access to repo secrets, for auto-labelling |
| 10.3 | Actions pinned to SHA | ABSENT | See 5.7 |
| 10.4 | Workflow static analysis | ABSENT | |
| 10.5 | Deploy secrets behind an Environment with required reviewers | ABSENT | A same-repo branch PR currently receives all repository secrets before any review |

### 3.11 HIPAA-relevant code and CI controls (added; not in the brief)

| # | Check in the standard | State | Note |
| --- | --- | --- | --- |
| 11.1 | Audit-trail coverage tests, including **read** paths | ABSENT | 164.312(b) is Required with no implementation specifications. Writes come nearly free from EF change tracking; reads are the gap and reads are what OCR asks about |
| 11.2 | No PHI in application logs | **ABSENT and actively violated** | See section 6.6 |
| 11.3 | Transport/session security assertions | ABSENT | |
| 11.4 | Compliance evidence retained 6 years | ABSENT | GitHub caps private-repo artifact retention at 400 days, defaults to 90. 164.316(b)(2)(i) requires six years |
| 11.5 | Addressable-specification decision records | ABSENT | 164.306(d)(3)(ii) does not permit silently skipping an addressable specification |

**Totals: 12 PRESENT or PARTIAL-acceptable, 16 MISCONFIGURED, 46 ABSENT.** The MISCONFIGURED column
is the one to read first -- those are the items currently reporting success.

---

## 4. The REMOVE list

The brief was right that this list would be non-empty, and it is longer than the three candidates
named. Of roughly 23 removal candidates, **about 15 are genuinely harmful false assurance** rather
than clutter.

They nearly all reduce to one mechanism on four surfaces. GitHub's workflow syntax reference states
step-level `continue-on-error` "Prevents a job from failing when a step fails" and job-level
"Prevents a workflow run from failing when a job fails". GitHub's protected-branches documentation
states required checks need only "a successful, skipped, or neutral status". Together: **every
`continue-on-error` check in this repo emits a status the merge gate cannot distinguish from a
genuine pass.** The same inversion reappears without `continue-on-error` in the spec-file guard
(missing tests become a pass), in the hooks (missing binary becomes a pass), and in prose (a README
asserting a gate that does not gate).
([workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax);
[about protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches);
accessed 2026-08-28.)

NIST SSDF PO.3.3 requires tools be configured to generate artifacts of their support of secure
development practices, and defines an artifact as "a piece of evidence", evidence being "grounds for
belief or disbelief". **This pipeline generates grounds for belief where none exists.** That is why
these are worse than an empty `.github` directory.

| # | Remove | Why it is worse than nothing | Effort |
| --- | --- | --- | --- |
| R1 | **`gitleaks` silent degradation** in `.husky/pre-commit` and `.husky/pre-push` | Prints a warning and exits 0 when the binary is absent. Nothing distinguishes "scanned, clean" from "not scanned". The machine most likely to lack it -- new laptop, fresh clone -- is the one whose commits go unscanned. All three downstream secret scanners are also non-blocking, so nothing catches it | 1-2 h |
| R2 | **`target-branch` in `dependabot.yml`** | Routes every security PR to a branch 465 commits stale that has never merged. Dependabot appears to be working -- it opens PRs -- while 88 alerts accumulate. Keep `open-pull-requests-limit: 0`; it is correct and does not affect security updates | 30 min |
| R3 | **`ci.yml:248` spec-file guard** | Skips the frontend test step with a warning if no `*.spec.ts` is found. Dead today, and dangerous precisely because it is dead: a deleted spec folder, a glob change during a reorg, a Yarn PnP resolution problem or a partial checkout each produce a green `Frontend: Build` -- one of only two required checks | 30 min |
| R4 | **`doc-check.yml`** | Entire body commented out; only live step is an echo. Posts a green tick named as though it validates documentation. Its cost is not compute -- it inflates the apparent check count | 10 min |
| R5 | **`continue-on-error` on the two Format steps inside `ci.yml`** | Worse than the standalone masked workflows: these live *inside* the only two required checks. A formatting failure is absorbed into a green `Backend: Build`. Not a check that does nothing -- a check that ran, failed, and reported pass | 1-2 h |
| R6 | **`continue-on-error` on `codeql-pr.yml`** | Hides *run* failures, not just findings. If autobuild fails against a .NET 10 / ABP solution or extraction OOMs, no SARIF uploads and the alert count does not move. Zero alerts then reads as clean rather than never-analysed. This is the likeliest explanation for contradiction 9 | 2-3 h |
| R7 | **`continue-on-error` + `fail-on-severity: critical` on `dependency-review.yml`** | Disabled twice. The action's default is `low`; someone moved it to `critical`, discarding low/moderate/high -- where the overwhelming majority of real advisories land | 1 h |
| R8 | **`continue-on-error` + `qualitygate.wait=false` on `sonarcloud.yml`, and the README sentence** | Also disabled twice, which is the tell that nobody was sure which switch was doing the work. The README states "SonarCloud is live and gates new-code coverage on PRs". In a HIPAA context a written attestation that a control operates when it does not is the worst item on this list | 1 h or 1-2 d (see section 6.4) |
| R9 | **`trufflehog-pr.yml`** | The third secret scanner, none of which can fail a merge. Three overlapping non-blocking scanners create a diffusion of responsibility measurably worse than one blocking scanner: each looks like backup for the others, so nobody checks whether any is load-bearing. None is | 1 h |
| R10 | **`labeler.yml`** | Runs on `pull_request_target`, which has write permission to the target repository and access to its secrets. Mitigated today by being private with no forks, but it is a privileged trigger spending real risk on auto-labelling | 30 min |
| R11 | **`commitlint.yml`** | Duplicates the `commit-msg` hook, cannot fail, and adds a row to the checks list. Merely useless rather than dangerous, but every meaningless tick raises the price of noticing a meaningful one | 15 min |
| R12 | **`pr-size.yml`** | Third enforcement point for message/size convention on a two-author repo | 15 min |
| R13 | **markdownlint half of `lint-meta.yml`, and the "Phase C flips them to blocking" comment** | The comment converts an indefinitely deferred intention into something a reader parses as a scheduled control. There is no Phase C at one dev-week a month. **Keep yamllint**, scoped to `.github/**` and blocking -- a malformed workflow file does not fail loudly, it silently does not run | 2 h |
| R14 | **`ci.yml:100-106` `-warnaserror` step** | Remove the step, but see contradiction 2: move the switch onto the real build rather than dropping it, or you lose MSBuild-task coverage you have today | 2-4 h |
| R15 | **`promote-staging.yml`, `auto-pr-dev.yml`** | Dormant since 2026-05-01. They carry credentials and permissions nobody watches, will fail confusingly if triggered, and make the repo look like it has release discipline going into a first public launch | 2-4 h |
| R16 | **`deploy-dev.yml`, or its name** | Two lies stacked. The name implies automated gated deployment; there is none. More seriously, this and `promote-staging.yml` are the **only** places `dotnet test` runs, and neither has run since 2026-05-01. Extract `dotnet test` into `ci.yml` first | 1-2 d |
| R17 | **`sonar.coverage.exclusions` for `angular/src/**`** | CI generates a frontend LCOV on every run and the scanner throws it away. A single percentage is displayed and read as project coverage while silently omitting 27k lines. Either publish it or stop generating it; the halfway state is the problem | 2-3 h |
| R18 | **Sonar `previous_version` new-code period** | Frozen at 2026-04-15, so the window has widened for four months. All four Sonar way conditions now apply to 80k lines at once. Unpassable by construction, which is precisely how a two-person team learns to ignore a tool. Switch to reference branch = `main` (no version-bump discipline to rot) | 1 h |
| R19 | **The dead endpoint carrying wildcard CORS** | Delete the endpoint, not just the header. Precise about exposure: per MDN the wildcard is refused by browsers for credentialed requests, so this is not a direct credentialed-read hole -- but an unused route on a service about to face the internet is attack surface regardless | 2-4 h |
| R20 | **`docs/devops/TESTING-STRATEGY.md`** | The "last verified 2026-06-01" stamp is the harmful part. Readers discount stale docs automatically; a recent verification date defeats that discount. It still references the ABP "Books" sample, so it has probably never described this app's tests -- the stamp attests to something never checked. Documentation-layer `continue-on-error` | 1 h |
| R21 | **README Node 20 badge** | Three Node versions across the repo (badge 20, `ci.yml` 22, `security.yml` 20) | 2 h |
| R22 | **`pre-push` full-tree `gitleaks detect`** | Rescans unchanged history on every push. The slowest hook, and what pushes developers to `--no-verify` -- which disables the staged scan that actually matters | included in R1 |
| R23 | **19 Sonar `multicriteria` suppressions** -- review, do not mass-delete | Keep the ABP DI and permission-string entries; those fire on framework shape. **Hand-review the email-template HTML ones**: HTML rules suppressed on email templates can mask escaping problems, and this application emails content around workers'-comp medical examinations | half day, after R8 |

**What the removals reveal is bigger than any single removal.** Strip out everything that cannot
fail, and the merge gate is two build jobs that execute no tests at all. That gap was invisible
because seventeen workflows and a dozen green ticks read as a thorough pipeline.

---

## 5. The recommended check set

### 5.1 The required list (answer to question 3)

**One required status check name.** Not two, not eight.

```text
Required status check:  ci / gate
```

`ci / gate` is an aggregator job declaring `needs: [backend-build, backend-test, frontend-build,
frontend-test, secrets, deps-audit, tenant-isolation, workflow-audit]`, running with `if: always()`,
failing unless every needed job result is `success` or `skipped`.

This is the highest-leverage single decision in the whole exercise and it costs one extra job. It
removes three failure modes at once: required-check names orphaned by a job rename (which blocks
every PR until an admin notices); path-filtered workflows sitting in Pending forever, which GitHub's
own documentation warns against; and the political cost of arguing about the required list every time
a job is added.

The eight jobs behind the gate:

| Job | What it does | Why it earns a gate slot |
| --- | --- | --- |
| `backend-build` | `dotnet build` with `-warnaserror` | Already exists; add the switch from R14 |
| `backend-test` | `dotnet test` (SQLite suite) | **Currently exists and cannot block. This is the single biggest gap** |
| `frontend-build` | `ng build --configuration production` incl. bundle budgets | Already exists |
| `frontend-test` | `ng test --no-watch` | Currently skippable via R3 |
| `secrets` | gitleaks on the PR diff, hard-failing | One blocking scanner replacing three advisory ones |
| `deps-audit` | NuGet audit NU1903/NU1904 as errors + `yarn npm audit --severity high` | Nearly free: restore already computes this data on net10.0+ and discards it |
| `tenant-isolation` | The cross-tenant suite against two real databases | The only check whose failure mode is a reportable breach across 11 offices |
| `workflow-audit` | zizmor + actionlint, scoped to `.github/workflows/**` | Fires on the handful of PRs that touch CI; invisible otherwise |

Plus branch ruleset rules, which are not status checks:

```text
Ruleset "main-protected", enforcement Active, bypass list EMPTY:
  - Require a pull request before merging
      - 1 required approval
      - Dismiss stale approvals when new commits are pushed
      - Require approval of the most recent reviewable push
      - Require all conversations resolved before merging
  - Require status checks to pass:  ci / gate
      - Require branches to be up to date before merging
  - Block force pushes
  - Restrict deletions
```

`Require approval of the most recent reviewable push` is the one people skip and the one that matters
most here: it means the person who last pushed cannot be the approver. That is the only mechanical
separation of duties available to a team of two.

Note that CIS Software Supply Chain Security 1.1.3 asks for two approvers, which is **arithmetically
impossible** at this team size because GitHub forbids self-approval. NIST SSDF PW.7.1 explicitly
leaves the human-versus-automated review mix to the organization, so one approval plus a real
automated gate is the defensible reading. Say so in writing rather than being asked about it later.

**Why one required check and not thirty.** DORA found no evidence that heavier external approval
lowers change failure rate. The CI Theater study of 1,270 projects and 534,417 builds found 85% had
at least one build broken for more than four days, with the smallest projects averaging 40 days to
fix. Gates that are slow or ignorable decay into theatre -- and this repo has already established the
bypass habit, merging past a red Sonar check since 2026-07-08.

### 5.2 What is deliberately advisory

CodeQL, SonarCloud, coverage, Lighthouse, axe on authenticated screens, DAST, mutation testing.

These must **exist**, and several are launch-blocking in the sense that they must have been run and
triaged once before anonymous traffic arrives. None should block a merge on findings. A required
check with an unclearable backlog is the fastest available route to bypass culture, and this team
already has one of those in SonarCloud.

The distinction the plan uses throughout: `gate_or_advisory` is about **merge mechanics**;
`launch_blocking` is about **whether it must be done before going public**. They are independent.

---

## 6. Per-area detail

### 6.1 Merge gates and branch protection

**The standard.** One protected default branch, changes only via pull request, a small number of
required checks that always report, and no silent bypass path. GitHub has consolidated this into
repository rulesets; an automatic branch-protection-to-ruleset migration tool shipped 2026-08-11 and
classic branch protection has no announced sunset.
([GitHub Changelog](https://github.blog/changelog/2026-08-11-automatically-migrate-branch-protection-rules-to-repository-rulesets/);
[available rules for rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets);
accessed 2026-08-28.)

**The one thing to know:** classic branch protection does **not** apply to repository admins unless
you explicitly enable "Do not allow bypassing the above settings". Rulesets invert this -- enforcement
is the default and bypass is an explicit list you populate. On a two-person repo where both
developers are admins, classic branch protection configured without that checkbox protects nobody.
This is the most common false sense of security in small-team GitHub setups, and `enforce_admins:
false` says it is present here.

**The gap.** Everything in 3.1. The critical items are 1.2 (tests cannot block), 1.7 (bypass) and 1.6
(most-recent-push approval).

**HIPAA's actual position:** it names no gate. 164.308(a)(1)(ii)(B) requires "security measures
sufficient to reduce risks and vulnerabilities to a reasonable and appropriate level"; 164.312(b)
requires audit controls; 164.316(b)(2)(i) requires the documentation be retained six years. So what
matters for compliance is that the gate config is versioned, its bypasses are reviewable, and the
record outlives GitHub's 180-day audit log. Pick rulesets, commit the JSON, and add a monthly
20-minute bypass review that writes its result -- including "no bypasses" -- to a file in the repo.

### 6.2 Test strategy and depth

**The standard.** Microsoft's position on EF Core testing is unusually strong and worth quoting
because it settles question 2: "we recommend either writing your tests against your real database,
or if using a test double is an absolute necessity, taking onboard the cost of a repository pattern",
and "testing against SQLite does not guarantee the same results as against SQL Server".
([Choosing a testing strategy](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy),
accessed 2026-08-28.)

**The gap that is not about SQLite.** The more serious finding is structural. ABP's
`*.EntityFrameworkCore.Tests` module builds the test schema with
`context.GetService<IRelationalDatabaseCreator>().CreateTables()`, which generates schema **from the
EF Core model, not from migrations**. So all 90 host and 15 tenant migrations -- the artifacts that
actually create your production databases -- have zero automated coverage, and the schema under test
is a SQLite-flavoured artifact no environment ever runs.
([ABP migrations docs](https://raw.githubusercontent.com/abpframework/abp/dev/docs/en/framework/data/entity-framework-core/migrations.md),
accessed 2026-08-28.)

**The second gap: authorization is not tested through the pipeline.** ABP's startup template pre-wires
`AddAlwaysAllowAuthorization()` into integration tests and resolves application services from DI
rather than over HTTP. Filters, model binding, routing and the multi-tenancy middleware therefore
never execute in any of your 2,261 tests. For an app about to expose anonymous registration, login,
password reset and document upload, this is the test that should exist and does not.

### 6.3 Coverage

**The standard.** There is no universal number, and saying otherwise would be inventing a standard.
No regulatory source (HIPAA, HITRUST, FDA) specifies a coverage percentage for software of this kind.
What credible sources converge on is *patch coverage*: measure the lines this PR changed. Sonar's
default is >=80% on new code explicitly to avoid "spending a lot of effort remediating old code".

**The gap.** Three distinct problems, and only one is "the number is low":

1. The frontend LCOV is generated and thrown away (R17). You are paying for a report you configured
   away.
2. There is no patch-coverage measurement at all -- the only metric worth gating.
3. The 80% gate against 52.2% actual, non-enforcing, is worse than no gate. Start advisory, print the
   number on every PR for four to six weeks, then set the threshold at your observed median rather
   than importing someone else's 80%.

**Note on the free-tier ceiling:** one research pass suggested this project may approach SonarQube
Cloud's 50k-LOC private-analysis cap. The verification pass corrected the arithmetic -- Sonar's LOC
count excludes test code, comments, blank lines and unsupported languages, so a raw line count
overstates it and the conclusion may reverse. `UNVERIFIED`; check the actual `ncloc` Sonar reports
(the baseline says 115,709 across all languages) against your plan before assuming either way.

### 6.4 Static analysis

**The standard.** SAST that runs, uploads, and whose *failure to run* is visible. A gate on new
findings only, not on the accumulated backlog.

**The three gaps, in value order:**

1. **`AnalysisModeSecurity`** -- section 1, item 2. One line. Caveat before gating on it: CA3001-class
   taint rules "can't track data across assemblies" and have a configurable interprocedural depth
   limit, so in a layered ABP solution they will miss cross-project flows. Treat them as warnings on
   the HTTP-facing projects first, not a hard gate on day one.
2. **`continue-on-error` on CodeQL** (R6) -- it hides run failures, which may be why there are zero
   JS/TS alerts.
3. **Sonar's third state** (R8, R18) -- it is neither enforcing nor deleted. Pick one. Deleting it is
   defensible at this size: CodeQL plus the newly-enabled Roslyn security analyzers is real coverage.
   If keeping it, the order matters: fix the new-code period, remove the coverage exclusions, clear
   the red on `main`, **then** set `qualitygate.wait=true` and remove `continue-on-error`. Reversing
   that order blocks you on day one and you will reach for admin bypass.

**A disagreement between two of my own research passes, surfaced rather than resolved silently.** The
SAST area concluded `security-extended` is non-negotiable because three C# queries that matter here
are extended-only. The CodeQL triage pass recommended dropping to the default suite, since GitHub's
docs say `security-extended` "may return a greater number of false positive results" and a team of
two drowning in low-precision alerts stops reading all of them. **My reading: keep
`security-extended`.** The deciding fact is empirical -- the repo already runs it and has 23 alerts,
which is a manageable afternoon. Revisit if the number grows past ~50 once JS/TS analysis is
actually running.

### 6.5 Supply chain

**The standard.** Pinned actions, lockfiles enforced in CI, an audit that fails the build on
high/critical, and dependency updates that actually reach the default branch.

**SBOM: not legally required here, and I want to be precise rather than cautious.** This application
is not a medical device, so FDA premarket SBOM requirements do not reach it. Executive Order 14028
and CISA guidance bind federal procurement, not a private workers' comp SaaS. CISA itself notes the
SBOM delivery model is unsettled for SaaS. Generate one on tagged releases as a workflow artifact
because it is three hours and it pays for itself the day a CVE lands four levels deep -- but do not
let anyone tell you it is a compliance obligation here.

**The gap.** R2 is the headline. Beyond it: 68 unpinned actions, no workflow linting, no container
scanning, and `.yarnrc.yml` `enableScripts` unverified. That last one deserves two minutes of your
attention: `enableScripts: false` became the Yarn 4.14 default, but projects upgraded across that
release are migrated with an explicit `enableScripts: true` written into the file. Install scripts are
the exact propagation vector every Shai-Hulud variant used.

### 6.6 Application security testing, and the finding set triage

**On the 338 "bugs":** covered in contradiction 3. The 330 accessibility findings contribute nothing
to the D rating; the 8 unexamined C#/TS bugs contribute all of it. Say it to the team in one sentence:
*D does not mean 338 defects, it means one bad one, and you have not found it yet.*

**On the 20 vulnerabilities (security rating E):** this is real and correctly earned -- the rating is
"E = at least one blocker vulnerability" and there are four blockers. The baseline under-weights the
right one:

- **`tssecurity:S6105` at `angular/src/tenant-bootstrap.ts:65` -- read this line before anything
  else.** Three things stack. It is a *taint* rule, so the engine found an actual source-to-sink path,
  which is much harder to trigger spuriously than a pattern match. The file is on the tenancy
  resolution path of an app whose tenancy comes from the Host header, with one PHI database per
  office. And it is days from public exposure. Fix with a server-side allow-list of the 11 office
  hosts, not with URL sanitisation.
- **`csharpsquid:S2068` on the seed contributors** is only MAJOR in Sonar's general scale, but in an
  ABP multi-tenant app "hard-coded credential in a data seed contributor" plausibly means a default
  admin password sitting in some subset of 11 tenant databases. Per-tenant database isolation does
  nothing about it, because the attacker would be legitimately inside a tenant. Query the databases;
  do not assume. Rotate now, while rotation is free.

**On the 31 hotspots:** the distinction the team is missing is that **a hotspot is a question, not an
accusation**. Sonar's own words: "a security-sensitive piece of code is highlighted, but overall
application security may not be impacted; it's up to the developer to review." They contributed zero
to the E rating -- `security_rating` counts only vulnerabilities. They drive
`security_review_rating`, where 0 of 31 puts you at E.

"Review" is a defined three-step procedure, not open-ended analysis. **31 of them is about three
hours in one sitting with both developers at one screen** -- roughly 12 minutes for the first instance
of each distinct rule, then 1-2 minutes for repeats. It is a half-day once, not a programme, and it
takes `security_review_rating` from E to A.

The 6 csrf hotspots are very likely false positives on a bearer-token API surface: Microsoft's
antiforgery docs treat CSRF as a cookie-credential problem and bearer tokens as protection against
it, and ABP applies `AbpAutoValidateAntiforgeryToken` globally while bypassing non-browser bearer
clients. The genuine exception is the AuthServer's cookie-authenticated Razor Pages -- which is
exactly the surface about to face the internet. Verify that class with one curl (cookie, no
antiforgery token, expect rejection) rather than triaging six items separately.

**On the 23 CodeQL alerts:** roughly a third is real, but the set is more useful as a diagnostic than
a work list. All three C# rules fire through name-based heuristics reaching one narrow sink set
(logger call, Trace, cookie, local file write). That single fact reframes everything:

- The 21 non-log-forging alerts are almost certainly **one underlying defect repeated** -- identifiers
  containing `email` or `password` being written to log statements.
- The "cleartext storage" alerts on the emailer have nothing to do with sending email. `MailMessage`
  subject/body is a `RemoteFlowSink`, a different hierarchy from the `ExternalLocationSink` this query
  consumes. Something is being **logged**.
- The seed-contributor alerts are cosmetic as alerts, but the question behind them is not: ABP data
  seeding **runs in production via DbMigrator on every deploy, and with database-per-tenant it also
  runs for every new office**. Unconditionally-registered `Demo*` contributors would seed demo
  accounts and synthetic patients into live tenant databases. Audit where they are registered; the
  robust fix is to not compile them into the deployed artifact at all.
- **CodeQL has no rule for the thing that actually matters to you.** Log forging is source-driven with
  no PHI concept; the exposure rule's 13-term name list cannot see patient name, DOB, claim number,
  injury description or appointment time. That intersection -- request-supplied PHI reaching a log --
  is CWE-532 and has no dedicated C# query. **Treat "18 exposure alerts" as a floor, not a count.**

The durable control is not tooling: enable CA2254 as a build warning so interpolated log templates
stop appearing, and write a one-paragraph "never log" rule citing the OWASP Logging Cheat Sheet. A
larger company would adopt `Microsoft.Extensions.Compliance.Redaction` with a formal data
classification taxonomy; that is a real refactor and not worth it here yet.

### 6.7 Performance

**The standard, fetched not remembered.** Core Web Vitals in 2026 are LCP, INP and CLS, with INP
having replaced FID. Thresholds and the percentile they are assessed at are on
[web.dev/vitals](https://web.dev/articles/vitals) (accessed 2026-08-28).

**The honest recommendation for this team:** the only performance check worth gating is the one that
is fully deterministic -- **bundle size**. The same commit produces the same bytes on any runner, so
it will never flake and therefore will never be bypassed. Timing metrics should be measured and
printed on every PR so the trend is visible, but a noisy shared runner must not be able to block a
merge.

**The live defect:** the current 2 MB warn / 2.5 MB error is the Angular scaffold default, so the team
has an unowned number guarding nothing, and there is no lazy-chunk budget at all. Replace it with
budgets derived from *today's measured* output plus a small headroom allowance. That converts an
inherited default into a real ratchet.

One caveat found during verification and worth knowing: **a bundle budget whose name matches no chunk
evaluates to zero bytes and silently passes.** Check your budget names against actual chunk names.

### 6.8 Accessibility

**The standard.** WCAG 2.2 Level AA. Unlabelled form controls fail SC 3.3.2 (Level A -- the floor, not
the aspiration) plus SC 1.3.1 and 4.1.2.

**The legal position, stated carefully because the verification pass caught two errors here.** This is
not legal advice; get counsel before relying on it.

- **DOJ's April 2024 rule applies to Title II entities** -- state and local government -- with
  compliance beginning **April 26, 2028** for larger entities. A private company serving workers' comp
  is not a Title II entity. It does not bind you directly.
- **California Government Code 7405 does not flow Section 508 down to private contractors.** 7405(a)
  binds "state governmental entities". An earlier research pass claimed otherwise and the verification
  pass corrected it. Do not repeat that claim.
- **Section 1557 of the ACA** is the most plausible direct route and turns on whether the entity
  receives Federal financial assistance. Worth asking counsel specifically.
- **ADA Title III via the Unruh Act in California** is the realistic litigation exposure, and it is
  genuinely contested: *Robles v. Domino's* rests on a nexus between the website and a physical place
  of public accommodation, and itself distinguishes *Weyer v. Twentieth Century Fox*, where the Ninth
  Circuit held an insurance company administering an employer-provided policy was **not** a covered
  place of public accommodation. Where you fall is a real legal question, not a settled one.

**The engineering answer does not depend on resolving that.** Six anonymous-reachable pages are what
generate a demand letter, and fixing them is a half-day.

**The gap and the fix.** `@angular-eslint`'s template accessibility preset -- **11 rules** (verified;
an earlier count of 12 including `no-positive-tabindex` was wrong) -- is not extended, which is the
entire reason 253 unlabelled inputs exist without a lint failure. Enable it with ESLint bulk
suppressions so day one is green and every *new* template is held to the rules. That is prevention,
which is what you actually need; the 253 existing ones are a backlog to drain during normal feature
work.

**Before touching any of the 253, spend 30 minutes.** Open the top file, pick five flagged controls,
and read the computed accessible name in Chrome DevTools' Accessibility pane. If `mat-form-field` is
generating the association at runtime, that whole class is a false positive and should be dismissed
with a recorded justification -- not bulk-fixed.

One tooling trap worth knowing: **axe-core's WCAG 2.2 rules are disabled by default**, "until WCAG 2.2
is more widely adopted and required". Passing an axe scan does not mean WCAG 2.2 conformance unless
you explicitly enable the `wcag22aa` tag.

### 6.9 Multi-tenant isolation

**Genuinely your strongest area, and it is testing the wrong thing.** 20 files under `MultiOffice/`
including an isolation matrix and a self-validating harness represents real care. But it all runs
against a single in-memory SQLite database, so it proves isolation **against a fake**. The mechanism
the entire security model rests on -- per-tenant connection-string routing to physically separate
databases -- has no coverage at all.

**What a rigorous standard adds**, beyond what unit tests can prove:

1. **Resolver precedence.** ABP registers query-string, route, header and cookie `__tenant` resolvers
   by default. "Host header only" is a claim about configuration that must be *proven*: fire the same
   authenticated request with `?__tenant=`, the `__tenant` header, the `__tenant` cookie and a route
   segment, and assert the resolved tenant does not change. Expect this test to fail on first run.
2. **Host filtering.** ASP.NET Core's host filtering middleware is disabled unless `AllowedHosts` is
   set; Kestrel "largely ignores the host name". An unknown Host must be rejected, not silently mapped
   to a default tenant.
3. **Two real databases.** Prove a query under tenant A physically hits A's database, and that data
   written under A is absent from B's at the SQL level. Two tenants plus the host proves every routing
   rule that 33 would -- do not provision all 11.
4. **Background jobs.** ABP derives job tenant from args only when the args type implements
   `IMultiTenant`; otherwise it falls back to the ambient tenant, which in a worker is null -- the
   host database. ABP's background-jobs documentation never mentions tenants, so nothing warns you.
   One reflection-driven test over every `IBackgroundJob<T>` closes this in two hours.
5. **Redis keys.** Only ABP's typed `IDistributedCache<T>` adds the tenant prefix. Injecting
   Microsoft's raw `IDistributedCache` produces unprefixed keys shared across all 11 offices, and
   neither the compiler nor code review will tell you.
6. **A static deny-list** for `DataFilter.Disable<IMultiTenant>()`, `[IgnoreMultiTenancy]`,
   `BlobContainerConfiguration.IsMultiTenant = false` and writes to `AbpDataFilterOptions`. With no
   senior reviewer, a single added line is exactly what review will miss -- and ABP's docs present
   these as ordinary configuration options without flagging them as security-critical.
7. **A per-tenant data fingerprint**, scheduled. This catches the failure mode that reports success:
   when `MultiTenantConnectionStringResolver` falls back to the default connection string, no
   exception is thrown -- the data simply lands in the wrong database.

### 6.10 CI/CD pipeline security (added)

Covered in 3.10 and R10. The `pull_request_target` finding is the one nobody on the team is likely to
know about: GitHub Security Lab documents that such workflows "have write permission to the target
repository" and "have access to target repository secrets", running in the target repository's
context rather than the merge commit. That is the pwn-request class. Mitigating factors here are
genuine -- private repo, two developers, no forks -- so this is a risk-versus-value judgement rather
than an emergency, and the value side of auto-labelling is close to zero.

The item the team almost certainly does not know they need: **a GitHub Environment holding deploy
secrets behind required reviewers.** Today a pull request from a same-repo branch receives all
repository secrets before any review. Everything else on the pipeline-security list assumes an
external attacker; this one assumes a stolen laptop or a compromised account belonging to one of two
developers -- a realistic scenario for a HIPAA system.

### 6.11 HIPAA mapped to code and CI (added)

Because the team will have to defend these choices, here is what is actually **Required** by
regulation versus what is industry practice. Not legal advice.

| 45 CFR | Standard | Status | What it implies for code/CI |
| --- | --- | --- | --- |
| 164.312(a)(1) | Access control | Required | The tenant-isolation suite. A missed data filter is a breach across 11 offices |
| 164.312(a)(2)(i) | Unique user identification | Required | Attributability in audit records is not negotiable |
| 164.312(a)(2)(iii) | Automatic logoff | Addressable | Needs a documented decision if not implemented |
| 164.312(b) | Audit controls | **Required, no implementation specifications** | Audit-trail tests including **read** paths. Writes come nearly free from EF change tracking; reads are the gap and reads are what OCR asks about |
| 164.312(e)(2)(ii) | Encryption in transit | Addressable | Moving from an office LAN to the public internet is precisely the risk-analysis change that makes this "you must" |
| 164.306(d)(3) | Addressable specifications | Required process | You may not silently skip one. Document why, and implement an equivalent where reasonable |
| 164.308(a)(8) | Periodic technical evaluation | Required | NIST SP 800-66r2 names "the outputs of automated tools" as a valid collection method -- your CI output is admissible evidence |
| 164.316(b)(2)(i) | Documentation retention | Required | **Six years.** GitHub caps private-repo artifact retention at 400 days and defaults to 90. Every scan you run and let expire is evidence you will not have |

**The January 2025 NPRM.** HHS issued a proposed rule (dated 2024-12-27, published in the Federal
Register in January 2025) that would make several currently-addressable specifications Required and
would add explicit cadences for vulnerability scanning and penetration testing. **Its status in 2026
was not conclusively established by this research and is marked `UNVERIFIED`** -- treat the proposed
cadences as a planning signal, not a current obligation, and confirm status with counsel. One
correction from the verification pass: the proposed MFA requirement is not absolute; proposed
164.312(f)(2)(iii) contains an explicit Exceptions paragraph.

Two practical consequences worth acting on regardless of the NPRM's fate: a monthly scheduled DAST
baseline beats the proposed six-month cadence at almost no cost, and a compliance-evidence export job
solves the six-year retention problem that GitHub's artifact expiry otherwise guarantees you will
fail.

---

## 7. The three questions

### Question 1: Does ABP Commercial 10.0.2 support Angular 20.3.27?

**Answer: qualified yes. The blocker is stale, and as written it is self-refuting.**

ABP 10.0.x targets the Angular 20 **major** and specifically the 20.0.x line. ABP's own 10.0 quick
start says "In this version ABP uses Angular 20.0.x version", and the 10.0.2 Angular app template pins
every `@angular/*` package at `~20.0.0`.
([ABP 10.0 quick start](https://abp.io/docs/10.0/framework/ui/angular/quick-start);
[template package.json](https://raw.githubusercontent.com/abpframework/abp/10.0.2/templates/app/angular/package.json);
accessed 2026-08-28.)

**That range excludes 20.3.19 and 20.3.27 identically.** There is no coherent reading in which your
current pin is acceptable but the bump is not -- you have already been running three minors beyond
ABP's stated target, apparently without issue.

The decisive finding: **`@abp/ng.core` 10.0.2 declares no `peerDependencies` at all.** Verified twice,
once in the registry manifest and once by extracting `package/package.json` from the published
tarball. The same holds for `ng.theme.shared`, `ng.identity`, `ng.account`, `ng.setting-management`,
`ng.permission-management` and `ng.tenant-management`. It also holds for the **Commercial** side:
`@volo/abp.ng.account` 10.0.2 is mirrored on public npm, likewise declares no peer dependencies, and
its bundle carries Angular partial-compilation markers of `20.0.7`. ABP does express peer ranges when
it wants to (`@abp/ng.theme.lepton-x` constrains its ABP peers), so the absence is a deliberate
convention. **No semver machinery in Yarn or npm can flag a 20.3.x bump**, which means the stated
blocker is not merely stale -- it is unfalsifiable.

The comment was almost certainly written about the Angular 20 -> 21 **major** jump, which is a real
ABP constraint (ABP moved its template to `~21.2.0` at 10.3.0 and `~22.0.1` at 10.6.0), and then
over-applied by disabling version updates wholesale.

**On the CVEs, with a source discrepancy stated rather than smoothed over.** Two independent research
passes disagree on the count: an OSV query for 20.3.19 returned **10** distinct CVEs across
`@angular/core`, `@angular/common` and `@angular/compiler`; a GHSA-by-GHSA pass confirmed the
baseline's seven (splitting `CVE-2026-54268` from `CVE-2026-50171`), added `CVE-2026-52725`, and found
`CVE-2026-27970` already fixed at 20.3.17. Both agree on the actionable part: **the highest first-fix
boundary is 20.3.27, and no minor or major bump is required.** Run `yarn npm audit` against your own
lockfile for the authoritative count -- and note that a `~20.3.19` manifest range permits anything
under 20.4.0, so the **resolved** version in `yarn.lock`, not the manifest, determines real exposure.

**Do not target 20.3.27.** It shipped 2026-07-29, so the closed PRs were current then, but 20.3.28,
.29 and .30 have since shipped and **20.3.30 is the current `v20-lts`**.

**And the four SSR-contingent CVEs.** GHSA-39pv-4j6c-2g6v states plainly that "Applications that do
not employ SSR with hydration are unaffected", and Angular's docs confirm "Hydration can be enabled
for server-side rendered (SSR) applications only". `HttpTransferCache` is a child of hydration. So if
this app is a plain client-rendered SPA, **`CVE-2026-54267`, `CVE-2026-68945`, `CVE-2026-50170` and
`CVE-2026-54266` are not exploitable here** -- which materially changes the urgency the baseline
assigns them. Fifteen minutes settles it: grep for `@angular/ssr`, `server.ts` / `main.server.ts`, a
`server`/`ssr`/`prerender` builder target in `angular.json`, and `provideClientHydration`.

**Action.** One atomic PR moving all `@angular/*` to 20.3.30 in lockstep. Replace the blanket
version-update disable with an `ignore` rule scoped to `version-update:semver-major`, which preserves
the real ABP constraint while letting patch and minor security fixes flow. Gate the merge on a
production build plus a manual smoke of login, password reset, upload and tenant resolution across two
office hostnames. **Half a developer-day.**

**Then schedule the real blocker:** ABP 10.0.2 -> 10.3+ is what actually gates Angular 21/22, and
**Angular 20 leaves LTS on 2026-11-28** -- about three months out. That is a multi-day upgrade and
should go in a monthly slot now rather than be discovered in November.

### Question 2: Is SQLite-only backend testing defensible?

**Answer: no, but the fix is one small SQL Server project, not a rewrite of 2,261 tests.**

**What SQLite cannot model that this application relies on:**

| Divergence | Consequence here |
| --- | --- |
| **NULL semantics in unique indexes** | SQL Server: "You cannot create a unique index on a single column if that column contains NULL in more than one row." SQLite: "NULL values are considered distinct from all other values, including other NULLs." **Opposite.** EF Core injects the compensating `IS NOT NULL` filter only via the SQL-Server-only `SqlServerIndexConvention`, which never runs in your harness |
| **Collation** | SQL Server's US-English default `SQL_Latin1_General_CP1_CI_AS` is case-**insensitive**; SQLite defaults to `BINARY`, and even `NOCASE` folds "only ASCII characters". This is how a duplicate-email check passes in CI and fails in production |
| **`rowversion` / concurrency tokens** | Microsoft lists database-generated concurrency tokens as flatly unsupported on SQLite. Any optimistic-concurrency test is testing a different mechanism than production |
| **`decimal`, `DateTimeOffset`** | Not natively supported; comparison and ordering fall back to client evaluation |
| **Schemas, sequences** | Unsupported |

([EF Core SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations);
[create unique indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-unique-indexes);
[SQLite CREATE TABLE](https://www.sqlite.org/lang_createtable.html);
[collation and Unicode support](https://learn.microsoft.com/en-us/sql/relational-databases/collations/collation-and-unicode-support);
all accessed 2026-08-28.)

> **[REFUTED 2026-08-28: the conclusion drawn in the next three paragraphs is wrong. `HasFilter` IS in `OnModelCreating`. The live hypothesis is SQLite/SQL Server filtered-index fidelity, which makes this evidence for remediation item 20.]**

**But filtered indexes are not on that list, and that matters for your flaky test.** SQLite has
supported UNIQUE **partial** indexes since 3.8.0 (2013), and EF Core's relational generator appends
`" WHERE " + createIndexOperation.Filter` with no SQLite override. So a model-level `HasFilter` *would*
have been honoured.

**Which means the flake is a true positive, and "fix it by relaxing the SQLite assertion" would delete
a real signal.** The likely cause is model/migration drift: `Fix_UniqueIndexesExcludeSoftDeleted` put
the filter in the migration file but not in `OnModelCreating`, and ABP's harness builds schema from
the model. Ten minutes of reading settles it. A secondary hypothesis worth twenty minutes: "flakes
rather than fails" also fits test-order data accumulation in the single shared `:memory:` database
plus a colliding confirmation-number generator. Check both.

**Testcontainers or GitHub Actions `services:`? Testcontainers.** Not because it is better
technology -- because the identical test then runs on both developers' laptops with zero setup, and
that is what actually determines whether a two-person team keeps a suite green rather than routing
around it. `services:` works only inside CI, cannot be used inside a composite action, needs explicit
port mapping and a hand-written mssql health check against `/opt/mssql-tools18/bin/sqlcmd` with `-No`
(a well-known trap since the tools18 secure-by-default change), and requires a second connection-string
code path for local runs. `Testcontainers.MsSql` 4.14.0 published 2026-08-14 and is actively
maintained.

**The recommended split -- 30 to 60 tests, not 2,261**, in priority order:

1. **Migration gate.** Apply all 90 host and 15 tenant migrations from empty against real SQL Server
   and assert success. Nothing currently tests the artifact that builds production.
2. **Move the tenant-isolation harness** -- move it, do not copy it. Two real databases, real
   host-header resolution.
3. **Every uniqueness and constraint rule**, especially soft-delete plus filtered-unique-index
   interactions.
4. **Case-sensitivity-dependent lookups**: email, username, confirmation-number search.
5. **Concurrency tokens and decimal precision.**

Everything else stays on SQLite as the fast inner loop.

**Free, do it this week regardless (~2 hours, no database required):** add `dotnet ef migrations
has-pending-model-changes` as a CI step for both DbContexts. From EF Core 9 onward, `Migrate()` with
pending model changes throws at runtime -- so a forgotten migration is a startup failure repeated
across 11 (headroom 33) databases at deploy time. One test file. Highest value per hour in the entire
test area.

**One infrastructure-dependent line, named and not designed here:** confirm the collation your
production databases are actually created with and pin the same value on the test container via
`MSSQL_COLLATION`, or the new tests validate the wrong string semantics.

**What a larger company would do that is not worth it here:** running all 2,261 tests against SQL
Server (the EF Core team does this with 30,000 tests and dozens of engineers); adopting the repository
pattern, which is Microsoft's own first-choice alternative but is a rearchitecture of 53k lines that
fights ABP's conventions -- explicitly reject this one; per-test database provisioning; a SQL Server
version matrix.

`UNVERIFIED`: the CI wall-clock cost of a SQL Server container on GitHub-hosted runners. No
primary-source benchmark was found and none is stated from memory. What is verified: private-repo
Ubuntu runners provide 2 CPU / 8 GB RAM / 14 GB SSD against SQL Server's 2 GB / 2 GB minimum, so it
fits. **Measure it once before committing it to the merge gate**, and decide on the measured number.

### Question 3: The minimum sustainable required-check set

**Answered in full in section 5.1.** In short: **one required status check name (`ci / gate`)**, an
aggregator over eight jobs, plus six branch-ruleset rules and an empty bypass list.

The reasoning that matters for defending it: gate *count* is the wrong variable. What determines
whether a gate survives is whether it can be cleared without heroics and whether failing it is
unambiguous. One aggregated check that genuinely blocks beats eight named checks that people learn to
bypass -- and this repo has already established the bypass habit.

---

## 8. Sources

All accessed 2026-08-28. This is the load-bearing subset; full per-claim citations sit behind each
statement above.

**Standards bodies**

- NIST SP 800-218 (SSDF v1.1): <https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-218.pdf>
- OWASP ASVS 5.0.0: <https://github.com/OWASP/ASVS>
- OWASP Top 10: <https://owasp.org/Top10/>
- W3C WCAG 2.2: <https://www.w3.org/TR/WCAG22/>
- W3C WAI on evaluation tools: <https://www.w3.org/WAI/test-evaluate/tools/selecting/>

**GitHub**

- Workflow syntax (`continue-on-error`): <https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax>
- About protected branches: <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches>
- Available rules for rulesets: <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets>
- Dependabot options reference: <https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference>
- Configuring Dependabot security updates: <https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates>
- Secure use of Actions: <https://docs.github.com/en/actions/reference/security/secure-use>
- CodeQL query help (C#): <https://codeql.github.com/codeql-query-help/csharp/>

**Microsoft**

- .NET support policy: <https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>
- Code analysis overview (`AnalysisMode`): <https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview>
- `AnalysisMode<Category>`: <https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#analysismodecategory>
- CA2100: <https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2100>
- Choosing an EF Core testing strategy: <https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy>
- EF Core SQLite limitations: <https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations>
- Managing migrations: <https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing>
- NuGet package auditing: <https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages>
- ASP.NET Core host filtering: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering>

**Angular / ABP**

- Angular release policy and LTS: <https://angular.dev/reference/releases>
- Angular hydration: <https://angular.dev/guide/hydration>
- ABP 10.0 Angular quick start: <https://abp.io/docs/10.0/framework/ui/angular/quick-start>
- ABP 10.0.2 Angular template manifest: <https://raw.githubusercontent.com/abpframework/abp/10.0.2/templates/app/angular/package.json>
- ABP EF Core migrations and test harness: <https://raw.githubusercontent.com/abpframework/abp/dev/docs/en/framework/data/entity-framework-core/migrations.md>

**Vulnerability data**

- OSV: <https://api.osv.dev/v1/query>
- GitHub Advisories: <https://github.com/advisories>
- NVD: <https://services.nvd.nist.gov/rest/json/cves/2.0>

**Regulation**

- 45 CFR Part 164: <https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164>
- NIST SP 800-66r2: <https://csrc.nist.gov/pubs/sp/800/66/r2/final>
- DOJ Title II web accessibility rule: <https://www.federalregister.gov/documents/2024/04/24/2024-07758/>

**Other**

- SQLite partial indexes: <https://www.sqlite.org/partialindex.html>
- Core Web Vitals: <https://web.dev/articles/vitals>
- Let's Encrypt challenge types: <https://letsencrypt.org/docs/challenge-types/>
