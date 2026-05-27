---
title: Docker lean images + disk reclaim
date: 2026-05-27
status: draft
owner: Adrian
branch: (new branch off feat/replicate-old-app or main, TBD at execution)
related:
  - docs/runbooks/investigations/2026-05-27-docker-findings.md
  - docs/runbooks/findings/bugs/OBS-22-docker-watch-misses-bind-mount-edits.md
---

# Docker lean images + disk reclaim

## Goal

Make the Patient Portal Docker stack consume as few resources as possible --
small images, small disk footprint, low memory -- while the application keeps
working exactly as it does now. No .NET SDK and no `node_modules` shipped inside
runtime images. Investigation backing this plan:
`docs/runbooks/investigations/2026-05-27-docker-findings.md`.

## Decisions locked (2026-05-27, Adrian)

1. **Disk shrink method:** manual `Optimize-VHD` (NOT WSL sparse auto-shrink),
   plus a `diskSize` cap in `.wslconfig`. (Sparse was off; `Optimize-VHD` is
   installed. Manual keeps the fallback open and avoids sparse overhead; small
   images are the real long-term cure for vhdx growth.)
2. **Images:** convert the local `docker-compose.yml` to build `api`,
   `authserver`, `angular` from the existing multi-stage production
   `Dockerfile`s (aspnet runtime / nginx) instead of the single-stage
   SDK-based `Dockerfile.dev`.
3. **Backend dev loop:** rebuild-on-change for now (`docker compose up --build
   <svc>`). Host-dev (fast reload) is **deferred to the weekend (Fri 2026-05-29)**
   for separate consideration. It has one open question to settle then (see
   Phase 3) -- this does NOT block Phase 1 or Phase 2.

## Execution order (Adrian: "plan all, cleanup first, then changes")

Phase 1 (cleanup) -> Phase 2 (lean images) -> Phase 3 (host-dev, Friday).

> **Sequencing nuance (compaction timing).** `Optimize-VHD` requires fully
> shutting down Docker Desktop + WSL, which stops BOTH running stacks (`main`
> and `replicate-old-app`). It is therefore most efficient to run the actual
> file compaction **once, after Phase 2**, when images are at their smallest --
> a single shutdown reclaims the most. The build-cache prune (Phase 1, Task 1)
> is non-disruptive and runs anytime. So: prune now for logical headroom;
> compact the file once after the images are slim. Adrian to confirm when he
> can pause Docker for the ~5-minute compaction.

---

## Phase 1 -- Cleanup (do first; mostly non-disruptive)

### Task 1.1 -- Reclaim build cache  [approach: code]
- `docker builder prune -f` (NO `-a`). Removes the ~20.8 GB of unused build
  cache. `-a` is forbidden (would wipe cache backing running stacks).
- **Non-disruptive:** running containers keep running.
- **Verify:** `docker system df` build-cache size drops from ~24 GB to ~3 GB.

### Task 1.2 -- Fix `.dockerignore` (root)  [approach: code]
- Change `Logs` -> `**/Logs` so per-service log folders
  (`src/.../<Service>/Logs`, ~388 MB) stop leaking into the build context /
  images. Closes the size leak AND the PHI-in-image path.
- (Optional, low value) broaden `**/bin/Debug` -> `**/bin` only if a future
  Release build could leak; current Debug-only exclusion is adequate.
- **Verify:** after a rebuild, `docker run --rm --entrypoint sh <img> -c 'du -sh
  /app/src/*/Logs'` shows no Logs (or build context "transferring context" drops).

### Task 1.3 -- Add `angular/.dockerignore`  [approach: code]
- The `./angular` build context has none, so `COPY . /app` pulls host
  `node_modules` (583 MB), `dist`, `.angular`. Add an ignore file (or the
  BuildKit per-Dockerfile `Dockerfile.dockerignore`) excluding `node_modules`,
  `dist`, `.angular`, `.git`, `e2e`.
- NOTE: largely moot once Phase 2 switches Angular to the multi-stage `Dockerfile`
  (which also benefits from this ignore file), but worth having regardless.
- **Verify:** build context size for the angular build drops sharply.

### Task 1.4 -- Add `diskSize` cap to `.wslconfig`  [approach: code]
- Add `diskSize=80GB` under `[wsl2]` (currently: memory=12GB, processors=10,
  swap=8GB, autoMemoryReclaim=gradual). Guardrail against runaway growth
  (default ceiling is 1 TB). Requires `wsl --shutdown` to take effect.
- **Trade-off:** a build needing >80 GB fails until raised; 80 GB is generous
  for the post-Phase-2 image sizes.

### Task 1.5 -- Compact the vhdx  [approach: code]  [DISRUPTIVE -- confirm timing]
- Best run ONCE after Phase 2. Steps (Adrian runs, or guided):
  1. Quit Docker Desktop (tray -> Quit). `wsl --shutdown`.
  2. Admin PowerShell: `Optimize-VHD -Path
     "$env:LOCALAPPDATA\Docker\wsl\disk\docker_data.vhdx" -Mode Full`.
  3. Restart Docker Desktop; bring stacks back up.
- **Pre-check:** confirm the vhdx is NOT sparse (it is not, per 2026-05-27); if
  it ever is, `Optimize-VHD` will refuse and sparse must be disabled first.
- **Verify:** the `docker_data.vhdx` file size on disk drops from ~72 GB toward
  live-content size.

---

## Phase 2 -- Lean images (rebuild-on-change)

Switch the three app services to their existing multi-stage prod `Dockerfile`s.
Expected: .NET ~4.56 GB -> ~0.6 GB; Angular ~5.51 GB -> ~0.15 GB.

### Task 2.1 -- API + AuthServer to multi-stage  [approach: test-after]
- In `docker-compose.yml`, point `api` + `authserver` `build.dockerfile` at
  `Dockerfile` (not `Dockerfile.dev`). Remove the `./src` source bind mount and
  the `Directory.Build.props` / `slnx` mounts (the published image is
  self-contained). Keep the `appsettings.secrets.json` and `~/.abp/cli` mounts.
- Confirm the secrets mount target matches the published content root (`/app`).
- **Verify:** `curl :44327/health-status` = 200; `:44368/.well-known/...` = 200;
  `docker images main-api` ~0.6 GB. Run a smoke login + one booking flow.

### Task 2.2 -- Angular to nginx multi-stage  [approach: test-after]
- Point `angular` `build.dockerfile` at `Dockerfile` (nginx-served static dist).
- **KNOWN INTEGRATION RISK (must solve):** the dev image's `dev-entrypoint.sh`
  writes `dynamic-env.json` from container env vars at startup (per-stack
  NG_PORT/AUTH_PORT/API_PORT -- BUG-015), which is how parallel worktrees get
  correct URLs. The prod `Dockerfile` bakes a static `dynamic-env.json`. To keep
  multi-worktree port handling, add a tiny nginx entrypoint that regenerates
  `dynamic-env.json` from env vars before nginx starts (port mapping must also
  move from container:80 to whatever nginx listens on). Verify the SPA boots and
  reaches AuthServer + API.
- **Verify:** `:4200` = 200; login UI loads; OIDC discovery fetch succeeds.

### Task 2.3 -- Update DOCKER-DEV.md  [approach: code]
- Document the new "rebuild to see backend changes" loop and the slim sizes;
  correct the stale "build from Dockerfile / six containers" text.

---

## Phase 3 -- Host-dev backend (DEFERRED to weekend 2026-05-29)

Parked for separate consideration over the weekend. Does NOT block Phase 1/2.
Idea: run API + AuthServer on the Windows host with `dotnet watch` (~1-2 s
reload), keeping SQL/Redis/MinIO/Gotenberg in Docker. Would involve repairing
`scripts/dev/start-dev-stack.ps1` (references deleted `docker-compose.dev.yml`,
hardcodes `replicate-old-app-*` names) and aligning OpenIddict redirect URIs /
authority + `*.localhost` tenant routing for host-run services.

> **One open question to settle when we pick this up (not a current blocker).**
> Saved guidance (`feedback_docker_only_dev`) says avoid `dotnet run`
> AuthServer/HttpApi.Host on the host due to **HSTS** (host .NET forcing https
> redirects that break the http-only dev flow) and Vite-DI fallout -- yet the
> repo ships `scripts/dev/dev-api.ps1` that does exactly this. So before doing
> host-dev: is that rule still binding, and can HSTS be cleanly disabled for the
> host-dev case (`ASPNETCORE_URLS=http://...`, `UseHsts()` gated to non-dev,
> `RequireHttpsMetadata=false`)? Just a question to answer Friday.
> (Angular host-dev stays OFF the table regardless: `ng serve` / `ng build
> --watch` are banned by project rules due to Vite breaking ABP DI.)

---

## Risks (cross-cutting)

- **Compaction is disruptive** (stops both stacks). Mitigation: run once, after
  Phase 2, when Adrian can pause Docker.
- **Angular dynamic-env regression** (Task 2.2) -- the multi-worktree URL
  injection must be preserved via an nginx entrypoint.
- **Secrets/content-root path** mismatch between dev mounts and the published
  image (Task 2.1) -- verify before declaring done.
- **Phase 3 HSTS/docker-only open question** -- to settle when host-dev is
  picked up (weekend); not a current risk.
- **Both worktrees share identical Docker config**, so Phase 1-2 edits apply to
  `main` and `replicate-old-app` alike; coordinate so a rebuild of one doesn't
  surprise the other.

## Verification gate per phase

Each phase ends with: stack healthy (`docker compose ps` all healthy), the three
endpoints return 200, and one real login + booking smoke flow passes. No phase
is "done" on image-size numbers alone.
