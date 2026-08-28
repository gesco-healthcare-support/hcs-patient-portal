# Remediation plan

> Ordered backlog derived from `code-standard-gap-analysis.md`. Every item has an effort estimate, a
> dependency, and a launch-blocking yes or no.
>
> Prepared 2026-08-28. Assumed capacity: **one developer-week per month (~40 hours)** alongside
> feature delivery, across two SDE 1 developers with no senior engineer, architect, security
> specialist, DevOps engineer or QA.

---

## 1. The capacity finding, first

**One developer-week per month is not enough for the launch-blocking set. This is a business
decision, not something to quietly absorb by trimming the list.**

| | Hours | At 40 h/month |
| --- | --- | --- |
| Tier 0 -- irreducible core | **58** | 1.5 months |
| Tier 1 -- rest of launch-blocking | **83** | 2 months |
| **Total launch-blocking** | **141** | **~3.5 months** |
| Do soon (first month after launch) | 62 | 1.5 months |
| Do eventually | 78 | 2 months |

Three honest options, and the business must pick one rather than letting the calendar pick by
default:

1. **Move the launch date out by roughly three and a half months.** Cleanest, and it keeps the
   destructive-testing window open while all data is still synthetic.
2. **Increase the allocation.** Two developers at one week each per month is 80 hours, which brings
   launch-blocking work to under two months. This is the option that most closely matches how the
   work actually decomposes -- several Tier 0 items genuinely want two people (the hotspot review is
   explicitly a both-of-you-at-one-screen exercise).
3. **Launch on Tier 0 only (~58 hours, 1.5 months) and accept Tier 1 as documented risk.** Defensible
   *only* if the acceptance is written down, dated, signed by whoever owns the risk, and scheduled.
   Under 45 CFR 164.306(d)(3) you cannot silently skip an addressable specification anyway, so the
   documentation habit is required regardless.

**What is not an option** is treating the list as aspirational and launching against the current
pipeline. Today the merge gate runs no tests, the only real SAST may not be running, and three secret
scanners all fail open. That is not a "some gaps remain" position; it is a pipeline that reports
success without checking.

**One piece of timing luck, and it expires.** There is no real patient data yet. Destructive testing,
credential rotation across all 11 tenant databases, and a full-history secret scan are all free right
now and all become materially harder the moment a real practice takes a real booking. Several Tier 0
items are cheap *this month* and expensive later. That argues for option 1 or 2 over option 3.

---

## 2. Do now -- Tier 0, the irreducible core (58 hours)

If only one block of work happens before launch, this is it. Ordered so that each week ends somewhere
sensible.

### Week 1: stop the pipeline lying (14 h)

| # | Item | Effort | Depends on | Launch-blocking |
| --- | --- | --- | --- | --- |
| 1 | **Hard-fail the gitleaks hooks.** Replace the silent-degradation check in `.husky/pre-commit` and `.husky/pre-push` with `command -v gitleaks >/dev/null \|\| { echo 'gitleaks not installed'; exit 1; }`. Same treatment for the `lint-staged only if node_modules exists` conditional, which fails open identically. Delete the pre-push full-tree scan -- it rescans unchanged history on every push and is what drives people to `--no-verify` | 2 h | -- | **YES** |
| 2 | **Delete `target-branch` from `dependabot.yml`.** Keep `open-pull-requests-limit: 0`; it is correct and does not affect security updates. Confirm security updates are enabled in repo Settings (a repo-level toggle, not a config key) | 0.5 h | -- | **YES** |
| 3 | **Delete the false attestations.** The README sentence "SonarCloud is live and gates new-code coverage on PRs", and `docs/devops/TESTING-STRATEGY.md` entirely -- including its "last verified 2026-06-01" stamp. Replace the latter with a ten-line README section stating what actually runs | 1 h | -- | **YES** |
| 4 | **Delete the `ci.yml:248` spec-file guard.** Replace with a floor assertion: `test $(find angular/src -name '*.spec.ts' \| wc -l) -ge 60 \|\| exit 1` | 0.5 h | -- | **YES** |
| 5 | **Enable the .NET security analyzers.** Add `<AnalysisModeSecurity>All</AnalysisModeSecurity>` to `Directory.Build.props`. Land as warnings first, triage what appears across 53k lines of C#, then promote the CA2100/CA3xxx/CA5xxx band to errors on the HTTP-facing projects only. Do not gate on it day one -- the taint rules cannot track across assemblies and will miss cross-project flows in a layered ABP solution | 6 h | -- | **YES** |
| 6 | **Add the migration/model drift test.** `context.Database.HasPendingModelChanges()` asserted false for both DbContexts, as a CI step. From EF Core 9 onward a pending model change throws at runtime, so a forgotten migration is a startup failure repeated across 11 databases | 2 h | -- | **YES** |
| 7 | **Diagnose the flaky test properly.** Check whether `Fix_UniqueIndexesExcludeSoftDeleted` put `HasFilter` in `OnModelCreating` or only in the migration file. Do **not** relax the SQLite assertion -- SQLite supports UNIQUE partial indexes and EF Core emits the filter, so the failure is a true-positive drift signal | 2 h | 6 | **YES** |

### Week 2: build the gate that does not exist (15 h)

| # | Item | Effort | Depends on | Launch-blocking |
| --- | --- | --- | --- | --- |
| 8 | **Move `dotnet test` into `ci.yml` and build the aggregator.** Add `backend-test` and `frontend-test` jobs, then a `ci / gate` job with `needs: [backend-build, backend-test, frontend-build, frontend-test, secrets, deps-audit]` and `if: always()`, failing unless every needed result is success or skipped. Add `tenant-isolation` and `workflow-audit` to `needs` as those land | 8 h | 4 | **YES** |
| 9 | **Set the branch ruleset.** Required check: `ci / gate` only. Bypass list **empty**. PR required, 1 approval, dismiss stale approvals, require approval of the most recent reviewable push, require conversation resolution, block force pushes, restrict deletions. Commit the ruleset JSON to `.github/rulesets/main.json` so the config is reviewable | 3 h | 8 | **YES** |
| 10 | **Un-mask CodeQL and confirm it is actually analysing TypeScript.** Remove `continue-on-error` from `codeql-pr.yml`. Open the last run and verify a `javascript-typescript` job completed and uploaded SARIF -- zero alerts across 27k lines is not credible while Sonar finds two BLOCKER vulnerabilities in the same code. Keep CodeQL advisory at the merge gate; a *run failure* must be red, findings need not block | 3 h | -- | **YES** |
| 11 | **Un-mask dependency review.** Remove `continue-on-error`; set `fail-on-severity: high` (the action default is `low`; someone moved it to `critical`, discarding the band where nearly all real advisories land) | 1 h | -- | **YES** |

### Week 3: read the code the tools are pointing at (18 h)

This week needs a developer reading source, not editing YAML. It is the week that most directly
reduces breach risk.

| # | Item | Effort | Depends on | Launch-blocking |
| --- | --- | --- | --- | --- |
| 12 | **Read `angular/src/tenant-bootstrap.ts:65`** (`tssecurity:S6105`) before anything else on this list. A taint-traced redirect from user-controlled data, on the tenancy resolution path of a Host-header-tenanted PHI app, days from public exposure. Trace the value to its source by hand. Fix with a server-side allow-list of the 11 office hosts, not URL sanitisation | 4 h | -- | **YES** |
| 13 | **Audit the seed contributors.** Two separate things: (a) confirm `Demo*` contributors cannot execute outside Development -- the robust fix is not compiling them into the deployed artifact, since ABP seeding runs in production via DbMigrator on every deploy *and* for every new office; (b) query all 11 tenant databases and confirm what admin password each actually holds. Rotate anything default, and make the seed path require the value from configuration with no literal fallback. **Free now, expensive after go-live** | 4 h | -- | **YES** |
| 14 | **Read `shared/ui/icon/icon.component.ts:58`** (`typescript:S6268`, sanitisation disabled). If the input is a literal union resolved against a static map, dismiss and narrow the type so it stays that way. If it is `string`, it is a reflected XSS primitive on every page including anonymous login | 1 h | -- | **YES** |
| 15 | **Read the 8 live-path CodeQL alerts and do the PHI sweep.** The 3 cleartext-storage on the emailer/dispatcher and the 5 exposure alerts on `ExternalAccountAppService`, the emailer, and `AppointmentChangeRequestsAppService.Approval`. Fix by *removing* the value and logging an entity id -- not by wrapping it in an encode call. Then the part CodeQL cannot do: a manual sweep of those files for the PHI the heuristic cannot see (patient name, DOB, claim/WCAB number, body part, injury description, appointment time) | 5 h | -- | **YES** |
| 16 | **Fix the 2 log-forging alerts and install the durable control.** Named message-template placeholders plus `.ReplaceLineEndings(string.Empty)` on the tainted argument. Then enable **CA2254** as a build warning solution-wide so interpolated log templates stop appearing, and write the one-paragraph "never log" rule (passwords, tokens, session ids, connection strings, keys, PHI) | 3 h | 5 | **YES** |
| 17 | **Rotate the dev-script credentials** (`secrets:S7539` in `dev-api.ps1:34`, `dev-authserver.ps1:37`). Determine whether the password is used anywhere beyond a local instance; move to `dotnet user-secrets` or an environment variable. Skip the git-history rewrite -- rotation achieves the same outcome for a fraction of the disruption | 1 h | -- | **YES** |

### Week 4: close the CVEs and prove the tenancy claim (11 h)

| # | Item | Effort | Depends on | Launch-blocking |
| --- | --- | --- | --- | --- |
| 18 | **Settle the SSR question, then bump Angular.** Fifteen minutes of greps (`@angular/ssr`, `server.ts`, `provideClientHydration`, an `ssr`/`prerender` builder target) determines whether four of the CVEs are even exploitable here. Then one atomic PR moving all `@angular/*` to **20.3.30** (current `v20-lts`, not the 20.3.27 in the closed PRs). Replace the blanket version-update disable with an `ignore` rule scoped to `version-update:semver-major`. Smoke: login, password reset, upload, and tenant resolution across two office hostnames | 4 h | 2 | **YES** |
| 19 | **Prove "Host header only".** HTTP-level tests through the real middleware asserting the *resolved tenant*, not the status code: `?__tenant=`, the `__tenant` header, the `__tenant` cookie and a route segment must not change the tenant when the Host says otherwise; an unknown Host must be rejected rather than silently mapped to a default. **Expect this to fail on first run** -- ABP registers all four `__tenant` resolvers by default, so the claim is about configuration and has never been tested | 7 h | 8 | **YES** |

**Tier 0 total: 58 hours.**

---

## 3. Do now -- Tier 1, rest of launch-blocking (83 hours)

Everything here is still launch-blocking in my assessment. If the business chooses option 3 above,
this is the list being explicitly deferred and it should be written down as such.

| # | Item | Effort | Depends on | Launch-blocking |
| --- | --- | --- | --- | --- |
| 20 | **SQL Server test project.** One new xUnit project on `Testcontainers.MsSql`, 30-60 tests, not 2,261. Priority order: apply all 105 migrations from empty and assert success; **move** (not copy) the tenant-isolation harness onto two real databases; every uniqueness/constraint rule especially soft-delete plus filtered-unique-index; case-sensitivity-dependent lookups; concurrency tokens and decimal precision. Measure the container startup cost once before committing it to the gate | 20 h | 6, 7 | **YES** |
| 21 | **File-upload contract tests.** Anonymous upload is the most dangerous new surface. Oversized file rejected; a `.pdf` whose bytes are an HTML/script payload rejected on magic-byte check; stored filename is a server-generated GUID not the client's; traversal filename neutralised. Build the ~8 malicious sample fixtures into the repo. Skip antivirus scanning -- that is a runtime concern, not CI | 10 h | 8 | **YES** |
| 22 | **Response-header and cookie contract tests.** Assert exact headers on anonymous GET, authenticated GET, 401 and 500: HSTS with a max-age, `X-Content-Type-Options: nosniff`, a CSP containing `object-src 'none'` and `base-uri 'none'` and a `frame-ancestors` directive, and `Secure`/`HttpOnly`/`SameSite` on auth cookies. Ten ASVS requirements as deterministic sub-second tests -- the best ratio in the entire security area, and it means a scanner never has to be a required check | 5 h | 8 | **YES** |
| 23 | **Rate-limit login and registration.** Today only password reset, public upload and external-account endpoints are throttled. Login and registration are the two endpoints anonymous internet traffic will hit hardest | 4 h | -- | **YES** |
| 24 | **Delete the dead wildcard-CORS endpoint** -- the endpoint, not just the header. Then grep for other `*` CORS values and confirm the real API uses an explicit per-tenant origin allow-list, with a test. Given Host-header tenancy that allow-list is load-bearing | 4 h | -- | **YES** |
| 25 | **Background-job tenant context test.** One reflection-driven test enumerating every `IBackgroundJob<T>`/`IAsyncBackgroundJob<T>` and asserting each argument type implements `IMultiTenant` with a non-null `TenantId` at enqueue time. ABP falls back to the ambient tenant otherwise -- null in a worker, which is the host database. ABP's own docs never mention tenants on this page, so nothing warns you | 3 h | -- | **YES** |
| 26 | **Tenancy forbidden-constructs deny-list.** A ripgrep step (the 1-2 hour version; Semgrep is the 4-6 hour version) failing on `DataFilter.Disable<IMultiTenant>()`, `[IgnoreMultiTenancy]`, `BlobContainerConfiguration.IsMultiTenant = false`, and writes to `AbpDataFilterOptions.DefaultStates`, with an allow-list file for justified uses. With no senior reviewer, a single added line is exactly what review misses | 2 h | 8 | **YES** |
| 27 | **Triage the 31 security hotspots in one sitting.** Both developers, one screen, three hours elapsed. Write the justification on the first instance of each distinct rule and paste onto siblings. Takes `security_review_rating` from E to A in an afternoon. Pull out the request-size-limit and CORS-origins hotspots as real work items; verify the csrf class with one curl against an AuthServer form endpoint rather than triaging six separately | 6 h | -- | **YES** |
| 28 | **Find the 8 bugs behind the reliability D.** One SonarCloud filter: Reliability + High/Blocker. The 330 accessibility findings contribute nothing to the rating; these 8 contribute all of it. A CRITICAL C# bug in a PHI backend is usually a null-deref, a disposed-object use, an `async void` or a swallowed exception on a data path | 4 h | -- | **YES** |
| 29 | **Pipeline security pass.** Add `permissions: contents: read` at the top of every workflow with narrow job-level grants; check Settings > Actions > General is on the restricted default (if the org predates February 2023 the read-only default was never applied); pin third-party actions to full commit SHAs; add zizmor + actionlint as one `workflow-audit` job scoped to `.github/workflows/**`; delete `labeler.yml` and its `pull_request_target` trigger | 8 h | 8 | **YES** |
| 30 | **Move deploy secrets into a GitHub Environment with required reviewers.** Today a PR from a same-repo branch receives all repository secrets before any review. This is the control assuming a stolen laptop rather than an external attacker | 2 h | -- | **YES** |
| 31 | **Enable `@angular-eslint` template accessibility rules with bulk suppressions.** The 11-rule preset, landed green on day one via ESLint bulk suppressions so nobody is asked to fix 253 things, while every new template is held to the rules from now on. This is prevention, which is what matters | 4 h | -- | **YES** |
| 32 | **axe on the six anonymous-reachable pages.** Tenant landing, registration, login, forgot-password, reset-password, document upload. Gate on critical/serious only, against a rule-id baseline. Note that axe-core's WCAG 2.2 rules are **disabled by default** -- you must enable the `wcag22aa` tag explicitly. Before this, spend 30 minutes sampling five of the 253 flagged inputs in the browser accessibility tree; if `mat-form-field` generates the association at runtime, that whole class is a false positive | 11 h | 31 | **YES** |

**Tier 1 total: 83 hours. Tier 0 + Tier 1 = 141 hours.**

---

## 4. Do soon -- first month after launch (62 hours)

| # | Item | Effort | Depends on | Launch-blocking |
| --- | --- | --- | --- | --- |
| 33 | **Resolve the Sonar third state.** Either delete `sonarcloud.yml` and the workflow outright -- defensible at this size, since CodeQL plus the newly-enabled Roslyn security analyzers is real coverage -- or make it real. If keeping it, **the order matters**: switch new-code to reference branch `main`, remove the `angular/src` coverage exclusions, clear the red standing on `main` since 2026-07-08, *then* set `qualitygate.wait=true` and drop `continue-on-error`. Reversing that order blocks you on day one | 16 h | 8 | no |
| 34 | **The workflow cull, one PR.** Delete `doc-check.yml`, `commitlint.yml`, `pr-size.yml`, `auto-pr-dev.yml`, `promote-staging.yml`, `trufflehog-pr.yml`, and the markdownlint half of `lint-meta.yml` plus its "Phase C" comment. Keep yamllint scoped to `.github/**` and make it blocking -- a malformed workflow file does not fail loudly, it silently does not run. Rename or delete `deploy-dev.yml` once `dotnet test` has moved. Target 6-8 checks, 1 required | 4 h | 8 | no |
| 35 | **Delete the `-warnaserror` step, move the switch onto the real build.** Not redundant as the baseline states: the property affects only the C# compiler, the switch affects all MSBuild tasks. Net effect is one build instead of two with strictly broader enforcement. Expect ABP's generated code to surface surprises | 4 h | 8 | no |
| 36 | **Patch coverage on the PR, advisory.** `coverlet` + `diff-cover` for both stacks, printing coverage of changed lines only. Set **no threshold** for four to six weeks -- collect real data, then set the number at your observed median rather than importing someone else's 80% | 8 h | 8 | no |
| 37 | **E2E smoke suite, hard-capped at 8-12 specs.** Playwright, Chromium only, `workers: 1`, `retries: 2`, trace on first retry. Two of the specs must be the cross-tenant negative journeys. The cap is the recommendation: two SDE 1s with no QA will abandon a 40-test suite within two months, and an abandoned suite teaches the team that red means nothing | 24 h | 20 | no |
| 38 | **Container image scanning and Dockerfile linting.** Trivy on the two images you author, `CRITICAL,HIGH`, `ignore-unfixed: true`; hadolint at `--failure-threshold error`; a 20-line `docker inspect` assertion that the final image does not run as root. The fixable-only threshold is what makes this survivable rather than aspirational | 6 h | 29 | no |

---

## 5. Do eventually (78 hours)

| # | Item | Effort | Launch-blocking |
| --- | --- | --- | --- |
| 39 | **Bundle budgets derived from measured output**, replacing the 2 MB/2.5 MB scaffold default, plus a lazy-chunk budget. Check budget names against actual chunk names -- a budget matching no chunk evaluates to zero bytes and silently passes | 3 h | no |
| 40 | **Lighthouse CI on the four anonymous routes.** Error-level assertions on byte counts only; timing metrics measured and printed but never blocking on a contended shared runner | 10 h | no |
| 41 | **Per-tenant data fingerprint, scheduled.** For every `IMultiTenant` table, assert `SELECT DISTINCT TenantId` returns exactly one value. Catches the failure mode that reports success: a silent connection-string fallback writes to the wrong database with no exception | 8 h | no |
| 42 | **Redis tenant-key audit, scheduled.** Assert every key carries the `t:{guid},` prefix or is on an explicit global allow-list. Only ABP's typed `IDistributedCache<T>` adds the prefix | 6 h | no |
| 43 | **Audit-trail coverage tests including read paths**, plus an architecture test that fails when a new controller touching ePHI has no audit attribute. 164.312(b) is Required with no implementation specifications; reads are the gap and reads are what OCR asks about | 14 h | no |
| 44 | **Compliance evidence export, scheduled.** Monthly bundle of DAST report, dependency audit, SBOM, tenant-isolation and audit-trail results, commit SHA and timestamp, written somewhere that survives six years. GitHub caps private-repo artifacts at 400 days and defaults to 90; 164.316(b)(2)(i) requires 2,190 | 7 h | no |
| 45 | **ZAP baseline scan, scheduled and advisory**, against an ephemeral compose stack with synthetic data. Beats the NPRM's proposed six-month cadence at near-zero cost. Be honest about its limits: ZAP cannot find cross-tenant bugs, because it does not know which resource belongs to which office | 8 h | no |
| 46 | **Review the 19 Sonar suppressions** -- only after item 33 decides Sonar's fate, since deleting Sonar deletes the question. Keep the ABP DI and permission-string entries; those fire on framework shape. **Hand-review the email-template HTML ones**, since this application emails PHI-adjacent content | 4 h | no |
| 47 | **The 77 mouse-event-without-keyboard findings.** Triage by element, not occurrence -- expect 5-8 repeated patterns (clickable row, clickable card) that convert to real `<button>` once in a shared component. Genuine WCAG 2.2 SC 2.1.1 Level A failures, but they contribute nothing to any rating | 4 h | no |
| 48 | **SBOM on tagged releases.** Not legally required here -- not a medical device, EO 14028 binds federal procurement. Three hours that pay for themselves the day a CVE lands four levels deep | 3 h | no |
| 49 | **Ruleset drift detection and a monthly bypass review.** A weekly job diffing the live ruleset against the committed baseline, plus a 20-minute monthly review that records its result -- including "no bypasses" -- to a file in the repo. The cheapest compensating control available for having no separation of duties | 6 h | no |
| 50 | **Mutation testing on tenancy and authorization namespaces, scheduled.** Answers the question coverage cannot: whether the assertions in those tests actually fail when the logic is wrong. Never a gate | 5 h | no |

---

## 6. Explicitly not recommended

The brief asked for what to skip. These are standard at a larger company and are the wrong call here.
Each would be bypassed within a month at two developers.

| Not doing | Why not |
| --- | --- |
| **Porting all 2,261 tests to SQL Server** | The EF Core team does this with 30,000 tests and dozens of engineers. ~90% of yours exercise application logic where the provider is irrelevant. 30-60 targeted tests get the coverage that matters |
| **Adopting the repository pattern** | Microsoft's own first-choice alternative to a test double, and their docs concede it "can incur significant cost to implement and maintain". It is a rearchitecture of 53k lines that fights ABP's built-in conventions. Reject this one explicitly |
| **A second commercial SAST alongside CodeQL** | One well-maintained analyser you actually read beats two you skim |
| **`Microsoft.Extensions.Compliance.Redaction` with a data-classification taxonomy** | A real refactor across every log call site, and `HmacRedactor` is still experimental. Buy the same assurance with CA2254 plus a written rule |
| **Custom CodeQL query packs modelling your PHI types** | Weeks of work. The manual sweep in item 15 gets most of it for five hours |
| **Full SCA platform with licence policy and SBOM attestation** | Dependabot plus dependency-review is right at this size |
| **Per-suppression justification-with-expiry ceremony** | A quarterly review (item 46) achieves it without the process |
| **DAST or Lighthouse as required merge gates** | Both are environment-dependent and flaky on shared runners. Scheduled and advisory, or they become the reason someone starts bypassing |
| **Automated WCAG gating on authenticated screens** | Behind a login, so far lower drive-by-litigation risk. Nightly advisory, and a manual keyboard pass on the six public flows |
| **Required linear history, merge queue, 2 required approvers** | CIS 1.1.3 asks for two approvers; GitHub forbids self-approval, so it is arithmetically impossible at two developers. Say so in writing rather than being asked later |
| **Load and stress testing** | Needs a target environment. Named as an infrastructure dependency and left to the separate exercise |

---

## 7. Sequencing dependencies at a glance

```
Week 1  (1)(2)(3)(4)(5)(6) ------------------> (7) flake diagnosis
                                    |
Week 2                              +--------> (8) ci/gate --> (9) ruleset
        (10)(11) un-mask                             |
                                                     +-------> (19)(21)(22)(26)(29)
Week 3  (12)(13)(14)(15)(16)(17) code reads
                                    |
Week 4  (18) Angular bump <---------(2)
        (19) tenancy proof <--------(8)
                                    |
Tier 1  (20) SQL Server path <------(6)(7) ---> (37) E2E smoke
        (31) a11y lint -----------------------> (32) axe
        (29) pipeline security ---------------> (38) container scanning
Do soon (33) Sonar decision -------------------> (46) suppression review
```

The one hard ordering constraint worth calling out: **item 8 (the aggregator) gates most of Tier 1**,
because there is no point writing tests that cannot block a merge. And **item 33's internal order is
not optional** -- fix the new-code period and clear the red *before* making Sonar blocking, or the
first PR after the change is stuck and someone reaches for admin bypass, which is the habit this whole
plan is trying to break.

---

## 8. What to tell the business

Three sentences, if that is all there is room for:

1. **The pipeline currently reports success without checking.** Seventeen workflows, and only a
   compile failure can stop a change reaching `main`; the test suite has not gated anything since
   2026-05-01.
2. **Getting to a defensible standard before public launch is about 141 developer-hours**, which at
   the current allocation of one developer-week per month is roughly three and a half months.
3. **The window where this is cheap is closing.** Credential rotation across 11 tenant databases,
   destructive upload testing, and a full-history secret scan are all free while the data is
   synthetic, and all become incidents once it is not.
