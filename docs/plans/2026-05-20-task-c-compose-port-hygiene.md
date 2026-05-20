---
feature: task-c-compose-port-hygiene
date: 2026-05-20
status: in-progress
base-branch: main
working-branch: feat/parallel-worktree-stacks
related-issues: []
closes: []
spec: docs/superpowers/specs/2026-05-20-task-c-compose-port-hygiene.md
task-of: parallel-worktree-stacks (C of 6)
---

# Plan: Task C -- compose port hygiene (MinIO + Gotenberg overrides)

## Goal

Document the three port overrides (`MINIO_API_PORT`,
`MINIO_CONSOLE_PORT`, `GOTENBERG_PORT`) that `docker-compose.yml`
reads but `.env.example` and `scripts/worktrees/add-worktree.sh`
don't surface, so parallel-stack workflows don't collide on
9000/9001/3000.

## Context

Task C is the 3rd of six commits on `feat/parallel-worktree-stacks`
(off `main`). Task A landed at `5cdae28` (BUG-014 fix). Task B
landed at `76348fd` (BUG-015 fix). All six tasks ship as ONE pull
request at the end -- no per-task PR. Per-task smoke gates are
verification checkpoints inside the branch.

Without Task C's documentation + automation, anyone copying the
documented override block from `.env.example` (or running
`add-worktree.sh` for a new feature worktree) still gets port
collisions on 9000/9001/3000 against an already-running stack on
canonical ports. This is the "automation gap" surfaced during the
2026-05-20 v3 plan analysis.

Authoritative design context:
`docs/superpowers/specs/2026-05-20-task-c-compose-port-hygiene.md`.

## Approach

**Chosen: extend the existing `+offset*10` arithmetic pattern from
AUTH/API/NG to also cover MinIO/Gotenberg.**

- `.env.example` gains 3 commented lines in the existing per-worktree
  override block, matching the format of the existing 5 lines.
- `scripts/worktrees/add-worktree.sh` computes 3 more port variables
  per offset using `base + offset*10` (same as AUTH/API/NG), echoes
  them in the "Creating..." message, appends them to the generated
  `.env` block, and surfaces them in the closing summary note.

**Alternatives rejected (2026-05-20 brainstorming):**

- `+offset*1` (matching SQL/REDIS in the script). Tighter packing
  but inconsistent with MinIO/Gotenberg's role as application-level
  services like AUTH/API/NG.
- Fix the pre-existing SQL/REDIS doc-vs-script inconsistency at the
  same time. YAGNI; out of scope for Task C.
- `+offset*2` (mirroring Adrian's 2026-05-14 hand-edit). That manual
  attempt is since abandoned (OBS-9) and didn't codify a convention.

## Tasks

- T1: Edit `.env.example` -- add 3 commented MinIO/Gotenberg override lines
  - approach: code
  - files-touched: [.env.example]
  - acceptance: After the existing `REDIS_HOST_PORT` comment (currently
    line 35), three new commented lines are present in this exact
    order and format:
    ```
    #MINIO_API_PORT=9010     # default 9000 (main)
    #MINIO_CONSOLE_PORT=9011 # default 9001 (main)
    #GOTENBERG_PORT=3010     # default 3000 (main)
    ```
    Existing AUTH/API/NG/SQL/REDIS lines unchanged.

- T2: Edit `scripts/worktrees/add-worktree.sh` -- extend port allocation
  - approach: code
  - files-touched: [scripts/worktrees/add-worktree.sh]
  - acceptance: Four extensions in the file:
    1. After `REDIS_HOST=$((6379 + offset))`, three new arithmetic
       lines: `MINIO_API=$((9000 + offset * 10))`,
       `MINIO_CONSOLE=$((9001 + offset * 10))`,
       `GOTENBERG=$((3000 + offset * 10))`.
    2. The "Creating $SLUG at $TARGET (...)" echo includes
       `MINIO_API=$MINIO_API MINIO_CONSOLE=$MINIO_CONSOLE GOTENBERG=$GOTENBERG`.
    3. The `cat >> "$TARGET/.env" <<ENV` heredoc appends three new
       lines after `REDIS_HOST_PORT=$REDIS_HOST`:
       `MINIO_API_PORT=$MINIO_API`,
       `MINIO_CONSOLE_PORT=$MINIO_CONSOLE`,
       `GOTENBERG_PORT=$GOTENBERG`.
    4. The closing `cat <<NOTE` summary surfaces MinIO API,
       MinIO Console, and Gotenberg URLs alongside the existing
       AuthServer / HttpApi.Host / Angular / SQL / Redis lines.
    `bash -n scripts/worktrees/add-worktree.sh` parses without error.

- T3: Smoke gate -- `docker compose config` parses without error
  - approach: code
  - files-touched: [] (verification)
  - acceptance: `cd /w/patient-portal/main && docker compose config`
    exits 0 with no parse errors. **STOP if FAIL.** Failure here
    would indicate the `.env.example` edits accidentally broke
    something (unlikely since `.env.example` isn't read by Compose,
    but verify).

- T4: Smoke gate -- `bash -n` script parse
  - approach: code
  - files-touched: [] (verification)
  - acceptance: `bash -n scripts/worktrees/add-worktree.sh` exits 0.
    **STOP if FAIL.** Indicates heredoc / quoting / arithmetic
    expansion syntax issue in the T2 edits.

- T5: Commit Task C as a single combined commit (no push, no PR)
  - approach: code
  - files-touched: [] (git only)
  - acceptance: After T1-T4 pass, ALL 4 Task C files (2 edits + spec
    + plan) staged and committed together via `git commit -m "..."`
    using the message in the "Task C commit message" section below.
    **DO commit.** **DO NOT run `git push`.** **DO NOT run
    `gh pr create`.** `git log --oneline -3` shows Task C commit at
    HEAD followed by Task B (76348fd) and Task A (5cdae28). PR opens
    only after all six tasks + cross-worktree verification.

## Risk / Rollback

**Blast radius:** Zero functional impact. `.env.example` is
documentation; `add-worktree.sh` runs only when manually invoked for
a new feature worktree. Existing worktrees + the live `main` stack
unchanged.

**Rollback:** Trivial `git restore` working-tree changes (nothing
committed until T5). If committed: revert the single commit. No
data cleanup.

**Smoke-gate behavior on FAIL:**

- T3 FAIL -> investigate compose config error; revert .env.example
  changes if they're somehow implicated.
- T4 FAIL -> bash parse error in add-worktree.sh; check heredoc
  delimiters and arithmetic syntax. Do NOT proceed to T5.

## Verification

End-to-end Task C verification (after T1-T4 all pass):

1. `cat .env.example | grep -A1 -B1 MINIO_API_PORT` shows the new
   override block lines alongside the existing AUTH/REDIS comments.
2. `grep -E "MINIO_API|MINIO_CONSOLE|GOTENBERG" scripts/worktrees/add-worktree.sh`
   shows the new variables in the arithmetic block, echo, heredoc,
   and summary note.
3. Smoke T3 and T4 pass as documented.

End-to-end "two stacks running in parallel" verification happens
during Task D's retrofit, which consumes Task C's documented
overrides. Not in Task C's scope.

## Task C commit message

After T1-T4 acceptance criteria all pass, commit Task C's
deliverables as ONE commit on `feat/parallel-worktree-stacks`:

```
chore(scripts): document MinIO + Gotenberg port overrides for worktrees

- .env.example gains 3 commented override lines for MINIO_API_PORT,
  MINIO_CONSOLE_PORT, GOTENBERG_PORT alongside the existing
  AUTH/API/NG/SQL/REDIS overrides
- add-worktree.sh extends port arithmetic with the 3 new vars
  (offset*10 pattern, matching AUTH/API/NG); appends them to the
  per-worktree .env block; surfaces them in the summary
- Closes the gap where parallel-stack workflows hit port collisions
  on 9000/9001/3000 (MinIO API/console + Gotenberg)
- Plan + spec docs under docs/plans/ + docs/superpowers/specs/
```

Header length: 65 chars (under 72 hard cap). Type `chore` because
no functional behavior change. Scope `scripts` because the primary
change is the worktree-creation script and a doc file that documents
the script's outputs.

## Do-not-push directive

`git push` is NOT run as part of Task C. `gh pr create` is NOT run.
The PR opens only after all six tasks (A and B done, C in progress,
D-F pending) pass their smoke gates AND the cross-worktree
verification (parallel stacks both running cleanly) succeeds.

## Out of scope

- Pre-existing SQL/REDIS `+1` vs `+10` inconsistency between
  `.env.example` (shows `+10`) and `add-worktree.sh` (uses `+1`).
  YAGNI for Task C.
- `scripts/worktrees/README.md` port table expansion. Task F.
- `docs/runbooks/DOCKER-DEV.md` port table expansion. Task F.
- BUG-014 / BUG-015 closure notes. Task F.
- Worktree role markers (Task E).
- `replicate-old-app` retrofit consuming this work (Task D).
