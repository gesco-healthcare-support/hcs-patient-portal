---
feature: doc-hygiene
date: 2026-04-24
status: draft
base-branch: main
related-issues: []
---

# Plan: Documentation Hygiene Pass (post-B-6 Tier 1)

Companion design: [docs/plans/2026-04-24-doc-hygiene-design.md](2026-04-24-doc-hygiene-design.md). Read that first for context, decisions, and parallelism strategy. This file is the executable task list.

## Goal

Ship 7 PRs to `main` that catalogue documentation drift, improve the doc-maintenance skills, regenerate per-entity `CLAUDE.md` files, reorganize `docs/issues/*.md` against `docs/product/` + `docs/gap-analysis/`, consolidate duplicate test-coverage claims, add a cross-stream navigation index, and archive stale root handoffs.

## Context

Tier 1 test-coverage merged 2026-04-24; live test count is 115 methods (+102 vs the inherited "13 methods" claim). Drift density survey across 21 inherited docs found 9 HIGH / 8 MEDIUM / 4 LOW. Sub-task A audit alone is expected to surface 50+ stale findings. Three sibling sessions are actively writing in `docs/product/**`, `docs/implementation-research/**`, and `test/**` -- their paths are off-limits for edits this session.

Constraints (full list in design doc):

- ASCII-only; no PHI; zero-trust verification on every cited claim.
- Write-targets: `docs/audits/**`, `docs/index/**`, `docs/handoffs/**`, my own `docs/plans/2026-04-24-doc-hygiene*.md`, source `CLAUDE.md` files (via skills only), `docs/issues/*.md` (via reorganization pass), `docs/testing/coverage-status.md` (new), root `*.md` (PR-7 only).
- Pause-and-report rhythm every 30 stale rows in any sub-task.
- Skill output must pass ASCII + Gesco-vocab + HIPAA gate before commit.
- No branch deletion without per-event approval, including this branch.

## Approach

Single-session, 7-PR sequenced pipeline with parallelism on read-only Phase 1 (3 Explore agents) and Phase 3 entity-CLAUDE.md regeneration (1 main-context validation gate then 14 parallel `general-purpose` agents). PRs merge sequentially after CI green; PR-1 + PR-7 may open in parallel since their files are disjoint.

**Alternatives rejected:**

- Three-session pipeline (catalogue this session, reorganize next, skills last): rejected because Adrian wants everything done now; deferring loses momentum.
- Single-PR everything bundled: rejected because review surface is too large for one PR; reviewer cannot triage findings independently.
- 15 micro-PRs for entity CLAUDE.md regeneration (one per entity): rejected because review burden multiplies without proportional value. Grouping by stream (tested vs untested) gives 2 PRs of similar size with cohesive narrative each.

## Tasks

### T0 -- Phase 1 parallel reads (research)

- approach: code (no tests required; read-only research pass)
- files-touched: none (subagent reports only)
- description: Dispatch 3 Explore agents in parallel: (A) inventory `docs/product/**/*.md` for which 15 entities have product-intent coverage and quality of coverage; (B) inventory `docs/gap-analysis/**/*.md` for OLD-vs-NEW factual claims keyed by entity; (C) read 4 maintenance skills' bodies (`generate-feature-doc`, `sync-feature-to-docs`, `update-docs`, `verify-docs`) + 15 entity code trees + 15 entity `CLAUDE.md` files.
- acceptance: 3 structured agent summaries returned and consolidated; entity coverage matrix ready (which 10 have `docs/product/` coverage, which 5 are deferred); skill-shape inventory ready for sub-task A's skill-audit row generation.
- stop-points: none (Phase 1 is research only; no commits)

### T1 -- PR-1: 3 audit markdowns

- approach: code
- files-touched:
  - `docs/audits/2026-04-24-doc-freshness-audit.md` (new)
  - `docs/audits/2026-04-24-domain-claude-md-audit.md` (new)
  - `docs/audits/2026-04-24-skill-audit.md` (new)
- description: Catalogue stale claims across the inherited doc surface (excluding sibling-session paths) with the schema `| file | line | claim | status (stale/current/indeterminate) | evidence | proposed fresh text |`. For each entity in the Feature Index, audit the per-entity `CLAUDE.md` for stale/current/missing claims. Audit the 4 maintenance skills against Gesco conventions (ASCII, HIPAA, vocab, line-count caps).
- acceptance:
  - Doc-freshness audit has at least 50 rows with full citation + verified-today evidence.
  - Domain CLAUDE.md audit has one section per of the 15 entities with stale/current/missing counts.
  - Skill audit names exact code locations needing change in PR-2 with rationale per change.
  - All ASCII; all rows have a `path:line` evidence cell or a captured command output; no PHI.
- stop-points:
  - **30-row pause:** After 30 stale rows in any single audit markdown, stop, report category breakdown to Adrian, replan remaining batches.
  - **Before commit:** Show diff summary, get Adrian's go-ahead.
  - **Before push:** Get Adrian's go-ahead.
  - **Before /open-pr:** Get Adrian's go-ahead.

### T2 -- PR-2: improve maintenance skills

- approach: code
- files-touched: skill bodies under `.claude/skills/` for `generate-feature-doc`, `sync-feature-to-docs`, `update-docs`, `verify-docs` (exact paths resolve in PR-1's skill-audit; expected to be in repo's `.claude/skills/<name>/SKILL.md`).
- description: Apply improvements identified by the skill-audit. Likely changes: enforce ASCII output, add HIPAA-safe phrasing checks, add line-count caps consistent with code-standards.md (400 lines for general docs, 250 for component-level), add Gesco vocabulary preferences (e.g. "evaluator" vs "doctor" in product-facing copy where applicable). Keep changes additive and minimal -- skills must remain backward-compatible for the docs-intended-behavior session if it relies on them.
- acceptance:
  - Skill-audit findings each have a corresponding code change with a comment cross-referencing the audit row.
  - All 4 skills run without error on a single test entity (validated in T3 before parallel dispatch).
  - No breaking changes to skill input schema (other sessions can still invoke unchanged).
- stop-points:
  - **Before commit / before push / before /open-pr** (each with Adrian go-ahead).
  - **PR-1 must be merged first** (skill-audit findings drive this PR's content).

### T3 -- skill-fidelity validation gate (single entity)

- approach: code
- files-touched: `src/HealthcareSupport.CaseEvaluation.Domain/Locations/CLAUDE.md` (test target; 1 file); plus throwaway diff log
- description: Run improved `generate-feature-doc` skill against `Locations` in main context. Compare output to current Locations CLAUDE.md and to the actual Locations code (entity, manager, AppService, repo, mapper). Verify: ASCII-only, no PHI, no contradictions with code, no contradictions with `docs/product/locations.md` (if present per T0 inventory), Gesco vocab consistent.
- acceptance:
  - Side-by-side diff captured + 5-bullet fidelity report.
  - Adrian explicitly approves "ship it" / "tweak first" / "regenerate after fix" / "defer" at this gate.
- stop-points:
  - **MANDATORY GATE:** before dispatching 14 parallel agents in T3a/T3b, present diff + report + AskUserQuestion to Adrian. Do not proceed without approval.

### T3a -- PR-3a: regenerate CLAUDE.md (tested entities)

- approach: code
- files-touched: 7 entity CLAUDE.md files: `src/HealthcareSupport.CaseEvaluation.Domain/{Patients,Appointments,DoctorAvailabilities,Locations,ApplicantAttorneys,Doctors,AppointmentAccessors}/CLAUDE.md`
- description: Dispatch 7 `general-purpose` agents in parallel (one per entity). Each agent: pulls latest `main`, runs improved `generate-feature-doc` skill on its entity, runs ASCII + HIPAA gate, applies any necessary post-skill cleanup, commits, returns the diff for synthesis. Main context inspects each diff against fidelity criteria from T3 before final commit.
- acceptance:
  - 7 commits, one per entity, with `docs(domain): regenerate {entity-kebab} CLAUDE.md (B-6 post-Tier-1)` subject.
  - Every regenerated file passes ASCII check + has no contradictions with current code.
  - Diff for each shows expected updates (test-coverage section transitions from "no tests" to actual test list, multi-tenancy claims accurate, etc.).
- stop-points:
  - **PR-2 must be merged.**
  - **T3 gate must have passed Adrian-approval.**
  - **Before commit / before push / before /open-pr.**

### T3b -- PR-3b: regenerate CLAUDE.md (untested entities)

- approach: code
- files-touched: 8 entity CLAUDE.md files: `src/HealthcareSupport.CaseEvaluation.Domain/{Books,AppointmentApplicantAttorneys,AppointmentEmployerDetails,AppointmentLanguages,AppointmentStatuses,AppointmentTypes,States,WcabOffices}/CLAUDE.md`
- description: Dispatch 8 `general-purpose` agents in parallel. Same procedure as T3a. PR-3a + PR-3b can be opened in parallel (disjoint files); merge order between them does not matter.
- acceptance: 8 commits, all checks identical to T3a's acceptance.
- stop-points: same as T3a.

### T4 -- PR-4: reorganize docs/issues/*.md

- approach: code
- files-touched: 9 files: `docs/issues/{ARCHITECTURE,BUGS,DATA-INTEGRITY,INCOMPLETE-FEATURES,OVERVIEW,QUESTIONS-FOR-PREVIOUS-DEVELOPER,SECURITY,TECHNICAL-OPEN-QUESTIONS,TEST-EVIDENCE}.md`
- description: Dispatch up to 9 agents in parallel (or batch by 3 files per agent for context efficiency). Each agent reorganizes its target file using the source-of-truth conflict resolution rule from the design doc: `docs/product/` wins over inherited `docs/issues/*.md`; `docs/gap-analysis/` wins for OLD-vs-NEW factual claims; if `docs/product/` is silent for an entity, fall back to `docs/gap-analysis/`; if both silent, preserve inherited text + `<!-- TODO: product-intent input needed -->`; two inherited docs contradict (both pre-Tier-1) -> mark `indeterminate`, preserve both, do not pick a winner.
- acceptance:
  - Each `docs/issues/*.md` reflects current product intent for the ~10 covered entities.
  - The ~5 uncovered entities have explicit TODO markers that a follow-up PR can grep for.
  - No new issues introduced; no fixes applied (audit + reorganize only -- this is not a remediation pass).
  - Severity ratings preserved per the conflict-resolution rule.
- stop-points:
  - **PR-1 findings must be available** (informs which issue rows are stale).
  - **Read of stable `docs/product/`** before agent dispatch (sibling session is still writing; pull main + check no live PR is open against these files at dispatch time).
  - **Before commit / before push / before /open-pr.**

### T5 -- PR-5: consolidate test-coverage claims

- approach: code
- files-touched:
  - `docs/testing/coverage-status.md` (new canonical)
  - 7+ downstream docs to update with pointer links (exact set resolves from PR-1 audit; expected: `docs/executive-summary.md`, `docs/issues/INCOMPLETE-FEATURES.md`, `docs/issues/TEST-EVIDENCE.md`, `docs/devops/TESTING-STRATEGY.md` if it exists, plus 3-5 entity overviews)
- description: Create a single canonical doc holding the current backend test count + per-entity test-coverage rollup + verification command. Each downstream doc that previously embedded a test-coverage paragraph gets that paragraph replaced with a one-line pointer (e.g. `See [docs/testing/coverage-status.md](../testing/coverage-status.md) for current backend test coverage. Last updated: 2026-04-24.`).
- acceptance:
  - `docs/testing/coverage-status.md` enumerates: total `[Fact]` + `[Theory]` count (verified by Grep on commit day), per-entity test files + counts, last-verified date.
  - 7+ downstream docs replaced their coverage paragraph with a pointer.
  - Grep across `docs/**/*.md` for "13 unique test methods" or "Only Doctors and Books" returns 0 matches after this PR.
- stop-points:
  - **PR-3a + PR-3b merged** (so entity CLAUDE.md coverage claims are fresh).
  - **Before commit / before push / before /open-pr.**

### T6 -- PR-6: doc-streams cross-reference index

- approach: code
- files-touched: `docs/index/doc-streams-cross-reference.md` (new)
- description: Single navigation file that makes the 5 documentation streams (product-intent, gap-analysis, implementation-research, maintainer-reference, issues-register) walkable. Required content: per-stream section (purpose + entry doc + when to consult), entity-keyed matrix (15 rows x 5 columns) with link to the most-specific doc per cell or literal `none`, edit protocol for keeping the index alive. If after reading the streams a matrix is the wrong primitive, pivot to topic-based cross-refs and document the pivot inline.
- acceptance:
  - File exists; >=80 lines.
  - Manual cold-read: pick 5 random matrix cells; each link resolves to a real file (`gh api ... contents` or local Glob).
  - 0 dead links across the matrix.
  - Edit protocol section names which session owns which stream so future sessions know who to coordinate with.
- stop-points:
  - **PR-3 + PR-4 merged** (so entity-doc state is stable for the matrix to reference).
  - **Before commit / before push / before /open-pr.**

### T7 -- PR-7: archive stale root handoffs

- approach: code
- files-touched:
  - `docs/handoffs/2026-04-22-b6-tier1-session-handoff/README.md` (new -- 1-paragraph banner)
  - `docs/handoffs/2026-04-22-b6-tier1-session-handoff/KICKOFF-PROMPT.md` (moved from `W:\patient-portal\KICKOFF-PROMPT.md`)
  - `docs/handoffs/2026-04-22-b6-tier1-session-handoff/B6-TIER1-SESSION-HANDOFF.md` (moved from `W:\patient-portal\B6-TIER1-SESSION-HANDOFF.md`)
- description: Move the 2 stale root markdown files into the handoffs directory (matches existing `docs/handoffs/2026-04-20-gitlab-flow-branching-model-verification/` pattern). Add a 1-paragraph README explaining: these were B-6 Tier 1 handoffs, superseded 2026-04-24 by PRs #129 + #135 + #136 marking Tier 1 complete. Files were untracked at container root; this PR adds them as tracked content under `docs/handoffs/`.
- acceptance:
  - `ls W:\patient-portal\*.md` shows neither file at container root after merge.
  - `docs/handoffs/2026-04-22-b6-tier1-session-handoff/` contains the 2 files + README.
  - PR body links to the existing `docs/handoffs/2026-04-20-...` directory as precedent.
- stop-points:
  - **MANDATORY DESTRUCTIVE-ACTION GATE:** before running any `mv` of root files, present the disposition (still move-to-handoffs?) + AskUserQuestion to Adrian.
  - **Before commit / before push / before /open-pr.**
- can open in parallel with PR-1 (disjoint files; no merge ordering with other PRs)

## Risk / Rollback

**Blast radius:** Worst case, all 7 PRs merge with bad content -> reviewers see drifted-but-now-officially-blessed claims. Mitigation: Adrian-approval gate at every PR plus the skill-fidelity gate at T3.

**Rollback:** Each PR is independently revertable via `gh pr revert <N>` since they merge to `main` and the cascade picks up reverts naturally. PR-3a/3b have the largest blast radius (15 source files); a revert is one PR but propagates to development/staging/production via the auto-cascade workflows. PR-7's `mv` is reversible by `gh pr revert` then re-running the file at container root from the merge SHA.

## Verification

Run after all 7 PRs merge:

1. `git log --oneline origin/main -10` shows 7 new commits, one per PR, in expected order.
2. `ls /w/patient-portal/*.md` returns empty (PR-7 outcome).
3. `ls /w/patient-portal/docs-freshness-audit-2026-04-24/docs/audits/` shows 3 audit markdowns >=100 lines each (PR-1).
4. `find /w/patient-portal/docs-freshness-audit-2026-04-24/docs -name CLAUDE.md | wc -l` returns 15+ (PR-3 + existing).
5. `cat /w/patient-portal/docs-freshness-audit-2026-04-24/docs/testing/coverage-status.md` shows current test count (PR-5).
6. `grep -r "13 unique test methods\|Only Doctors and Books" /w/patient-portal/docs-freshness-audit-2026-04-24/docs/` returns 0 matches.
7. `cat /w/patient-portal/docs-freshness-audit-2026-04-24/docs/index/doc-streams-cross-reference.md` shows the matrix (PR-6).
8. `ls /w/patient-portal/docs-freshness-audit-2026-04-24/docs/handoffs/2026-04-22-b6-tier1-session-handoff/` shows 3 files (PR-7).
9. `gh pr list --state merged --search "doc-hygiene OR doc-freshness OR doc-streams OR docs(audits) OR docs(domain) OR docs(issues) OR docs(testing) OR docs(index) created:>=2026-04-24"` returns the 7 expected PRs.
10. `gh run list --workflow auto-pr-dev.yml --limit 5` shows 7 cascade runs (one per merge), all green.
11. Cold-read: a fresh reader starting at `docs/INDEX.md` reaches any audit markdown in <=3 clicks via the new cross-reference index.

## Stop-point summary (mandatory wait-for-OK gates)

- T0 -> T1: present Phase-1 inventory; replan if entity coverage matrix differs from design assumption.
- 30-row finding: every 30 stale rows across any in-flight audit markdown.
- T2: PR-1 merged before T2 dispatches.
- T3 (validation gate): present diff + fidelity report + AskUserQuestion before T3a/T3b parallel dispatch.
- T3a/T3b: PR-2 merged before parallel dispatch.
- T4: stable `docs/product/` read + PR-1 findings ready.
- T5: PR-3a + PR-3b merged.
- T6: PR-3 + PR-4 merged.
- T7: destructive `mv` confirmation; PR-7 disposition unchanged.
- Every commit / push / `/open-pr` invocation: explicit Adrian go-ahead.
- `gh pr merge --admin --squash` only after `gh pr checks` shows all 23 required checks green.
