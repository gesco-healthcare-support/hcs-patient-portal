# Investigation Brief: Docker Image Bloat + Broken Hot-Reload

- **Type:** Investigation / research ONLY. Do **not** change Dockerfiles,
  compose, `.dockerignore`, scripts, or source. Produce findings and an
  options matrix. A separate session acts on them.
- **Created:** 2026-05-27. **Fact-checked:** 2026-05-27 against the live code
  in `W:\patient-portal\main` (corrections folded in; see Section 3).
- **Run this in a fresh session, from the `W:\patient-portal\main` worktree.**
- **This file physically lives in the `replicate-old-app` worktree** at
  `W:\patient-portal\replicate-old-app\docs\runbooks\investigations\2026-05-27-docker-image-size-and-hot-reload.md`.
  The Docker config is **byte-identical** between the `main` and
  `replicate-old-app` worktrees (verified: `docker-compose.yml`,
  `.dockerignore`, all `Dockerfile.dev` files diff clean), so every file
  path below resolves the same in `main`. Open this doc from the main
  session via its absolute path, or copy it into `main` first.

- **Two questions to answer:**
  - **A.** Why are the backend/frontend dev images so large (~5-6 GB each),
    and what specifically causes it?
  - **B.** Why does "hot-reload" not work, and what are the options to fix or
    remove it?
- **Deliverable:** a findings report (Section 9). Lead with answers, then
  evidence, then an options matrix with trade-offs. **Do not pick a winner.**

### How to use this document (adaptability clause)

Section 3 lists findings **already confirmed** on 2026-05-27. They exist so
you extend and validate rather than rediscover from zero. **They are
point-in-time observations, not gospel** -- re-verify the load-bearing ones
with the probes, and if the evidence diverges, **follow the evidence and say
so in the report.** Each probe is written as **Goal -> Why -> Probe (exact
commands; adapt freely) -> Capture (the contract) -> Open questions.** You
have full latitude on *how*; the **Capture** lists are what the report must
contain. Do not let this brief lock you in: new findings are welcome and
expected.

---

## 1. Background & Motivation (why this investigation exists)

This project (HCS Patient Portal, ABP Commercial 10 / .NET 10 / Angular 20)
is developed across **multiple git worktrees that each run their own full
Docker stack** so parallel sessions don't collide (`main`, `development`,
`staging`, and feature worktrees like `replicate-old-app`). Each stack is
~9 containers: SQL Server, Redis, MinIO (+init), Gotenberg, DbMigrator,
AuthServer, HttpApi.Host, Angular.

**What triggered this brief (session of 2026-05-27):**

1. The developer (Adrian) reported Docker/WSL was **clogging host memory and
   disk**. We cleaned the engine: reclaimed ~5.66 GB of stale build cache and
   an orphaned volume. Remaining footprint was still large: **~25-31 GB of
   images, ~15-20 GB of build cache**.
2. We raised the WSL2 caps in `C:\Users\RajeevG\.wslconfig` from
   **10 GB / 4 CPU / 4 GB swap** to **12 GB / 10 CPU / 8 GB swap** (host has
   only **15.5 GB physical RAM**, 14 logical / 12 physical cores -- so 12 GB
   is near the practical ceiling; Windows needs the rest).
3. We rebuilt the `replicate-old-app` stack while `main` was also running.
   During the cold build, **free host RAM fell to ~0.3 GB**, layer exports
   crawled (~80 s each = swap thrashing), and the **`api` container crashed**
   mid-startup-compile (`MSB4018` + `MSB3026: ... EntityFrameworkCore.dll ...
   being used by another process`). It then **recovered automatically** on
   Docker's restart once memory freed -- so the crash was
   **memory-pressure-dependent, not deterministic**.
4. Investigating the crash exposed the structural issues this brief targets:
   the images are huge, "hot-reload" was quietly removed months ago, and the
   "production" compose is actually a **dev** compose that compiles inside the
   container at startup.

**Why Adrian wants this:** he wants a Docker dev setup that (a) doesn't
exhaust a 15.5 GB host when two stacks run, (b) builds smaller images, and
(c) has a working, fast edit->see-change loop -- or a clear, documented
decision to drop in-container reload in favour of something that works. He
explicitly wants **options with trade-offs**, framed for a solo developer on
a constrained machine, not a unilateral fix.

**What we already suspect is the root cause** (to be validated): the local
stack builds from `Dockerfile.dev` variants that use the **full .NET SDK
image**, bind-mount host source, and run `dotnet run` / `ng build` **at
container start** -- so the image ships the SDK + a baked restore/build it
then redoes at runtime. Proper small **production** `Dockerfile`s already
exist in the repo but are **not wired into the local compose**.

---

## 2. Hard constraints (binding)

- **Do NOT edit** Dockerfiles, `docker-compose.yml`, `.env`, `.dockerignore`,
  scripts, or source. Reading is fine; mutating is out of scope.
- **Do not disrupt other worktrees' running stacks.** You will run from
  `main`; another worktree's stack (e.g. `replicate-old-app`) may also be up.
  The one allowed exception is Part C1 (briefly stopping the *other* stack to
  isolate a memory variable) -- if you do, restart it and confirm healthy.
- **Never run `docker system prune -a` or `docker builder prune -a`** -- they
  would wipe cache/images backing running stacks.
- **No host dev servers as a side effect** (`ng serve` / `yarn start` /
  `ng build --watch` on the host break ABP DI per project CLAUDE.md). Running
  the existing `scripts/dev/*.ps1` is in-scope ONLY if a probe explicitly
  calls for it and you note the state change.
- This is research. **Resist fixing.** Obvious fixes go in the Options
  section as candidates -- do not apply them.
- **HIPAA note:** if you inspect leaked `Logs/` content (see 3.4), do not
  copy real-looking identifiers into the report; describe, redact, quantify.

---

## 3. What we already confirmed (2026-05-27) -- validate & extend

> Confidence tags: HIGH = measured/read from source this session;
> MEDIUM = reputable docs/inference; LOW = hypothesis to test.

### 3.1 There are THREE Dockerfile variants per service (HIGH)
For AuthServer, HttpApi.Host, and Angular, the repo has:
- **`Dockerfile`** -- proper **multi-stage production** image. Backend:
  SDK build -> `dotnet publish -c Release` -> `FROM mcr.microsoft.com/dotnet/aspnet:10.0`
  runtime, `COPY --from=build /app/publish`, `ENTRYPOINT ["dotnet", "...dll"]`.
  Angular: node build -> `ng build` -> `FROM nginx:alpine` serving static
  `dist`. These are the **small** images. `etc/helm/build-image.ps1` builds
  via `-f Dockerfile`, so these are the deployment artifacts.
- **`Dockerfile.dev`** -- **single-stage dev** image: `FROM sdk:10.0` (or
  `node:20-alpine`), copies source, bakes a `dotnet restore` + `dotnet build`
  (backend) / `yarn install` (angular), then at runtime bind-mounts `./src`
  (or `./angular/src`) and runs `dotnet run` / one-shot `ng build` +
  browser-sync. **This is what `docker-compose.yml` builds.** These are the
  ~5-6 GB images.
- **`Dockerfile.local`** -- thin image that `COPY bin/Release/net10.0/publish/`
  (expects a **host-side** `dotnet publish` first) into an SDK base. A third,
  separate workflow.

> **CORRECTION to an earlier draft:** it is **wrong** that "db-migrator is the
> only multi-stage prod image." db-migrator is only the one whose **prod
> `Dockerfile` is wired into compose**; api/authserver/angular have prod
> `Dockerfile`s too -- just unused locally. This materially changes Option 3
> (Section 8): a prod image path largely **already exists**.

### 3.2 The local compose is a DEV compose (HIGH)
`docker-compose.yml` (folded 2026-05-06; the old `docker-compose.dev.yml`
overlay was **deleted**) builds `api`, `authserver`, `angular` from
`Dockerfile.dev`. `db-migrator` and `gotenberg` use their own `Dockerfile` /
`Dockerfile.gotenberg-fonts`. There is **only one compose file** -- no
prod/local compose profile exists.

### 3.3 Hot-reload was deliberately removed (HIGH)
OBS-22 (resolved 2026-05-22, see
`docs/runbooks/findings/bugs/OBS-22-docker-watch-misses-bind-mount-edits.md`):
`dotnet watch` and `ng build --watch` were **removed** because inotify events
don't propagate reliably through Docker Desktop's Windows bind mount (atomic-
rename-on-save drops inodes; polling fallback misses them; refs Docker Desktop
GH #8694, #15171). Current "reload" = `docker compose restart <service>`,
which re-runs `dotnet restore` + `dotnet run` (recompile) / one-shot
`ng build`. **Measured cost (from OBS-22): ~30 s per .NET restart, ~90 s for
Angular.** ENGINEERING-ROADMAP.md lists a "better fix bundled in Slot rework
Phase 2 pre-flight" -- so a future fix is already on the radar.

### 3.4 Image bloat causes (mixed confidence -- validate in Part A)
- **SDK base instead of runtime base** (HIGH, structural): `Dockerfile.dev`
  ships `sdk:10.0` (~3 GB, MEDIUM -- base not pulled standalone locally, so
  re-measure) where prod uses `aspnet:10.0` (~220 MB, MEDIUM). This is the
  single biggest lever.
- **Redundant baked restore+build** (HIGH): `api` image `docker history`
  showed `dotnet restore` = **1.52 GB**, `dotnet build` = **275 MB** baked in
  -- then the entrypoint **deletes restore artifacts and redoes restore+build**
  against bind-mounted source at every start. The baked ~1.8 GB is pure waste.
- **`COPY src/` = 817 MB, and `.dockerignore` is incomplete** (HIGH --
  important correction): `.dockerignore` **does** exclude `**/obj`,
  `**/bin/Debug`, `**/node_modules`. But it has `Logs` (matches only the
  **repo-root** `/Logs`, NOT `src/**/Logs`) and `**/bin/Debug` (misses
  `bin/Release` and other bin output). Host measurement:
  `src/.../AuthServer/Logs` = **272 MB**, `src/.../AuthServer/bin` = **223 MB**
  -- both leak into the build context and inflate `COPY src/`. (`obj` = 16 MB
  IS excluded.) So the `COPY src/` bloat is a **Logs/bin leak**, NOT an obj
  leak. The leaked `Logs/` may also contain PHI -> security angle.
- **Angular `COPY . /app` pulls host `node_modules`** (HIGH): the angular
  build **context is `./angular`**, which has **no `.dockerignore`** (the root
  `.dockerignore`'s `angular/node_modules` rule does NOT apply -- different
  context root). Host `angular/node_modules` = **583 MB**, so `Dockerfile.dev`'s
  `COPY . /app` copies it into the image **on top of** the `yarn install` it
  already ran (and also `dist/`, `.angular/`). Double node_modules.

### 3.5 Host-dev mode exists but is partly broken (HIGH)
`scripts/dev/` has `start-dev-stack.ps1`, `dev-api.ps1`, `dev-authserver.ps1`.
`dev-api.ps1` runs **`dotnet watch run` on the host** (host inotify works ->
**real** hot reload), wiring CORS / `127.0.0.1` SQL+Redis / localhost
AuthServer. **But `start-dev-stack.ps1` is stale/broken:** line 12 references
the **deleted** `docker-compose.dev.yml`, and it hardcodes
`replicate-old-app-sql-server-1` / `-redis-1` container names (wrong for
`main`). So Option 4 is "mostly built, needs repair." See also
`docs/runbooks/LOCAL-DEV.md`.

### 3.6 Documentation drift (MEDIUM)
`docs/runbooks/DOCKER-DEV.md` says services build from `Dockerfile` (they
build from `Dockerfile.dev`) and says "six containers" (there are ~9 now;
MinIO + Gotenberg were added). Its timing/disk benchmarks are from 2026-04-16
(commit `4ed9c4b`): "first build cold ~5 min", "~28 GB images, ~24 GB build
cache" -- useful historical baseline, but stale on specifics. Treat DOCKER-DEV
content as a lead, not truth; verify against `docker-compose.yml`.

### 3.7 The runtime crash (HIGH)
Verbatim signature, for reference:
```
...Microsoft.NET.Sdk.targets(308,5): error MSB4018:
  at ...DependencyContextBuilder.GetRuntimeLibrary(...)
  at ...GenerateDepsFile.WriteDepsFile(String depsFilePath)
warning MSB3026: Could not copy "obj/Debug/net10.0/...EntityFrameworkCore.dll"
  to "bin/Debug/..." ... because it is being used by another process.
The build failed. Fix the build errors and run again.
```
Likely a combination of (a) memory pressure / OOM during the in-container
compile and (b) **host `obj/bin` (514 MB across 20 dirs) leaking through the
`./src:/app/src` bind mount** (bind mounts ignore `.dockerignore`), colliding
with the container's own build. Recovered on auto-restart once memory freed.

---

## 4. Environment context (so probes make sense)

- **Host:** Windows 11, **15.5 GB RAM**, 14 logical / 12 physical cores. Shell
  is **Git Bash (MSYS2)**, NOT WSL. Docker Desktop uses the **WSL2 backend**.
- **WSL/engine caps** (`C:\Users\RajeevG\.wslconfig`, set 2026-05-27):
  `memory=12GB`, `processors=10`, `swap=8GB`, `autoMemoryReclaim=gradual`.
  Changing it needs `wsl --shutdown` + Docker Desktop restart.
- **You run from `W:\patient-portal\main`** -> compose project `main` ->
  `main-*` containers on **canonical ports**: Angular 4200, AuthServer 44368,
  API 44327, SQL 1434, Redis 6379, MinIO 9000/9001, Gotenberg 3000. `main`'s
  `.env` sets **no** PORT overrides (it uses the compose defaults).
- **Other worktrees** use offset ports from their own `.env`
  (e.g. `replicate-old-app`: 4230 / 44398 / 44357 / 1437 / 6382 / 9030-9031 /
  3030). Per-worktree isolation = compose project name (dir basename) +
  per-`.env` ports. See DOCKER-DEV.md "Running multiple worktrees".
- **Stack control (from the `main` worktree root):**
  - Up + build: `docker compose up -d --build`   (omit `--build` if no
    Dockerfile change)
  - Down (keep volumes): `docker compose down`    (`down -v` also drops the
    SQL/MinIO data volumes -> full reseed)
  - Reload one service (current mechanism): `docker compose restart api`
- **Verify health:** `docker compose ps`;
  `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:44327/health-status`
  (API), `:4200` (SPA), `:44368/.well-known/openid-configuration` (AuthServer).

---

## 5. Ground-truth files to read first

- `docker-compose.yml` -- the single folded compose; note `api`/`authserver`/
  `angular` build from `Dockerfile.dev`.
- `src\...\HttpApi.Host\Dockerfile.dev`, `...\AuthServer\Dockerfile.dev`,
  `angular\Dockerfile.dev` + `angular\dev-entrypoint.sh` -- the heavy dev path.
- `src\...\HttpApi.Host\Dockerfile`, `...\AuthServer\Dockerfile`,
  `angular\Dockerfile` -- the **existing prod multi-stage** images (key to
  Options).
- `src\...\HttpApi.Host\Dockerfile.local` (+ AuthServer / DbMigrator /
  angular `.local`) -- the host-publish-then-thin-image variant.
- `src\...\DbMigrator\Dockerfile` -- the only prod image wired into compose
  (585 MB) -- a working size baseline.
- `.dockerignore` (repo root) -- read it; note the `Logs` vs `**/Logs` and
  `**/bin/Debug` vs `**/bin` gaps. Check whether `angular/.dockerignore`
  exists (it does **not**).
- `scripts\dev\start-dev-stack.ps1`, `dev-api.ps1`, `dev-authserver.ps1` --
  host-dev mode (note the stale `docker-compose.dev.yml` reference).
- `docs\runbooks\DOCKER-DEV.md`, `docs\runbooks\LOCAL-DEV.md` -- documented
  workflows (drift-checked above).
- `docs\runbooks\findings\bugs\OBS-22-docker-watch-misses-bind-mount-edits.md`
  -- watcher-removal history + candidate fixes.
- `docs\runbooks\ENGINEERING-ROADMAP.md` (OBS-22 row), `etc\helm\` -- deploy
  path using prod Dockerfiles.

> A stale memory note once described a `docker-compose.dev.yml` + `dotnet
> watch` overlay. That setup is **gone**. Trust the files above.

---

## 6. Part A -- Image & build size investigation

**Objective:** attribute the ~5-6 GB of each dev image to causes, separate
*necessary* from *accidental* (SDK base, redundant baked build, leaked
Logs/bin, doubled node_modules), and quantify what a prod-image path saves.

### A1. Per-image, per-layer attribution
- **Goal:** byte-level breakdown per large image.
- **Probe:** `docker images "main-*" --format "table {{.Repository}}\t{{.Size}}"`;
  `docker history --no-trunc --format "{{.Size}}\t{{.CreatedBy}}" <image>` for
  api/authserver/angular; measure bases:
  `docker pull mcr.microsoft.com/dotnet/sdk:10.0` then `... aspnet:10.0` then
  `... node:20-alpine`, and `docker image inspect --format '{{.Size}}'` each.
  (Bases were not standalone-pulled on 2026-05-27; pulling them read-only is
  fine and lets you quantify the SDK-vs-runtime delta.)
- **Capture:** per-image table: layer -> size -> class (`base` /
  `nuget-restore` / `build-output` / `source-copy` / `logs-leak` /
  `bin-leak` / `node-modules` / `dev-only`), with class subtotals; the exact
  SDK vs aspnet vs node base sizes.

### A2. Build-context size + `.dockerignore` gap quantification
- **Goal:** measure what each context actually ships, and confirm the
  Logs/bin leak and angular node_modules copy.
- **Probe:**
  - Backend context: `DOCKER_BUILDKIT=1 docker build -f src/.../HttpApi.Host/Dockerfile.dev . 2>&1 | grep -i "transferring context"` (Ctrl-C after the context line).
  - Confirm leak: `du -sh src/*/Logs src/*/bin 2>/dev/null`; cross-check
    against `.dockerignore` patterns.
  - Angular context: it is `./angular` with no `./angular/.dockerignore`;
    measure `du -sh angular/node_modules angular/dist angular/.angular`.
  - Inspect a built image for leaked content:
    `docker create --name _probe main-api:latest && docker export _probe | tar -tv | grep -E "/(Logs|bin)/" | head; docker rm _probe`.
- **Capture:** context size per service; MB of Logs + bin shipped into the
  image; whether angular host node_modules is in the image; the precise
  `.dockerignore` lines that would need to change (e.g. `**/Logs`, `**/bin`,
  add `angular/.dockerignore`) -- as findings, not edits.
- **Open questions:** Do leaked `Logs/` contain PHI/identifiers? (Describe,
  don't reproduce.)

### A3. NuGet restore footprint & layer sharing
- **Goal:** quantify restored package volume and whether api/authserver share
  the restore layer or duplicate it.
- **Probe:** compare the `dotnet restore` layer hashes across api/authserver
  via `docker history`; inspect `~/.abp/cli` mount; note test projects being
  restored into a would-be runtime image.
- **Capture:** restore size; degree of layer sharing; redundant test restore.

### A4. The size baselines: db-migrator (good) + the prod Dockerfiles
- **Goal:** quantify how small the images *could* be using the existing prod
  `Dockerfile`s.
- **Probe:** read the three prod `Dockerfile`s; build one read-only to a temp
  tag to measure (e.g. `docker build -f src/.../HttpApi.Host/Dockerfile -t _probe_api_prod --build-arg ABP_NUGET_API_KEY=... .` -- needs the key from `.env`; if you'd rather not build, estimate from db-migrator's 585 MB + `docker history`). Remove temp images after.
- **Capture:** measured/estimated prod image size vs the 5-6 GB dev image;
  the delta = the "size prize."

### A5. Angular image (5.51 GB) specifics
- **Goal:** attribute the angular image (base, yarn install, COPY-. node_modules
  duplication, browser-sync, dist).
- **Probe:** `docker history` the angular image; confirm the doubled
  node_modules from A2; compare to what the prod `angular/Dockerfile`
  (nginx-served) would produce.
- **Capture:** node_modules contribution (installed + copied), browser-sync
  cost, prod-vs-dev delta.

### A6. Engine-wide disk + why the vhdx doesn't shrink
- **Goal:** total WSL `.vhdx` consumption vs reclaimable, and the compaction
  story (the core "WSL clogged" complaint).
- **Probe:** `docker system df -v`; `docker builder du`; locate the WSL data
  `ext4.vhdx` (`%LOCALAPPDATA%\Docker\wsl\...` or
  `wsl.exe --list -v` + the distro's disk path); compare its on-disk size to
  `docker system df`. Document (don't run) compaction options:
  `wsl --manage <distro> --set-sparse true`, or
  `diskpart` `compact vdisk`, or `Optimize-VHD`.
- **Capture:** vhdx physical size vs logical usage; how much is structurally
  required vs reclaimable; the compaction procedure to recommend.

### Part A questions for the report
1. Of each ~5-6 GB dev image: how many GB are SDK base, NuGet restore, build
   output, source copy, **Logs leak**, **bin leak**, **doubled node_modules**?
2. What is *necessary for in-container dev* vs *pure waste*?
3. What would the existing prod `Dockerfile`s cost (measured/estimated)?
4. Exact `.dockerignore` / `angular/.dockerignore` gaps and their MB impact.
5. Total host-disk cost across worktrees; reclaimable vs required; vhdx
   compaction recommendation.

---

## 7. Part B -- Hot-reload investigation

**Objective:** establish what "reload" means today, why the watcher was
removed, whether it could work now, and the full landscape of options.

### B1. Reconstruct history (mostly done -- verify)
- **Probe:** read OBS-22 in full; `git log --oneline -- docker-compose.yml "src/**/Dockerfile*" angular/Dockerfile.dev angular/dev-entrypoint.sh scripts/dev/` and read the 2026-05-06 (fold) and 2026-05-22 (watcher removal) commits.
- **Capture:** confirm/refine the timeline and the exact failure that drove
  removal.

### B2. Measure the current reload cost (verify the ~30 s / ~90 s claim)
- **Goal:** real edit->serving time today on this hardware.
- **Probe (mutating; main stack):** `time docker compose restart api`, then
  `docker logs -f main-api-1` until `Now listening on:`. Repeat for `angular`
  (watch for `ng build` done + browser-sync). Note what dominates (restore vs
  compile vs ABP init).
- **Capture:** seconds per backend/frontend reload; whether the entrypoint's
  `dotnet restore` on every restart is necessary overhead.

### B3. Is the Windows watcher problem still real? (verify -- it may have moved)
- **Goal:** decide whether polling/inotify could work on the current Docker
  Desktop, since that gates Options 1 and 4.
- **Probe:** `docker version` + Docker Desktop About for the engine version;
  research current status of Docker Desktop GH #8694 / #15171 and .NET 10
  `dotnet watch` behaviour; evaluate `DOTNET_USE_POLLING_FILE_WATCHER` memory
  cost at 12 GB (the old claim that polling OOMs may no longer hold).
- **Capture:** Docker Desktop version; whether inotify forwarding works now;
  polling cost on this source-tree size.

### B4. Options to research (do NOT choose; capture trade-offs for each)
For each: how it works, effort, size impact, reload impact, risk, dual-stack-
on-15.5 GB fit. **Note: several are partly built already.**
- **Option 1 -- Re-enable `dotnet watch` in-container with polling.** Reverse
  OBS-22 with `DOTNET_USE_POLLING_FILE_WATCHER=1` + scoped watch tree.
  Depends on B3. Risk: the exact drift that caused removal.
- **Option 2 -- Volume-isolate `obj/bin`.** Mount named/anonymous volumes over
  `src/**/obj` + `src/**/bin` so host artifacts never cross the bind mount
  (addresses the file-lock crash AND speeds restore). OBS-22 already lists
  this as a candidate. Research the standard ".NET-in-Docker obj/bin volume"
  pattern.
- **Option 3 -- Use the existing prod `Dockerfile`s for a runtime stack.**
  Add a compose profile / second compose that builds the prod multi-stage
  images (small) for "just run it", keeping a dev path for iteration. Most of
  the image-size win; the prod Dockerfiles already exist. Research: do they
  build clean today? what breaks (no bind mount = rebuild per change)?
- **Option 4 -- Host-dev for backend (`dotnet watch` on host).** Run
  SQL/Redis/MinIO/Gotenberg in Docker; run api + authserver on the host where
  inotify works -> real hot reload. The `scripts/dev/*.ps1` already do this
  **but `start-dev-stack.ps1` is broken** (references deleted
  `docker-compose.dev.yml`, hardcodes `replicate-old-app-*` names). Research
  the repair + what breaks (CORS, OpenIddict redirect URIs, the
  `falkinstein.localhost` tenant URL, Swagger authority).
- **Option 5 -- Remove in-container reload entirely.** Standardise on
  `docker compose restart` (or Option 3/4) and make it as fast as possible
  (drop the redundant per-restart restore; precompile). Research the minimal
  reliable cycle.
- **Option 6 -- `Dockerfile.local` (host publish -> thin image).** Evaluate
  the existing `.local` variant: `dotnet publish` on host, image just copies
  output. Trade-off: fast small image, but no in-container build and a manual
  publish step.
- **Option 7 -- File-sync layer (Mutagen / docker-sync).** Heavier tooling
  that sidesteps Windows inotify. Research fit/cost.

### Part B questions for the report
1. What does "reload" do today and how slow is it (measured)?
2. Is the Windows watcher problem still real on the current Docker Desktop?
3. Per option: feasibility, effort, risk, dual-stack fit, and how much is
   already built. Which 2-3 deserve a follow-up spike?
4. Does this project even need in-Docker backend iteration, or is host-dev
   (Option 4) the natural fit given inotify on Windows?

---

## 8. Part C -- Build fragility / the runtime crash

**Objective:** classify the `api` startup-compile crash (memory vs file-lock
vs both). Investigate; do not fix.

### C1. Reproduce & classify
- **Probe:** with the *other* worktree's stack stopped (the one allowed
  exception -- e.g. `cd ../replicate-old-app && docker compose stop`, then
  restart after), bring up `main` and watch whether `api` compiles cleanly;
  contrast with both stacks up. Monitor `docker stats --no-stream` + host free
  RAM (`Get-CimInstance Win32_OperatingSystem | % {[math]::Round($_.FreePhysicalMemory/1MB,1)}`)
  during the compile. Check OOM:
  `docker inspect <c> --format '{{.State.OOMKilled}}'`.
- **Capture:** does the crash recur under pressure but not when isolated?
  Per-container memory at failure; OOM flag; minimum free RAM for a clean cold
  `up --build` with both stacks.

### C2. File-lock hypothesis
- **Probe:** confirm host `src/**/obj` + `src/**/bin` (514 MB / 20 dirs) are
  bind-mounted in via `./src:/app/src`; review whether the `Dockerfile.dev`
  entrypoint's artifact-deletion (`find src -name project.assets.json -delete
  ...`) covers the locked file class (`*.dll` in `obj/Debug`). Assess whether
  Option 2 (volume-isolated obj/bin) prevents it.
- **Capture:** memory vs file-lock attribution; whether obj/bin isolation
  would eliminate the crash.

### Part C questions for the report
1. Memory, file-lock, or both?
2. Does isolating obj/bin and/or removing memory pressure eliminate it?
3. Minimum free RAM for a clean dual-stack cold build?

---

## 9. Cross-cutting questions (synthesize)

- Can a 15.5 GB host realistically run two full stacks **and** rebuild one, or
  is the practical model "one stack hot, others stopped"? Quantify peak RAM.
- Single highest-leverage change: prod images (size) vs volume-isolated
  obj/bin (crash+speed) vs host-dev backend (reload) vs `.dockerignore` fix
  (size+security)? Rank by effort/benefit.
- Are `main` and other worktrees' Docker configs identical (verified yes on
  2026-05-27 for main vs replicate) so a fix applies everywhere?
- Do leaked `Logs/` in images create a PHI exposure path?

---

## 10. Deliverable

Write findings to
`docs/runbooks/investigations/2026-05-27-docker-findings.md` (in the worktree
you run from). Structure:
1. **Answers first** (5-8 sentences): why images are huge; why reload is
   broken; the crash cause.
2. **Part A evidence** -- per-image attribution; necessary-vs-waste; the
   `.dockerignore`/leak quantification; prod-image size prize.
3. **Part B evidence** -- history, measured reload times, watcher status.
4. **Part C evidence** -- crash classification.
5. **Options matrix** -- one row per option (Section 7.B4 + A/C fixes),
   columns: how it works | already built? | effort | size impact | reload
   impact | risk | dual-stack-15.5GB fit. **No single recommendation** -- flag
   the 2-3 worth a spike.
6. **Open questions / what could not be verified.**

Label confidence on each claim (HIGH/MEDIUM/LOW) and cite file paths +
command output. If you contradict a Section 3 finding, say so explicitly with
evidence -- that is a success, not a failure.

---

## 11. What NOT to do (recap)
- Do not edit Dockerfiles, compose, `.env`, `.dockerignore`, scripts, source.
- Do not disrupt other worktrees' stacks (one momentary `stop` allowed in C1,
  then restart + verify).
- No `docker system prune -a` / `docker builder prune -a`.
- No host dev servers except the explicit, noted probes.
- Do not apply fixes. Produce the findings report + options matrix.

---

## 12. Appendix -- data captured 2026-05-27 (baseline to extend)

| Item | Value | Source |
| --- | --- | --- |
| Host RAM / CPU | 15.5 GB / 14 logical, 12 physical | PowerShell CIM |
| WSL caps (new) | 12 GB / 10 CPU / 8 GB swap | `.wslconfig` |
| `main-authserver` image | 6.23 GB | `docker images` |
| `main-api` image | 6.18 GB | `docker images` |
| `main-angular` image | 5.51 GB | `docker images` |
| `main-gotenberg` image | 2.46 GB | `docker images` |
| `main-db-migrator` image | 585 MB | `docker images` (prod multi-stage) |
| api layer: `dotnet restore` | 1.52 GB | `docker history` (replicate build) |
| api layer: `COPY src/` | 817 MB | `docker history` |
| api layer: `dotnet build` | 275 MB | `docker history` |
| host `src/**/obj`+`bin` | 514 MB / 20 dirs | `du` / `find` (main) |
| `src/.../AuthServer/Logs` | 272 MB | `du` (main) -- leaks (Logs not `**/Logs`) |
| `src/.../AuthServer/bin` | 223 MB | `du` (main) -- partial leak (`bin/Debug` only excluded) |
| host `angular/node_modules` | 583 MB | `du` (main) -- copied by `COPY .` (no angular/.dockerignore) |
| engine images total | ~25.6 GB | `docker system df` (post-cleanup) |
| engine build cache | ~14.9 GB | `docker system df` (post-cleanup) |
| reload cost (from OBS-22) | ~30 s .NET / ~90 s Angular | OBS-22 |
| DOCKER-DEV.md baseline (2026-04-16) | ~28 GB images, ~24 GB cache, 5 min cold | DOCKER-DEV.md |

> Note: `main-*` image sizes run ~1 GB larger than the `replicate-old-app-*`
> equivalents (authserver 6.23 vs 4.99 GB, etc.) -- a build-cache/layer
> accounting artifact, not a config difference. The configs are identical.
