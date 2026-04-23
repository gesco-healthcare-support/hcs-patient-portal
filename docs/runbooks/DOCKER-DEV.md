[Home](../INDEX.md) > Runbooks > Docker Development

# Docker Development Runbook

Docker Compose is the alternative to local .NET + Angular development. It packages the full stack (SQL Server, Redis, DbMigrator, AuthServer, HttpApi.Host, Angular) into six containers on a shared network.

**Source of truth:** [`docker-compose.yml`](../../docker-compose.yml) at the repo root.

---

## Services

| Service | Image / Build | Exposed Port | Purpose |
|---|---|---|---|
| `sql-server` | `mcr.microsoft.com/mssql/server:2022-latest` | `1434 -> 1433` | Database |
| `redis` | `redis:7-alpine` | `6379` | Cache + data protection |
| `db-migrator` | Built from `src/.../DbMigrator/Dockerfile` | -- | Runs migrations, exits |
| `authserver` | Built from `src/.../AuthServer/Dockerfile` | `44368 -> 8080` | OpenIddict OIDC |
| `api` | Built from `src/.../HttpApi.Host/Dockerfile` | `44327 -> 8080` | Main API |
| `angular` | Built from `angular/Dockerfile` | `4200 -> 80` | Nginx-served SPA |

Dependencies enforce the same startup order as local dev: SQL + Redis ready -> DbMigrator completes -> AuthServer healthy -> API healthy -> Angular ready.

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

---

## Service URLs

Once containers are healthy:

- Angular UI: `http://localhost:4200`
- API Swagger: `http://localhost:44327/swagger`
- AuthServer OIDC discovery: `http://localhost:44368/.well-known/openid-configuration`
- SQL Server: `localhost:1434` (user `sa`, password from `.env`)

---

## Common Operations

**Rebuild after code change** (rebuild only what changed):
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
The `-v` flag removes the `sqldata` volume; DbMigrator recreates schema and seed data on next start.

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

```
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

# Only main's stack
cd /w/patient-portal/main && docker compose ps

# Stop only main's stack (others keep running)
cd /w/patient-portal/main && docker compose down
```

### Resource caveats

Each worktree's stack runs its own SQL Server (roughly 2 GB RAM), AuthServer, API, Angular, and Redis. Running three concurrently needs at least 8 GB allocated to Docker Desktop / WSL2. If WSL is capped tighter in `~/.wslconfig`, bump it before bringing up a third stack.

---

## Troubleshooting

**`docker compose up` stuck at "waiting for db-migrator":**
DbMigrator may have failed silently. Check:
```bash
docker compose logs db-migrator
```
Common cause: invalid `AbpLicenseCode` in `docker/appsettings.secrets.json`.

**Angular returns 502 / nginx gateway error:**
Angular container healthy but API unreachable. Verify `api` container is healthy:
```bash
docker compose ps
```
If unhealthy, check `docker compose logs api` -- usually missing license or DB connection issue.

**AuthServer login redirects return `invalid_client`:**
OpenIddict seed ran against a different `RootUrl` than Angular uses. The compose file hard-codes the Angular root URL in the DbMigrator environment:
```
OpenIddict__Applications__CaseEvaluation_App__RootUrl: "http://localhost:4200"
```
If you changed the Angular port, reset the DB (`docker compose down -v`) so the migrator re-seeds client registrations.

**Build fails with "ABP NuGet unauthorized":**
`ABP_NUGET_API_KEY` in `.env` is missing or invalid. Update `.env` and rebuild with `--no-cache`.

**Port conflict on 1434 / 44327 / 44368 / 4200 / 6379:**
Another worktree's stack is bound to the same ports, or a local dev run is using them. Options: (a) stop the conflicting process, or (b) add the per-worktree override block to this worktree's `.env` so it binds a different offset. See "Running multiple worktrees concurrently" above.

---

## E2E Validation Status

**Last tested:** 2026-04-16 on Windows 11 Enterprise (Docker 29.4.0, Compose v5.1.1)
**Git commit:** `4ed9c4b` (main)
**Overall result:** PASS -- all 6 services start, all 8 health checks pass, auth flow and all CRUD pages work

### Timing Benchmarks

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

**Menu labels show localization key prefixes** (cosmetic).
Sidebar items display as "Menu:Home", "Menu:Dashboard", etc. instead of resolved display names. The localization API (`/api/abp/application-localization`) returns 200 but the `Menu:*` keys are not resolving to friendly strings. Navigation works correctly despite the display issue. This also occurs in local dev -- not Docker-specific.

**Page title shows "MyProjectName"** (cosmetic).
The browser tab title intermittently shows "MyProjectName" (ABP template default) instead of "CaseEvaluation". A search-and-replace for "MyProjectName" across the Angular and AuthServer projects is needed.

**Git Bash rewrites `/opt/` paths in `docker exec`** (Windows only, cosmetic).
Running `docker exec patient-portal-db /opt/mssql-tools18/bin/sqlcmd ...` from Git Bash (MSYS2) converts `/opt/` to a Windows path before Docker receives it. Fix: wrap the command in `bash -c '...'` inside the container, or use PowerShell/WSL instead.

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
| Patients | `/doctor-management/patients` |
| Locations | `/doctor-management/locations` |
| Doctor Availabilities | `/doctor-management/doctor-availabilities` |
| WCAB Offices | `/doctor-management/wcab-offices` |
| Applicant Attorneys | `/applicant-attorneys` |
| States | `/configurations/states` |

Full route tree with guards and components: [Routing & Navigation](../frontend/ROUTING-AND-NAVIGATION.md)

---

## What This Setup Does NOT Do

- **Production deployment.** `docker-compose.yml` is for local dev. Production deploy targets (Kubernetes manifests, cloud host configs) are not yet in the repo.
- **TLS between services.** Intra-container traffic is plaintext. See [Threat Model](../security/THREAT-MODEL.md#component-4-sql-server-database).
- **Persistent TLS cert for AuthServer.** AuthServer runs in HTTP mode (`AuthServer__RequireHttpsMetadata: false`) for dev convenience.
- **External storage.** SQL data lives in the `sqldata` named volume. Back it up externally if you need persistence beyond `docker compose down -v`.

---

## Related Documents

- [Local Dev Troubleshooting](LOCAL-DEV.md) -- non-Docker dev path
- [docker-compose.yml](../../docker-compose.yml) -- service definitions
- [Secrets Management](../security/SECRETS-MANAGEMENT.md) -- how secrets get injected
- [devops/DEVELOPMENT-SETUP.md](../devops/DEVELOPMENT-SETUP.md) -- broader DevOps context
