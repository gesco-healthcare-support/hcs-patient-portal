---
id: OBS-22
title: dotnet watch + ng build --watch silently miss source edits through Docker bind-mount on Windows
severity: low
status: documented
found: 2026-05-19
resolved: 2026-05-22
flow: dev-stack
component: src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Dockerfile.dev | src/HealthcareSupport.CaseEvaluation.AuthServer/Dockerfile.dev | angular/Dockerfile.dev | angular/dev-entrypoint.sh | docker-compose.yml
---

> **Resolution 2026-05-22.** Watchers removed entirely. The actual iteration workflow had already collapsed to "edit on host -> `docker compose restart <service>`" because the bind-mount / inotify drift was so unreliable. Removing the watchers matches the real workflow with no UX loss and eliminates an entire class of "did my edit take effect?" debugging time.
>
> **Changes shipped:**
> - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Dockerfile.dev` -- `dotnet watch run` -> `dotnet run`, drop `DOTNET_USE_POLLING_FILE_WATCHER` env var.
> - `src/HealthcareSupport.CaseEvaluation.AuthServer/Dockerfile.dev` -- same.
> - `angular/Dockerfile.dev` -- drop `CHOKIDAR_USEPOLLING=1`, drop `concurrently` from yarn-add (no longer needed without a parallel watch+serve pair).
> - `angular/dev-entrypoint.sh` -- replace `concurrently` running `ng build --watch` + browser-sync with a single-shot `ng build` followed by `exec browser-sync`. The `ensure_dynamic_env` background loop is kept as a defensive measure.
> - `docker-compose.yml` -- drop `DOTNET_USE_POLLING_FILE_WATCHER` env vars on both .NET services; rewrite all watcher-related comments to reflect the restart-on-edit workflow.
>
> **New dev workflow:** edit source on host -> `docker compose restart <service>` (api, authserver, or angular) -> container re-runs `dotnet restore + run` (or `ng build`) -> serve. Approximate per-restart cost: ~30s for .NET, ~90s for Angular (consistent with the measurements in the original observation below).
>
> The bind mounts on `./src` and `./angular/src` are retained -- they are how the restart picks up host edits without an image rebuild.
>
> Original observation preserved below for context on why this was done.

# Observation

The dev-stack runs three watchers inside containers, each consuming a Windows host directory via a Docker bind mount:

| Container | Watcher | Mounted source |
|---|---|---|
| `replicate-old-app-api-1` | `dotnet watch run` (with `DOTNET_USE_POLLING_FILE_WATCHER=1`) | `W:\patient-portal\replicate-old-app\src` -> `/app/src` |
| `replicate-old-app-authserver-1` | `dotnet watch run` (with `DOTNET_USE_POLLING_FILE_WATCHER=1`) | same |
| `replicate-old-app-angular-1` | `ng build --watch` + browser-sync | `W:\patient-portal\replicate-old-app\angular` -> `/app` |

On 2026-05-19, during the tenant-admin-internal-users build, three `.cs` files and a `.ts` / `.html` pair were edited on the host. The watchers detected **none** of them:

- API and AuthServer logs showed no "Build started" / "Hot reload" / "File changed" entries in the minutes after the edits.
- The Angular dist `main-*.js` mtime stayed pinned to 19:00 even when the source mtime moved to 23:05.
- Touching the source file inside the container (`docker exec ... touch /app/src/...`) didn't help — the watcher remained silent.
- A `docker compose restart <service>` forced a full rebuild and the latest source was then served. After restart, the watcher resumed catching subsequent edits **for a while**, then drifted silent again.

The watchers themselves are healthy — when they fire, the rebuild lands. The problem is the filesystem-event bridge from the Windows host into the Linux container.

## Why this happens (best current understanding)

Docker Desktop on Windows mediates bind-mount file events via `9p` / SMB plus inotify shimming. inotify events are not reliably synthesized from Windows file-change notifications for every editor. Editors that use atomic-rename-on-save (and most modern ones do) drop new inode numbers each save, and the shim's polling fallback can miss them. The `DOTNET_USE_POLLING_FILE_WATCHER=1` env var in the .NET Dockerfiles is supposed to side-step this by polling inside the container, but the polling interval and the timing of the host's mount-cache flush combine to make detection unreliable.

This is a known class of issue (see e.g. Docker Desktop GH issues #8694, #15171, and the .NET watch troubleshooting note). It is not specific to this repo.

## Impact

- Smoke-testing after a code edit can return *stale* results unless the developer remembers to restart the affected container.
- Subagent / CI flows that assume the watcher picks up source changes will quietly run against the previous build.
- This wasted ~15 minutes of investigation time during the 2026-05-19 build (initial assumption was the source edit hadn't landed at all).

## Workarounds

1. **Always `docker compose restart <service>` after touching a watched source file** before testing. Costs ~30s per .NET restart, ~90s for Angular.
2. **Verify dist mtime before testing**:
   ```bash
   docker exec replicate-old-app-angular-1 sh -c "ls -la /app/dist/CaseEvaluation/browser/main-*.js"
   ```
   If the mtime is older than the source edit, restart the container.
3. **For .NET**: tailing `docker logs <api|authserver> -f` and watching for "Hot reload" / "File changed" entries gives confirmation when the watcher does pick up; absence of those entries after the expected delay means the edit was lost.

## Recommended next steps (not blocking)

- Try **`docker compose restart`** as a hook tied to file-save in the IDE (VS Code task / pre-task) so the developer doesn't have to remember.
- Try the **named-volume cache** pattern (`type: volume` instead of `type: bind`) for `obj/` and `bin/` so only `.cs` / `.ts` edits cross the bind boundary -- may improve detection.
- Pin `DOTNET_USE_POLLING_FILE_WATCHER_INTERVAL` (custom env, not standard) by using `dotnet watch --poll-interval` if the .NET 10 SDK supports it -- check release notes.
- Switch the Angular container to **direct local-host `serve`** off `host.docker.internal` when developing the SPA in isolation, bypassing the bind-mount entirely.

None of these are urgent; the manual restart workaround keeps work moving. Tracking this so the next dev who hits a "my edit didn't take effect" mystery finds the cause quickly.
