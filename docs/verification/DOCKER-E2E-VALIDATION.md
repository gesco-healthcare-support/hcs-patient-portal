[Home](../INDEX.md) > Verification > Docker E2E Validation

# Docker Cold-Start E2E Validation

**Last verified:** 2026-04-16
**Verified by:** Claude Code (automated, Playwright MCP for browser testing)
**Verification method:** Full cold-start from empty directory -- clone, configure secrets, `docker compose up --build -d`, automated browser tests via Playwright, teardown/restart/rebuild cycles
**Git commit:** `4ed9c4b` (`main`)
**Host environment:** Windows 11 Enterprise 10.0.26200, Docker 29.4.0, Compose v5.1.1, Buildx v0.33.0

---

## Summary

The Patient Portal Docker stack builds and runs successfully from a fresh clone with no prior setup. All 6 services start in correct dependency order, all 8 health checks pass, authentication and every CRUD page work end-to-end, and the stack survives both restart and full rebuild-from-wipe cycles. **Overall result: PASS**, with 3 open issues identified (2 cosmetic, 1 degraded functionality).

Timing baseline: see the [E2E Validation Status section of DOCKER-DEV.md](../runbooks/DOCKER-DEV.md#e2e-validation-status) for the authoritative timings and known-issues list. This document records outcomes and cross-links to the tracked issue register; DOCKER-DEV.md records the operational details that a developer needs mid-setup.

---

## Phase Results

| Phase | Status | Notes |
|---|---|---|
| 1. Environment Check | PASS | Docker running, all 5 ports free (1434, 6379, 44368, 44327, 4200), 136 GB free disk, ABP CLI token present |
| 2. Clone and Configure | PASS | One pre-existing `.env` file in target dir moved aside before clone -- unavoidable without a clean scratch dir |
| 3. Build and Start | PASS | 6 containers up; db-migrator exited cleanly with code 0; cold build ~5 min |
| 4. Health Checks | PASS | All 8 HTTP / database / cache probes green |
| 5. App Functionality | PASS | Auth flow, all CRUD pages, API (behind auth), tenant switcher render. 3 issues identified (see below) |
| 6. Teardown | PASS | Restart without rebuild ~2 min; rebuild after `down -v` ~1 min; DbMigrator re-seeded 67 tables |

---

## Health Check Results

All checks executed after initial cold-start (Phase 4) and re-executed after teardown cycles (Phase 6). All passed in both runs.

| # | Check | Expected | Actual |
|---|---|---|---|
| 1 | AuthServer `/health-status` | 200 | 200 |
| 2 | API `/health-status` | 200 | 200 |
| 3 | Angular `/` | 200 | 200 |
| 4 | Swagger `/swagger/index.html` | 200 | 200 |
| 5 | OIDC discovery | valid JSON | valid (issuer: `http://localhost:44368/`) |
| 6 | API unauthenticated request | 401 | 401 (auth enforced) |
| 7 | SQL Server `SELECT name FROM sys.databases` | success | 5 databases found including `CaseEvaluation` |
| 8 | Redis `PING` | `PONG` | `PONG` |

---

## Open Issues Discovered

Three issues surfaced during the 2026-04-16 run that were not previously tracked. All are now in the central issue register.

| ID | Severity | Summary | Tracked In |
|---|---|---|---|
| [BUG-11](../issues/BUGS.md#bug-11-menu-labels-show-localization-key-prefixes) | Medium | Sidebar and dashboard show `Menu:Home`, `Menu:Dashboard`, etc. instead of resolved display strings. Occurs in Docker and local dev. | [BUGS.md](../issues/BUGS.md) |
| [BUG-12](../issues/BUGS.md#bug-12-page-title-shows-myprojectname-placeholder) | Low | Browser tab title intermittently shows `MyProjectName` (ABP template default) instead of `CaseEvaluation`. | [BUGS.md](../issues/BUGS.md) |
| [FEAT-08](../issues/INCOMPLETE-FEATURES.md#feat-08-swagger-oauth-does-not-work-from-browser-in-docker) | Medium | Swagger UI Authorize button fails with `ERR_NAME_NOT_RESOLVED` because it uses the Docker-internal OIDC MetaAddress. Angular is unaffected. | [INCOMPLETE-FEATURES.md](../issues/INCOMPLETE-FEATURES.md) |

Two additional observations were resolved in-test and not tracked as persistent issues:

- **Clone destination already contained an `.env` file.** Expected to resolve itself: the test prompt assumes an empty scratch directory.
- **Git Bash MSYS2 rewrites `/opt/` paths in `docker exec` commands.** Windows + Git Bash only. Workaround is now documented in [DOCKER-DEV.md -- Known Issues](../runbooks/DOCKER-DEV.md#known-issues-docker-specific).

---

## Gaps & Recommendations

Gaps identified during the onboarding flow. These are candidates for follow-up work but are not currently blocking onboarding.

**Docker setup**
- No healthcheck defined for the `angular` service in `docker-compose.yml`. Every other service (sql-server, redis, authserver, api) has one.
- Split-horizon Swagger authority URL missing -- see [FEAT-08](../issues/INCOMPLETE-FEATURES.md#feat-08-swagger-oauth-does-not-work-from-browser-in-docker).

**Application**
- No demo/seed data beyond ABP framework tables. Doctors, Appointments, Patients, Locations all show as empty on a fresh install, making the "empty list" state the default experience for every page.
- Localization resource gap for `Menu:*` keys -- see [BUG-11](../issues/BUGS.md#bug-11-menu-labels-show-localization-key-prefixes).

**Documentation**
- Angular route map -- added to [DOCKER-DEV.md -- Angular Route Quick Reference](../runbooks/DOCKER-DEV.md#angular-route-quick-reference) during this test run.
- The manual steps required for fresh onboarding (secret collection, `.env` + `docker/appsettings.secrets.json` creation) are not scripted. A `scripts/setup.sh` that prompts for the 4 secret values would cut setup friction.

**Automation**
- No automated E2E harness exists in the repo (no Playwright, Cypress, or similar configured). This test run used Claude Code + Playwright MCP tools manually driven from a prompt. A persistent smoke-test script would make future regression detection cheap.
- No standalone health-check script. Each developer runs the 8 checks by hand.

---

## Onboarding Verdict

**A new developer can get this running in under 30 minutes** provided they have Docker Desktop already installed and ABP credentials in hand. The recorded path was ~11 minutes (6 min configure + 5 min cold build). The main friction points are:

1. Obtaining the 4 secret values (ABP license code, NuGet API key, string encryption passphrase, SQL SA password) -- not scriptable; requires access to ABP account + password manager.
2. Running `abp login` once to create `~/.abp/cli/access-token.bin` if not already done.

After that, `docker compose up --build -d` runs end-to-end with no further manual intervention.

---

## Related

- [DOCKER-DEV.md](../runbooks/DOCKER-DEV.md) -- operational runbook: services, URLs, common ops, known issues, route map
- [GETTING-STARTED.md](../onboarding/GETTING-STARTED.md) -- non-Docker local-dev onboarding path
- [Issues Overview](../issues/OVERVIEW.md) -- full issue register with severity matrix
- [Documentation Baseline](BASELINE.md) -- separate doc-health verification (structural + link checks)
