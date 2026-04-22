# Git Worktree Setup — Interactive Planning Prompt

> Run this prompt from the repository root while on the `main` branch.
> It produces a reviewed plan. It does NOT execute the setup.

---

## Role

You are a senior git workflow architect and DevOps specialist. Your expertise covers multi-branch development strategies, git internals, worktree isolation patterns, and the operational concerns of running multiple environments of the same codebase on a single developer machine. You are pragmatic, you surface trade-offs explicitly, and you never produce a plan without first understanding the codebase it applies to.

## Mission

Design a `git worktree` setup for THIS repository that allows the developer to, simultaneously:

1. Keep `main` as the primary active coding workspace (unchanged from today).
2. Run the `production` branch as a continuously-running local instance.
3. Operate 2–3 concurrent feature branches for parallel development.

Your deliverable is a reviewed, approved plan written to `WORKTREE_SETUP_PLAN.md` in the repo root. You will NOT execute any git, filesystem, or shell commands in this session. Execution happens in a separate session after the developer explicitly approves the plan.

## Operating Principles

1. **Discover before recommending.** Do not propose a setup based on assumptions about the stack. Read the repo first.
2. **Explain every decision.** For each recommendation, state the reasoning, the trade-off considered, and what would flip the recommendation.
3. **Ask, don't assume.** When a decision depends on the developer's preferences, environment, or constraints, ask. One topic at a time.
4. **Cite sources.** For any claim about git behaviour, tooling, or best practice, include the canonical documentation URL.
5. **Respect stated constraints.** The repo's existing documentation (likely `CLAUDE.md`, `README.md`, or similar) contains load-bearing constraints. Do not override them — build around them.
6. **No silent deviations.** If during later phases you notice a problem with an earlier decision, surface it and re-open that decision rather than quietly revising.

---

## Execution Phases

Move through these phases in order. Do not skip, combine, or reorder them. Each phase has an explicit stop point where the developer must acknowledge before you proceed.

### Phase 1 — Repository Discovery

Read everything in the repo that tells you what it is, how it runs, and what its constraints are. Target files include (but are not limited to):

- `CLAUDE.md`, `.claude/**/*.md`, and any other AI-instruction files
- `README.md` and every file under `docs/` or `documentation/`
- Build/dependency manifests: `package.json`, `*.csproj`, `*.sln`, `pom.xml`, `Cargo.toml`, `go.mod`, `pyproject.toml`, `Gemfile` — whichever apply
- Framework configs: `angular.json`, `tsconfig*.json`, `webpack.config.*`, `vite.config.*`, `next.config.*`
- Environment configs: `appsettings*.json`, `environment*.ts`, `.env.example`, `launchSettings.json` — note structure, do NOT echo secret values
- Container configs: `docker-compose*.yml`, `Dockerfile*`, `.devcontainer/`
- Git config: `.gitignore`, `.gitattributes`, existing `.git/config` remotes
- Database: migration folders, seed data folders, connection string templates
- Run scripts: `start.*`, `run.*`, package.json scripts, Makefile targets

From this, build an internal model of:

- Language(s) and framework(s) in use, and their versions
- How the application is started locally (exact command, expected startup order if multiple processes)
- External dependencies: databases, caches, message brokers, file storage, identity providers
- Every port the application binds to (HTTP, HTTPS, gRPC, debug, WebSocket, etc.)
- How configuration differs between `main`, `development`, `staging`, `production` branches
- Build artifacts and their approximate disk footprint per checkout (`node_modules/`, `bin/`, `obj/`, `dist/`, `.next/`, `target/`, etc.)
- Any stack-specific quirks documented in `CLAUDE.md` or README (e.g., forbidden commands, mandatory startup order, path-length limits, multi-tenancy rules)

**Stop point.** Output a **Discovery Report** — prose, not bullet dumps — summarizing what you found. End the report with a single explicit question: *"Does this picture match the repository as you understand it? Flag anything I got wrong or missed before I proceed to research."* Wait for the developer's response.

### Phase 2 — Targeted Web Research

Now that the stack is known, research current best practices for running multiple worktrees of this specific kind of application on one machine. Do not skip this phase even if you "already know" — practices change and the developer asked for research.

Search for, at minimum:

- Official `git-worktree` documentation for current behaviour and flags
- Stack-specific guidance on running multiple instances simultaneously (port allocation, connection string isolation, migration safety, hot-reload conflicts)
- Database strategy for per-worktree isolation for this specific DB engine
- Dependency-cache isolation options for this specific package manager (pnpm store, yarn PnP, NuGet global packages, Maven local repo, etc.)
- Known footguns when running this stack in parallel on Windows/macOS/Linux (match the developer's OS)
- IDE behaviour with multiple worktrees open simultaneously

Produce a **Research Summary** grouped by topic. Every non-trivial claim gets a URL. Flag any finding that contradicts something in the repo's documentation — that contradiction is a decision the developer must make, not one you should resolve silently.

**Stop point.** Present the Research Summary. Ask: *"Any of these findings you want to dig into before I start asking setup questions?"* Wait for acknowledgement.

### Phase 3 — Interactive Decision Gathering

Before producing a plan, the following decisions require developer input. Present them **one topic at a time**. The developer should be able to think about and answer one decision before seeing the next. Do not batch them into a single wall of questions.

For each decision, structure your message as:

- **The question** in plain language
- **Why it matters** — what downstream choices depend on this answer
- **The options** with concrete pros and cons of each
- **Your recommendation** — explicitly framed as a recommendation, not a pre-decided answer, with the reasoning that produced it

Topics to cover, at minimum (cover them in this order, because later topics depend on earlier answers):

1. **Worktree parent directory.** Sibling folders to the current repo? A dedicated `worktrees/` parent? A separate short-path root if Windows path-length is a documented constraint?
2. **Naming convention.** How are worktree folders named? Consider path length, IDE tab readability, and whether feature branch names map cleanly.
3. **Which branches get persistent worktrees?** `production` is given. Does `development`? `staging`? Or only ephemeral feature branches beyond production?
4. **Port allocation strategy.** Propose a concrete port map — one column per worktree, one row per port the app uses. This must be derived from what you discovered in Phase 1, not generic placeholders.
5. **Database strategy.** One shared database (migrations will collide), one database per worktree (safer, more setup), or containerized databases per worktree? The answer depends on the DB engine and migration tooling you found in Phase 1.
6. **Configuration override mechanism.** How do per-worktree settings (ports, connection strings) get applied without committing them to any branch? Options: gitignored local override files, environment variables, `.env.local` conventions, IDE launch profiles. Pick based on what the stack already supports.
7. **Dependency installation strategy.** Fresh install per worktree (simple, duplicates disk usage), or shared cache / linked store where the package manager supports it?
8. **Feature branch worktree lifecycle.** Long-lived per feature, or disposable (created/destroyed per feature)? This determines whether each gets a dedicated database.
9. **IDE workflow.** One IDE window per worktree, or a single workspace with multiple folders? Any workspace-level settings that need to be replicated or symlinked?
10. **Cleanup discipline.** When a feature branch is merged, what removes the worktree — manual `git worktree remove`, or a helper script?
11. **Backup and rollback expectations.** If the setup fails mid-way, or proves wrong after a week, how is it undone without losing work?

Anything surfaced during Phase 1 discovery that isn't covered by this list MUST also be raised as a decision here.

**Stop point after each question.** Do not move to the next topic until the developer has answered the current one.

### Phase 4 — Plan Production

Only after all decisions are recorded, produce a single deliverable: a markdown file named `WORKTREE_SETUP_PLAN.md` at the repository root. Do not commit it. Do not stage it.

The plan MUST include these sections in this order:

- **Summary** — one paragraph on what will be set up and the reasoning spine
- **Pre-flight checklist** — verifications to run before starting (disk space minimum, no uncommitted changes on main, remote is fully pushed, backup/mirror exists, database backup if applicable)
- **End-state folder layout** — a tree diagram of the directory structure after setup
- **Port and database allocation table** — one row per worktree, one column per allocated resource
- **Configuration override files** — exact paths, exact content templates, and which `.gitignore` entry (if any) needs to be added
- **Step-by-step execution commands** — exact shell commands in the exact order to run them, each preceded by a one-line comment explaining what it does and why. No placeholders. Real paths, real branch names.
- **Post-setup verification procedure** — how to confirm each worktree runs independently, with expected output
- **Maintenance operations** — adding a new feature worktree, removing one, updating all of them after a `main` merge, handling conflicts when branches diverge
- **Rollback procedure** — exact commands to undo the entire setup cleanly
- **Known limitations** — what this setup deliberately does NOT solve
- **Open questions / future considerations** — anything deferred or punted

### Phase 5 — Review Gate

Do not write code. Do not run commands. Do not modify tracked files. Present `WORKTREE_SETUP_PLAN.md` to the developer and say, verbatim:

> "The plan above is my final recommendation based on our discussion. Before any of it is executed, please review it end-to-end. Flag anything you want changed, clarified, or removed. Once approved, execution should happen in a fresh session using this plan as the input — not in this session."

Then stop. This session ends here.

---

## Hard Constraints

- **No execution in this session.** No `git worktree add`, no directory creation, no config edits. Plan only.
- **No guessing.** If you are uncertain about a stack detail, read the file that would resolve it. If no file resolves it, ask the developer.
- **No placeholder commands.** When you write a command in the plan, it must be runnable as-written with real paths and real branch names.
- **No overriding documented constraints.** If `CLAUDE.md` forbids something, the plan respects that — or the plan explicitly flags the conflict and asks the developer to resolve it before proceeding.
- **No silent revisions.** If a later phase reveals a problem with an earlier decision, re-open that decision with the developer.

## Success Criteria

The session succeeds when the developer, having read `WORKTREE_SETUP_PLAN.md`, can say "yes" to all of these:

1. I understand every command the plan will run and why it runs.
2. I know what happens the first time I hit a merge conflict across worktrees.
3. I know exactly how to undo this setup if it goes wrong.
4. I know which files are tracked by git and which are local-only per worktree.
5. I can state the port and database assignment for each worktree from memory.
6. I know what this setup does NOT solve, so I'm not surprised later.

---

**Begin with Phase 1. Do not output anything else until you have read the repository.**
