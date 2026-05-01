# Kickoff prompt for the fresh Claude Code CLI session (revised 2026-04-23)

Paste EVERYTHING between the two `---PROMPT-START---` / `---PROMPT-END---` fences into your new Claude CLI session. The fresh session starts cold: it sees the repo, `MEMORY.md`, and global rules, but it has no memory of the previous VS Code session. This prompt is self-contained to fix that.

---PROMPT-START---

You are Claude Code in a fresh CLI session, helping Adrian (sole developer at Gesco) finish **Phase B-6 Tier 1 test coverage** on the Patient Portal repo. Four of six Tier-1 PRs are merged; two remain: PR-1D (Locations) and PR-1E (ApplicantAttorneys). A prior session did a full plan consolidation on 2026-04-22 and produced two handoff files at the root of this clone:

- `W:\patient-portal\B6-TIER1-SESSION-HANDOFF.md` -- branch-agnostic context brief. Read this FIRST.
- `W:\patient-portal\KICKOFF-PROMPT.md` -- this file.

`W:\patient-portal\` now contains sibling worktrees -- `main`, `development`, `staging` -- laid out by the helper scripts at `scripts/worktrees/` on main. All four git branches (main, development, staging, production) are aligned as of 2026-04-23 via the cascade work done earlier that day. You can read any canonical file from `W:\patient-portal\main\` since main is content-current.

## Mandatory first reads (in order)

1. `W:\patient-portal\B6-TIER1-SESSION-HANDOFF.md` -- read ALL of it. It describes PR-1D / PR-1E scope, the p:\-era prep that is now irrelevant (P: no longer holds project code as of 2026-04-22; the repo is cloned into `C:\src\patient-portal\` and substituted as `W:\`), and three decisions Adrian has to make.
2. `W:\patient-portal\main\docs\plans\2026-04-20-phase-b6-tier1.md` -- canonical plan. Read sections "T5 - PR-1D" and "T6 - PR-1E" in full. Scan the rest. The plan file also lives on development and staging (content is identical across the four branches); pick whichever worktree you are sitting in.
3. `W:\patient-portal\main\docs\handoffs\2026-04-20-gitlab-flow-branching-model-verification\VERDICT.md` -- explains why the canonical plan's `base-branch: development` frontmatter is known-off-spec. You will need this for decision 1 below.
4. `W:\patient-portal\main\CLAUDE.md` -- project rules. Skim.

Do NOT read the whole canonical plan blindly; go deep on T5 and T6 only.

## First action (REQUIRED stop point -- before any code or worktree creation)

After the four reads, reply to Adrian with, in under ~250 words:

1. **What I understood** -- 3-5 bullets confirming the state (4 PRs merged, 2 remaining, where the plan lives on every branch post-reconciliation, that the cascade main -> development -> staging -> production is operational and automated through `auto-pr-dev.yml` + `deploy-dev.yml`, and that all per-worktree tooling -- `docker compose up -d`, helper scripts, husky hooks on Windows worktrees -- was validated end-to-end on 2026-04-23 via PR #125).

2. **The three decisions I need from you**, each as Option A / Option B / Option C with a "(Recommended)" flag and a one-line reason. Do NOT invent new options:

   (a) **Target branch for the two PRs.** Option A: merge to `main` (GitLab-flow proper; PRs then cascade automatically through dev -> staging -> production via the promotion workflows). Option B: merge directly to `development` (matches the canonical plan's `base-branch: development` frontmatter, which VERDICT.md documents as known-off-spec from earlier Phase B-6 work). Option C: ask Adrian to resolve the spec drift before picking either. **(Recommended: A)** because yesterday's reverse-merge put main back in sync and all cascade automation targets main-first; continuing to land PRs on main closes the gap rather than widening it.

   (b) **Worktree topology for PR-1D and PR-1E.** Option A: one feature worktree (e.g. `W:\patient-portal\test-locations-coverage`) hosting two sequential branches via `git checkout -b` inside it -- branch 1 for PR-1D, merge, then branch 2 for PR-1E in the same directory. Option B: two separate feature worktrees, one per PR. Option C: work directly in main's worktree without a dedicated feature worktree. **(Recommended: A)** because Adrian works the two PRs sequentially, one worktree keeps docker / node_modules / bin caches warm across both, and the 2026-04-23 smoke test confirmed sub-branches inside one worktree don't spawn new worktrees; Option B wastes build cache and Option C contaminates main's working directory.

   (c) **Disposition of the two handoff .md files.** Option A: leave at `W:\patient-portal\` root as untracked branch-independent scratch. Option B: move into `docs/handoffs/2026-04-22-b6-tier1-session-handoff/` on `main` and cascade (matches the existing `docs/handoffs/2026-04-20-gitlab-flow-branching-model-verification/` pattern). Option C: delete after PR-1E merges. **(Recommended: B)** if Adrian wants the decision trail preserved alongside the other handoff; otherwise C because the files are session-scoped and the canonical plan carries the durable context.

3. **Wait for Adrian's reply.** Do not invoke `add-worktree.sh`, do not checkout a branch, do not draft tests until he has answered all three.

## Decision latitude

- **Full latitude:** test design within the canonical plan's T5 / T6 acceptance criteria. Naming, `[Theory]` vs `[Fact]` split, assertion style (Shouldly), seed-data shapes within the stated constraints. Draft, review, refine.
- **No latitude; must stop and ask Adrian:**
  - Choosing a target branch, worktree topology, or handoff-file disposition (covered by the three decisions above).
  - Committing, pushing, merging, tagging, or deleting anything on main, development, staging, or production directly.
  - Deviations from the canonical plan's T5 / T6 task spec.
  - Creating a worktree any way other than `scripts/worktrees/add-worktree.sh <branch>` -- because the helper allocates non-colliding ports, seeds `.env` + `.env.local`, copies secrets, renders Local.json + environment.local.ts, and installs deps. Raw `git worktree add` skips all of that and reproduces the gaps PR #125 just fixed.
  - Editing the canonical plan's frontmatter -- because the off-spec `base-branch: development` is an intentional artifact noted in VERDICT.md.
  - Editing tracked files outside the two remaining PRs' scope (tests-only PRs).
- **Stop points (mandatory wait-for-OK per PR):**
  1. After the first-read summary (above).
  2. After drafting the test list for PR-1D, before writing any `.cs` file.
  3. After the last test passes locally, before `git commit`.
  4. After the commit passes husky hooks, before `git push`.
  5. After CI is green, before `gh pr merge --admin --squash`.
  Same five stops repeat for PR-1E.

## Non-negotiable guardrails

- `[RemoteService(IsEnabled = false)]` on every AppService -- because ABP otherwise registers duplicate routes alongside the manual controller.
- Riok.Mapperly only -- because `ObjectMapper.Map<>` / AutoMapper is not wired in this project.
- Every concrete EF test class carries `[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]` -- because SQLite in-memory connection sharing breaks across classes otherwise.
- Every repository test body wrapped in `await WithUnitOfWorkAsync(async () => { ... })` -- because ABP repository calls require an active UoW in tests.
- Synthetic data only, via Bogus v35.6.5 with `Randomizer.Seed = new Random(20260420)` -- because the PHI scanner hook blocks real data and deterministic seeds make assertions stable.
- Branch names describe the work (e.g., `test/applicant-attorneys-coverage`), not plan steps -- because plans get deleted; branch names survive in `git log`. Pass `test/<short-slug>` as the full branch name to `add-worktree.sh`; the helper derives the directory slug automatically.
- Commit subject format: `test({entity-kebab}): add <short scope> (B-6 T1)` -- matches merged history and `~/.claude/rules/commit-format.md`.
- `gh pr merge --admin --squash` only after every required CI check is green (currently `Backend: Build` and `Frontend: Build`) -- because Adrian is solo and cannot self-review, but admin does not mean bypass failing CI.
- Never delete branches without Adrian's explicit approval -- even after a successful squash-merge.
- On every dotnet command, export `DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development` -- per `.claude/rules/dotnet-env.md`.
- Zero-trust verification applies: verify every claim -- Adrian's input, your training knowledge, subagent output, prior session data -- against live sources before acting. Per `~/.claude/rules/zero-trust-verification.md`.

## Success criteria

- PR-1D: 9 new `[Fact]` passing locally AND in CI, all T5 acceptance criteria met, merged to the branch Adrian picked in decision (a) via admin squash after his OK.
- PR-1E: 11 new `[Fact]` passing locally AND in CI, all T6 acceptance criteria met, merged via admin squash after his OK.
- After PR-1E merges: report the actual SonarCloud overall-coverage delta (baseline 7.3%; plan target 25-30%).
- If decision (a) was A (merge to main), verify the cascade through development / staging / production completes; if it stalls, report and let Adrian unblock.

## If you get stuck

- Three retries max on any single technical problem before stepping back and asking Adrian. Per `~/.claude/rules/code-standards.md` error-handling protocol.
- If you believe a canonical-plan constraint is wrong or stale, STOP. Do not "correct" it. Raise it to Adrian with evidence (file paths, grep output, CI logs).
- If husky rejects a commit with a `$REPO_ROOT/$1` ENOENT path error you might have seen in older logs, that was fixed in PR #125; re-run `yarn install` in the worktree to regenerate the husky `_/` dispatchers before debugging further.

## What you are NOT doing

- You are NOT creating worktrees via raw `git worktree add`. Use `scripts/worktrees/add-worktree.sh` exclusively.
- You are NOT touching `main`, `development`, `staging`, or `production` working copies directly. All changes go through a feature branch + PR.
- You are NOT writing the Tier 2 plan (deferred until post-PR-1E SonarCloud measurement).
- You are NOT fixing the canonical plan's `base-branch: development` frontmatter.
- You are NOT touching `PHASE-B6-TEST-COVERAGE-KICKOFF.md` if you encounter it -- Adrian will decide when to delete it.
- You are NOT operating against any `p:\` path. The `P:` drive letter on this machine now points at `C:\Users\RajeevG\Documents\Projects`, which holds the user's other three project roots (Case Tracking, MRR AI, Digital Forms). Do not read, edit, or clean anything under `P:\`.

Begin by reading the four mandatory files listed above, in order. Report back when done.

---PROMPT-END---

## How to use this

1. Open a new VS Code window or terminal at `W:\patient-portal\`.
2. Start a Claude Code CLI session there.
3. Paste everything between `---PROMPT-START---` and `---PROMPT-END---` as your first message.
4. Answer the session's three decision questions on turn 2.
5. Proceed with PR-1D.

## Notes for Adrian

- Both files (`B6-TIER1-SESSION-HANDOFF.md` + `KICKOFF-PROMPT.md`) live at `W:\patient-portal\` ROOT, not inside any worktree. They are branch-independent and the new session can read them regardless of which worktree it cd's into. They are NOT tracked by git, so C: cleanup that touches `AppData\Local\Temp` or similar does not affect them -- they live under `C:\src\patient-portal\` which is project data, not cache.
- Disposition is decision (c) in the prompt above. If you decide to track them in the repo, the prompt recommends `docs/handoffs/2026-04-22-b6-tier1-session-handoff/` on `main` (matches the existing `2026-04-20-gitlab-flow-branching-model-verification/` handoff pattern).
- This prompt was rewritten on 2026-04-23 to reflect the post-worktree-setup state. The previous revision was written before the worktree helper scripts, docker compose per-worktree parameterisation, and the Sqlite-NRE / husky-path / .env-seed / docker-secrets-copy fixes landed. The rewrite uses the write-prompt rubric at `~/.claude/rules/prompt-writing.md`. If the prompt needs another refresh for a future session, context on what landed since is in `~/.claude/projects/C--Users-RajeevG/memory/worktree_setup_lessons.md`.
