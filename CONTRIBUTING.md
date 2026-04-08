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
| production | + Secret Detection, Docker Build | 2 |

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
