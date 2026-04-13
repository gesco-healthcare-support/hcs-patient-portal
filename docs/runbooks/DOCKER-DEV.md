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
sqlcmd -S localhost,1434 -U sa -P "<SA_PASSWORD>" -C -Q "SELECT name FROM sys.databases"
```

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

**Port conflict on 1434 / 44327 / 44368 / 4200:**
Another process (or a local dev run) is using the port. Stop the conflicting process or change the host port in `docker-compose.yml` (e.g. `"45327:8080"` instead of `"44327:8080"`).

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
