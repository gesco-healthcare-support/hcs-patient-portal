# Method, reasoning and analysis

> How this research was structured, what was decided and why, where the evidence conflicted and how
> those conflicts were resolved, and what would change the conclusions.
>
> This document exists so the receiving session can audit the reasoning rather than inherit the
> conclusions. Produced 2026-08-28.

---

## 1. The failure mode this was designed to avoid

The commissioning brief named the most likely way the exercise goes wrong: a session reads the three
attached documents, reorganises their contents, and hands back an analysis that never left the room.
The attachments say what the codebase **has**. They do not say what it **should** have.

Two design decisions follow from that, and both are load-bearing:

**The standards research was run blind to the codebase.** The 14 area-research agents were given the
application context (stack, tenancy model, scale, regulatory posture) and the team context (two SDE
1s, one dev-week per month, no senior engineer) but **were not given the baseline's list of known
gaps**. If you hand an agent the list of gaps and ask it to research a standard, it returns a
standard shaped around those gaps and finds nothing else. The comparison against the codebase was
done afterwards, by me, against a standard established independently.

That is why the verification table contains items nobody was looking for: background-job tenant
context, Redis cache key prefixing, `pull_request_target` privilege, `AnalysisMode` versus
`AnalysisLevel`, the six-year evidence retention problem. None of those appear in the baseline's
"what does not exist" list.

**Every citation was adversarially re-checked.** A second agent per area re-opened each URL under an
instruction to default to "does not support the claim". 290 claims were checked. Six areas came back
`material-errors`. Appendix D records what that caught -- including a fabricated quotation of a W3C
success criterion and a wrong reading of California statute, either of which would have been
actively harmful delivered to a team that cannot check them.

---

## 2. Architecture of the research

35 agents, four phases, ~12.2M tokens of subagent work, zero errors.

```
Phase 1  Standards        14 agents, one per area, blind to the codebase
              |            each returns: standard summary, individually-citable claims with
              |            URLs, recommended checks with tool-maintenance evidence,
              |            "not worth it here" list, surprises, source disagreements
              v
Phase 2  Verify           14 agents, one per area, adversarial
              |            re-opens every URL; defaults to "does not support the claim";
              |            returns per-claim verdict plus correction
              v
Phase 3  Questions         4 agents
              |            Q1 ABP/Angular compatibility
              |            Q2 SQLite vs SQL Server test fidelity
              |            Q3 Angular CVE confirmation, independent of the baseline
              |            Q4 first-party .NET 10 / ABP CI guidance
              v
Phase 4  Triage            3 agents
                           Sonar findings, CodeQL findings, REMOVE-list discovery
                           each instructed to open the vendor rule page before judging
```

Phases 1 and 2 ran as a pipeline rather than a barrier, so each area verified as soon as its research
completed rather than waiting for the slowest of fourteen.

**Q3 was deliberately redundant with the baseline.** The baseline already listed six Angular CVEs.
Rather than accept that list, a separate agent was told to confirm each one independently against NVD
and GHSA and to report if any ID did not resolve. That redundancy is what surfaced the three errors
in the baseline's CVE table (see section 4.3).

---

## 3. Judgment calls I made, and why

These are mine, not the research agents'. They are the places where the evidence permitted more than
one answer.

### 3.1 I added two research areas the brief did not ask for

**CI/CD pipeline security.** The brief's nine areas cover what the pipeline *checks*. None covers
whether the pipeline is itself attackable. `labeler.yml` runs on `pull_request_target`, which carries
write permission to the target repository and access to its secrets; `enforce_admins: false` on a
two-admin repo; 68 unpinned actions; no `permissions:` blocks. This was the single richest source of
findings the team could not have known to look for.

**HIPAA mapped to code and CI.** "HIPAA-regulated" is asserted throughout all three input documents
and never grounded in a single regulatory citation. The brief asks these developers to defend their
choices; "HIPAA requires it" is not a defensible sentence without a CFR reference. Grounding it also
produced the six-year evidence-retention finding, which no engineering-shaped analysis would surface.

### 3.2 I recommended one required check name, not eight

The obvious answer is to make the important checks required. The aggregator pattern (`ci / gate` with
`if: always()` over `needs`) is better for three specific reasons, and I want the reasoning on record
because it will be questioned:

- A required check is matched **by name**. Renaming a job silently orphans the requirement and blocks
  every PR until an admin notices. With eight names that is eight ways to break the repo.
- Path-filtered workflows that skip sit in Pending forever against a required name. GitHub's own
  documentation warns against requiring skippable workflows. This repo already uses
  `dorny/paths-filter`.
- Adding a ninth check becomes a code change rather than a settings negotiation.

The cost is one extra job. The evidence on gate count is one-directional: DORA found no evidence that
heavier external approval lowers change failure rate, and the CI Theater study (1,270 projects,
534,417 builds) found 85% had a build broken more than four days, smallest projects averaging 40 days
to fix. This repo has already established the bypass habit -- Sonar red on `main` since 2026-07-08,
every cascade merge a named bypass -- so the design constraint is not "how much can we require" but
"what will still be respected in six months".

### 3.3 I refused to trim the launch-blocking list to fit the budget

The brief said to say plainly if one dev-week per month is not enough, and that this is a finding the
business needs rather than something to quietly absorb. 141 hours against 40 per month is about three
and a half months.

I split it into a 58-hour Tier 0 and an 83-hour Tier 1 so the business can make an explicit tradeoff,
and named three options with the third (launch on Tier 0, document the rest as accepted risk)
flagged as defensible **only** if the acceptance is written, dated and owned. That is not a
softening: 45 CFR 164.306(d)(3) does not permit silently skipping an addressable specification
anyway, so the documentation habit is required regardless of what the business chooses.

### 3.4 I treated "launch-blocking" and "merge gate" as independent axes

Several items are launch-blocking (must be done and triaged before anonymous traffic arrives) while
being explicitly **advisory** at the merge gate (must not block a PR on findings). CodeQL is the
clearest case: it must exist and have been triaged once, and it must never block a merge on findings,
because a required check with an unclearable backlog is the fastest route to bypass culture -- and
this repo already has one of those.

Conflating the two axes is how teams end up with thirty required checks.

### 3.5 I ranked "delete" above "add"

Roughly 60% of the remediation value in the first month is deletion. That is unusual advice and worth
stating why: fifteen of the ~23 removal candidates are not clutter but **false assurance**, and the
gap they hide is the biggest single finding in the exercise. Strip out everything structurally
incapable of failing and the merge gate is two compile jobs that run no tests.

That gap was invisible precisely because seventeen workflows and a dozen green ticks read as a
thorough pipeline. Removal is what makes it visible, and it is also the cheapest work available.

---

## 4. Where the evidence conflicted, and how it was resolved

Recorded rather than silently reconciled, because a reader may resolve them differently.

### 4.1 Two of my own agents disagreed on CodeQL's query suite

- The **SAST area** concluded `security-extended` is non-negotiable, because three C# queries mapping
  to this application's risks are extended-only.
- The **CodeQL triage** recommended dropping to the default suite, citing GitHub's documentation that
  `security-extended` "may return a greater number of false positive code scanning results", and
  arguing that a team of two drowning in low-precision alerts stops reading all of them.

**Resolved: keep `security-extended`.** The deciding fact is empirical rather than theoretical -- the
repo already runs it and has 23 alerts, which is a manageable afternoon, not a flood. The triage
agent was reasoning from the general case without weighting the observed count. Flagged in the
deliverable with a revisit trigger: reconsider if the count passes ~50 once JS/TS analysis is
actually running.

### 4.2 The two CVE passes returned different counts

- **Q1** queried OSV for 20.3.19 and got **10** distinct CVEs across `@angular/core`,
  `@angular/common` and `@angular/compiler`.
- **Q3** worked GHSA-by-GHSA and confirmed the baseline's seven (splitting `CVE-2026-54268` from
  `CVE-2026-50171`), added `CVE-2026-52725`, and found `CVE-2026-27970` already fixed at 20.3.17 --
  so eight open, not ten.

**Resolved: report the discrepancy rather than pick.** Both agree on the actionable part, which is
that 20.3.27 is the highest first-fix boundary and no minor or major bump is needed. The deliverable
tells the team to run `yarn npm audit` against their own lockfile for the authoritative count, and
notes that a `~20.3.19` manifest range permits anything under 20.4.0 -- so the **resolved** version in
`yarn.lock`, not the manifest, determines real exposure. Neither agent could check that, because
neither had the repository.

### 4.3 The CodeQL triage's leading hypothesis conflicts with the baseline

The triage agent's top explanation for zero JS/TS alerts was GitHub's documented behaviour that
"CodeQL only analyzes the language with the most source files" when `languages:` is not explicitly
declared -- and C# at 53k lines against TypeScript at 27k is exactly that ratio.

**But the baseline states `codeql-pr.yml` runs a matrix of `csharp` + `javascript-typescript`.** So
the language *is* declared and that explanation does not hold as stated.

**Resolved: reframed around a mechanism the agent had available but did not connect.**
`codeql-pr.yml` is `continue-on-error: true`. That does something nastier than ignoring findings --
it hides *run* failures. If autobuild fails against a .NET 10 / ABP Commercial solution or extraction
OOMs, the analyze step fails, no SARIF uploads, and the alert count simply does not move. Zero alerts
then reads as clean rather than never-analysed, and nothing surfaces it.

That reframing is mine, not any agent's, and it is why R6 in the REMOVE list is rated
launch-blocking. The check that settles it is one minute: open the last CodeQL run and confirm the
`javascript-typescript` job completed and uploaded.

### 4.4 The verification pass reversed one of its own area's conclusions

The coverage research concluded this project exceeds SonarQube Cloud's 50k-LOC private-analysis cap.
The verifier found the LOC methodology wrong -- Sonar excludes test code, excluded files, unsupported
languages, comments and blank lines -- and noted the conclusion may reverse.

**Resolved: marked `UNVERIFIED` in the deliverable** with the instruction to check the `ncloc` Sonar
actually reports against the plan, rather than asserting either way. This is the pattern the brief
asked for: visible holes beat silent mixing.

### 4.5 Three places where research contradicts the baseline document

The brief explicitly asked for these to be surfaced. Full table in Document A section 2; the three
most consequential:

- **`open-pull-requests-limit: 0` is not the Dependabot fault.** GitHub documents that security
  update PRs "are not subject to this limit and do not count toward it". `target-branch` is the kill
  switch, and GitHub says explicitly not to set it. The fix is a one-line deletion, not a rewrite.
- **The `-warnaserror` step is not redundant.** Microsoft documents that `TreatWarningsAsErrors`
  "only impacts the C# compiler, not any other MSBuild tasks", while the command-line switch "impacts
  all tasks". Deleting the step without moving the switch **loses** coverage.
- **Reliability D is not accessibility debt.** Sonar sets the rating from the single worst issue, not
  volume. The two accessibility rules cap at C and B respectively. The D comes entirely from the 8
  unexamined C#/TS bugs.

I accepted all three against the baseline because each rests on a first-party vendor page that the
verification pass confirmed, and each is checkable by the receiving session in minutes.

---

## 5. What I could not do, and what follows from it

**I did not have the repository.** The working directory available to this session contained a
personal profile README and three resume PDFs. Every statement about the codebase's current state is
inherited from `01-ci-tests-and-checks.md` and `02-project-status.md`.

Concretely, this means:

| Cannot be confirmed here | Settled by | Cost |
| --- | --- | --- |
| Whether the app uses Angular SSR | grep for `@angular/ssr`, `server.ts`, `provideClientHydration`, an `ssr`/`prerender` builder target | 15 min |
| Whether `Fix_UniqueIndexesExcludeSoftDeleted` put `HasFilter` in `OnModelCreating` | read the migration and the model config | 10 min |
| Whether the EF test module calls `IRelationalDatabaseCreator.CreateTables()` (ABP's documented default) or runs migrations | read `CaseEvaluationEntityFrameworkCoreTestModule.cs` | 5 min |
| Whether the CodeQL `javascript-typescript` job actually completes and uploads | open the last run | 1 min |
| Whether `.yarnrc.yml` carries an explicit `enableScripts: true` | read the file | 1 min |
| Whether CI passes `dotnet restore --locked-mode` | read the workflow | 2 min |
| What admin password each of the 11 tenant databases actually holds | query them | 1 h |
| Whether the 5 `csharpsquid:S2068` flags are all dev placeholders | read them individually | 1 h |
| Sonar's actual `ncloc` against the free-tier cap | read the Sonar project page | 2 min |

The first six total under 35 minutes and between them change the urgency of four separate
recommendations. **They are the right first action for whoever picks this up with repository
access.**

**Two tooling limitations hit the research agents**, recorded because they shape confidence:

- `rules.sonarsource.com` was unreachable from the sandbox (502 on CONNECT). The Sonar triage agent
  substituted SonarSource's live rule API at `next.sonarqube.com` and the analyzer source
  repositories on `raw.githubusercontent.com` -- the artifacts the rendered pages are generated from,
  so this is a good substitution rather than a gap. Two closed-source commercial analyzers
  (`tssecurity:S6105`, `secrets:S7539`) yielded title, type, severity and effort but not prose, and
  the agent said so rather than filling the gap from memory.
- ABP Commercial's private registry `npm.abp.io` is behind a licence wall and unreachable. The Q1
  agent mitigated by inspecting `@volo/abp.ng.account` 10.0.2, a Commercial package mirrored on
  public npm, and found it identical in the relevant respect (no `peerDependencies`, compiled at
  Angular 20.0.7). That is an inference from a sampled Commercial package rather than the complete
  licensed set -- rated high confidence, but stated as inference.

**Several agents exhausted a 200-call WebSearch budget** partway through and completed their work via
direct `WebFetch` against named primary sources. That is arguably better sourcing, not worse, but it
means some "is this tool still maintained" checks were done against registry APIs and release feeds
rather than search results.

---

## 6. What would change the conclusions

Stated so the receiving session knows what to watch for rather than treating this as settled.

| If this turns out to be true | Then |
| --- | --- |
| The app uses Angular SSR with hydration | Four CVEs move from "not exploitable here" back to urgent, and the Angular bump becomes Tier 0 week 1 rather than week 4 |
| `HasFilter` **is** in `OnModelCreating` | The flaky-test diagnosis is wrong; fall back to the secondary hypothesis (test-order data accumulation in the shared `:memory:` database plus a colliding confirmation-number generator) |
| The CodeQL JS/TS job does complete and upload | Zero alerts is a real finding about CodeQL's JS taint queries needing DOM/Node sources a template-bound Angular app rarely touches, not a configuration bug. R6 stays (masking run failures is still wrong) but drops from launch-blocking |
| Sonar's `ncloc` is under the free-tier cap | The "keep or kill Sonar" decision gets easier and the keep option gets cheaper |
| The business allocates two dev-weeks per month | Launch-blocking work fits in under two months and the Tier 0 / Tier 1 split stops mattering |
| Counsel confirms Section 1557 applies | Accessibility moves from contested-exposure to a direct regulatory obligation, and items 31/32 move up |
| The January 2025 HIPAA Security Rule NPRM was finalised | Vulnerability scanning and penetration testing cadences become required rather than advisory. **Status was not conclusively established here and is marked `UNVERIFIED`** |

---

## 7. Provenance of every number in the deliverables

| Number | Where it comes from |
| --- | --- |
| 141 launch-blocking hours | Sum of Tier 0 (58) + Tier 1 (83) in `remediation-plan.md`; per-item estimates from the research agents' `effort_hours` fields, adjusted by me for overlap |
| 74 verification-table rows | My synthesis across the 14 areas' `recommended_checks`, deduplicated |
| 23 REMOVE candidates | Triage agent C3, working from the full inventory in `01-ci-tests-and-checks.md` |
| 290 claims verified | Count of `results` entries across the 14 verification agents (Appendix D) |
| "~15 of 23 are genuinely harmful" | Triage agent C3's own classification, `verdict` field |
| "roughly 6 of 389 findings deserve an afternoon" | Triage agent C1's overall read |
| 11 angular-eslint a11y rules | Verifier's read of `accessibility.json` at tag v20.7.0, correcting the research agent's 12 |
| 20.3.30 as current `v20-lts` | npm dist-tags for `@angular/core`, read 2026-08-28 |
| Every threshold, version and requirement ID | Appendix A, each with URL, access date and verification verdict |

Anything not in that table and not in Appendix A should be treated as my reasoning rather than
sourced fact, and challenged accordingly.
