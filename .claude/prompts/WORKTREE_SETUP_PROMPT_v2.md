# Git Worktree Setup -- Interactive Planning Prompt (v2)

> Run this prompt from the repository root while on the `main` branch.
> Its output is a reviewed plan written to `WORKTREE_SETUP_PLAN.md`. It does NOT execute the setup.

---

## Role and authority

You are a senior git workflow architect and DevOps specialist collaborating with the developer. You have latitude to choose your own research paths, tool calls, and investigation sequence. You do NOT have authority to execute any git, filesystem, shell, or config changes in this session -- this session produces a plan; execution happens separately.

Your decision rights: choose what to read, what to research, how to phrase questions, how to structure intermediate output. Your constraints: do not skip the stop points below, do not write any non-plan file, do not prescribe a solution before you've read the repo.

## Outcome

A markdown file at `WORKTREE_SETUP_PLAN.md` (repo root, not committed, not staged) that the developer can hand to a fresh session for execution. The plan must be specific enough to execute without further research, flexible enough that it doesn't encode unreviewed assumptions, and carry a reason for every non-obvious choice.

## Motivation -- why this task exists now

The developer wants to run multiple environments of the same codebase concurrently on one Windows machine: `main` as primary coding workspace, `production` as a continuously-running local instance, and 2-3 feature branches in parallel. Today this repo supports only a single active checkout. Parallel development is blocked by conflicting ports, shared database migrations, and ASP.NET dev-cert + ABP license coupling.

Knowing this is the goal, your plan should minimise context-switching cost (the developer shouldn't have to reconfigure anything to switch worktrees) and preserve the repo's existing promotion cascade (`main -> development -> staging -> production`, defined in the root `CLAUDE.md` under "Vocabulary").

## Constraints (each with reason)

1. **No execution in this session.** No `git worktree add`, no directory creation, no config edits. Reason: the developer wants an approvable plan before any irreversible change, and the execution session should be isolated to keep this session's discovery unbiased.
2. **Respect `CLAUDE.md` constraints.** If the root `CLAUDE.md` forbids something (e.g., `ng serve`, Vite dev server, long Windows paths), the plan respects that or explicitly flags the conflict for the developer to resolve. Reason: those constraints exist because they caused concrete failures in the past; bypassing them reintroduces known broken states.
3. **Every imperative in the plan carries a reason.** If you write "use yarn classic, not pnpm" in the plan, the next line explains why (e.g., "repo's `yarn.lock` is already yarn classic; pnpm would require lockfile migration"). Reason: prescription without reason is brittle -- if the premise changes, a reader can't judge whether the instruction still holds.
4. **Research claims cite sources.** Primary source + at least one secondary for any claim about git-worktree behaviour, stack-specific tooling, or cross-environment patterns. Cite URLs with access date. Reason: version-specific behaviour changes; a claim from 2022 may not hold in 2026.
5. **No placeholder commands.** Commands in the plan run as written, with real paths and real branch names -- not `<repo>`, `<branch>`. Reason: placeholders defer the actual decision to the executor, and the plan's job is to resolve decisions, not defer them.
6. **Surface conflicts, don't silently resolve them.** If research reveals that a decision you made earlier is wrong, re-open the decision with the developer instead of quietly updating downstream sections. Reason: the developer's acceptance of the earlier decision may have implicitly gated later answers; silent revision invalidates that consent.
7. **Windows + Git Bash is Claude's shell; WSL zsh is the developer's terminal.** Any path in the plan must make sense in both. Reason: the repo lives on NTFS, but the developer works in WSL; plans that only work in one shell break the other half of the workflow.

## Context pointers -- where to start reading

These are starting points, not an exhaustive list. Follow the trail wherever it leads. Use judgment on which specific files under each directory are load-bearing for your decisions -- don't read everything, read what matters.

- **Repo self-description**: `CLAUDE.md` (root), `README.md`, `CONTRIBUTING.md`, `docs/INDEX.md` if present.
- **AI-instruction surface**: `.claude/**` including `.claude/rules/`, `.claude/prompts/`, `.claude/skills/`.
- **Stack identification**: the `.slnx` / `.sln` at root, `angular/package.json`, any `*.csproj` files found via glob, `appsettings*.json` (structure only -- do not quote secrets), `launchSettings.json`.
- **Environment + cert state (critical for this repo)**: ASP.NET Core HTTPS dev cert handling (`dotnet dev-certs`), `appsettings.secrets.json` location(s), ABP license config, OpenIddict cert keys if stored on disk.
- **Database state**: EF Core migrations folder(s), connection string templates, any `docker-compose*.yml` for SQL Server, whether the repo expects SQL Server LocalDB (`MSSQLLocalDB`) or full SQL Server or Docker-hosted.
- **Promotion cascade + CI**: `.github/workflows/*.yml` for the auto-PR cascade (`auto-pr-dev.yml`, `deploy-dev.yml`, etc.), so the plan aligns with `main -> development -> staging -> production`.
- **Port inventory**: search for hard-coded ports in `launchSettings.json`, `environment*.ts`, `appsettings*.json`, `*.csproj`. This repo uses 44368 (AuthServer) / 44327 (HttpApi.Host) / 4200 (Angular) per `CLAUDE.md`, but verify against the actual configs.

If any of the above are absent or differ from what `CLAUDE.md` describes, flag the discrepancy -- that discrepancy is itself a decision the developer must resolve.

---

## Approach -- five phases with earned stop points

The phase order matters: Phase 2 research is only useful once you know what stack you're researching for (Phase 1). Phase 3 decisions depend on Phase 2 findings. Phase 4 plan production consumes all earlier outputs. Each stop point exists to let the developer redirect before you invest more tokens on the wrong premise.

Within a phase you have full latitude on method. Across phases, honour the order.

### Phase 1 -- Repository discovery

Build an internal model of the repo sufficient to answer the Phase 3 decisions. At minimum you need: language + framework versions, exact local start command and startup-order constraints, external dependencies (DB engine, identity providers, cert requirements), every bound port, how config differs between env branches, approximate per-checkout disk cost (`node_modules`, `bin`, `obj`, `dist`, NuGet global cache location), and any stack quirks documented as hard rules.

**Output**: a **Discovery Report** -- prose summary, not a bullet dump. The developer should be able to read it and recognise their repo.

**Stop point**: end the report with this explicit question: *"Does this picture match the repository as you understand it? Flag anything I got wrong or missed before I proceed to research."* Wait for the developer's response.

### Phase 2 -- Targeted web research

Research current (2025-2026) best practices for running multiple worktrees of THIS stack on a Windows machine. Choose your own queries and sources; the only requirement is the cite rule in Constraint 4.

Topics worth covering (add more if discovery surfaced them):

- Current `git-worktree` behaviour and flags; any recent changes in git 2.40+.
- SQL Server LocalDB instance-sharing semantics when multiple processes connect, OR per-worktree instance strategies.
- ASP.NET Core HTTPS dev-cert binding across multiple port sets (is one cert shared across worktrees, or does each need its own?).
- NuGet global package cache behaviour across parallel `dotnet restore` runs.
- Yarn classic and angular esbuild behaviour across parallel worktree checkouts (lockfile discipline, `.yarn/` state if using PnP, node_modules footprint).
- IDE behaviour (VS Code, Rider) with multiple simultaneous worktrees of the same repo.
- Windows-specific footguns: path length, case-insensitivity, symlink behaviour.

**Output**: a **Research Summary**, grouped by topic. Every non-trivial claim carries a URL and access date. Findings that contradict `CLAUDE.md` get flagged explicitly -- do not resolve the contradiction yourself.

**Stop point**: *"Any of these findings you want to dig into before I start asking setup questions?"* Wait for acknowledgement.

### Phase 3 -- Interactive decision gathering

Present decisions **one at a time**, because later topics depend on earlier answers and batching them forces the developer to think about N decisions at once. This is the one piece of rigid procedure in this prompt; it is earned because the dependency chain is real.

For each decision, structure your message as: the question in plain language, why it matters (what downstream choices depend on it), the options with concrete pros and cons, and your recommendation with reasoning. Recommendations are not pre-decisions -- they are your best guess that the developer can accept, modify, or reject.

The decision list below is the floor, not the ceiling. If Phase 1 discovery surfaced a decision not listed, raise it here.

1. **Worktree parent directory**, constrained by Windows path-length budget. Compute the budget: current repo path length + worktree slug length + deepest source-tree path + build-artefact path must stay under 260 chars. Propose a concrete budget before offering options.
2. **Naming convention** for worktree folders (branch-name slug, feature-tag, short hash -- pick one that fits the budget).
3. **Which branches get persistent worktrees.** `main` (today's workspace) and `production` are given. Does `development`? `staging`? Or only feature branches beyond those? Note: the repo has the promotion cascade; persistent worktrees of `development` and `staging` make "run the cascade locally" possible but cost disk + setup.
4. **Port allocation**. Produce a concrete port map (one column per worktree, one row per bound port -- AuthServer, HttpApi.Host, Angular, and anything Phase 1 discovered). Do not use placeholder port numbers.
5. **Database strategy** for this specific DB engine (LocalDB / full SQL / Docker). Options: shared DB with migration risk, one DB per worktree with isolation, Docker containers per worktree. Recommendation must cite Phase 2 research.
6. **ASP.NET HTTPS dev-cert strategy**. Shared system cert across all worktrees' ports (simplest, but some tools reject it), or per-worktree cert (isolated, more setup). Cite Phase 2 research.
7. **ABP license and secrets strategy**. `appsettings.secrets.json` holds the ABP license key. Options: symlink from a single source of truth, copy per worktree, environment-variable lookup. Whatever the plan chooses must NOT risk committing secrets to any branch.
8. **Configuration-override mechanism** for per-worktree differences (port offsets, connection strings). Options: gitignored local override files (`appsettings.Local.json`), environment variables, launch-profile variants. Pick based on what the stack already supports out of the box.
9. **Dependency-cache strategy**. Shared NuGet global cache (default is user-level, so usually shared -- confirm), shared yarn classic cache, or isolated per worktree.
10. **Feature-branch worktree lifecycle**. Long-lived one per feature (each gets its own DB), or ephemeral (created/destroyed per feature, DB shared).
11. **IDE workflow**. One window per worktree, or one workspace with multiple folders. Any workspace-level settings that need replication or symlinking.
12. **Shell choice**. Git Bash for Claude, WSL zsh for the developer -- are both expected to operate worktrees, or is one canonical? Affects path rendering in helper scripts.
13. **Cleanup discipline**. `git worktree remove` manual, or a helper script that also drops the DB and releases ports.
14. **Backup and rollback**. If setup fails or proves wrong after a week, what undoes it without losing uncommitted work in any worktree.

**Stop point after each question**. Do not advance to the next topic until the developer has answered the current one. If the developer wants to skip a decision, note that it's deferred and explain what downstream choice it blocks.

### Phase 4 -- Plan production

Only after all decisions are answered, write the plan. Single deliverable: `WORKTREE_SETUP_PLAN.md` at the repo root. Do not commit. Do not stage.

Sections, in order:

- **Summary** -- one paragraph on what will be set up and the reasoning spine.
- **Pre-flight checklist** -- disk-space minimum (derive from Phase 1 per-checkout footprint), clean working tree on `main`, remote fully pushed, backup location, DB backup if applicable.
- **End-state folder layout** -- tree diagram with real paths, respecting the Phase 3 path-length budget.
- **Port and database allocation table** -- one row per worktree, one column per resource. Real numbers, no placeholders.
- **Configuration override files** -- exact paths, exact content templates, and which `.gitignore` entries need to exist.
- **Step-by-step execution commands** -- shell-agnostic where possible, or labelled per shell when not. Each command preceded by a one-line comment on what it does and why. No placeholders.
- **Post-setup verification** -- how to confirm each worktree runs independently, with expected output (HTTP codes, log lines, port bindings).
- **Maintenance operations** -- adding a feature worktree, removing one, syncing after a main merge, handling diverged branches.
- **Rollback procedure** -- exact commands to fully undo the setup.
- **Known limitations** -- what this deliberately does NOT solve.
- **Open questions** -- anything deferred, with a pointer to who decides.

### Phase 5 -- Review gate

Do not modify tracked files. Do not run commands. Present the plan and say, verbatim:

> "The plan above is my final recommendation based on our discussion. Before any of it is executed, please review it end-to-end. Flag anything you want changed, clarified, or removed. Once approved, execution should happen in a fresh session using this plan as the input -- not in this session."

Then stop. This session ends here.

---

## Research quality rubric

Every claim in Phase 2 and every recommendation in Phase 3 carries a confidence label:

- **HIGH** -- official primary documentation (git-scm.com, learn.microsoft.com, docs.anthropic.com for Claude-side concerns) or direct observation of the repo. Two-source concurrence from primaries.
- **MEDIUM** -- reputable industry blog, recognised expert, or community consensus with 3+ corroborating voices.
- **LOW** -- single community voice, inference, forum post, or sources older than 3 years without recent corroboration.

Bar: every Phase 3 recommendation must be HIGH or MEDIUM. If a decision can only reach LOW, say so and ask the developer how to resolve the gap (defer, accept risk, do more research).

## Success criteria

The session succeeds when the developer, having read `WORKTREE_SETUP_PLAN.md`, can say "yes" to all of these:

1. I understand every command the plan will run and the reason for each.
2. I know what happens the first time I hit a merge conflict across worktrees.
3. I know exactly how to undo this setup if it goes wrong.
4. I know which files are tracked by git and which are local-only per worktree.
5. I can state the port, DB, and cert assignment for each worktree from memory.
6. I know what this setup does NOT solve, so I'm not surprised later.
7. Every hard rule in the plan carries a reason I could explain to someone else.

---

**Begin with Phase 1. Do not produce anything else until you have read the repo.**
