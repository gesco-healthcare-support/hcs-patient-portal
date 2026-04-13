# Getting Started

[Home](../INDEX.md) > [Onboarding](./) > Getting Started

---

This guide takes you from a fresh clone to a running application. The Patient Portal is a workers' compensation IME scheduling system built with .NET 10, Angular 20, and ABP Commercial. It runs three services locally: an OAuth authentication server, a REST API, and an Angular single-page application.

For detailed configuration (connection strings, HTTPS certificates, Redis, ABP Studio profiles), see [Development Setup](../devops/DEVELOPMENT-SETUP.md).

## ABP Commercial License (Required Before Anything Else)

This project uses **ABP Commercial**, which validates its license at runtime. Before running the application — whether via Docker or locally — you must authenticate the ABP CLI on your machine:

```bash
# Install ABP CLI (if not already installed)
dotnet tool install -g Volo.Abp.Studio.Cli

# Log in with your ABP Commercial account
abp login <your-username>
```

This creates `~/.abp/cli/access-token.bin` on your machine. Docker mounts this file into containers automatically. Without it, all .NET services will fail at startup with `ABP-LIC-ERROR - License check failed`.

You also need the `AbpLicenseCode` value — find it in any `src/*/appsettings.secrets.json` file. If you don't have these files yet, ask a team member for the ABP license credentials.

---

## Quick Start with Docker (Recommended)

The fastest path from clone to running app. Only requires **Docker Desktop** and **Git** — no .NET SDK, Node.js, or SQL Server installation needed.

```bash
git clone https://github.com/gesco-healthcare-support/hcs-patient-portal.git
cd hcs-patient-portal

# 1. Create environment file
cp .env.example .env
# Edit .env with your ABP NuGet key, SA password, encryption passphrase, and ABP license code

# 2. Copy ABP secrets
cp docker/appsettings.secrets.json.example docker/appsettings.secrets.json
# Edit docker/appsettings.secrets.json — paste your AbpLicenseCode

# 3. Start everything
docker compose up --build
```

Wait ~3-5 minutes for first build. When you see all health checks pass, open http://localhost:4200.

| Service | URL | Container |
|---------|-----|-----------|
| Angular | http://localhost:4200 | patient-portal-ui |
| API + Swagger | http://localhost:44327/swagger | patient-portal-api |
| AuthServer | http://localhost:44368 | patient-portal-auth |
| SQL Server | localhost:1434 | patient-portal-db |
| Redis | localhost:6379 | patient-portal-redis |

```bash
# Stop all services
docker compose down

# Stop and wipe database (fresh start)
docker compose down -v
```

> **Windows path length note:** .NET native DLLs can fail to load if the project path exceeds ~200 characters. Clone to a short path (e.g., `C:\Dev\hcs-portal` or use `subst` to create a drive alias). On macOS/Linux this is not an issue. See [Troubleshooting](#deep-dive-windows-path-length) for details.

---

## Local Setup (Without Docker)

Use this method when you need full debugging, hot-reload, or IDE integration. Requires installing all tools locally.

### Prerequisites

| Tool | Version | How to Check | How to Install |
|------|---------|-------------|----------------|
| .NET SDK | 10.0 | `dotnet --version` | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Node.js | LTS (22+) | `node --version` | [nodejs.org](https://nodejs.org/) |
| SQL Server | Any (LocalDB, Docker, or full) | See database setup below | See database setup below |
| Angular CLI | Latest | `ng version` | `npm install -g @angular/cli` |
| ABP CLI | Latest | `abp --version` | `dotnet tool install -g Volo.Abp.Studio.Cli` |

Optional: Redis (disabled by default).

### Step 1: Clone and Position

```bash
git clone <repository-url>
cd hcs-case-evaluation-portal
```

> **Windows path length note:** .NET native DLLs can fail to load if the project path exceeds ~200 characters. Clone to a short path (e.g., `C:\Dev\hcs-portal` or use `subst` to create a drive alias). On macOS/Linux this is not an issue. See [Troubleshooting](#deep-dive-windows-path-length) for details.

## Step 2: Install Dependencies

```bash
# Backend (.NET packages)
dotnet restore

# Frontend (Angular packages)
cd angular
npm install
cd ..
```

The `npm install` step downloads ~1GB of Angular + ABP packages. `ERESOLVE` warnings are typically safe to ignore for ABP projects.

## Step 3: Database Setup

The application needs a SQL Server instance. Choose one option:

### Option A: Docker (recommended, cross-platform)
```bash
# If you have Docker installed:
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name sql-server -d mcr.microsoft.com/mssql/server:2022-latest
```
Then update `ConnectionStrings:Default` in `src/*/appsettings.json` to use `Server=localhost;...`.

### Option B: SQL Server LocalDB (Windows only)
```bash
sqllocaldb start MSSQLLocalDB
```
The default connection strings already point to LocalDB — no config changes needed.

### Option C: Full SQL Server
Point the connection strings in `src/*/appsettings.json` to your SQL Server instance.

### Run Migrations
```bash
dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator
```

This creates the database, applies all migrations, and seeds initial data (admin user, OAuth clients, permissions).

**Default admin credentials:** `admin@abp.io` / see `TEST_PASSWORD` in `.env.local`

## Step 4: Trust HTTPS Certificates

```bash
dotnet dev-certs https --trust
```

## Step 5: Start the Application

The three services must start in this exact order:

```mermaid
flowchart LR
    A[1. AuthServer\nPort 44368] --> B[2. API Host\nPort 44327]
    B --> C[3. Angular\nPort 4200]
    A -.->|validates tokens| B
    B -.->|REST calls| C
```

**Terminal 1 -- AuthServer** (start first):
```bash
dotnet run --project src/HealthcareSupport.CaseEvaluation.AuthServer
```
Wait for `Now listening on: https://localhost:44368`.

**Terminal 2 -- API Host** (start after AuthServer is ready):
```bash
dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
```
Wait for `Now listening on: https://localhost:44327`.

**Terminal 3 -- Angular** (start last):
```bash
cd angular
npx ng build --configuration development
npx serve -s dist/CaseEvaluation/browser -p 4200
```

> **Critical:** Never use `ng serve` or `yarn start`. Angular 20's Vite pre-bundler creates duplicate `InjectionToken` instances, causing `NullInjectorError: CORE_OPTIONS`. The `ng build` + `npx serve` approach avoids this. See [Deep Dive](#deep-dive-why-ng-serve-breaks) below.

## Step 6: Verify Everything Works

```bash
# AuthServer -- should return 200
curl -sk -o /dev/null -w "%{http_code}" https://localhost:44368/.well-known/openid-configuration

# API Host -- should return 200
curl -sk -o /dev/null -w "%{http_code}" https://localhost:44327/swagger/index.html

# Angular -- should return 200
curl -s -o /dev/null -w "%{http_code}" http://localhost:4200/
```

Open **http://localhost:4200**, log in with `admin@abp.io` and the `TEST_PASSWORD` from your `.env.local`. You should see the LeptonX dashboard with sidebar menu (Appointments, Doctors, Patients, Locations).

| Service | URL | Expected |
|---------|-----|----------|
| AuthServer | https://localhost:44368 | OpenIddict login page |
| API Host | https://localhost:44327/swagger | Swagger API explorer |
| Angular | http://localhost:4200 | LeptonX themed SPA |

## Running Services Independently

Use these commands when you need to start individual services for debugging, testing, or focused development.

### Docker Compose — Single Service

```bash
# Start only infrastructure (SQL + Redis)
docker compose up -d sql-server redis

# Run migrations only
docker compose up db-migrator

# Start AuthServer only (after migrations)
docker compose up authserver

# Start API only
docker compose up api

# Start Angular only
docker compose up angular

# Start everything except Angular (backend-only development)
docker compose up sql-server redis db-migrator authserver api
```

### Docker Compose — Log Levels

```bash
# Default: shows interleaved logs from all services
docker compose up

# Detached (background) — no console output
docker compose up -d

# Follow logs for a specific service
docker compose logs -f authserver
docker compose logs -f api
docker compose logs -f angular

# Follow logs for multiple services
docker compose logs -f authserver api

# Show last 50 lines only
docker compose logs --tail 50 api

# Show timestamps
docker compose logs -f -t api
```

### Docker Compose — Rebuild and Debug

```bash
# Rebuild a single service (after Dockerfile or code changes)
docker compose up --build api

# Rebuild without cache (forces fresh NuGet/npm restore)
docker compose build --no-cache api
docker compose up api

# Run a shell inside a running container
docker exec -it patient-portal-api /bin/bash
docker exec -it patient-portal-auth /bin/bash
docker exec -it patient-portal-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C

# Check container resource usage
docker stats --no-stream

# Inspect environment variables inside a container
docker exec patient-portal-api env | sort
```

### Local Development (No Docker)

For local development with full .NET hot-reload and debugging:

**AuthServer** (Terminal 1 — start first):
```bash
# Standard
dotnet run --project src/HealthcareSupport.CaseEvaluation.AuthServer

# Verbose logging (shows SQL queries, ABP internals)
dotnet run --project src/HealthcareSupport.CaseEvaluation.AuthServer --verbosity detailed

# Watch mode (auto-restart on code changes)
dotnet watch run --project src/HealthcareSupport.CaseEvaluation.AuthServer
```

**API Host** (Terminal 2 — start after AuthServer):
```bash
# Standard
dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host

# Verbose logging
dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host --verbosity detailed

# Watch mode
dotnet watch run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
```

**Angular** (Terminal 3 — start last):
```bash
cd angular

# Standard build + serve (use this, NOT ng serve)
npx ng build --configuration development && npx serve -s dist/CaseEvaluation/browser -p 4200

# Production build (optimized, no source maps)
npx ng build --configuration production && npx serve -s dist/CaseEvaluation/browser -p 4200
```

> **Critical:** Never use `ng serve` or `yarn start`. See [Deep Dive](#deep-dive-why-ng-serve-breaks).

**DbMigrator** (one-time, run before services):
```bash
# Standard
dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator

# Skip Redis connection (useful when Redis isn't running)
dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator -- --disable-redis
```

### Logging Configuration

Logging is configured via Serilog in each service's `Program.cs`. Override at runtime using environment variables:

```bash
# .NET services — set minimum log level
Serilog__MinimumLevel__Default=Debug dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host

# Docker — override via environment
docker compose exec api sh -c 'export Serilog__MinimumLevel__Default=Debug && dotnet HealthcareSupport.CaseEvaluation.HttpApi.Host.dll'
```

| Level | What it shows | Use when |
|-------|---------------|----------|
| `Information` (default) | Startup, requests, migrations | Normal development |
| `Debug` | + ABP module loading, DI resolution | Investigating startup issues |
| `Warning` | Only warnings and errors | Watching for problems in stable environment |
| `Verbose` | Everything including framework internals | Last resort deep debugging |

Override specific namespaces for targeted debugging:
```bash
# See all SQL queries
Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore=Debug dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host

# See ABP internals
Serilog__MinimumLevel__Override__Volo.Abp=Debug dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
```

### Health Check Endpoints

Both AuthServer and API Host expose health check endpoints:

| Endpoint | Purpose |
|----------|---------|
| `/health-status` | JSON health report (database, Redis connectivity) |
| `/health-ui` | Visual health dashboard (browser) |
| `/health-api` | Machine-readable health API |

```bash
# Quick check from terminal
curl http://localhost:44327/health-status
curl http://localhost:44368/health-status
```

---

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| `NullInjectorError: CORE_OPTIONS` | Used `ng serve` instead of `ng build` + `npx serve` | Kill any running Angular processes, rebuild with `npx ng build --configuration development`, serve with `npx serve` |
| CORE_OPTIONS persists after rebuild | Ghost dev server still on port 4200 | Check `lsof -i :4200` (macOS/Linux) or `netstat -ano | findstr :4200` (Windows), kill the process, then restart |
| `OIDC configuration error` from API Host | AuthServer not running or not ready | Start AuthServer first, wait for "listening" message |
| `HTTP 500` on API requests (Windows) | Project path exceeds 260 chars | Move project to a shorter path. See [path length note](#deep-dive-windows-path-length) |
| SQL connection error on startup | Database server not running | Start your SQL Server (Docker: `docker start sql-server`, LocalDB: `sqllocaldb start MSSQLLocalDB`) |
| SSL certificate errors in browser | Dev cert not trusted | Run `dotnet dev-certs https --trust` |
| Port already in use | Previous instance still running | Find and kill: `lsof -i :44327` (macOS/Linux) or `netstat -ano | findstr :44327` (Windows) |
| Angular build fails with ABP library errors | ABP client-side libs not installed | Run `abp install-libs` from the solution root |
| `Host version X does not match binary Y` (esbuild) | Stale esbuild binary | Delete `node_modules/@esbuild/*/esbuild*`, re-run `npm install` |
| Migration error: "database already exists" | Partial previous run | Drop the `CaseEvaluation` database and re-run DbMigrator |

---

## Deep Dives

### Deep Dive: Windows Path Length

On Windows, .NET native DLLs (like `Microsoft.Data.SqlClient.SNI.dll`) are loaded by `LoadLibrary`, which enforces a 260-character path limit regardless of the `LongPathsEnabled` registry setting. If your project is deeply nested (e.g., `C:\Users\YourName\Documents\Projects\Long Folder Name\hcs-case-evaluation-portal`), the build output paths can exceed this limit, causing HTTP 500 on every API request.

**Fix:** Clone to a short path like `C:\Dev\portal` or use Windows drive substitution (`subst X: "C:\Your\Long\Path"`). This is a Windows-only issue — macOS and Linux are not affected.

### Deep Dive: Why `ng serve` Breaks

Angular 20's dev server uses Vite's `optimizeDeps` pre-bundler, which splits `@abp/ng.core` across two JS chunks. Each chunk creates its own `new InjectionToken("CORE_OPTIONS")`. Angular DI uses `===` identity, so the provider (token A from chunk 1) never matches the injection (token B from chunk 2). The `ng build` output (esbuild, no Vite) produces exactly one token instance, so the build works correctly. This is a Vite behavior, not a code bug.

---

## Advanced Configuration

### Configuration Files

| File | Purpose | Key Settings |
|------|---------|-------------|
| `src/.../HttpApi.Host/appsettings.json` | API configuration | `App:SelfUrl`, `AuthServer:Authority`, CORS origins |
| `src/.../AuthServer/appsettings.json` | Auth server config | `App:SelfUrl`, redirect URLs, signing cert |
| `src/.../DbMigrator/appsettings.json` | Database + seeding | Connection string, OAuth client registration |
| `angular/src/environments/environment.ts` | Frontend config | API URL, OAuth client ID |

### Redis (Optional)

Redis is disabled by default. Only needed for multi-instance deployment. Set `Redis:IsEnabled` to `true` in appsettings.json and run Redis on port 6379.

---

**Next steps:**
- [Common Tasks](COMMON-TASKS.md) -- add entities, run migrations, create tests
- [Architecture Overview](../architecture/OVERVIEW.md) -- understand the system structure
- [Docker & Deployment](../runbooks/DOCKER-DEV.md) -- containerization
- [Testing Strategy](../devops/TESTING-STRATEGY.md) -- running and writing tests
