# CI/CD & Dockerization Master Plan

> **Source:** Originally at `~/.claude/plans/snug-giggling-noodle.md` — saved here 2026-04-09 for easy reference.

## What Was Done (Complete History)

### Session 1: 2026-04-08 — GitHub Org + CI/CD Setup
- Created GitHub org `gesco-healthcare-support`, public repo `hcs-patient-portal`
- Extracted 53 secrets from codebase (NuGet.Config, appsettings, Helm, scripts, docker-compose)
- Fresh git init with clean history, 4 branches: main, development, staging, production
- 10 CI/CD workflows created (ci, auto-pr-dev, deploy-dev, promote-staging, security, dependency-review, labeler, pr-size, release, doc-check)
- All 9 CI checks green (Backend Build/Test, Frontend Build/Lint/Test, Dep Review, doc-check, label, size)
- Progressive branch protection on all 4 branches (main lightest -> production strictest)
- Auto-PR pipeline working (main->dev->staging via AUTO_PR_TOKEN PAT)
- Git hooks created (.husky/pre-commit, commit-msg, pre-push) -- files exist but husky npm install not done yet
- Community health files: LICENSE, SECURITY.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md
- Issue templates, PR template, CODEOWNERS
- GitHub Secrets configured: ABP_NUGET_API_KEY, ABP_LICENSE_CODE, AUTO_PR_TOKEN

### Session 1 continued: Vulnerability Fixes
- Diagnosed all 43 Dependabot alerts (all npm, zero .NET)
- Created comprehensive audit: docs/devops/VULNERABILITY-AUDIT.md
- Fixed 36/43 via yarn resolution overrides in angular/package.json and AuthServer/package.json
- 7 remaining: Angular XSS (20.0->20.3 needed, blocked by ABP 10.0.2 incompatibility)
- Dependabot alerts active, version-update PRs disabled (open-pull-requests-limit: 0)

### Session 1 continued: Dockerization (ATTEMPTED, REVERTED)
- Created docker/ directory with 4 Dockerfiles, docker-compose.yml, entrypoint, nginx.conf
- Created .dockerignore, .github/workflows/docker-build.yml
- PR #33 merged to main with all CI checks green
- **Problem:** db-migrator container exited with code 214 (no logs, no output)
  - Root cause investigation: entrypoint `echo` fails silently with very long ABP_LICENSE_CODE base64 string (~1500 chars)
  - Switched to `printf` -- still exit 214 via `docker compose up` (though `docker compose run` worked)
  - Inconsistency between `docker compose run` vs `docker compose up` was never resolved
- **Decision:** Reverted all Docker files from project. Will redo from scratch with proper research.

### Session 2: 2026-04-09 — Dependabot Cleanup + Docker Attempt
- Committed dependabot.yml changes (all open-pull-requests-limit set to 0)
- Documented temporary Dependabot disable in VULNERABILITY-AUDIT.md
- Merged PR #33 (Dockerization) to main
- Fixed gh CLI PATH issue (added to Windows User PATH + Git Bash .bashrc)
- Added PATH auto-fix rule to ~/.claude/CLAUDE.md
- Attempted Docker local testing -- hit exit 214 bug described above
- Reverted: deleted docker/, .dockerignore, docker-build.yml, DOCKER-AND-DEPLOYMENT.md, etc/docker-compose/
- Cleaned CONTRIBUTING.md (removed Docker section), reverted .gitattributes

## Current State (as of 2026-04-09 evening)

### What Works
- GitHub org + repo: gesco-healthcare-support/hcs-patient-portal (PUBLIC)
- 4-branch model: main, development, staging, production
- CI pipeline: 10 workflows, all green (minus Docker Build which was removed)
- Auto-PR cascade: main -> development -> staging
- Branch protection on all 4 branches
- 36/43 vulnerability fixes applied
- Dependabot security alerts active (version-update PRs paused)
- gh CLI authenticated and in PATH

### What's Deleted (Needs Redo)
- All Docker files (Dockerfiles, compose, entrypoint, nginx, .dockerignore)
- Docker Build CI workflow
- Docker deployment documentation
- Legacy etc/docker-compose/ directory

### What Was Never Started
- SonarCloud setup
- Codecov integration
- Git hooks activation (husky npm install)
- Auto-PR description passthrough
- Standardized auto-PR merge messages
- PR structure/format validation

### Blocked (External Dependencies)
- 7 Angular XSS vulnerabilities -- needs ABP to release version targeting Angular 20.3+
- Re-enable Dependabot version-update PRs -- after Angular vulns fixed

### Maintenance
- AUTO_PR_TOKEN rotation -- expires ~early July 2026

## GitHub Secrets

| Secret | Status | Notes |
|---|---|---|
| ABP_NUGET_API_KEY | Set | ABP Commercial NuGet feed |
| ABP_LICENSE_CODE | Set | ABP license for CI builds/tests |
| AUTO_PR_TOKEN | Set | PAT for auto-PR creation, expires ~July 2026 |

---

## Dockerization Redo Plan

### Why the First Attempt Failed

**Exit 214 root cause:** The entrypoint shell script (`entrypoint-dotnet.sh`) was unnecessary and fragile. It used `echo` to generate `appsettings.secrets.json` from the ABP_LICENSE_CODE env var (~1500 chars base64). This caused:
1. CRLF corruption on Windows (Git Bash `core.autocrlf=true` converts LF to CRLF)
2. Shell expansion risks with special characters in the base64 string
3. Inconsistent behavior between `docker compose up` (non-interactive, exit 214) and `docker compose run` (interactive, works)

**Architectural mistake:** We created new Dockerfiles in `docker/` instead of using the existing ABP-scaffolded Dockerfiles already in each project directory.

### Key Design Decisions for the Redo

**1. No entrypoint script.** ASP.NET Core natively reads environment variables as config keys. Instead of generating `appsettings.secrets.json` at runtime, pass `AbpLicenseCode` directly as an env var in docker-compose.yml. .NET's configuration system picks it up automatically.

**2. Use existing project Dockerfiles as the base.** The ABP scaffolding already created Dockerfiles in each project:
- `angular/Dockerfile`
- `src/.../AuthServer/Dockerfile`
- `src/.../HttpApi.Host/Dockerfile`
- `src/.../DbMigrator/Dockerfile`

We'll update these in-place rather than creating separate files in `docker/`.

**3. CRLF protection.** Add `.gitattributes` rules for shell scripts AND run `sed -i 's/\r$//'` in Dockerfiles as a safety net.

**4. Multi-stage builds with build-arg NuGet key.** Pass `ABP_NUGET_API_KEY` as a Docker build arg and substitute it into `NuGet.Config.template` during the build stage. This is already how CI works.

**5. Health checks use existing `/health-status` endpoints.** Both AuthServer and HttpApi.Host already have these configured and working.

**6. Angular uses `dynamic-env.json` for runtime config.** The existing `angular/nginx.conf` serves it at `/getEnvConfig`. Volume-mount it in docker-compose for environment-specific URLs.

### File Plan

#### New Files to Create

| File | Purpose |
|---|---|
| `docker-compose.yml` | Root-level orchestration (6 services: sql-server, redis, db-migrator, authserver, api, angular) |
| `docker-compose.override.yml` | Local dev overrides (ports, dev environment, volume mounts) |
| `.dockerignore` | Exclude .git, docs, node_modules, bin/obj from build context |
| `.env.example` | Template for required Docker env vars (at repo root, next to compose file) |
| `.github/workflows/docker-build.yml` | CI: build all 4 images on PR to validate Dockerfiles |

#### Existing Files to Update

| File | Change |
|---|---|
| `angular/Dockerfile` | Update to multi-stage build if needed, ensure dist path is correct |
| `src/.../AuthServer/Dockerfile` | Update to multi-stage with NuGet.Config.template substitution |
| `src/.../HttpApi.Host/Dockerfile` | Same pattern as AuthServer |
| `src/.../DbMigrator/Dockerfile` | Same pattern, ensure clean exit |
| `.gitattributes` | Add `*.sh text eol=lf` rule |
| `CONTRIBUTING.md` | Re-add Docker section once working |
| `docs/devops/DOCKER-AND-DEPLOYMENT.md` | Re-create after validation |

### docker-compose.yml Design

```yaml
services:
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest  # NOT azure-sql-edge (avoid ARM emulation)
    environment:
      SA_PASSWORD: "${SA_PASSWORD}"
      ACCEPT_EULA: "Y"
    ports: ["1434:1433"]
    volumes: [sqldata:/var/opt/mssql]
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "${SA_PASSWORD}" -Q "SELECT 1" -C -b
      interval: 10s
      retries: 10
      start_period: 15s

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]

  db-migrator:
    build:
      context: .
      dockerfile: src/HealthcareSupport.CaseEvaluation.DbMigrator/Dockerfile
      args:
        ABP_NUGET_API_KEY: "${ABP_NUGET_API_KEY}"
    environment:
      DOTNET_ENVIRONMENT: Development
      AbpLicenseCode: "${ABP_LICENSE_CODE}"       # Direct env var — no entrypoint script
      ConnectionStrings__Default: "Server=sql-server;Database=CaseEvaluation;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True"
      Redis__Configuration: redis
    depends_on:
      sql-server: { condition: service_healthy }
      redis: { condition: service_healthy }
    restart: "no"

  authserver:
    build:
      context: .
      dockerfile: src/HealthcareSupport.CaseEvaluation.AuthServer/Dockerfile
      args:
        ABP_NUGET_API_KEY: "${ABP_NUGET_API_KEY}"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: "http://+:8080"
      AbpLicenseCode: "${ABP_LICENSE_CODE}"
      App__SelfUrl: "http://localhost:44368"
      App__CorsOrigins: "http://localhost:4200,http://localhost:44327"
      AuthServer__Authority: "http://localhost:44368"
      AuthServer__RequireHttpsMetadata: "false"
      ConnectionStrings__Default: "Server=sql-server;..."
      Redis__Configuration: redis
      StringEncryption__DefaultPassPhrase: "${STRING_ENCRYPTION_PASSPHRASE}"
    ports: ["44368:8080"]
    depends_on:
      db-migrator: { condition: service_completed_successfully }
      redis: { condition: service_healthy }
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health-status || exit 1"]
      interval: 15s
      retries: 5
      start_period: 30s

  api:
    # Same pattern as authserver, port 44327:8080
    # AuthServer__Authority: "http://authserver:8080" (internal DNS)

  angular:
    build:
      context: ./angular
      dockerfile: Dockerfile
    ports: ["4200:80"]
    volumes:
      - ./angular/dynamic-env.json:/usr/share/nginx/html/dynamic-env.json:ro
    depends_on:
      api: { condition: service_healthy }

volumes:
  sqldata:

networks:
  default:
    name: patient-portal-network
```

### Implementation Steps

1. ~~**Update existing Dockerfiles**~~ DONE — multi-stage builds with NuGet template, VFS stubs, curl for health
2. ~~**Create docker-compose.yml**~~ DONE — 6 services, volume-mount secrets + ABP CLI token
3. ~~**Create .dockerignore**~~ DONE
4. ~~**Create .env.example**~~ DONE — at repo root
5. ~~**Update .gitattributes**~~ DONE — `*.sh text eol=lf`
6. ~~**Create docker/ config files**~~ DONE — `appsettings.secrets.json.example`, `dynamic-env.json`
7. ~~**Add MetadataAddress support**~~ DONE — 4-line code change in HttpApi.Host for Docker OAuth
8. ~~**Build and test locally**~~ DONE — all 6 services healthy
9. ~~**Verify endpoints**~~ DONE — AuthServer 200, API 200, Angular 200, Swagger 200, OAuth 200
10. ~~**Update GETTING-STARTED.md**~~ DONE — Docker as primary method, ABP license prominent
11. ~~**Create Claude Code setup prompt**~~ DONE — `docs/onboarding/CLAUDE-SETUP-PROMPT.md`
12. **Test onboarding flow** — IN PROGRESS (user testing Claude prompt in separate folder)
13. **Re-create CI workflow** — TODO: `.github/workflows/docker-build.yml`
14. **Re-add Docker section to CONTRIBUTING.md** — TODO
15. **Commit, PR, merge** — TODO

### Discoveries During Implementation (Not in Original Plan)

| Discovery | Impact | Fix |
|---|---|---|
| ABP CLI access token required at runtime | Exit 214 on authserver/api — ABP license check phones home | Mount `~/.abp/cli/` into containers via compose volume |
| ABP VFS crashes in Development mode | `DirectoryNotFoundException` for sibling source dirs | Create empty stub directories in Dockerfile runtime stage |
| `.dockerignore` excludes `appsettings.secrets.json` | `find` command can't create placeholders during build | Explicit `echo '{}' >` for each known path |
| `SA_PASSWORD` deprecated in SQL Server 2022 | Warning in logs | Use `MSSQL_SA_PASSWORD` instead |
| Exit 214 is ABP license failure code | Misdiagnosed as entrypoint bug in first attempt | License code + CLI token + volume mount = working |

### Verification Checklist

- [x] All 6 containers start without errors
- [x] db-migrator exits with code 0 (not 214)
- [x] `curl http://localhost:44368/health-status` returns 200
- [x] `curl http://localhost:44327/health-status` returns 200
- [x] `curl http://localhost:4200/` returns 200
- [x] Swagger UI loads at http://localhost:44327/swagger
- [x] OAuth discovery at http://localhost:44368/.well-known/openid-configuration
- [ ] Can log in via Angular UI (not tested yet)
- [ ] Fresh clone + docker compose up works (user testing now)

### Remaining Work

1. **CI workflow** — `.github/workflows/docker-build.yml` to build all 4 images on PR
2. **CONTRIBUTING.md** — re-add Docker Setup section (was removed during cleanup)
3. **Commit + PR + merge** — feature branch, all changes, merge to main
