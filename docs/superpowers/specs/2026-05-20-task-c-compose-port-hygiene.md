---
title: "Task C: Compose Port Hygiene (MinIO + Gotenberg override docs)"
date: 2026-05-20
status: draft
branch: feat/parallel-worktree-stacks
task-of: parallel-worktree-stacks (C of 6)
closes: []
related:
  - Task D (replicate-old-app retrofit; consumes the documented overrides)
  - Task F (DOCKER-DEV.md update; expands the port table)
---

# Task C: Compose Port Hygiene (MinIO + Gotenberg override docs)

Single-PR plan: this task is the third of six commits on
`feat/parallel-worktree-stacks`. All six tasks ship together as one PR
to `main` at the end. Task A landed at `5cdae28`, Task B at `76348fd`.

## 1. Problem

`docker-compose.yml` reads three port-override variables that
`.env.example` does NOT document and `scripts/worktrees/add-worktree.sh`
does NOT compute:

- `MINIO_API_PORT` (default 9000) -- compose line 55
- `MINIO_CONSOLE_PORT` (default 9001) -- compose line 56
- `GOTENBERG_PORT` (default 3000) -- compose line 269

Effect: anyone copying the documented override block from `.env.example`
(or starting a feature worktree via `add-worktree.sh`) into a second
stack would still get port collisions on 9000/9001/3000 against the
first stack. The parallel-worktree workflow is incomplete without
these.

This gap was surfaced during the v3 plan analysis on 2026-05-20 and
Adrian explicitly scoped Task C to close it.

## 2. Solution

### 2a. `.env.example` -- 3 new commented overrides

Insert after the existing `REDIS_HOST_PORT` comment (currently line 35),
matching the existing AUTH/API/NG/SQL/REDIS format with `+10` offset
examples (consistent with the other 5 lines in the block):

```
#MINIO_API_PORT=9010     # default 9000 (main)
#MINIO_CONSOLE_PORT=9011 # default 9001 (main)
#GOTENBERG_PORT=3010     # default 3000 (main)
```

### 2b. `scripts/worktrees/add-worktree.sh` -- 3 new computed ports

Extend the port arithmetic block (currently L48-52) with three more
variables using the `base + offset*10` pattern that AUTH/API/NG already
use (chosen by Adrian 2026-05-20 over the `base + offset` pattern that
SQL/REDIS use):

```bash
MINIO_API=$((9000 + offset * 10))
MINIO_CONSOLE=$((9001 + offset * 10))
GOTENBERG=$((3000 + offset * 10))
```

Update the creation-message echo (L55) to include the new vars:

```bash
echo "Creating $SLUG at $TARGET (AUTH=$AUTH API=$API NG=$NG SQL=$SQL_HOST REDIS=$REDIS_HOST MINIO_API=$MINIO_API MINIO_CONSOLE=$MINIO_CONSOLE GOTENBERG=$GOTENBERG)"
```

Extend the env-write heredoc (L82-91) to append the three new ports
after `REDIS_HOST_PORT=$REDIS_HOST`:

```bash
MINIO_API_PORT=$MINIO_API
MINIO_CONSOLE_PORT=$MINIO_CONSOLE
GOTENBERG_PORT=$GOTENBERG
```

Also update the closing note (L98-105) so the worktree summary table
shows MinIO + Gotenberg URLs alongside AuthServer / HttpApi / Angular /
SQL / Redis.

## 3. Architecture impact

- **Compose, app code, tests:** unchanged.
- **Per-worktree behavior:** future `add-worktree.sh` invocations
  produce a `.env` with 8 documented port overrides (was 5) so the
  parallel-stack workflow has no missing pieces.
- **Existing worktrees:** unaffected. Any worktree created before
  Task C still has its old 5-port `.env` block; Adrian can append the
  3 new lines manually if desired (Task D will do this for
  `replicate-old-app`).

## 4. Why `+10` and not `+1`

The script uses two patterns: `+offset*10` for AUTH/API/NG (room to
spread cleanly across 100+ feature worktrees) and `+offset` for
SQL_HOST/REDIS_HOST (tighter packing for "small" services). MinIO and
Gotenberg are application-level services like AUTH/API/NG, not
infrastructure like SQL/Redis. Adrian's 2026-05-20 brainstorming
locked `+10` to keep the worktree-allocation pattern consistent across
all five application services.

Pre-existing inconsistency between `.env.example` (shows `+10` for
SQL/REDIS examples) and the script (uses `+1` for SQL/REDIS) is
flagged but out of scope for Task C. YAGNI; documenting MinIO/Gotenberg
correctly is the goal, not retrofitting SQL/REDIS doc.

## 5. Testing

No unit tests (script/config only). Smoke gates:

- **T-syntax:** `bash -n scripts/worktrees/add-worktree.sh` parses
  without error.
- **T-compose-config:** `docker compose config` parses without error
  (sanity check; `.env.example` isn't loaded directly but the file is
  documentation).

End-to-end "two stacks running in parallel without port collisions"
verification happens in Task D's retrofit, which consumes the new
documented port overrides.

## 6. Blast radius

Zero functional blast radius. `.env.example` is documentation;
`add-worktree.sh` only runs when invoked manually for a new feature
worktree. Existing worktrees + the live `main` stack are completely
untouched.

Rollback: trivial `git restore`. Or revert the commit; no data
cleanup.

## 7. HIPAA / PHI impact

None. Port numbers only.

## 8. Files changed

| File | Type | Approx lines | Why |
|---|---|---:|---|
| `.env.example` | edit | +3 | Three new commented override lines |
| `scripts/worktrees/add-worktree.sh` | edit | +6/-1 | Three new port arithmetic vars + extended echo + three env-write lines + summary note |
| `docs/superpowers/specs/2026-05-20-task-c-compose-port-hygiene.md` | new | +~120 | This spec |
| `docs/plans/2026-05-20-task-c-compose-port-hygiene.md` | new | +~80 | Plan |
| **Total** | | **~210 lines / 4 files** | (incl. docs) |

## 9. Acceptance criteria

- [ ] `.env.example` shows three new commented lines in the
  per-worktree override block.
- [ ] `bash -n scripts/worktrees/add-worktree.sh` parses without error.
- [ ] `docker compose config` parses without error.
- [ ] Commit message follows `commit-format.md`: `chore(scripts): document MinIO + Gotenberg port overrides for worktrees`.
- [ ] Task C commit lands on `feat/parallel-worktree-stacks` as a
  single combined commit; not pushed.

## 10. Out of scope

- SQL/REDIS `+1` vs `+10` inconsistency (pre-existing; could be a
  one-liner follow-up but YAGNI for Task C).
- `scripts/worktrees/README.md` port table expansion (currently lists
  only 5 ports per worktree row). Task F territory.
- `docs/runbooks/DOCKER-DEV.md` port table expansion. Task F.
- BUG-014 / BUG-015 closure notes. Task F.
- Two-stack end-to-end verification (Task D + cross-worktree).

## 11. Confidence

HIGH. Mechanical change, no logic, no external dependencies. The
existing AUTH/API/NG arithmetic pattern is the reference; this just
extends it to three more vars. Smoke gates are syntactic
(`bash -n`, `docker compose config`).
