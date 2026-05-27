# Docker Image Bloat + Broken Hot-Reload -- Findings Report

- **Date:** 2026-05-27. **Run from:** `W:\patient-portal\main` (compose project
  `main`, canonical ports). Both `main` and `replicate-old-app` stacks were UP
  and healthy throughout (this let me measure dual-stack memory directly without
  the optional C1 stop).
- **Scope:** investigation ONLY. No Dockerfiles / compose / `.dockerignore` /
  scripts / source were modified. Brief named: see
  `docs/runbooks/investigations/2026-05-27-docker-image-size-and-hot-reload.md`
  (in the `replicate-old-app` worktree).
- **State changes I made (all benign / reversible):** pulled 4 base images
  (`sdk:10.0`, `aspnet:10.0`, `node:20-alpine`, `nginx:alpine`) for measurement
  -- these are reused by future builds, so I left them cached; restarted
  `main-api` and `main-angular` once each to time the reload cycle (both back to
  HTTP 200); ran several ephemeral `--rm` containers that auto-removed. I did
  **not** touch the `replicate-old-app` stack and did **not** force the Part C
  cold-build crash (rationale in Part C).
- **Confidence tags:** HIGH = measured/read this session; MEDIUM = reputable
  docs/inference; LOW = hypothesis.

Written for a developer new to Docker. Jargon is explained on first use.

---

## A 60-second Docker vocabulary (so the rest reads cleanly)

- **Image** = a frozen, read-only snapshot of a filesystem + a start command.
  You `run` an image to get a **container** (a live process).
- **Layer** = an image is built as a stack of read-only layers, one per
  Dockerfile instruction (`COPY`, `RUN`, ...). Layers are **immutable and
  additive**: deleting a file in a later layer does **not** shrink the image --
  the bytes stay in the earlier layer, hidden by a "whiteout" marker. (Docker
  Docs, *Understanding image layers*.) This single fact explains most of the
  bloat below.
- **Base image** = the image your Dockerfile starts `FROM`. `dotnet/sdk` (big,
  has the compiler) vs `dotnet/aspnet` (small, runtime only).
- **Build context** = the folder you point `docker build` at; its entire
  contents are sent to the build engine. **`.dockerignore`** trims that folder.
  Each build context has its **own** `.dockerignore` at its root.
- **Bind mount** = a host folder mapped live into a container
  (`./src -> /app/src`). Edits on the host appear instantly in the container.
  **Bind mounts ignore `.dockerignore`** (that only filters the build context).
- **Multi-stage build** = one Dockerfile with several `FROM` stages; you build
  in a fat SDK stage, then `COPY --from=build` only the finished binaries into a
  thin runtime stage. The final image keeps only the last stage.
- **WSL2 / vhdx** = on Windows, Docker Desktop runs the Linux engine inside a
  WSL2 virtual machine whose disk is a single file, `docker_data.vhdx`. It
  **grows** as you add images but does **not auto-shrink** when you delete them.

---

## 1. Summary of the current setup (plain terms)

We run the whole app as a Docker "stack" of ~9 containers per git worktree:
SQL Server, Redis, MinIO (+init), Gotenberg, DbMigrator, AuthServer, the API,
and Angular. There is **one** compose file, `docker-compose.yml`, and it is a
**development** compose -- there is no separate "prod" compose.

Three of the app services -- `api`, `authserver`, `angular` -- build from
`Dockerfile.dev` files. Those are **single-stage** images built `FROM` the full
.NET **SDK** image (or `node:20-alpine`). They copy the source in, bake a
`dotnet restore` + `dotnet build` (or `yarn install`) into the image, and then at
container **start** they bind-mount the host source over the top and run
`dotnet run` / a one-shot `ng build` again. So the image ships the heavy build
toolchain AND a baked build that it largely re-does at runtime.

Meanwhile, **proper multi-stage production `Dockerfile`s already exist** in the
repo for all three services (and are what `etc/helm/build-image.ps1` uses) --
they are just **not wired into the local compose**. The one prod image that *is*
wired in, `db-migrator`, is **585 MB** -- a working proof that small images are
already achievable here.

Measured image sizes (live, `docker images`, 2026-05-27):

| Image | Size | How it's built |
| --- | --- | --- |
| `main-angular` | **5.51 GB** | `Dockerfile.dev`, single-stage, `node:20-alpine` |
| `main-authserver` | **4.6 GB** | `Dockerfile.dev`, single-stage, `sdk:10.0` |
| `main-api` | **4.56 GB** | `Dockerfile.dev`, single-stage, `sdk:10.0` |
| `main-gotenberg` | 2.46 GB | `Dockerfile.gotenberg-fonts` (fonts baked on gotenberg:8) |
| `main-db-migrator` | **585 MB** | `Dockerfile`, **multi-stage prod**, `aspnet:10.0` |

> **Correction to the brief's appendix.** The appendix recorded api/authserver at
> 6.18 / 6.23 GB; live values are **4.56 / 4.6 GB**. The brief flagged the larger
> numbers as a build-cache accounting artifact -- confirmed. I use the live
> numbers throughout. (HIGH)

"Hot-reload" was **deliberately removed** on 2026-05-22 (OBS-22). Today "reload"
means `docker compose restart <service>`, which re-runs restore+compile (.NET)
or `ng build` (Angular) inside the container.

---

## Answers first (the three questions)

1. **Why are the dev images ~4.5-5.5 GB?** Three structural reasons, all
   measured: (a) they build `FROM` the **SDK** base (1.23 GB) instead of the
   **runtime** base (340 MB) and never drop the toolchain via a multi-stage
   copy; (b) they **bake a full `dotnet restore` (1.52 GB) + build (~280 MB)**
   into the image that the entrypoint then throws away and redoes at runtime;
   (c) the Angular image stores **node_modules and a 1.8 GB yarn cache multiple
   times across layers** and additionally **copies the host's `node_modules`,
   `dist`, and `.angular` in** because the `./angular` build context has **no
   `.dockerignore`**. The existing prod Dockerfiles would cut each .NET image to
   ~0.6 GB and Angular to ~0.15 GB. (HIGH)
2. **Why is reload broken / slow?** It was removed on purpose: Docker Desktop on
   Windows does not reliably forward host file-change events into a Linux
   container across a bind mount, so `dotnet watch` / `ng build --watch` silently
   missed edits (OBS-22; Docker/.NET GitHub issues). The replacement --
   `docker compose restart` -- works but is slow because the .NET entrypoint runs
   a **full-solution `dotnet restore` on every restart**. **Measured this
   session under dual-stack load: API restart->serving = ~108 s; Angular = ~28 s.**
   (HIGH -- and this *contradicts* OBS-22's "30 s .NET / 90 s Angular"; see
   Part B.)
3. **What caused the startup-compile crash?** Evidence points to **two
   independent causes acting together**: memory pressure (a cold build of both
   stacks drives the WSL VM toward its 12 GB cap on a host with only ~5 GB of
   genuinely free headroom, causing swap thrashing) AND a **file-lock**
   (`*.dll ... being used by another process`) from the host's `obj/bin`
   leaking through the `./src:/app/src` bind mount, which ignores
   `.dockerignore`. It recovered on auto-restart once memory freed, so it is
   pressure-dependent, not deterministic. (MEDIUM -- not re-triggered this
   session; see Part C.)

---

## 2. Issues (each: what / why it bloats / evidence / source)

### Issue 1 -- Dev images use the SDK base and never shed it (biggest lever)

- **What.** `api`/`authserver` `Dockerfile.dev` are `FROM
  mcr.microsoft.com/dotnet/sdk:10.0` and stay single-stage, so the compiler +
  full SDK ships in the final image.
- **Why it bloats.** The SDK image is built for *building* (compiler, CLI,
  PowerShell, git) and is intentionally large; the runtime image (`aspnet`) is
  built for *running* and is intentionally small. Microsoft: the SDK image's
  "tools installed for development and compilation make the image relatively
  large"; the aspnet runtime image is "relatively small ... only the binaries
  and content needed to run an app." A multi-stage build exists precisely to
  copy the publish output into the small base and discard the SDK.
- **Evidence (HIGH).** Measured standalone base sizes:
  `sdk:10.0` = **1.23 GB** vs `aspnet:10.0` = **340 MB** -> ~**890 MB** saved per
  .NET image just on the base. `node:20-alpine` = 194 MB; `nginx:alpine` =
  93.6 MB. The prod `db-migrator` image (multi-stage, aspnet base) is **585 MB**
  -- proof the pattern already works here.
  > Note: the brief guessed the SDK base at ~3 GB. **It is 1.23 GB.** So the
  > base swap alone is worth ~0.9 GB, not ~2.8 GB -- still real, but the bigger
  > .NET win is Issue 2 (the baked restore), not the base.
- **Source.** MS Learn, *Run an ASP.NET Core app in Docker containers*
  (https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0);
  MS Learn, *Official .NET Docker images*
  (https://learn.microsoft.com/dotnet/architecture/microservices/net-core-net-framework-containers/official-net-docker-images).
  **Official guidance.**

### Issue 2 -- A full `dotnet restore` + build is baked in, then redone at runtime

- **What.** `Dockerfile.dev` runs `dotnet restore` (whole solution) and
  `dotnet build` at *image build* time. Then the **entrypoint** deletes restore
  artifacts and runs `dotnet restore` + `dotnet run` **again** at *container
  start* against the bind-mounted host source.
- **Why it bloats / wastes.** The baked restore+build are kept as image layers
  forever (immutable layers), but functionally they are used only for the very
  first boot; every subsequent `restart` ignores them. So ~1.8 GB of image is
  near-dead weight, and the runtime restore is the thing that actually matters.
- **Evidence (HIGH).** `docker history main-api`: `dotnet restore` layer =
  **1.52 GB**, `dotnet build` layer = **276 MB** (authserver build = 309 MB).
  The entrypoint (`Dockerfile.dev` lines 38-43) does
  `find src -name project.assets.json -delete ... ; dotnet restore ... ; dotnet
  run --no-restore`.
- **Source.** Layer immutability: Docker Docs, *Understanding image layers*
  (https://docs.docker.com/get-started/docker-concepts/building-images/understanding-image-layers/);
  qmacro, *Immutable layers, file deletion and image size*
  (https://qmacro.org/blog/posts/2024/10/26/immutable-layers-file-deletion-and-image-size-in-docker/).
  **Community + official.**

### Issue 3 -- Application logs leak into the .NET images (~388 MB) -- also a PHI angle

- **What.** The root `.dockerignore` line is `Logs` (no slashes). Docker anchors
  that pattern to the **context root** (`/Logs`), so it does **not** match
  `src/.../<Service>/Logs`. Those per-service log folders are copied by
  `COPY src/ src/`.
- **Why it bloats.** Hundreds of MB of runtime logs become permanent image
  layers. Logs also change constantly, so they **bust the build cache** and slow
  rebuilds.
- **Evidence (HIGH).** Inside the built `main-api` image (ephemeral
  `docker run --entrypoint sh ... du`): baked `/app/src/.../AuthServer/Logs` =
  **271 MB**, `/app/src/.../HttpApi.Host/Logs` = **117 MB** (~**388 MB** total,
  per .NET image). Fix would be changing `Logs` -> `**/Logs` in `.dockerignore`.
  `**/obj` (32 MB) *is* correctly excluded; `**/bin/Debug` excludes the host's
  Debug output (the `bin/Debug` you see in the image is the container's *own*
  build, which is necessary).
- **HIPAA note.** I did not open the log files. By path and size these are
  ASP.NET request logs; request logs in this app can contain tenant/user context,
  so **shipping them inside an image is a PHI-exposure path** (the image is a
  redistributable artifact). Treat as a security finding, not just size.
- **Source.** `.dockerignore` pattern anchoring + per-context location: Docker
  Docs, *Build context* (https://docs.docker.com/build/concepts/context/).
  **Official.**

### Issue 4 -- The Angular image carries node_modules ~3x and a 1.8 GB yarn cache

- **What.** `angular/Dockerfile.dev`: `yarn install` (creates node_modules),
  then `COPY . /app` (copies the host's `angular/` **including** its
  `node_modules`, `dist`, `.angular`), then `yarn add browser-sync@3` (rewrites
  node_modules again). The `./angular` build context has **no `.dockerignore`**.
- **Why it bloats.** Each step that touches node_modules writes a brand-new
  full copy as a new immutable layer; the older copies are not reclaimed (union
  filesystem). On top of that, `yarn install` leaves a large package cache in the
  image that is never needed at runtime.
- **Evidence (HIGH).** `docker history main-angular`: `yarn install` layer =
  **2.7 GB**, `COPY . /app` = **746 MB**, `yarn add browser-sync@3` = **685 MB**.
  Inside the image: `/app/node_modules` = **723 MB** (587 packages),
  **`/usr/local/share/.cache` (yarn cache) = 1.8 GB**, `/app/dist` = 34.6 MB,
  `/app/.angular` = 11.1 MB. `ls angular/.dockerignore` -> **does not exist**.
  Host `angular/node_modules` = 583 MB (the thing `COPY . /app` needlessly
  pulls in). Fix: add `angular/.dockerignore` (or, modern BuildKit, an
  `angular/Dockerfile.dev.dockerignore`) excluding `node_modules`, `dist`,
  `.angular`; and `yarn cache clean` in the same `RUN` as `yarn install`.
- **Source.** Per-context / per-Dockerfile `.dockerignore`: Docker Docs, *Build
  context* (https://docs.docker.com/build/concepts/context/). Cache/cleanup in
  the same layer: Docker Docs *Understanding image layers* (above) + Baeldung,
  *Removing Files in Different Docker Layers*
  (https://www.baeldung.com/ops/docker-layers-delete-files-directories).
  **Official + community.**

### Issue 5 -- The WSL `docker_data.vhdx` is 72 GB and never auto-shrinks

- **What.** Docker Desktop stores everything (images, build cache, volumes) in
  one growing virtual-disk file. Deleting images frees space *inside* the Linux
  VM but the Windows-side file stays at its high-water mark.
- **Why it matters.** This is the literal "WSL is clogging my disk" complaint:
  the file can be far larger than current contents and only a manual **compaction**
  reclaims it.
- **Evidence (HIGH).** `docker_data.vhdx` = **72.17 GB** on disk
  (`C:\Users\RajeevG\AppData\Local\Docker\wsl\disk\`), while `docker system df`
  reports only ~62 GB of live content (37.78 GB images + 24.11 GB build cache +
  0.55 GB volumes), and much of "37.78 GB images" is **shared layers
  double-counted** (see Redundancy 1). Build cache is **24.11 GB of which
  ~20.8 GB is reclaimable** (`docker builder du`). Active distro: `docker-desktop`
  (WSL v2).
- **Source.** Microsoft Q&A, *WSL2 sparse vhd does not shrink*
  (https://learn.microsoft.com/en-us/answers/questions/1526083/in-wsl2-with-sparse-vhd-the-storage-usage-does-not);
  Hanselman, *Shrink your WSL2 Virtual Disks*
  (https://www.hanselman.com/blog/shrink-your-wsl2-virtual-disks-and-docker-images-and-reclaim-disk-space).
  **Official + reputable community.**

### Issue 6 -- The host cannot fit two cold builds at once (memory)

- **What.** 15.46 GB host RAM; WSL capped at 12 GB. At **idle**, the dual-stack
  is fine; under a **cold build**, WSL balloons toward 12 GB while Windows itself
  is already using ~10 GB -> swap thrashing -> stalls/crash.
- **Evidence (HIGH for idle; MEDIUM for the cold-build peak).** Live, both full
  stacks idle: host **free = 1.7 GB of 15.46 GB**; `vmmemWSL` = **3.57 GB**;
  per-container idle totals ~2.15 GB (main) + ~1.7 GB (replicate) = **~3.85 GB**.
  The brief recorded free RAM falling to ~0.3 GB during a cold dual build (the
  trigger for this investigation). No current container shows `OOMKilled`
  (all `false`, `RestartCount=0`).
- **Source.** WSL memory cap behaviour: `.wslconfig` `memory=` is a hard cap
  (Microsoft / Hanselman, above). **Official + community.**

---

## 3. Redundancies (duplicated work / layers / dependencies)

1. **Shared base layers are double-counted in the headline number.** `docker
   system df` shows "37.78 GB images," but `docker system df -v` shows
   `main-api` and `main-authserver` **share 4.167 GB** (identical SDK base + COPY
   src + restore) and differ by only their ~390-430 MB build layer; the two
   `*-angular` images **share 3.865 GB**; the two gotenberg images share 2.46 GB.
   So the *true unique* on-disk image footprint is far below 37.78 GB. The disk
   problem is dominated by **build cache (24 GB)** + the **un-compacted vhdx**,
   not by unique image bytes. (HIGH)
2. **Restore+build done twice** (baked at build time, redone at every container
   start) -- Issue 2. Pure duplicated work. (HIGH)
3. **node_modules materialised ~3 times** in the Angular image (yarn install ->
   COPY . overwrites -> yarn add rewrites), plus a 1.8 GB yarn cache that
   duplicates what is already unpacked in node_modules -- Issue 4. (HIGH)
4. **Two ways to express the same secrets path.** Compose bind-mounts
   `appsettings.secrets.json` to both `/app/...` and the content-root path; the
   Dockerfiles also `echo '{}'` placeholder secrets. Not a size issue, but it is
   duplicated config that a reader must reconcile. (MEDIUM)
5. **Three Dockerfile variants per service** (`Dockerfile`, `.dev`, `.local`)
   encode three different workflows; only `.dev` is wired into compose, `Dockerfile`
   only into `etc/helm`, `.local` into nothing. Maintenance duplication, and the
   reason "we already have small images" is easy to miss. (HIGH)

---

## 4. Hot-reload-specific problems (separate root cause from image size)

These are about the **edit -> see-change loop**, not bytes on disk.

### 4a. Why "watch" was removed (history -- verified)

OBS-22 (resolved 2026-05-22, commit `87fe5ae` "remove .NET + ng watchers"):
`dotnet watch` and `ng build --watch` were dropped because **Docker Desktop on
Windows does not reliably deliver host filesystem events into the Linux
container across a bind mount.** Editors that save via atomic-rename change the
file's inode; the inotify shim and the polling fallback both miss it, so the
watcher goes silent and you test stale code. The team's real workflow had already
become "edit -> `docker compose restart`," so the watchers were removed to match.
This is a well-known, not-our-bug class of issue. (HIGH)

- **Source.** GitHub: dotnet/aspnetcore #26492 and docker/for-win #8749
  (`PollingFileWatcher` with WSL/bind mounts); dotnet/sdk #53337 (.NET 10 watch
  "address already in use" on restart over a mounted volume); MS guidance that
  `DOTNET_USE_POLLING_FILE_WATCHER=1` is the intended workaround **but** is not
  honoured by every .NET file watcher and costs CPU. **Community + official.**

### 4b. Current reload cost -- MEASURED, and it contradicts OBS-22

I timed a real `docker compose restart` on the live `main` stack (both stacks up):

| Service | Container start -> serving | Dominated by | OBS-22 claim |
| --- | --- | --- | --- |
| **API** | **~108 s** (18:30:45 -> "Now listening" 18:32:33; first ABP log at +103 s) | per-restart **full-solution `dotnet restore`** + compile, under memory pressure | "~30 s" |
| **Angular** | **~28 s** (18:39:23.5 -> Browsersync "Serving" 18:39:51.9) | only `ng build` (node_modules is pre-baked) | "~90 s" |

> **Two corrections (HIGH, measured).** (1) .NET reload is **~3.6x worse** than
> the documented 30 s -- because the entrypoint re-runs a whole-solution restore
> every time and the box is under memory pressure. (2) Angular reload is **~3x
> better** than the documented 90 s -- node_modules being baked into the image
> means a restart only rebuilds, it doesn't reinstall. So today the **API is the
> slow one**, the opposite of the OBS-22 framing. The single biggest reload win
> is **dropping the per-restart `dotnet restore`** (it only needs to run when a
> `.csproj` actually changed).

### 4c. Is the Windows watcher problem still real? (gates Options 1 + 4)

Environment now: Docker Desktop **4.75.0**, engine **29.5.2**, WSL **v2**
distro. The underlying inotify-over-bind-mount limitation on Windows is still
reported as of 2026 (the GitHub issues above remain open / recurring, including
a fresh .NET 10 variant). So **in-container watch on a Windows bind mount remains
unreliable** -- Option 1 inherits exactly the risk OBS-22 was created for.
However, **host-side `dotnet watch` (Option 4) uses native Windows file events
and does not cross the bind-mount boundary**, so it sidesteps the whole class.
(MEDIUM -- based on current docs + still-open issues, not a re-test of the
watcher on this exact build.)

---

## Part C -- the startup-compile crash (classification)

- **What was reported.** During a cold dual-stack `up --build`, `api` died with
  `MSB4018` (in `GenerateDepsFile`/`DependencyContextBuilder`) + `MSB3026`
  "could not copy `EntityFrameworkCore.dll` ... because it is being used by
  another process." It **recovered automatically** on Docker's restart once RAM
  freed.
- **Two causes, both supported, independent:**
  1. **Memory pressure (MEDIUM-HIGH).** Free host RAM hit ~0.3 GB during the cold
     dual build; a process stalled/killed mid-`GenerateDepsFile` is consistent
     with swap thrashing. "Recovered once memory freed" is the signature of a
     pressure-dependent, non-deterministic failure.
  2. **File-lock via the bind mount (MEDIUM).** `./src:/app/src` is a bind mount,
     and **bind mounts ignore `.dockerignore`**, so the host's `obj/bin`
     (514 MB / 20 dirs) is visible inside the container. The entrypoint deletes
     `project.assets.json` and `*.nuget.g.*` but **not** the `*.dll` in
     `obj/Debug`, so a host-side or parallel-container build can hold a lock on
     `EntityFrameworkCore.dll` -- exactly the `MSB3026` text.
- **Why I did not force a repro (and what that costs the report).** Re-creating
  the crash means a cold `up --build` of both stacks at <2 GB free RAM, which
  risks destabilising the running `main` stack the developer may be using. The
  reload probe I *did* run executed a compile under pressure (api 108 s) and did
  **not** crash, which suggests the failure needs the heavier *simultaneous cold
  full-restore of both stacks*, not a single restart. Classification is therefore
  evidence-based inference, not a reproduction. (Flagged.)
- **Do the fixes address it?** Option 2 (volume-isolate `obj/bin`) removes the
  file-lock leg; running one stack at a time, or Option 3 prod images, removes
  the memory leg. The legs are independent, so a durable fix likely needs **both**.
- **Minimum free RAM for a clean dual cold build:** not measured (repro
  declined). Inference: idle dual WSL = 3.57 GB; a single cold build drove host
  free RAM to ~0.3 GB; so two simultaneous cold builds do not fit under the
  current 12 GB cap with Windows holding ~10 GB. Realistic model: **one stack
  builds at a time** (LOW-MEDIUM, inferred).

---

## 5. What I should decide (decisions + trade-offs -- not chosen for you)

### Options matrix

Legend: **Built?** = how much already exists in the repo. Effort is for a solo dev.

| # | Option | How it works | Built? | Effort | Size impact | Reload impact | Risk | Fit on 15.5 GB host |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Q1 | **`.dockerignore` fix** | `Logs`->`**/Logs`; add `angular/.dockerignore` (node_modules/dist/.angular) | partial | **XS** | -~0.39 GB/.NET img; stops 0.58 GB host node_modules copy; faster cache | none | very low | helps a little |
| Q2 | **Reclaim disk now** | `docker builder prune` (NOT `-a`) + compact vhdx (see below) | n/a | **XS** | reclaim ~20 GB cache + shrink 72 GB vhdx | none | low (don't use `-a`) | frees host disk immediately |
| 1 | **Re-enable in-container `dotnet watch` (polling)** | revert OBS-22 + `DOTNET_USE_POLLING_FILE_WATCHER=1` | was built, removed | M | none | best *if it works* | **high** -- the exact Windows drift OBS-22 removed; still open upstream | poor (polling burns CPU) |
| 2 | **Volume-isolate `obj`/`bin`** | named/anon volumes over `src/**/obj`+`bin` so host artifacts never cross the mount | no (OBS-22 lists it) | S-M | small | faster restore; fixes crash file-lock leg | low-med | good |
| 3 | **Use the existing prod Dockerfiles (runtime stack)** | compose profile that builds the multi-stage `Dockerfile`s (aspnet/nginx) | **mostly built** (Dockerfiles exist; db-migrator proves it) | M | **huge**: .NET ~4.56->~0.6 GB; Angular ~5.5->~0.15 GB | **no live edit** -- rebuild per change | low-med (do they build clean today?) | best for "just run it" + dual-stack |
| 4 | **Host-dev backend (`dotnet watch` on host)** | SQL/Redis/MinIO/Gotenberg in Docker; api+authserver on host (native file events) | partly built, **broken** (`start-dev-stack.ps1` refs deleted `docker-compose.dev.yml`, hardcodes `replicate-old-app-*`) | M | n/a (no .NET image) | **real hot reload** | med (CORS/OpenIddict redirect URIs / `*.localhost` tenant / Swagger authority must line up) | excellent (frees the WSL VM) |
| 5 | **Drop in-container reload, optimise `restart`** | keep restart-on-edit; remove per-restart full `dotnet restore` (only restore when csproj changed); precompile | trivial delta from today | S | small | API reload ~108 s -> seconds | low | good |
| 6 | **`Dockerfile.local` (host publish -> thin image)** | `dotnet publish` on host, image just `COPY`s output | exists, wired to nothing | M | small image | no in-container build; manual publish step each change | med | good |
| 7 | **File-sync layer (Mutagen / docker-sync)** | external tool syncs host<->container, bypassing Windows inotify | no | **L** | none | can restore real watch | med-high (extra moving part) | neutral |

### The decisions in front of you

1. **Disk, right now (independent of everything else).** Do you want me to
   reclaim the ~20 GB of build cache and compact the 72 GB vhdx?
   - Trade-off: cache prune means the *next* build is slower (cold). Compaction
     needs Docker Desktop + WSL fully shut down for a few minutes.
   - **Compaction caveat (must choose a path):** the modern way is `wsl --manage
     docker-desktop --set-sparse true`, but once a vhdx is sparse, `Optimize-VHD`
     **refuses** to run on it (they are mutually exclusive). The classic way is
     `wsl --shutdown` then `Optimize-VHD -Path ...docker_data.vhdx -Mode Full`
     (needs the Hyper-V PowerShell module, Admin). Decision: **sparse auto-shrink**
     vs **manual `Optimize-VHD`** -- not both.
     (Source: MS Q&A + Hanselman, above.)

2. **Image size: adopt the prod Dockerfiles (Option 3) or not?** This is the
   single biggest size lever and it is *mostly already built*. The cost is that a
   prod-style image has **no live source mount** -- you rebuild to see a change.
   So this pairs naturally with a separate fast-iteration path (Option 4 or 5).
   Decision: do you want a **two-mode setup** (small "just run it" stack + a
   dedicated iteration mode), or keep one dev stack and only trim it?

3. **Iteration model: where does the backend run?** The measured reality is that
   in-container watch on Windows is unreliable (Option 1 high-risk), while
   host-side `dotnet watch` (Option 4) gets real hot reload but needs the broken
   `start-dev-stack.ps1` repaired and the auth/CORS/tenant URLs aligned.
   Decision: **(4) host-dev backend** vs **(5) keep restart but make it fast**
   (drop the per-restart restore -- low risk, big win on the 108 s number) vs
   **(1) gamble on in-container watch again**.

4. **Cheap hygiene regardless of the above (Q1).** The `.dockerignore` fixes
   (`**/Logs`, add `angular/.dockerignore`) are near-zero-risk, shrink images,
   speed builds, and **close a PHI-leak path**. The only decision is whether to
   bundle them into whichever option above you pick or do them first as a
   standalone change.

### My read on leverage (ranked, for your judgement -- not a pick)

- **Best size-per-effort:** Option 3 (prod images) -- ~4 GB off each .NET image,
  ~5.4 GB off Angular, and it largely exists. Spike: confirm the prod Dockerfiles
  build clean today with the ABP key.
- **Best crash + restore-speed-per-effort:** Option 2 (obj/bin volumes) +
  Option 5 (drop per-restart restore). Both low-risk, directly hit the 108 s
  reload and the file-lock crash leg.
- **Best reload experience:** Option 4 (host-dev backend) -- but it has the most
  wiring to get right. Spike: repair `start-dev-stack.ps1` and verify
  OpenIddict redirect URIs / `*.localhost` tenant routing still work host-side.
- **Lowest risk overall:** Q1 + Q2 (dockerignore + reclaim) -- do these no matter
  which big option wins.

**Three worth a follow-up spike:** Option 3 (prod images), Option 2+5 (obj/bin
volume + drop redundant restore), Option 4 (host-dev backend).

---

## 6. Cross-cutting answers

- **Can a 15.5 GB host run two stacks AND rebuild one?** At **idle**, yes -- two
  full stacks use ~3.85 GB. During a **cold build**, no -- one cold build alone
  drove free RAM to ~0.3 GB. Realistic model: **one stack hot; stop the others
  before a cold `up --build`.** (HIGH idle / MEDIUM build.)
- **Single highest-leverage change?** For **size**: Option 3 (prod images). For
  **crash + reload speed**: Option 2 + Option 5. For **disk-now**: Q2. For
  **security+size+cache, ~free**: Q1. They are largely independent.
- **Are `main` and other worktrees' Docker configs identical?** The brief
  verified `main` vs `replicate-old-app` diff clean for compose/.dockerignore/
  Dockerfiles; image sizes differ only by build-cache accounting. So a fix
  applies everywhere. (HIGH, per brief; not re-diffed this session.)
- **Do leaked `Logs/` create a PHI exposure path?** Yes -- ~388 MB of ASP.NET
  request logs are baked into each .NET image, which is a redistributable
  artifact. Treat Issue 3 as security + size. (MEDIUM -- by path/size; I did not
  open the files per HIPAA rule.)

---

## 7. Open questions / could not verify

1. **Exact prod image sizes for api/authserver** -- estimated ~0.6 GB from the
   measured 585 MB db-migrator baseline; not built this session (would need the
   ABP NuGet key and a full publish). AuthServer's prod build also runs
   `abp install-libs` (Node in the build stage), so its final image may be
   somewhat larger -- verify by building to a temp tag.
2. **The crash was not reproduced** (memory + file-lock classification is
   inference; repro declined to protect the running stack). A controlled repro
   with the other stack stopped would confirm the OOM-vs-lock split.
3. **Minimum free RAM for a clean dual cold build** -- not measured.
4. **`docker history` layer sums (~3.5 GB) are below the reported image size
   (~4.56 GB).** The class-level attribution is solid (base / restore / build /
   logs); ~1 GB is unattributed in `history` and is most likely BuildKit/overlay
   accounting -- not chased further as it does not change any recommendation.
5. **Whether Option 1's in-container watch would behave better on Docker Desktop
   4.75 / .NET 10** -- the upstream issues are still open; not empirically
   re-tested on this exact build.

---

## 8. Evidence appendix (commands run this session)

- `docker images "main-*"`, `docker system df [-v]`, `docker builder du`
- `docker history --no-trunc main-{api,authserver,angular}:latest`
- `docker pull` + size of `sdk:10.0` (1.23 GB), `aspnet:10.0` (340 MB),
  `node:20-alpine` (194 MB), `nginx:alpine` (93.6 MB)
- `docker run --rm --entrypoint sh main-api:latest -c 'du ...'` (baked Logs/bin/obj)
- `docker run --rm --entrypoint sh main-angular:latest -c 'du ...'` (node_modules,
  yarn cache, dist, .angular)
- `du -sh src/*/{Logs,bin,obj} angular/{node_modules,dist,.angular}` (host)
- `docker stats --no-stream`; `docker inspect ... OOMKilled/RestartCount`
- PowerShell `Win32_OperatingSystem` free/total RAM; vhdx file size via
  `Get-ChildItem ... *.vhdx`; `wsl --list -v`
- `docker compose restart {api,angular}` + `docker logs --timestamps` (reload timing)
- `git log -- docker-compose.yml src/**/Dockerfile* angular/Dockerfile.dev ...`

### Sources

Official:
- MS Learn -- Run an ASP.NET Core app in Docker containers: https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0
- MS Learn -- Official .NET Docker images (dev vs prod): https://learn.microsoft.com/dotnet/architecture/microservices/net-core-net-framework-containers/official-net-docker-images
- MS Learn -- .NET container images (size-optimised: Alpine/chiseled): https://learn.microsoft.com/dotnet/core/docker/container-images
- Docker Docs -- Build context & .dockerignore: https://docs.docker.com/build/concepts/context/
- Docker Docs -- Understanding image layers: https://docs.docker.com/get-started/docker-concepts/building-images/understanding-image-layers/
- Docker Docs -- Bind mounts: https://docs.docker.com/engine/storage/bind-mounts/ ; Volumes: https://docs.docker.com/engine/storage/volumes/
- Microsoft Q&A -- WSL2 sparse vhd does not shrink: https://learn.microsoft.com/en-us/answers/questions/1526083/in-wsl2-with-sparse-vhd-the-storage-usage-does-not

Community (reputable):
- Hanselman -- Shrink your WSL2 Virtual Disks: https://www.hanselman.com/blog/shrink-your-wsl2-virtual-disks-and-docker-images-and-reclaim-disk-space
- qmacro -- Immutable layers, file deletion and image size: https://qmacro.org/blog/posts/2024/10/26/immutable-layers-file-deletion-and-image-size-in-docker/
- Baeldung -- Removing files in different Docker layers: https://www.baeldung.com/ops/docker-layers-delete-files-directories
- GitHub: dotnet/aspnetcore#26492, docker/for-win#8749, dotnet/sdk#53337 (dotnet watch / polling over bind mounts on Windows)

Internal:
- `docs/runbooks/findings/bugs/OBS-22-docker-watch-misses-bind-mount-edits.md`
- `docker-compose.yml`; `src/.../{AuthServer,HttpApi.Host}/Dockerfile{,.dev,.local}`;
  `src/.../DbMigrator/Dockerfile`; `angular/Dockerfile{,.dev}`,
  `angular/dev-entrypoint.sh`; `.dockerignore`
