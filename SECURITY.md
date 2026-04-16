# Security Policy

This application handles Protected Health Information (PHI) under HIPAA. Security
reports are handled with the priority that regulatory context demands.

## Reporting a Vulnerability

If you discover a security vulnerability in this project:

1. **Do NOT open a public GitHub issue or pull request.**
2. **Do NOT include real PHI in the report.** Use synthetic data to reproduce
   issues involving patient fields. If you cannot reproduce without real data,
   describe the behaviour abstractly and we will work with you.
3. Email [AdrianG@gesco.com](mailto:AdrianG@gesco.com) with the subject prefix
   `[SECURITY]`.

This project is currently maintained by a single developer, so response times
depend on availability. Acknowledgement target: **72 hours**. Fix timeline
varies by severity.

### What to Include in a Report

A good report speeds up triage. Include as many of the following as you can:

- A short summary of the issue and its impact
- Affected version or commit SHA
- Affected component (AuthServer, HttpApi.Host, Angular, DbMigrator, Docker
  stack, CI workflow)
- Steps to reproduce, using synthetic data only
- Whether the issue exposes, or could expose, PHI
- Your suggested severity (Critical / High / Medium / Low) and reasoning
- Optional: a suggested fix or mitigation

### Disclosure Policy

- Coordinated disclosure. We will work with you on a fix before any public
  discussion.
- We will credit reporters in the release notes unless you prefer otherwise.
- Do not publish details of unpatched vulnerabilities to third parties (blog
  posts, conference talks, social media) without coordination.

## Supported Versions

| Branch        | Status                                                            |
| ------------- | ----------------------------------------------------------------- |
| `production`  | Supported (once deployed -- no production environment exists yet) |
| `staging`     | Pre-release                                                       |
| `development` | Active development                                                |
| `main`        | Integration branch                                                |

## Secret Management

This repository is covered by pre-commit and server-side secret scanning
([Gitleaks](https://github.com/gitleaks/gitleaks) + GitHub secret scanning +
TruffleHog in CI). Never commit real secrets.

All runtime secrets are managed externally:

- **ABP NuGet API key**: GitHub secret `ABP_NUGET_API_KEY`, injected at build
  time via `NuGet.Config.template`.
- **ABP license code**: GitHub secret `ABP_LICENSE_CODE`, injected into
  `appsettings.secrets.json` at build time.
- **Encryption passphrases**: local `appsettings.Local.json` (gitignored) or
  GitHub Environment secrets.
- **Database credentials**: GitHub Environment secrets per environment
  (development / staging / production).
- **Test passwords**: `$env:TEST_PASSWORD` environment variable.

See [CONTRIBUTING.md](CONTRIBUTING.md) for local setup. See
[docs/security/SECRETS-MANAGEMENT.md](docs/security/SECRETS-MANAGEMENT.md) for
the full inventory of how secrets flow through the system, including known
gaps.

## HIPAA and PHI

- **No PHI in the repository.** Code, tests, documentation, seed data, and
  commit messages must never contain real patient data.
- **All test fixtures use synthetic values.** See
  [docs/devops/TESTING-STRATEGY.md](docs/devops/TESTING-STRATEGY.md) for the
  seeding pattern.
- **Runtime PHI** is stored only in environment-specific databases and never
  logged. PII logging gaps are tracked in
  [docs/issues/SECURITY.md](docs/issues/SECURITY.md).
- A PHI scanner hook runs on every local tool invocation to catch accidental
  inclusion of protected fields during development.

## Further Reading

- [docs/security/THREAT-MODEL.md](docs/security/THREAT-MODEL.md) -- STRIDE
  analysis of Angular, API, AuthServer, and SQL Server.
- [docs/security/AUTHORIZATION.md](docs/security/AUTHORIZATION.md) --
  permission matrix, roles, endpoint mappings.
- [docs/security/DATA-FLOWS.md](docs/security/DATA-FLOWS.md) -- where PHI lives
  and how it moves between components.
- [docs/security/HIPAA-COMPLIANCE.md](docs/security/HIPAA-COMPLIANCE.md) --
  technical safeguards inventory and HIPAA-readiness gaps.
- [docs/issues/SECURITY.md](docs/issues/SECURITY.md) -- currently tracked
  security issues (open and closed).
