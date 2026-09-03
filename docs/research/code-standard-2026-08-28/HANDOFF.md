# Handoff: production code standard research

> Complete package for delivering this research back to the session that commissioned it.
>
> Produced 2026-08-28 by a Claude Code session with web access and no repository access.
> Research scale: 35 agents, four phases, ~12.2M subagent tokens, 2,259 tool calls, zero errors.
> 290 individual claims adversarially re-verified: **155 clean, 124 corrected, 10 wrong, 1
> unreachable.** Only 53% survived first-pass scrutiny, which is the argument for the verification
> pass having existed.

> **REPOSITORY VERIFICATION, 2026-08-28.** This research ran with web access but **no repository
> access**. Its repo checks have since been run against source, and both deliverables are corrected
> in place -- see the verification block at the top of `code-standard-gap-analysis.md` and section
> 0 of `remediation-plan.md`. Summary: the flaky-test drift theory and the `target-branch`
> Dependabot diagnosis are **refuted**; CodeQL IS running; remediation items 12 and 14 are **false
> positives**; item 13 is mostly already done; item 8's test jobs already exist. A new
> launch-blocking item 0 was added (the Hangfire dashboard authorises everyone). **Revised
> launch-blocking total: ~135 h, not 141**, and the 40 h/month capacity assumption originated in the
> brief rather than from a stated allocation.

---

## 1. What is in this package

### The deliverables (what was asked for)

| File | Size | What it is |
| --- | --- | --- |
| `code-standard-gap-analysis.md` | 72 KB | **Document A.** One-page summary, the nine contradictions with the baseline, the 74-row verification table, per-area detail, the 23-entry REMOVE list, and the three answers |
| `remediation-plan.md` | 26 KB | **Document B.** Ordered backlog, 50 items, effort and launch-blocking on every one, grouped Tier 0 / Tier 1 / do soon / do eventually, with the capacity verdict |

### The supporting record (so the conclusions can be audited, not just inherited)

| File | Size | What it is |
| --- | --- | --- |
| `research-method-and-analysis.md` | 19 KB | **Read this second.** How the research was structured and why, the judgment calls that were mine rather than the agents', where the evidence conflicted and how it was resolved, what could not be verified, and what would change the conclusions |
| `appendix-A-standards-research.md` | 931 KB | Every standards claim from the 14 area agents, with source URL, access date, stated confidence, and the verifier's verdict on that specific claim |
| `appendix-B-blocking-questions.md` | 84 KB | The four targeted research passes in full: ABP/Angular compatibility, SQLite fidelity, independent CVE confirmation, first-party .NET/ABP CI guidance. Every evidence item with its quote |
| `appendix-C-finding-triage.md` | 138 KB | The three triage passes in full: SonarCloud, CodeQL, and REMOVE-list discovery |
| `appendix-D-verification-record.md` | 164 KB | **The integrity record.** What the adversarial pass caught, including a fabricated W3C quotation and a wrong reading of California statute. Per-area results, every non-clean verdict with its correction |
| `raw-research.json` | 1376 KB | The unprocessed structured output from all 35 agents. Machine-readable, for anything the appendices flattened |

**Reading order for a human:** Document A section 1 (one page), then Document A section 2 (the nine
contradictions), then `remediation-plan.md` section 1 (the capacity finding). That is about fifteen
minutes and carries the decision. Everything else is reference.

---

## 2. The five things the receiving session most needs to know

**1. The research was run blind to the codebase, deliberately.** The 14 standards agents were given
the application and team context but not the baseline's list of known gaps. Handing an agent the gap
list produces a standard shaped around those gaps and nothing else. The comparison was done
afterwards. That is why the verification table contains items nobody was looking for.

**2. This session never had the repository.** The working directory held a personal profile README
and three resume PDFs. Every statement about the codebase's current state is inherited from the two
baseline documents. Where the baseline is wrong, the deliverables are wrong in the same place.

**3. Nine findings contradict the baseline.** Table in Document A section 2, each with the check that
settles it. Three are load-bearing: the Dependabot diagnosis is half wrong in a way that changes the
fix from a rewrite to a one-line deletion; the `-warnaserror` step is not redundant and deleting it
naively loses coverage; and the reliability D rating is not accessibility debt at all.

**4. Under 40 minutes of repository access resolves seven open questions** that between them change the
urgency of four recommendations. The list is section 3 below. Do that first.

**5. The capacity answer is uncomfortable and was not softened.** 141 launch-blocking hours against
40 per month is about three and a half months. The brief asked for this to be stated plainly rather
than absorbed by trimming the list, so it was.

---

## 3. Do this first: ~40 minutes with the repository

Every one of these was blocked only by lack of repository access, and each changes something.

| # | Check | How | Changes |
| --- | --- | --- | --- |
| 1 | Does the app use Angular SSR? | grep for `@angular/ssr`, `server.ts` / `main.server.ts`, `provideClientHydration`, and an `ssr` / `server` / `prerender` builder target in `angular.json` | If no SSR, four of the CVEs are **not exploitable here** and drop in urgency. If yes, the Angular bump moves to week 1 |
| 2 | Did `Fix_UniqueIndexesExcludeSoftDeleted` put `HasFilter` in `OnModelCreating`, or only in the migration file? | read both | Confirms or refutes the model/migration-drift diagnosis of the flaky test. If refuted, fall back to the test-order hypothesis |
| 3 | Does the EF test module call `IRelationalDatabaseCreator.CreateTables()`? | read `CaseEvaluationEntityFrameworkCoreTestModule.cs` around line 123 | Confirms whether all 105 migrations really have zero test coverage |
| 4 | Did the CodeQL `javascript-typescript` job actually complete and upload SARIF on the last run? | open the run | Determines whether zero JS/TS alerts is a config bug or a real finding |
| 5 | Does `.yarnrc.yml` carry an explicit `enableScripts: true`? | read the file | Install scripts are the propagation vector every Shai-Hulud variant used |
| 6 | Does CI pass `dotnet restore --locked-mode`? | read the workflow | `RestoreLockedMode=false` in the props file is only a gap if CI does not pass the flag |
| 7 | What is Sonar's actual `ncloc` against your plan's private-analysis cap? | Sonar project page | Marked `UNVERIFIED`; affects the keep-or-kill-Sonar decision |

---

## 4. The handoff prompt

Attach `code-standard-gap-analysis.md`, `remediation-plan.md` and
`research-method-and-analysis.md` at minimum. Attach the appendices if the session will be
challenging specific claims. Then paste:

```text
I commissioned a research project on production code standards and I am handing the results back
to you. Attached are the deliverables and the supporting record.

CONTEXT

The application is a workers' compensation medical-examination scheduling portal: .NET 10, ABP
Commercial 10.0.2, Angular 20.3.19, SQL Server, EF Core, OpenIddict, Redis, MinIO, all
containerised. Multi-tenant, database per office, 11 offices at launch with headroom to 33.
Tenancy resolves from the HTTP Host header only. It is about to move from an internal office LAN
to the public internet for the first time. Maintained by two SDE 1 developers with no senior
engineer, no architect, no security specialist, no DevOps and no QA, at roughly one developer-week
per month for this work. There is no real patient data in it yet; everything is synthetic.

The research established what an industry-standard CI pipeline, test suite and automated check set
looks like in 2026 for an application of this shape, then verified the codebase against it. Scope
was code and CI only; hosting, cloud and infrastructure are a separate exercise already in
progress.

WHAT IS ATTACHED

- code-standard-gap-analysis.md   Document A: one-page summary, nine contradictions with the
                                  baseline, a 74-row verification table, per-area detail, a
                                  23-entry REMOVE list, and answers to the three blocking
                                  questions
- remediation-plan.md             Document B: 50-item ordered backlog with effort and a
                                  launch-blocking yes/no on every item, plus the capacity verdict
- research-method-and-analysis.md How the research was structured, the judgment calls, where the
                                  evidence conflicted and how it was resolved, and what would
                                  change the conclusions
- appendix-A / B / C / D          Raw research with sources, the blocking questions in full, the
                                  finding triage in full, and the adversarial verification record
- raw-research.json               Unprocessed structured output from all 35 agents

Read Document A section 1 and section 2 first. Do not rewrite either deliverable until I ask.

THE CRITICAL LIMITATION

The research session had web access but NOT repository access, so every statement about the
codebase's current state is inherited from the two baseline documents rather than read from
source. You presumably do have the repository. Section 3 of HANDOFF.md lists seven checks totalling
under an hour that were blocked only by that, and between them they change the urgency of four
separate recommendations. Running those and reporting back is the single most valuable thing you
can do next.

THE THREE FINDINGS THAT CHANGE WHAT WE DO

1. AnalysisLevel=latest does not enable the .NET security analyzers. AnalysisLevel selects rule
   vintage; AnalysisMode decides how many are on, and its default enables no Security-category rules
   at all. CA2100 and CA3001 both state "Enabled by default in .NET 10: No". The entire
   CA2100/CA3xxx/CA5xxx band is dead code in our build. Fix is one line:
   <AnalysisModeSecurity>All</AnalysisModeSecurity>.

2. The Dependabot fix is a one-line deletion and our diagnosis was half wrong.
   open-pull-requests-limit: 0 does NOT affect security updates -- GitHub documents that security
   PRs are exempt from that limit. target-branch is the kill switch, and GitHub says explicitly not
   to set it alongside security updates.

3. The SonarCloud reliability D is not accessibility debt. Sonar sets the rating from the single
   worst issue, not volume; the two accessibility rules cap the rating at C and B respectively.
   The D comes entirely from the 8 C#/TS bugs nobody has opened. The 330 accessibility findings
   contribute nothing to it.

RULES OF EVIDENCE THAT WERE APPLIED, AND SHOULD CONTINUE TO APPLY

Every standard asserted carries a source URL and an access date, or is marked UNVERIFIED with what
was tried. 290 individual claims were re-checked by a second adversarial agent instructed to
default to "does not support the claim"; that pass caught a fabricated quotation of a W3C success
criterion, a wrong reading of California Government Code 7405, several thresholds cited to pages
that do not contain them, and a handful of stale version numbers. Appendix D is the record.

Please preserve that convention. Do not replace a cited figure with one from memory, and do not
promote anything marked UNVERIFIED to asserted without opening the source yourself.

WHAT I WANT FROM YOU

Start by telling me what you would do next, and flag anything in the analysis you think is wrong or
that the repository contradicts. Likely next steps, in the order I would guess:

(a) Run the seven repository checks in HANDOFF.md section 3 and report what they change.
(b) Execute Tier 0 week 1 from remediation-plan.md -- seven items, 14 hours, all of them things
    that are currently misreporting success.
(c) Take the capacity finding to the business as a decision rather than letting the calendar
    decide: 141 launch-blocking hours against 40 per month is roughly three and a half months, and
    the three options are laid out in remediation-plan.md section 1.

One framing to preserve: roughly 60% of the first month's value is DELETION, not addition. Fifteen
of the twenty-three REMOVE candidates are not clutter, they are checks that report success without
checking anything -- and the gap they hide is that our merge gate runs no tests and has not since
2026-05-01. Removing them is what makes that visible.

One piece of timing that expires: there is no real patient data yet, so credential rotation across
all 11 tenant databases, destructive upload testing and a full-history secret scan are all free
right now and all become incidents later. Several Tier 0 items are cheap this month and expensive
after go-live.
```

---

## 5. If you only carry three files

`code-standard-gap-analysis.md`, `remediation-plan.md`, `research-method-and-analysis.md`.

The appendices are reference material for challenging specific claims. The method document is what
lets the receiving session tell my reasoning apart from the agents' sourced findings, which matters
more than any individual citation.

---

## 6. Known gaps in this package

Stated so nothing here is mistaken for complete.

- **No repository verification of anything.** See section 3.
- **The January 2025 HIPAA Security Rule NPRM status in 2026 was not conclusively established.**
  Marked `UNVERIFIED` in Document A section 6.11. If it was finalised, vulnerability scanning and
  penetration testing cadences become required rather than advisory, which changes several items
  from "do eventually" to "do now". Worth ten minutes with counsel or the Federal Register.
- **Sonar's `ncloc` against the free-tier private-analysis cap is unresolved.** The first research
  pass concluded the project exceeds it; the verifier found the LOC methodology wrong and noted the
  conclusion may reverse. Marked `UNVERIFIED` rather than asserted either way.
- **CI wall-clock cost of a SQL Server container on GitHub-hosted runners is unmeasured.** No
  primary-source benchmark was found and none was stated from memory. Measure it once before
  committing that suite to the merge gate.
- **ABP Commercial's private registry was unreachable**, so the Angular-compatibility conclusion for
  the Commercial package set is an inference from one Commercial package mirrored on public npm
  (`@volo/abp.ng.account` 10.0.2), not from the complete licensed set. Rated high confidence,
  labelled as inference.
- **Two closed-source Sonar analyzers** (`tssecurity:S6105`, `secrets:S7539`) yielded title, type,
  severity and effort from the live rule API but not the rule prose, because
  `rules.sonarsource.com` was unreachable. The triage says so rather than filling the gap.
