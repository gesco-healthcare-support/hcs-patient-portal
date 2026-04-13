# Claude Code Setup Prompt

Copy the prompt below into **Claude Code (Opus 4.6, 1M context, Plan Mode)** in an empty folder. Claude will clone, configure, build, and verify the entire Patient Portal application using Docker.

**Before running:** You need these 4 values ready:
- **ABP NuGet API Key** — GUID from your ABP Commercial account
- **ABP License Code** — base64 string from `appsettings.secrets.json`
- **Encryption Passphrase** — 16-char string from `StringEncryption:DefaultPassPhrase`
- **ABP CLI logged in** — run `abp login <username>` once on this machine

---

## The Prompt

```text
I need you to set up the Patient Portal application from scratch in this empty directory. This is a workers' compensation IME scheduling system using .NET 10, Angular 20, ABP Commercial 10.0.2, SQL Server, and Redis — all running in Docker.

## Step 1: Clone
Clone the repo and cd into it:
  git clone https://github.com/gesco-healthcare-support/hcs-patient-portal.git
  cd hcs-patient-portal

## Step 2: Collect Secrets
Ask me for these 4 values (one question, all at once):
1. ABP_NUGET_API_KEY — a GUID for the ABP Commercial NuGet feed
2. ABP_LICENSE_CODE — a long base64 string (the AbpLicenseCode value)
3. STRING_ENCRYPTION_PASSPHRASE — a 16-character encryption key
4. MSSQL_SA_PASSWORD — any strong password for the Docker SQL Server (you can generate one if I don't provide one)

## Step 3: Configure
Create these two files using the values I provide. Both go inside the cloned repo:

File 1 — `.env` at repo root (same directory as docker-compose.yml):
(```
MSSQL_SA_PASSWORD=<value>
ABP_NUGET_API_KEY=<value>
STRING_ENCRYPTION_PASSPHRASE=<value>
ABP_LICENSE_CODE=<value>
```)

File 2 — `docker/appsettings.secrets.json`:
(```json
{"AbpLicenseCode": "<ABP_LICENSE_CODE value>"}
```)

IMPORTANT:
- The .env file MUST have LF line endings (not CRLF). After creating it, run: sed -i 's/\r$//' .env
- The .env file must be at the repo root, next to docker-compose.yml — NOT in docker/
- Variable names must match exactly: MSSQL_SA_PASSWORD, ABP_NUGET_API_KEY, STRING_ENCRYPTION_PASSPHRASE, ABP_LICENSE_CODE

## Step 4: Verify Prerequisites
Check that:
- Docker Desktop is running (`docker info`)
- ABP CLI access token exists at ~/.abp/cli/access-token.bin (docker-compose.yml mounts this into containers via $HOME — without it, ABP license validation fails with exit code 214)
- No port conflicts on 1434, 6379, 44368, 44327, 4200

If ABP CLI token is missing, tell me to run `abp login <username>` on the host machine and wait for confirmation.

## Step 5: Build and Start
Run from the repo root directory:
  docker compose up --build

Monitor the output. First build takes 5-10 minutes (downloads .NET SDK, Node.js, restores NuGet/npm packages).

Expected startup sequence:
1. sql-server + redis start and become healthy (~15-30s)
2. db-migrator runs migrations and exits with code 0 (~30-60s)
3. authserver + api start and become healthy (~30-60s each — authserver is slower because it installs client-side libs)
4. angular starts after api is healthy (~5s)

If any .NET container exits with code 214: ABP license issue. Check:
  1. docker/appsettings.secrets.json has the correct AbpLicenseCode
  2. ~/.abp/cli/access-token.bin exists on the host (run `abp login` if missing)
  3. The $HOME env var resolves correctly (check with `echo $HOME`)

If authserver/api crash with DirectoryNotFoundException mentioning "Domain.Shared": The Dockerfiles need VFS stub directories — check that the runtime stage creates empty mkdir commands for ABP's Virtual File System paths.

If authserver returns 500 with "The Libs folder is missing!": The Dockerfile's `abp install-libs` step failed — check the build logs for Node.js or ABP CLI installation errors.

## Step 6: Verify
Once all containers are running, check:
- `curl http://localhost:44368/health-status` returns 200 (AuthServer)
- `curl http://localhost:44327/health-status` returns 200 (API)
- `curl http://localhost:4200/` returns 200 (Angular)
- `curl http://localhost:44327/swagger/index.html` returns 200 (Swagger)
- `curl http://localhost:44368/.well-known/openid-configuration` returns valid JSON (OAuth)
- Open http://localhost:4200 in a browser, click Login, verify redirect to AuthServer login page
- Login with admin@abp.io / 1q2w3E* — should redirect back to Angular with logged-in session

Report the status of each check. If all pass, the setup is complete.

## Constraints
- NEVER output or log any secret values (ABP keys, passwords, license codes)
- All .env and secrets files are gitignored — never commit them
- Use `sed -i 's/\r$//'` on any file you create to ensure LF line endings
- The docker-compose.yml, Dockerfiles, and all config are already in the repo — do not modify them unless a build fails
- If something fails, read the container logs (`docker logs <container-name>`) before attempting fixes
- This setup is OS-agnostic — works on Windows (Docker Desktop), macOS, and Linux
```

---

## What This Prompt Does

Claude Code will:
1. Clone the repo
2. Ask you for the 4 secret values
3. Create `.env` and `docker/appsettings.secrets.json` with your values
4. Verify Docker is running and ABP CLI is authenticated
5. Run `docker compose up --build`
6. Monitor the build and startup
7. Run health checks against all 5 endpoints
8. Report success or diagnose failures

Total time: ~5-10 minutes (mostly Docker build on first run).
