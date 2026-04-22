# Worktree helper scripts

Automation for the Patient Portal git-worktree workflow. Manages persistent worktrees (`main`, `development`, `staging`) and ad-hoc feature worktrees under `W:\patient-portal\` (= `C:\src\patient-portal\`).

## Scripts

| Script | Purpose |
|---|---|
| `add-worktree.sh <branch>` | Create a feature worktree: allocate ports, copy secrets, render `appsettings.Local.json` (3 files) + `environment.local.ts`, run `dotnet restore` + `yarn install`. |
| `rm-worktree.sh <slug> [--force]` | Remove a worktree. Prompts to drop the DB for persistent worktrees; never touches the shared `CaseEvaluation` DB used by main + features. |
| `render-config.sh <wt> <AUTH> <API> <NG> <DB>` | Emit per-worktree `appsettings.Local.json` (3) + `environment.local.ts`. Uses Python's `json.dump` for reliable LocalDB backslash escaping. |
| `refresh-secrets.sh` | Re-copy `docker/appsettings.secrets.json` into every worktree's four service locations. Run after the ABP license rotates or when bootstrapping a new worktree by hand. |
| `run.sh` | Print the three-tab launch order for the current worktree (does not actually launch services -- Kestrel's boot log is load-bearing). |

## Port + DB allocation

| Worktree | AuthServer | HttpApi.Host | Angular | Database |
|---|---:|---:|---:|---|
| `main` | 44368 | 44327 | 4200 | `CaseEvaluation` |
| `development` | 44378 | 44337 | 4210 | `CaseEvaluation_development` |
| `staging` | 44388 | 44347 | 4220 | `CaseEvaluation_staging` |
| feature (first) | 44398 | 44357 | 4230 | `CaseEvaluation` (shared with main) |
| feature (second) | 44408 | 44367 | 4240 | `CaseEvaluation` (shared) |

Features share `CaseEvaluation` so they can be tested against current main state. Persistent worktrees are isolated per-DB.

## Quick start

```bash
# Start a feature for issue #287
cd /w/patient-portal/main
./scripts/worktrees/add-worktree.sh feat/287-contact-method

# Print launch order from the new worktree
cd /w/patient-portal/feat-287-contact-method
./scripts/worktrees/run.sh

# After the feature PR is merged, clean up
cd /w/patient-portal/main
./scripts/worktrees/rm-worktree.sh feat-287-contact-method
```

## Assumptions

- `W:` is subst'd to `C:\src` (persisted by the `HCS-Subst-W` scheduled task).
- LocalDB `(LocalDb)\MSSQLLocalDB` is running (autostarts on connection).
- A single `dotnet dev-certs https --trust` cert is installed for `localhost`.
- `docker/appsettings.secrets.json` holds the ABP license code (single key: `AbpLicenseCode`). Other service-level secrets files are copies of this; if additional keys are ever added they live only in this one canonical file.
