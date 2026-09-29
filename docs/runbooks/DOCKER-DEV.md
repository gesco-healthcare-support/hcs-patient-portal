[Home](../index.md) > Runbooks > Docker Development

# Docker Development Runbook

> Purpose: run the full Patient Portal stack on a developer machine with Docker Compose, and get it
> running again when it does not come up.
> Audience: developers.
> Owner: the portal maintainer.
> **Last tested: 2026-06-01, at commit `4ed9c4b` (the end-to-end run recorded under "E2E
> Validation Status").** The compose file has changed since: the `dev` build targets arrived on
> 2026-06-12 and the images were pinned on 2026-09-04. This page was re-checked against
> `docker-compose.yml` on 2026-09-28, not re-run.

Docker Compose is the alternative to local .NET + Angular development. It packages the full stack (SQL Server, Redis, MinIO, MinIO bucket initializer, DbMigrator, AuthServer, HttpApi.Host, packet-renderer, Angular) into nine containers on a shared network.

**Source of truth:** [`docker-compose.yml`](https://github.com/gesco-healthcare-support/hcs-patient-portal/blob/main/docker-compose.yml) at the repo root.

## When you need this page

| Situation | Go to |
| --- | --- |
| Setting up the Docker stack for the first time | "First-Time Setup", then "Running" |
| A code change does not show up in the running stack | "Common Operations" |
| The stack does not come up, or a page fails once it is up | "Troubleshooting" |
| Running a second stack alongside another worktree's | "Running multiple worktrees concurrently" |
| Giving up on a broken stack for now | "Abort" |

---

## Services

| Service | Image / Build | Exposed Port | Purpose |
|---|---|---|---|
| `sql-server` | `mcr.microsoft.com/mssql/server` (pinned tag) | `1434 -> 1433` | Database |
| `redis` | `redis` (pinned Alpine tag) | `6379` | Cache + data protection |
| `minio` | `quay.io/minio/minio` (pinned release) | `9000 -> 9000`, `9001 -> 9001` | Blob store for document uploads (API + console) |
| `minio-init` | `quay.io/minio/mc` (pinned release) | -- | One-shot bucket initializer, exits |
| `db-migrator` | Built from `src/.../DbMigrator/Dockerfile`, `dev` target | -- | Runs migrations and seeding, exits |
| `authserver` | Built from `src/.../AuthServer/Dockerfile`, `dev` target | `44368 -> 8080` | OpenIddict OIDC |
| `api` | Built from `src/.../HttpApi.Host/Dockerfile`, `dev` target | `44327 -> 8080` | Main API |
| `packet-renderer` | Built from `docker/packet-renderer/Dockerfile` | `3001 -> 3001` | WeasyPrint HTML to fillable-PDF packet renderer |
| `angular` | Built from `angular/Dockerfile`, `dev` target | `4200 -> 80` | SPA, built at container start and served by nginx |

Every image is pinned to the version production runs. The exact tags live in `docker-compose.yml`
and are not repeated here, so this table cannot drift from them. Every host port binds to
`127.0.0.1` only.

Startup order: SQL Server, Redis and MinIO healthy -> DbMigrator completes -> AuthServer and API
start (the API also waits for packet-renderer) -> Angular starts once the API is healthy.

---

## Prerequisites

- Docker Desktop (Windows / macOS) or Docker Engine (Linux)
- Valid ABP commercial license (`ABP_LICENSE_CODE`) and NuGet key (`ABP_NUGET_API_KEY`)
- On Windows: WSL 2 backend recommended (`docker context use default`)

---

## First-Time Setup

1. **Create `.env`** at repo root from `.env.example`:

   ```bash
   cp .env.example .env
   ```

   Edit `.env` and fill in:
   - `MSSQL_SA_PASSWORD` -- SQL SA password (strong; e.g. `ChangeMe_Local_1234`)
   - `STRING_ENCRYPTION_PASSPHRASE` -- random string for ABP string encryption
   - `ABP_LICENSE_CODE` -- your commercial license
   - `ABP_NUGET_API_KEY` -- your ABP NuGet feed key

2. **Create `docker/appsettings.secrets.json`** from the example:

   ```bash
   cp docker/appsettings.secrets.json.example docker/appsettings.secrets.json
   ```

   Edit the file and set `AbpLicenseCode`.

3. **Mount your ABP CLI cache** (optional, speeds up builds):
   The compose file mounts `${HOME}/.abp/cli` read-only. On Windows, ensure `HOME` is set (default in Git Bash). If the directory does not exist, Docker will still build but the container will re-download ABP CLI tools.

---

## Running

Start everything:

```bash
docker compose up -d
```

Watch logs in another terminal:

```bash
docker compose logs -f authserver api angular
```

Expected: `docker compose ps` shows `db-migrator` exited with code 0, `minio-init` exited, and every
other service `(healthy)`. The first start restores and builds inside the containers, so AuthServer
and the API can take a few minutes to report healthy.

---

## Service URLs

Once containers are healthy:

- Angular UI: `http://localhost:4200`
- API Swagger: `http://localhost:44327/swagger`
- AuthServer OIDC discovery: `http://localhost:44368/.well-known/openid-configuration`
- SQL Server: `localhost:1434` (user `sa`, password from `.env`)

---

## Common Operations

**Pick up a code change.** The app services run their `dev` build target: the source is
bind-mounted into the container, so a restart rebuilds from your working tree. No image rebuild is
needed:

```bash
docker compose restart api
```

The same applies to `authserver`, `db-migrator` and `angular`. There is no file watcher (Docker
Desktop drops file events on Windows bind mounts), so a restart is the step that picks the change up.

**Rebuild an image** (only after a Dockerfile or build-stage change):

```bash
docker compose build api
docker compose up -d api
```

**Rebuild from scratch** (ignore cache):

```bash
docker compose build --no-cache
docker compose up -d
```

**Reset database** (destroys data):

```bash
docker compose down -v
docker compose up -d
```

The `-v` flag removes the named volumes, including `sqldata` and `miniodata`; DbMigrator recreates schema and seed data on next start.

**Stop without destroying data:**

```bash
docker compose down
```

**Connect to SQL from host:**

```bash
# Main uses SQL_HOST_PORT=1434 by default; other worktrees override in their .env.
sqlcmd -S "localhost,${SQL_HOST_PORT:-1434}" -U sa -P "<SA_PASSWORD>" -C -Q "SELECT name FROM sys.databases"
```

---

## Running multiple worktrees concurrently

The compose file is parameterised so you can run one stack per worktree simultaneously, e.g. `main` + `development` + a feature branch, each on its own host ports with its own isolated SQL container and volume. This is the recommended workflow for parallel Claude Code sessions.

Per-worktree isolation comes from two mechanisms:

1. **Compose project name defaults to the worktree directory basename.** Containers become `main-sql-server-1`, `development-sql-server-1`, etc. Networks become `<project>_default`. Named volumes become `<project>_sqldata`. No explicit config needed.
2. **Host ports and URL env vars come from each worktree's own `.env` file.** The compose file reads these as `${AUTH_PORT:-44368}`, etc., falling back to main's defaults when unset.

### Per-worktree `.env` overrides

Main uses the defaults (nothing to set). For `development`, `staging`, or a feature worktree, append the override block to the worktree's `.env` (the file is gitignored):

```text
AUTH_PORT=44378           # 44368 + (offset * 10), default 44368 = main
API_PORT=44337            # 44327 + (offset * 10), default 44327
NG_PORT=4210              # 4200 + (offset * 10), default 4200
SQL_HOST_PORT=1444        # 1434 + offset, default 1434
REDIS_HOST_PORT=6389      # 6379 + offset, default 6379
NG_CONFIG=local           # 'local' reads environment.local.ts (generated per worktree by scripts/worktrees/render-config.sh); 'docker' (default) reads environment.docker.ts
```

`scripts/worktrees/add-worktree.sh` writes this block automatically for feature worktrees. For the persistent worktrees (`main`, `development`, `staging`), the values are:

| Worktree | AUTH_PORT | API_PORT | NG_PORT | SQL_HOST_PORT | REDIS_HOST_PORT | NG_CONFIG |
|---|---:|---:|---:|---:|---:|---|
| `main` | 44368 | 44327 | 4200 | 1434 | 6379 | docker (default) |
| `development` | 44378 | 44337 | 4210 | 1444 | 6389 | local |
| `staging` | 44388 | 44347 | 4220 | 1454 | 6399 | local |

See `.env.example` for the authoritative documented block.

### Inspecting running stacks

```bash
# All containers grouped by compose project
docker ps --format "table {{.Names}}\t{{.Status}}" | awk -F- '{print $1}' | sort -u

# Only one worktree's stack: run from that worktree's root directory
docker compose ps

# Stop only that worktree's stack (others keep running)
docker compose down
```

### Resource caveats

Each worktree's stack runs its own SQL Server (roughly 2 GB RAM), AuthServer, API, Angular, and Redis. Running three concurrently needs at least 8 GB allocated to Docker Desktop / WSL2. If WSL is capped tighter in `~/.wslconfig`, bump it before bringing up a third stack.

---

## Troubleshooting

Each entry is symptom, diagnosis, fix, then how to confirm the fix worked.

### `docker compose up` stuck at "waiting for db-migrator"

- **Diagnosis.** DbMigrator may have failed without a clear message at the `up` prompt. Read its log:

  ```bash
  docker compose logs db-migrator
  ```

- **Fix.** The common cause is an invalid `AbpLicenseCode` in `docker/appsettings.secrets.json`.
  Correct it, then `docker compose up -d`.
- **Verify.** `docker compose ps` shows `db-migrator` exited with code 0.

### Angular returns 502 / nginx gateway error

- **Diagnosis.** The Angular container is up but the API is unreachable. Check the API's health:

  ```bash
  docker compose ps
  ```

- **Fix.** If `api` is not healthy, read `docker compose logs api`. The usual causes are a missing
  license or a database connection problem.
- **Verify.** `api` reports `(healthy)` and `http://localhost:4200` loads.

### AuthServer login redirects return `invalid_client`

- **Diagnosis.** The OpenIddict client registrations were seeded for a different Angular URL than
  the one you are using. The DbMigrator seeds the client root URL from `NG_PORT`:

  ```text
  OpenIddict__Applications__CaseEvaluation_App__RootUrl: "http://localhost:${NG_PORT:-4200}"
  ```

  So changing `NG_PORT` after the database was seeded leaves the stored registration pointing at
  the old port.

- **Fix.** Re-run the migrator so the seeder, which creates or updates each client
  (`CreateOrUpdateApplicationAsync` in `OpenIddictDataSeedContributor.cs`), rewrites the
  registration:

  ```bash
  docker compose run --rm db-migrator
  ```

  If that does not clear it, reset the database (`docker compose down -v`, which destroys the
  local data) so the migrator seeds from scratch.
- **Verify.** Logging in at `http://localhost:${NG_PORT}` returns to the SPA without the error.

### Build fails with "ABP NuGet unauthorized", or `write-nuget-config: secret not readable`

- **Diagnosis.** `ABP_NUGET_API_KEY` in `.env` is missing or invalid.
- **Fix.** Update `.env` and rebuild with `--no-cache`. The key reaches the image as a BuildKit
  secret rather than a build ARG, so building these Dockerfiles by hand needs
  `--secret id=abp_nuget_key,env=ABP_NUGET_API_KEY`; through `docker compose build` it is already
  wired.
- **Verify.** `docker compose build` finishes without the error.

### Port conflict on 1434 / 44327 / 44368 / 4200 / 6379

- **Diagnosis.** Another worktree's stack is bound to the same ports, or a local dev run is using
  them. `docker ps` shows which compose project holds them.
- **Fix.** Either stop the conflicting process, or add the per-worktree override block to this
  worktree's `.env` so it binds a different offset. See "Running multiple worktrees concurrently"
  above.
- **Verify.** `docker compose up -d` starts without a bind error.

---

## Abort

If the stack will not come up and you need the machine back, stop it without deleting its data:

```bash
docker compose down
```

This keeps the named volumes, so the database and uploaded files are still there on the next `up`.
Do not add `-v` unless you mean to discard the local database. Then escalate with what you have.

## Escalation

If the stack is not fully healthy within 30 minutes of working through "Troubleshooting", stop and
hand to the portal maintainer. Include the output of `docker compose ps` and
`docker compose logs --tail=200 <service>` for each service that is not healthy.

---

## E2E Validation Status

**Last tested:** 2026-06-01 on Windows 11 Enterprise (Docker 29.4.0, Compose v5.1.1)
**Git commit:** `4ed9c4b` (main)
**Overall result:** PASS -- all 9 services start, all 8 health checks pass, auth flow and all CRUD pages work

### Timing Benchmarks

Measured on 2026-06-01, before the `dev` build targets (2026-06-12) changed how the app services
build and pick up code. Not re-measured since.

| Metric | Duration | Notes |
|--------|----------|-------|
| Clone + configure | ~6 min | Includes secret collection |
| First build (cold) | ~5 min | Pulls base images + NuGet/npm restore + compile |
| Restart (no rebuild) | ~2 min | `docker compose down` then `up -d` |
| Full rebuild after `down -v` | ~1 min | Docker layer cache warm; DB reseeded (67 tables) |
| Total clone-to-running | ~11 min | |
| Disk usage | ~28 GB images, ~24 GB build cache | First build only |

### Known Issues (Docker-Specific)

**Swagger OAuth does not work from browser** (degraded, not a blocker).
The API's `AuthServer__MetaAddress` is `http://authserver:8080` -- a Docker-internal hostname for backend-to-backend OIDC validation. Swagger UI runs in the user's browser and cannot resolve `authserver`. Clicking "Authorize" in Swagger fails with `ERR_NAME_NOT_RESOLVED`. Workaround: use curl with a manually obtained bearer token, or test via the Angular UI. Fix requires a separate browser-accessible authority URL for Swagger's OpenAPI security definition.

**Menu labels show localization key prefixes** (cosmetic; recorded 2026-06-01).
Sidebar items displayed as "Menu:Home", "Menu:Dashboard", etc. instead of resolved display names. The `Menu:*` keys have since been defined in `Localization/CaseEvaluation/en.json`; this has not been re-checked in a running stack.

**Page title showed "MyProjectName"** (resolved).
The ABP template default no longer appears anywhere in the source.

**Git Bash rewrites `/opt/` paths in `docker exec`** (Windows only).
Running `docker compose exec sql-server /opt/mssql-tools18/bin/sqlcmd ...` from Git Bash (MSYS2) converts `/opt/` to a Windows path before Docker receives it. Fix: prefix the command with `MSYS_NO_PATHCONV=1`, or wrap it in `bash -c '...'` inside the container.

### Angular Route Quick Reference

Routes use ABP module prefixes. Use the sidebar for navigation; these are the actual URLs:

| Feature | Route |
|---------|-------|
| Dashboard | `/dashboard` |
| Appointments | `/appointments` |
| Appointment Types | `/appointment-management/appointment-types` |
| Appointment Statuses | `/appointment-management/appointment-statuses` |
| Appointment Languages | `/appointment-management/appointment-languages` |
| Doctors | `/doctor-management/doctors` |
| Patients | `/user-management/patients` |
| Locations | `/doctor-management/locations` |
| Doctor Availabilities | `/doctor-management/doctor-availabilities` |
| WCAB Offices | `/doctor-management/wcab-offices` |
| Applicant Attorneys | `/applicant-attorneys` |
| States | `/configurations/states` |

Full route tree with guards and components: [Routing & Navigation](../frontend/ROUTING-AND-NAVIGATION.md)

---

## What This Setup Does NOT Do

- **Production deployment.** `docker-compose.yml` is for local dev. The production stack is a separate file, `docker-compose.prod.yml`; see [Hosting backup and restore](hosting-backup-restore.md) and [CHECKPOINT 1 -- local prod-compose verification](hosting-local-verification.md).
- **TLS between services.** Intra-container traffic is plaintext. See [Threat Model](../security/THREAT-MODEL.md#component-4-sql-server-database).
- **Persistent TLS cert for AuthServer.** AuthServer runs in HTTP mode (`AuthServer__RequireHttpsMetadata: false`) for dev convenience.
- **External storage.** SQL data lives in the `sqldata` named volume and uploaded documents in `miniodata`. Back them up externally if you need persistence beyond `docker compose down -v`.

---

## Related Documents

- [Local Dev Troubleshooting](LOCAL-DEV.md) -- non-Docker dev path
- [docker-compose.yml](https://github.com/gesco-healthcare-support/hcs-patient-portal/blob/main/docker-compose.yml) -- service definitions
- [Secrets Management](../security/SECRETS-MANAGEMENT.md) -- how secrets get injected
- [devops/TESTING-STRATEGY.md](../devops/TESTING-STRATEGY.md) -- broader DevOps context
