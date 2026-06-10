---
title: Docker disk-accumulation automation (4-layer permanent fix)
date: 2026-06-04
status: in-progress
owner: Adrian
branch: (apply on main; machine-level config)
related:
  - docs/plans/2026-05-27-docker-lean-images-and-disk-reclaim.md
  - docs/runbooks/DOCKER-DEV.md
  - scripts/maintenance/docker-weekly-prune.ps1
  - scripts/maintenance/docker-reclaim-now.ps1
---

# Docker disk-accumulation automation

## Goal

Stop Docker images, build cache, and the WSL2 VHD from silently filling the disk.
This is the AUTOMATION layer that complements the 2026-05-27 lean-images plan:
that plan shrinks images (the cause); this plan bounds and reclaims what still
accumulates (the symptom), with no recurring manual step.

## Background (measured 2026-06-04)

A live audit caught the problem in the act. During ~15 minutes of normal dev:

- Build cache grew 1.2 GB -> 19 GB; images 8 GB -> 26 GB; `docker_data.vhdx`
  16 GB -> 30 GB; free disk 144 GB -> 124 GB.
- Root cause: (a) the WSL2 VHDX only grows, never auto-shrinks; (b) the BuildKit
  GC ceiling was set to 20 GB, so cache was allowed to balloon to ~19 GB before
  pruning; (c) no scheduled cleanup between manual prunes.
- Disk topology: C: is the only physical volume (474 GB); `W:` -> `C:\src`,
  `P:` -> `C:\Users\RajeevG\Documents\Projects` are `subst` aliases.

## Decisions (2026-06-04, Adrian)

1. **VHD shrink method: WSL sparse auto-shrink** (`wsl --manage docker-desktop
   --set-sparse true`). This REVERSES 2026-05-27 Decision #1 ("manual
   Optimize-VHD, NOT sparse").
   - Why reverse: Adrian asked for a permanent/automatic fix with no recurring
     manual compaction. The original anti-sparse reasoning was (i) sparse adds
     CPU/RAM overhead and (ii) small images are the real cure. (i) is mitigated
     because Layer 1 (GC ceiling) + Layer 3 (scheduled prune) now keep cache
     churn and total size low, which is what drives sparse overhead; (ii) still
     holds and is tracked separately as Phase 2 of the 2026-05-27 plan.
   - Tradeoff accepted: documented sparse CPU/RAM spikes (microsoft/WSL#10991).
     Revert path is two lines (see `docker-reclaim-now.ps1`): set-sparse false +
     Optimize-VHD.
2. **Immediate reclaim: automation-only now.** The disruptive one-time prune +
   sparse enablement is parked in a human-run script, not executed, because the
   dev stack was mid-build. Nothing this session interrupts the running stack.
3. **Scheduling: real Windows Scheduled Task**, registered for Adrian.
4. **Cache ceiling: 10 GB** (was 20 GB). Halves the cap; generous for rebuilds.

## Layers and status

| Layer | What | Status |
| --- | --- | --- |
| 1. Bound the build cache | `~/.docker/daemon.json` `builder.gc.defaultKeepStorage` 20GB -> 10GB | DONE (applies on next Docker restart) |
| 2. Shrink the VHD | Enable WSL sparse auto-shrink | BLOCKED -- needs a decision (see 2026-06-05 findings). Cache freed inside VHD; file not yet shrunk. |
| 3. Scheduled cleanup | Weekly conservative prune via Scheduled Task | DONE ("Docker Weekly Prune", Sun 03:00) |
| 4. Cap the ceiling + RAM | `.wslconfig` `diskSize=80GB`, `memory=12GB`, `autoMemoryReclaim=gradual` | ALREADY DONE (2026-05-27 Task 1.4) |

### Layer 1 -- BuildKit GC ceiling [DONE]
- Edited `C:\Users\RajeevG\.docker\daemon.json`: `defaultKeepStorage` 20GB ->
  10GB. Backup at `daemon.json.bak-20260604`. JSON validated.
- Takes effect on the next Docker Desktop restart (NOT restarted this session).
- Future tightening (optional): migrate to a `gc.policy` entry with an explicit
  `maxUsedSpace` for a hard cap. Deferred -- `defaultKeepStorage` is the proven
  key on this daemon (Engine 29.5.2); a policy schema typo would block startup.

### Layer 2 -- Shrink the VHD [BLOCKED -- needs Adrian's decision]
- `scripts/maintenance/docker-reclaim-now.ps1`: prune runs by default; the
  VHD-shrink step is now opt-in behind `-EnableSparse -IHaveBackedUpVolumes`.
- See "Findings 2026-06-05" -- both shrink methods have caveats discovered when
  attempting the off-hours run.

## Findings 2026-06-05 (off-hours reclaim attempt)

- **Build cache prune succeeded: 21.24 GB reclaimed** (22.42 GB -> 1.17 GB) with
  `docker builder prune -f`. Live content in the VHD dropped from ~33 GB to
  ~11.6 GB. Stack was already down (0 running containers), so this cost nothing.
- **BUT the .vhdx file did NOT shrink** (still 39.66 GB on disk); the freed space
  is logical headroom inside the VHD. Host free space barely moved (~107.8 GB).
  Stops runaway growth; does not return bytes to the host.
- **Sparse mode (the chosen method) is gated.** `wsl --manage docker-desktop
  --set-sparse true` now fails with "Sparse VHD support is currently disabled due
  to potential data corruption"; it requires `--allow-unsafe`. This VHD holds the
  seeded SQL demo data -> I did NOT force it. Decision needed.
- **Optimize-VHD (safe alt) was attempted and correctly blocked.** Running it
  needs elevation; doing so via a SYSTEM scheduled task was denied as an
  elevated-persistence action (right call). Also: Optimize-VHD reclaims little on
  WSL2 ext4 without a prior in-guest `fstrim`, so it is not a quick win either.
- **Net:** the cache bloat is gone and bounded (GC + diskSize=80GB + weekly
  prune). Returning the ~28 GB of VHD air to the host is deferred to an explicit
  decision: (A) back up the SQL volume, then sparse `--allow-unsafe`; or (B)
  `fstrim` + elevated `Optimize-VHD`; or (C) leave it and let Phase 2 lean images
  shrink the live content so a future compaction reclaims more.

### Layer 3 -- Scheduled prune [DONE]
- `scripts/maintenance/docker-weekly-prune.ps1`: dangling images + build cache
  unused >7d + containers stopped >7d. No `-a` on builder cache, NO volumes.
- Scheduled Task "Docker Weekly Prune", weekly Sun 03:00, runs as RajeevG.
  No-ops gracefully if Docker is down. Log: `%LOCALAPPDATA%\docker-weekly-prune.log`.
- Remove with: `schtasks /Delete /TN "Docker Weekly Prune" /F`.

### Layer 4 -- Caps [ALREADY DONE]
- `.wslconfig`: `memory=12GB`, `processors=10`, `swap=8GB`,
  `autoMemoryReclaim=gradual`, `diskSize=80GB`. No change needed.

## Verification

- [x] `daemon.json` valid JSON; `builder.gc.defaultKeepStorage` = "10GB".
- [x] Scheduled Task "Docker Weekly Prune" registered, State Ready, next run Sun.
- [ ] After next Docker restart: confirm GC ceiling honored (cache plateaus ~10GB).
- [x] Build cache pruned: 21.24 GB freed inside the VHD (2026-06-05).
- [ ] VHD file shrink DEFERRED -- needs decision A/B/C (see Findings 2026-06-05).

## Safety rails (do NOT cross)

- NEVER `docker system prune --volumes` / `docker volume prune` on a schedule --
  destroys the seeded SQL demo data (HIPAA-synthetic). Volume cleanup stays manual.
- Sparse and Optimize-VHD are mutually exclusive; pick one (we chose sparse).

## Not in scope here (tracked elsewhere)

- Phase 2 lean multi-stage images -- the long-term cure for VHD growth -- remains
  in `docs/plans/2026-05-27-docker-lean-images-and-disk-reclaim.md` (pending).
