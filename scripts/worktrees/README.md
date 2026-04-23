# Worktree helper scripts

Automation for the Patient Portal git-worktree workflow. Manages persistent worktrees (`main`, `development`, `staging`) and ad-hoc feature worktrees under `W:\patient-portal\` (= `C:\src\patient-portal\`).

The intended runtime is **`docker compose up -d`** inside any worktree. Host ports and URL env vars are parameterised in `docker-compose.yml` (see `docs/runbooks/DOCKER-DEV.md`); per-worktree overrides live in each worktree's gitignored `.env`.

## Scripts

| Script | Purpose |
|---|---|
| `add-worktree.sh <branch>` | Create a feature worktree: allocate ports, copy secrets, render Angular/dotnet per-worktree config, write per-worktree compose overrides to `.env`, run `dotnet restore` + `yarn install` for the direct-dotnet-run fallback path. |
| `rm-worktree.sh <slug> [--force]` | Remove a worktree. Prompts to drop the LocalDB database for persistent worktrees (development/staging/production); never touches shared state. Does not touch docker volumes -- run `docker compose down -v` in the worktree first if you want to clean those. |
| `render-config.sh <wt> <AUTH> <API> <NG> <DB>` | Emit per-worktree `appsettings.Local.json` (3) + `environment.local.ts`. Uses Python's `json.dump` for reliable LocalDB backslash escaping. Invoked by `add-worktree.sh`; can be re-run manually if config drifts. |
| `refresh-secrets.sh` | Re-copy `docker/appsettings.secrets.json` into every worktree's four service locations after the ABP license rotates. |
| `run.sh` | Print the docker-compose launch command + smoke-test URLs for the current worktree. |

## Port + DB allocation

| Worktree | AuthServer | HttpApi.Host | Angular | SQL host port | Redis host port |
|---|---:|---:|---:|---:|---:|
| `main` | 44368 | 44327 | 4200 | 1434 | 6379 |
| `development` | 44378 | 44337 | 4210 | 1444 | 6389 |
| `staging` | 44388 | 44347 | 4220 | 1454 | 6399 |
| feature (first) | 44398 | 44357 | 4230 | 1437 | 6382 |
| feature (second) | 44408 | 44367 | 4240 | 1438 | 6383 |

Compose uses the worktree directory basename as the project name (so main's containers auto-name as `main-sql-server-1`, etc.), which gives free isolation for container names, networks, and volumes. Each worktree's SQL container is independent; the DB name inside can stay as the default `CaseEvaluation`.

## Quick start

```bash
# In any worktree, start the full stack:
docker compose up -d

# From main: spin up a feature worktree for issue #287
cd /w/patient-portal/main
./scripts/worktrees/add-worktree.sh feat/287-contact-method

# Inside the new worktree:
cd /w/patient-portal/feat-287-contact-method
docker compose up -d

# Back on main: PR merged, clean up
cd /w/patient-portal/main
cd /w/patient-portal/feat-287-contact-method && docker compose down -v    # optional: drop this worktree's DB volume
cd /w/patient-portal/main
./scripts/worktrees/rm-worktree.sh feat-287-contact-method
```

## Assumptions

- `W:` is subst'd to `C:\src` (persisted by the `HCS-Subst-W` scheduled task).
- Docker Desktop running with WSL2 backend. Recommended: at least 8 GB allocated to the WSL VM if you expect 3+ concurrent worktrees (each SQL Server container wants ~2 GB).
- Repo root `.env` has `MSSQL_SA_PASSWORD`, `ABP_NUGET_API_KEY`, `STRING_ENCRYPTION_PASSPHRASE`, `ABP_LICENSE_CODE` populated (copy from `.env.example` and fill in).
- `docker/appsettings.secrets.json` holds the ABP license code (single key: `AbpLicenseCode`). Other service-level secrets files are generated copies of this by `refresh-secrets.sh`.
- A single `dotnet dev-certs https --trust` cert is installed for `localhost` (needed only for the direct-dotnet-run fallback path; the Docker flow runs HTTP-only internally).

## Related

- `docs/runbooks/DOCKER-DEV.md` -- canonical Docker dev runbook, including the "Running multiple worktrees concurrently" section.
