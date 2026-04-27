---
feature: doc-hygiene
date: 2026-04-24
status: design
base-branch: main
related-issues: []
---

# Design: Documentation Hygiene Pass (post-B-6 Tier 1)

## Context

B-6 Tier 1 merged to `main` on 2026-04-24 (PRs #99, #101, #102, #103, #104, #115, #129, #135, with #136 marking Tier 1 complete). Tier 2 is mid-flight: #141 (Wave-2 seeds) and #142 (AppointmentAccessors tests) merged today. Live backend test count is 113 `[Fact]` + 2 `[Theory]` = 115 methods across 17 files. Inherited documentation still claims 13 test methods covering only Doctors and Books; contradicts today's reality by +102.

Drift density survey (21 files) found 9 HIGH-drift, 8 MEDIUM, 4 LOW. Expected stale findings in sub-task A alone: 50+.

Three sibling sessions are actively writing: `docs-intended-behavior` (docs/product/), `implementation-research` (docs/implementation-research/), and the test-coverage session (cycling through test/* branches, currently on `test/appointment-applicant-attorneys-coverage`). Each session has exclusive edit rights on its paths.

The original plan was a 4-PR read-only catalogue pass. Adrian expanded scope on 2026-04-24 to include: reorganizing `docs/issues/*.md` against `docs/product/` (primary truth) + `docs/gap-analysis/` (secondary truth), consolidating duplicated test-coverage claims, and auditing/improving/triggering the repo's maintenance skills so CLAUDE.md files auto-regenerate as drift is detected. Everything ships in this session; parallelize whatever is independent.

## Goal

Consolidated post-Tier-1 doc-hygiene pass that produces audit reports, improved doc-maintenance skills, auto-regenerated per-entity `CLAUDE.md` files, reorganized `docs/issues/*.md`, consolidated duplicate claims, a cross-stream navigation index, and archival of stale root handoffs -- shipped as 7 merge-sequential PRs with parallel open where files are disjoint.

## Scope

### In scope (this session)

- Read-only audit across all non-excluded inherited docs.
- Per-entity domain CLAUDE.md audit + regeneration via improved skills (15 entities).
- Skill audit + improvements for `generate-feature-doc`, `sync-feature-to-docs`, `update-docs`, `verify-docs`.
- Reorganize `docs/issues/*.md` against `docs/product/` for the ~10 entities with product-intent coverage. The ~5 uncovered entities get TODO markers + deferred.
- Consolidate duplicate test-coverage claims into canonical `docs/testing/coverage-status.md`; update 7+ downstream docs with pointer links.
- `docs/index/doc-streams-cross-reference.md` -- entity-keyed matrix across 5 streams.
- `docs/handoffs/2026-04-22-b6-tier1-session-handoff/` -- archive stale root `KICKOFF-PROMPT.md` + `B6-TIER1-SESSION-HANDOFF.md`.

### Out of scope (exclude or defer)

- `docs/product/**` (owned by `docs-intended-behavior` session).
- `docs/gap-analysis/**` (completed 2026-04-23; read-only source).
- `docs/implementation-research/**` (owned by `implementation-research` session).
- Other sessions' in-flight plan files under `docs/plans/` (read-only for reference).
- Test-file modifications, CI-workflow modifications, domain-code modifications (docs-only session).
- Swagger-OAuth-in-Docker fix (FEAT-08) and any other open-issue resolution -- audit and flag only.
- Entities without `docs/product/` coverage: flag and defer the issues-reorganization for those.

## Decisions locked

1. **Worktree + branch:** `W:\patient-portal\docs-freshness-audit-2026-04-24\` on `docs/freshness-audit-2026-04-24`, HEAD 3559065, created via `scripts/worktrees/add-worktree.sh`.
2. **Execution shape:** do everything in this session; parallelize independent work.
3. **30-finding pause-rhythm:** after every 30 stale rows across any in-flight sub-task, stop, report counts + category breakdown, get guidance, replan the remaining batches.
4. **Source-of-truth conflict resolution:**
   - `docs/product/` wins over `docs/issues/*.md` -- issue wording updated to match product intent.
   - `docs/gap-analysis/` wins over `docs/issues/*.md` for OLD-vs-NEW factual claims; `docs/issues/` keeps the severity rating.
   - If `docs/product/` is silent on an entity: fall back to `docs/gap-analysis/`. If also silent: preserve inherited text + `<!-- TODO: product-intent input needed -->`.
   - Two inherited docs contradict each other (both pre-Tier-1): flag as `indeterminate` in the audit; do not pick a winner.
5. **Consolidation target for test-coverage claims:** new canonical `docs/testing/coverage-status.md`. 7+ downstream docs replace their coverage paragraph with a one-line pointer + date stamp.
6. **PR-3 shape:** grouped by stream -- one PR for Tier-1-tested entities (Patients, Appointments, DoctorAvailabilities, Locations, ApplicantAttorneys, Doctors, AppointmentAccessors), one PR for untested entities (Books + the 7 remaining).
7. **Skill-fidelity gate:** before dispatching 15 parallel regeneration agents in Phase 3, test the improved skill on ONE entity in main context; report fidelity to Adrian before proceeding.
8. **Design + plan file paths:** `docs/plans/2026-04-24-doc-hygiene-design.md` (this file) + `docs/plans/2026-04-24-doc-hygiene.md` (plan file produced by `/feature-design`).

## PR pipeline

| # | Type + scope | Deliverables | Depends on | Opens in parallel with |
| --- | --- | --- | --- | --- |
| PR-1 | `docs(audits):` consolidated catalogue | `docs/audits/2026-04-24-doc-freshness-audit.md`, `docs/audits/2026-04-24-domain-claude-md-audit.md`, `docs/audits/2026-04-24-skill-audit.md` | none | PR-7 |
| PR-2 | `chore(skills):` improve doc-maintenance skills | changes to `generate-feature-doc`, `sync-feature-to-docs`, `update-docs`, `verify-docs` skill bodies under repo's `.claude/skills/**` | PR-1 merged (skill-audit findings inform) | -- |
| PR-3a | `docs(domain):` regenerate CLAUDE.md (tested entities) | 7 entity CLAUDE.md files: Patients, Appointments, DoctorAvailabilities, Locations, ApplicantAttorneys, Doctors, AppointmentAccessors | PR-2 merged | PR-3b (disjoint files) |
| PR-3b | `docs(domain):` regenerate CLAUDE.md (untested entities) | 8 entity CLAUDE.md files: Books + AppointmentApplicantAttorneys, AppointmentEmployerDetails, AppointmentLanguages, AppointmentStatuses, AppointmentTypes, States, WcabOffices | PR-2 merged | PR-3a |
| PR-4 | `docs(issues):` reorganize issues register | 9 `docs/issues/*.md` files updated per conflict-resolution rule; 5 uncovered entities get TODO markers | PR-1 findings + stable `docs/product/` read | PR-5, PR-6 |
| PR-5 | `docs(testing):` canonical coverage doc | `docs/testing/coverage-status.md` new; 7+ downstream docs updated to point here | PR-3a + PR-3b merged (so entity CLAUDE.md coverage claims are fresh) | PR-4 |
| PR-6 | `docs(index):` cross-stream nav | `docs/index/doc-streams-cross-reference.md` + an edit-protocol entry | PR-3 + PR-4 merged | PR-5 |
| PR-7 | `chore(cleanup):` archive root handoffs | `docs/handoffs/2026-04-22-b6-tier1-session-handoff/README.md` + 2 moved files | none -- root files are untracked, no upstream conflict | PR-1 |

Merge order: PR-1 -> PR-2 -> (PR-3a || PR-3b) -> (PR-4 || PR-5 || PR-6) -> PR-7. PR-7 is order-independent and can merge any time.

## Parallelism strategy

**Phase 1 (research reads)** -- 3 Explore agents dispatched in parallel:

- Agent A: `docs/product/**/*.md` inventory (which entities have coverage, quality of coverage, intended behaviours).
- Agent B: `docs/gap-analysis/**/*.md` inventory (OLD-vs-NEW findings for secondary-source reverification).
- Agent C: 4 maintenance skills' bodies + sample outputs + the 15 entity code trees (`Domain/{Entity}/` + `Application/{Entity}/` recursively).

Expected wall-clock: ~5 min. No conflict risk (read-only).

**Phase 3 (entity CLAUDE.md regeneration)** -- sequential validate, then parallel dispatch:

1. Main context: test improved `generate-feature-doc` on ONE entity (propose: `Locations` -- medium complexity, tests landed recently).
2. Report fidelity to Adrian. On approval:
3. Dispatch 14 `general-purpose` agents (one per remaining entity) with precise task briefings. Each agent: read CLAUDE.md + code, run the skill, commit the regenerated file, report back.
4. Main context synthesizes agent outputs and commits PR-3a + PR-3b.

Expected wall-clock: ~10 min for parallel phase vs 2-3 hrs serial.

**Phase 4 (docs/issues reorganization)** -- parallel per-file:

- 9 `docs/issues/*.md` files dispatched to 9 general-purpose agents (or 3 agents handling 3 files each). Each agent reads the target file + relevant `docs/product/` + `docs/gap-analysis/` entries, applies the conflict-resolution rule, produces the reorganized content.

Expected wall-clock: ~8 min vs 45 min serial.

**Phases 5, 6, 7** -- sequential in main context, each small.

## Risk mitigations

- **Baseline drift during audit.** Other sessions merging to main concurrently. Mitigation: pull `main` + rebase before every PR; re-verify any just-cited line numbers before committing.
- **Skill regressions.** Improved skills might break for edge-case entities (e.g. Books which is vestigial). Mitigation: Phase 3 validation gate on one entity before parallel dispatch; per-entity fallback ladder (regenerate with tweak / hand-edit / defer).
- **PR-3 file conflict between 3a and 3b.** None in practice -- 3a touches 7 entity dirs, 3b touches 8 disjoint dirs. Safe to merge either order.
- **`docs/product/` incompleteness.** 5-8 entity intent docs still being written. Mitigation: PR-4 explicitly skips those entities with TODO markers. Adrian opens a follow-up after `docs-intended-behavior` session completes.
- **Skill output contradicts Adrian's style.** Mitigation: ASCII-check + Gesco-vocab check before commit; escalate to Adrian via AskUserQuestion if ambiguous.
- **Finding count overshoot.** If sub-task A's audit catalogues 90+ stale rows, the 30-finding pause triggers 3x. Mitigation: pause-report-replan as planned; accept that the session may go deeper than estimated.
- **HIPAA.** Mitigation: pre-commit PHI scanner hook runs on every tool use; this is passive but non-trivial. No examples involving real patient data; synthetic only.

## Testing / verification

- **Per-PR CI:** `gh pr checks <N>` green on all 23 required checks before merge request.
- **Per-PR smoke:** for PR-3: manually diff 3 random regenerated CLAUDE.md files against their code; confirm claims match. For PR-4: cold-read 2 random `docs/issues/*.md` entries and verify they match the product intent.
- **End-to-end verification after all merges:**
  - `git log --oneline origin/main -10` shows 7 new commits (one per PR).
  - `ls W:\patient-portal\*.md` returns empty (no stale root markdown).
  - `docs/audits/` contains 3 audit markdowns.
  - `docs/testing/coverage-status.md` exists + 7+ downstream docs link here.
  - `docs/index/doc-streams-cross-reference.md` exists; manually click 5 random links, 0 dead.
  - `docs/handoffs/2026-04-22-b6-tier1-session-handoff/` contains the 2 moved files + a README.
  - `auto-pr-dev.yml` opens a green `main -> development` cascade PR after the last merge.

## Hard guardrails

- ASCII-only everywhere (enforced by commit-message hook per `~/.claude/rules/code-standards.md`).
- No PHI in any excerpt, example, or audit row (enforced by PHI scanner hook).
- Zero-trust verification on every audit row -- every stale finding carries a command/grep/Read executed today with output captured in the audit.
- No writes to other sessions' paths (`docs/product/**`, `docs/gap-analysis/**`, `docs/implementation-research/**`, `docs/plans/**` outside my own two files).
- No branch deletion without Adrian's per-event approval (including after squash-merge of my own branch).
- No `ng serve`, `yarn start`, `ng build --watch`, `dotnet run --watch`, `DbMigrator` -- not needed for docs work.
- `DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development` on any dotnet command (none planned in this session).
- Use `scripts/worktrees/add-worktree.sh` for any additional worktrees; never raw `git worktree add`.

## Open items (resolve during /feature-design or early in execution)

- Final inventory of which entities have `docs/product/` coverage vs which are deferred (resolves in Phase 1 Agent A).
- Final count of maintenance skills that need improvement vs which are already Gesco-compliant (resolves in PR-1 skill audit).
- Exact wording of the `docs/testing/coverage-status.md` canonical format (resolves in PR-5 drafting).
- Whether PR-4 needs a separate "deferred entities" follow-up tracking issue (decide after reading Phase 1 inventory).

## Next step

Hand off to `/feature-design` to produce an executable plan file at `docs/plans/2026-04-24-doc-hygiene.md` with per-task acceptance criteria and per-task `approach` flag (all `code`, no tests required -- docs-only work, per `~/.claude/rules/rpe-workflow.md` table). Per-task stop points will land in the plan file.
