# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability, please report it responsibly:

1. **Do NOT open a public issue**
2. Email security@gesco.com with details
3. Include steps to reproduce if possible
4. We will respond within 48 hours

## Supported Versions

| Branch | Status |
|---|---|
| production | Supported |
| staging | Pre-release |
| development | Development |
| main | Latest |

## Secret Management

This is a public repository. All secrets are managed externally:

- **NuGet API key**: GitHub Secret `ABP_NUGET_API_KEY`, injected at build time via `NuGet.Config.template`
- **Encryption passphrases**: Local `appsettings.Local.json` (gitignored) or GitHub Environment secrets
- **Database credentials**: GitHub Environment secrets per environment (development/staging/production)
- **Test passwords**: `$env:TEST_PASSWORD` environment variable

See `CONTRIBUTING.md` for local development setup.

## HIPAA Compliance

This application handles Protected Health Information (PHI) at runtime.
- No PHI exists in this repository (code, tests, or documentation)
- All test data uses synthetic/dummy values
- Runtime PHI is stored only in environment-specific databases
