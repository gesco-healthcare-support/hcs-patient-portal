# Phase B-6 Kickoff Prompt (Claude Code — new session)

**Saved:** 2026-04-20 at the end of Phase-B closure.
**Purpose:** paste the fenced block below into a fresh Claude Code session at the root of `p:/Patient Appointment Portal/hcs-case-evaluation-portal` when you're ready to start Phase B-6 (test coverage expansion).
**Why this file is local-only:** `.claude/prompts/` is git-ignored per repo convention. This survives session deletion because it lives on disk, not in the Claude context.

---

## How to use

1. Open a fresh Claude Code session at the repo root.
2. Copy everything inside the fenced block below (from the opening ` ``` ` to the closing ` ``` `).
3. Paste as the first user message.
4. The session will run external research first, then codebase research, then pause for your approval. Expect a research-summary reply, NOT code.

---

## The prompt

```
You are starting Phase B-6 of the Gesco Patient Portal Layer-2 CI/CD hardening plan: expand
automated test coverage from ~6% to >=60% overall / >=80% on new code on SonarCloud, while
keeping the Quality Gate green on new code. Phase B itself closed on 2026-04-20; this is the
final outstanding Phase-B criterion.

## Your starting point — read first, in this order

1. docs/plans/PHASE-B6-TEST-COVERAGE-KICKOFF.md — the treasure-map. Read it end to end before
   touching anything else. It has 19 sections plus appendices.
2. docs/verification/PHASE-B-CONTINUATION.md — Phase-B closure record, including the 61
   SonarCloud Security Hotspots Adrian still has to dispose manually.
3. docs/issues/research/P-11.md — the DoctorAvailabilities S3776 refactor you will unblock
   after Tier 1 PR-1B lands.
4. docs/issues/research/SEC-06.md — the Scorecard backlog (Phase C, for your awareness only).
5. Root CLAUDE.md. Then the per-feature CLAUDE.md for every entity you plan to touch in Tier 1
   (at minimum: Appointments, Patients, DoctorAvailabilities, Locations, ApplicantAttorneys).

## DO NOT skip to the codebase. External research first.

Before you open a single .cs file in this repo, conduct genuine external research to understand
the how, why, and what of testing a .NET + ABP + Angular application of this shape. By research
I mean: read official documentation, tutorials, conference talks, community-expert articles,
and study similar repositories with >=500 GitHub stars. Do not rely on training data — verify
every version-specific claim against a live source at today's date per
.claude/rules/zero-trust-verification.md.

Research rubric — you must be able to answer these from external sources, not from this repo:

1. ABP testing model: how does AbpIntegratedTest<TStartupModule> compose modules for tests?
   How does IDataSeedContributor fit into module initialization? When is SeedAsync called and
   in what order when multiple contributors exist? What are the real-world pitfalls people hit
   in production ABP codebases? (Start: https://abp.io/docs/latest/testing + abpframework/abp
   on GitHub + eShopOnAbp. Look at real test suites, not just docs.)
2. EF Core testing strategy: what are the tradeoffs of SQLite in-memory vs in-process SQL
   Server vs Testcontainers? When do you hit cases where SQLite passes but SQL Server fails?
   Who has written publicly about this? (Start: Microsoft EF Core testing guide + Jon P Smith
   + Andrew Lock blog posts + 500+ star repos using EF Core in tests.)
3. xUnit collection fixtures: what exactly does ICollectionFixture<T> guarantee about instance
   lifetime, parallelism, and test isolation? What breaks when you get it wrong?
4. Coverage targets: is 60% overall + 80% new-code a defensible target? Google / Microsoft /
   ABP publish their own targets; compare. What do industry experts (Fowler, Osherove,
   Khorikov) say about coverage percentages vs test quality?
5. Test-pyramid placement: why are E2E tests not part of this phase? Cite Fowler's test pyramid
   and find at least one contrary view (e.g., Kent C. Dodds "Testing Trophy") so you
   understand the tradeoff you are making.
6. HIPAA + synthetic test data: beyond .claude/rules/test-data.md, what do HHS Safe Harbor,
   NIST, and healthcare testing literature say? Are there libraries (Bogus, AutoFixture) that
   generate synthetic data safely? Which ones are HIPAA-compatible?
7. Testing multi-tenancy in ABP: how do experienced ABP teams test IMultiTenant behaviour? How
   do they handle the CurrentTenant.Change() pattern? What about entities like this project's
   Patient that have TenantId without IMultiTenant?
8. Testing Angular 20 standalone components: since this project has 0 Angular tests today and
   Karma is being deprecated, should Tier 1 introduce Karma spec files, or is Jest / Angular
   CLI's test migration a better foundation? Research the Angular team's current direction.
9. Pre-existing bug encoding: this repo has known behaviour gaps (Appointment state machine
   not enforced, Patient non-IMultiTenant, slot not released on delete). How do mature testing
   cultures encode existing gaps — failing tests with xUnit's [Fact(Skip="...")], separate
   tests with a "KnownGap" category, or behaviour-matching tests with TODO comments?
10. Commit-stage hygiene: your tests must build 0/0 with -warnaserror. Research how other
    ABP/Angular repos keep their test suites warning-free under aggressive analyzer settings.

For each of the 10 topics above, produce a short written answer citing at least two external
sources (one official doc or primary source, one community or expert secondary source). Prefer
sources from the last 18 months. If a source contradicts the kickoff doc, flag the
contradiction explicitly.

## Then codebase research

Once you have written research notes for the 10 topics, switch to the codebase:

- /load-context
- /feature-research with scope: "Phase B-6 Tier 1 test coverage baseline"
- Read every per-feature CLAUDE.md for Tier 1 entities
- Verify what sections 4-13 of the kickoff doc claim against what is actually on main. The
  kickoff was written 2026-04-20; drift is expected.

Produce a structured research summary with these sections:

- What the external sources taught you (10 topics)
- What the kickoff doc got right
- What the kickoff doc got wrong or is stale about (be specific: line numbers)
- Baseline numbers you re-verified (coverage %, test count, Sonar issue count)
- Surprises / gotchas discovered in the external research that the kickoff missed
- Proposed Tier-1 PR order and scope (not a plan yet — just a proposal to discuss)
- Open questions for Adrian — anything you need decided before writing the Tier-1 plan

## Then pause

Do NOT run /feature-design yet. Post the research summary. Wait for Adrian's questions and
answers. Adrian WILL push back on your proposal — that is the point. You are expected to
defend your choices or update them based on his input.

Only after Adrian explicitly approves the research summary AND the proposed Tier-1 scope do
you proceed to /feature-design. /feature-design writes docs/plans/YYYY-MM-DD-phase-b6-tier1.md
(one plan file per tier). Do NOT write the Tier-2 plan until Tier 1 has merged to main and the
coverage delta has been measured on SonarCloud.

## Hard constraints (non-negotiable — these are also in the kickoff)

- One tier per plan file. One entity per PR inside each tier.
- Every commit: dotnet build -warnaserror = 0/0. dotnet test passes all discovered classes.
- Synthetic data only. No realistic patient names, SSNs, emails, phone numbers, DOBs.
- [Collection(CaseEvaluationTestConsts.CollectionDefinitionName)] on every concrete EF test.
- WithUnitOfWorkAsync wrapper on every repository test.
- Test-only PRs. If a test reveals a bug, open a separate bug-fix PR with approach: tdd.
- Do NOT add E2E / Playwright / Cypress to Phase B-6 scope. That is Layer 3 / Phase C+.
- Do NOT touch the P-11 suppression in sonarcloud.yml until Tier 1 PR-1B
  (DoctorAvailabilities) has landed and tests pass.
- Solo-dev workflow: Adrian admin-merges after CI is green. Do not request self-approval.

Begin.
```

---

## If this file gets lost

A working copy also exists as part of the Phase-B closure session transcript (saved in
`C:/Users/RajeevG/.claude/projects/p--Patient-Appointment-Portal-hcs-case-evaluation-portal/`).
The exact content can be reconstructed from `docs/plans/PHASE-B6-TEST-COVERAGE-KICKOFF.md`
(which is the source of truth) plus the research rubric enumerated above.
