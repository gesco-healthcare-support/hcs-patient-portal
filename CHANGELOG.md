# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
once a first release is cut.

> This project was inherited without prior release history. Entries begin with
> the foundation and documentation phase that made the repository ready for
> ongoing development. Earlier pre-inheritance history is not reconstructed
> here; consult `git log` for the full commit record.

## [Unreleased]

### Added

- Repository documentation (75+ markdown files under [docs/](docs/)) covering
  architecture, backend, database, API, frontend, business domain, security,
  DevOps, runbooks, features, issues, and ADRs.
- Per-feature `CLAUDE.md` files for all 15 documented domain features.
- GitHub Actions CI/CD pipeline: `ci.yml`, `security.yml`, `deploy-dev.yml`,
  `promote-staging.yml`, `release.yml`, `auto-pr-dev.yml`, `pr-size.yml`,
  `dependency-review.yml`, `labeler.yml`, `doc-check.yml` (placeholder).
- Husky pre-commit, commit-msg, and pre-push hooks with
  [commitlint](https://commitlint.js.org/), [lint-staged](https://github.com/lint-staged/lint-staged),
  [Prettier](https://prettier.io/), [ESLint](https://eslint.org/),
  [Gitleaks](https://github.com/gitleaks/gitleaks), and `dotnet format`.
- Docker Compose stack with six services: `sql-server`, `redis`, `db-migrator`,
  `authserver`, `api`, `angular`.
- Branch protection with progressive hardening across `main`, `development`,
  `staging`, and `production`.
- Issue and pull-request templates under `.github/`, including a HIPAA
  compliance checklist.
- Five accepted Architecture Decision Records under
  [docs/decisions/](docs/decisions/).

### Changed

- Upgraded ABP Framework to `10.0.2` (Commercial).
- Upgraded Angular to `~20.0.0` with standalone components and the esbuild
  application builder.
- Upgraded the .NET target framework to `10.0`.
- Replaced AutoMapper with [Riok.Mapperly](https://github.com/riok/mapperly)
  for all mapping code (compile-time source generation).
- Migrated from `.sln` to the newer `.slnx` solution format
  (`HealthcareSupport.CaseEvaluation.slnx`).

### Security

- Added Gitleaks pre-commit and pre-push scans plus TruffleHog + CodeQL in CI.
- Removed previously committed secrets from active files and rotated affected
  keys; historical git objects are tracked for remediation in
  [docs/issues/SECURITY.md](docs/issues/SECURITY.md).
- Added PHI scanner hook covering every local development tool invocation.

### Known Issues

- 29 tracked issues across five categories (security, data integrity, bugs,
  incomplete features, architecture) -- see
  [docs/issues/OVERVIEW.md](docs/issues/OVERVIEW.md).
- Test coverage is limited to two entities (Doctors, Books). All other domain
  features are untested -- see
  [docs/devops/TEST-CATALOG.md](docs/devops/TEST-CATALOG.md).
- No staging or production environment is deployed yet. The application runs
  only on localhost or via the local Docker stack.
- Seven Angular XSS advisories are blocked on ABP 10.3+ packages being
  published.

[Unreleased]: https://github.com/gesco-healthcare-support/hcs-patient-portal
