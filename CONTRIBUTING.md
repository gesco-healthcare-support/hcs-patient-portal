# Contributing to Patient Portal

## Branch Model

```
feature/* --> main --> development --> staging --> production
```

- **main**: Default PR target. Latest integrated code.
- **development**: In-house hosted testing and experimentation.
- **staging**: Comprehensive and exhaustive pre-production testing.
- **production**: Live application serving real users.

## Branch Rules (Progressive Hardening)

| Branch | Required Checks | Approvals |
|---|---|---|
| main | Backend Build, Frontend Build | 1 |
| development | + Backend Test, Frontend Lint | 1 |
| staging | + Frontend Test, Dependency Review | 1 |
| production | + Secret Detection | 2 |

## Development Workflow

1. Create a feature branch from `main`: `git checkout -b feature/my-feature main`
2. Make changes, commit using [conventional commits](https://www.conventionalcommits.org/):
   - `feat(appointments): add bulk scheduling endpoint`
   - `fix(doctors): correct availability overlap check`
   - `docs(patients): update API reference`
3. Push and open a PR to `main`
4. After CI passes and 1 approval, merge
5. Code automatically promotes: main -> development -> staging (via auto-PRs)
6. Production promotion requires 2 approvals and manual merge

## Local Setup

### Prerequisites
- .NET SDK 10.0.x
- Node.js 20.x + Yarn
- SQL Server (LocalDB on Windows, or Docker)
- Gitleaks (for pre-commit secret scanning)

### First-Time Setup

```bash
git clone https://github.com/gesco-healthcare-support/hcs-patient-portal.git
cd hcs-patient-portal

# 1. Configure NuGet (requires ABP Commercial API key)
./scripts/setup-nuget.sh

# 2. Create local appsettings overrides
cp src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.Local.json.example \
   src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.Local.json
cp src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.Local.json.example \
   src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.Local.json
# Edit the .Local.json files with your own passphrases

# 3. Start SQL Server, then run migrations
dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator

# 4. Start services (3 terminals)
dotnet run --project src/HealthcareSupport.CaseEvaluation.AuthServer
dotnet run --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
cd angular && yarn install && npx ng build --configuration development && npx serve -s dist/CaseEvaluation/browser -p 4200
```

**NEVER use `ng serve` or `yarn start`** -- causes NullInjectorError with ABP on Angular 20.

### Activating Local Git Hooks

Git hooks (Husky + commitlint + lint-staged + gitleaks) install automatically
the first time you run `yarn install` inside `angular/`:

```bash
cd angular && yarn install   # also runs husky bootstrap
```

After install, every `git commit` runs:

- `gitleaks protect --staged` against staged files (blocks committed secrets)
- `lint-staged` (prettier --write + eslint --fix on staged `.ts` / `.html` files; prettier --write on `.scss` / `.css` / `.json` / `.md` files)
- `dotnet format --verify-no-changes` on staged `.cs` files
- `commitlint` against your commit message (must be Conventional Commits)

Every `git push` also runs:

- Full-repo `gitleaks detect`
- `dotnet build HealthcareSupport.CaseEvaluation.slnx -c Debug`
  (CI runs Release on the PR; Debug here keeps push fast)

**Gitleaks must be installed separately** (it is not an npm package):

```bash
# macOS
brew install gitleaks

# Linux / WSL (x64)
curl -sSfL -o /tmp/gitleaks.tar.gz \
  https://github.com/gitleaks/gitleaks/releases/download/v8.21.2/gitleaks_8.21.2_linux_x64.tar.gz
tar -xzf /tmp/gitleaks.tar.gz -C /tmp gitleaks
install -m 0755 /tmp/gitleaks ~/.local/bin/gitleaks

# Windows / Git Bash: download the windows-x64 zip from the same releases
# page and place gitleaks.exe on PATH (e.g. C:\Users\<you>\bin\).
```

Verify with `gitleaks version` (expect 8.21.x). If gitleaks is not on PATH,
both commit and push hooks print a warning and continue (they do NOT block).
CI scans server-side regardless, so a missing local gitleaks is a
degraded-but-not-broken state, not a security regression.

**WSL users:** the hooks detect WSL and handle cross-shell quirks automatically
(path translation for Windows Node, `dotnet` -> `dotnet.exe` fallback). If
WSL `git status` shows every file as "modified" after pulling, your WSL git
probably has `core.autocrlf=false` while Windows git has `true`. Align them:

```bash
git config --global core.autocrlf true
# then, inside the repo:
git rm --cached -r -q . && git reset --hard HEAD
```

**To skip hooks for an emergency commit:** `git commit --no-verify`. Do not
make this a habit; CI catches what you skipped, but only after you have
already pushed.

### GUI Git Clients (VS Code, GitHub Desktop)

GUI clients do not source `.bashrc` or `.zshrc`, so nvm-managed Node is
invisible to hooks. If commits from the VS Code source-control sidebar fail
with "command not found", create a Husky init script:

**Windows (Git Bash) -- if Node is installed via nvm-windows:**

```bash
mkdir -p ~/.config/husky
cat > ~/.config/husky/init.sh << 'EOF'
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
EOF
```

**WSL / macOS / Linux -- if Node is installed via nvm:**

```bash
mkdir -p ~/.config/husky
cat > ~/.config/husky/init.sh << 'EOF'
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
EOF
```

This file is machine-local (not committed to the repo). Husky sources it
before every hook execution, ensuring `node`, `npx`, and `yarn` are on PATH.

### Service Ports
- AuthServer: https://localhost:44368
- API: https://localhost:44327
- Angular: http://localhost:4200

### Running Tests
```bash
dotnet test                          # All backend tests
cd angular && yarn test              # Frontend tests
cd angular && yarn lint              # Frontend lint
```

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/). Enforced by commitlint.

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `ci`, `perf`, `build`, `revert`

## HIPAA Requirements

- NEVER include real patient data in code, tests, or documentation
- All test data must use synthetic/dummy values
- Do not log PHI fields
- Flag any code that could expose Protected Health Information
