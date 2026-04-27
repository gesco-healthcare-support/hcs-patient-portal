# Documentation Freshness Audit -- 2026-04-24

## Context

This audit was triggered by Phase B-6 Tier 1 backend test coverage landing on `main` via PRs #99, #101, #102, #103, #104, #115, #129, #135 (Tier 1 marked complete by #136), with Tier 2 work in flight (#141 Wave-2 seeds and #142 AppointmentAccessors tests merged 2026-04-24). The asserted live count is 113 `[Fact]` + 2 `[Theory]` = 115 backend test methods across 17 files on origin/main HEAD `3559065`, against the inherited "13 unique test methods" / "Only Doctors and Books have tests" claims that pervade the docs corpus. This audit also captures drift on path references (P:\ no longer canonical; canonical clone is `C:\src\patient-portal\`), CI/CD state (FEAT-06 fixed 2026-04-17 -- 17 GitHub Actions workflows now in `.github/workflows/`), and the BUG-03 / BUG-08 / BUG-11 fixes already noted inline. Categories C audited were chosen by drift risk: `docs/devops/TEST-CATALOG.md`, `docs/devops/TESTING-STRATEGY.md`, and `docs/devops/DEVELOPMENT-SETUP.md` (highest density of test-coverage and dev-setup claims).

## Methodology

- Read each target file on origin/main HEAD `3559065` in worktree `W:\patient-portal\docs-freshness-audit-2026-04-24\`.
- Classify each drift-sensitive claim as `stale` (provably wrong now), `current` (still accurate), or `indeterminate` (cannot resolve without input).
- Every `stale` row carries an independent evidence cell with path:line or command output captured 2026-04-24.
- Live test-count baseline: 115 methods across 17 files (113 `[Fact]` + 2 `[Theory]`), 8 entities with backend tests (Appointments, DoctorAvailabilities, Doctors, Patients, Books, AppointmentAccessors, ApplicantAttorneys, Locations); 7 entities still untested (AppointmentApplicantAttorneys, AppointmentEmployerDetails, AppointmentLanguages, AppointmentStatuses, AppointmentTypes, States, WcabOffices).
- Workflow count: `ls .github/workflows/ | wc -l` returns 17 on this worktree (2026-04-24).

## Summary table

| Stat | Count |
| --- | --- |
| Files audited | 18 |
| Stale rows | 35 |
| Current rows | 4 |
| Indeterminate rows | 3 |

## Findings by file

### `CLAUDE.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 113 | "Only Doctors, Books, and framework services have tests" | stale | 115 [Fact]+[Theory] across 17 files; 8 entities with tests including Appointments, Patients, Locations, DoctorAvailabilities (Grep on this worktree 2026-04-24) | "Currently 8 of 15 entities have backend tests (Appointments, DoctorAvailabilities, Doctors, Patients, Books, AppointmentAccessors, ApplicantAttorneys, Locations); ~115 test methods. 7 entities still untested." |
| 113 | "Patients, Appointments, Locations, DoctorAvailabilities ... untested" | stale | `test/HealthcareSupport.CaseEvaluation.Application.Tests/Patients/PatientsAppServiceTests.cs` (15 facts), `Appointments/AppointmentsAppServiceTests.cs` (12), `Locations/LocationsAppServiceTests.cs` (13), `DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs` (18) | Remove these four from the "untested" list; Tier 1 covered them. Replace with the 7 still-untested entities. |
| 154-160 | "Whether the P: drive subst mapping has been verified" | stale | Per audit context: P:\ no longer holds Patient Portal code; canonical clone is `C:\src\patient-portal\` accessed via `W:\` subst | Remove or replace with: "Whether the W:\ subst to C:\src\patient-portal\ has been verified" |

### `docs/executive-summary.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 17 | "A comprehensive audit identified 29 tracked issues across 5 categories" | stale | `docs/issues/INCOMPLETE-FEATURES.md` now lists FEAT-01..FEAT-14 (14 entries), and `docs/issues/ARCHITECTURE.md` includes ARC-08; total exceeds 29 | "A comprehensive audit identified 39+ tracked issues across 5 categories (with 14 incomplete-feature entries and 8 architecture entries)." |
| 22 | "7 incomplete features (2 high, 5 medium)" | stale | `docs/issues/INCOMPLETE-FEATURES.md` includes FEAT-01..FEAT-14 (14 entries); FEAT-06 also marked Fixed | "14 incomplete features tracked (FEAT-06 fixed 2026-04-17), severities re-tabulated against the live INCOMPLETE-FEATURES list." |
| 22 | "missing email system, near-zero test coverage" | stale | 115 backend test methods now exist; "near-zero" no longer applies | "...missing email system, partial backend test coverage (Tier 1 complete, Tier 2 in flight)." |
| 24 | "258 automated end-to-end tests were executed on 2026-04-02" | current | TEST-EVIDENCE.md confirms run on 2026-04-02; this is a historical statement | (Add note: "Independent xUnit suite has grown to 115 methods as of 2026-04-24.") |
| 44 | "Only 13 backend test methods exist, covering only the Doctors entity and a scaffold sample" | stale | 115 methods across 17 files on HEAD 3559065; coverage spans Appointments, DoctorAvailabilities, Doctors, Patients, Books, AppointmentAccessors, ApplicantAttorneys, Locations | "115 backend test methods exist (Tier 1 complete 2026-04-23). 7 of 15 entities still untested." |
| 44 | "All core features (appointments, patients, availability, external signup) are untested" | stale | Appointments / Patients / DoctorAvailabilities tests live on main; ExternalSignup remains untested | "External signup, tenant creation, and 7 lookup entities still lack backend tests." |
| 44 | "No Angular component tests exist" | indeterminate | Not independently re-verified on this worktree this session; Tier 1 was backend-only by spec | (Verify before edit.) |
| 45 | "no CI/CD pipeline deploying to a staging or production server" | stale | 17 workflows in `.github/workflows/` on HEAD 3559065; FEAT-06 fixed 2026-04-17 | "CI/CD pipeline added 2026-04-17 (17 GitHub Actions workflows). No staging/production deployment target yet." |
| 45 | "Docker support has been added but not yet deployed" | current | No deployment evidence found in this audit window | (Keep.) |

### `docs/INDEX.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 245 | "Incomplete Features ... no CI/CD, near-zero test coverage" | stale | FEAT-06 fixed 2026-04-17 (17 workflows); 115 backend tests across 17 files | "Incomplete Features -- Missing workflows, placeholder UI, partial test coverage." |
| 247 | "E2E test results mapping (258 tests, 2026-04-02)" | current | Historical statement, accurate as of run date | (Keep; add note that xUnit suite has since grown to 115 methods.) |
| 250 | "Test Catalog -- Complete test suite documentation (258 automated + 11 exploratory)" | stale | TEST-CATALOG.md does not yet cover the xUnit Tier 1 suite (115 methods) | "Test Catalog -- E2E suite (258 automated + 11 exploratory, 2026-04-02). xUnit Tier 1 suite (115 methods) catalogued separately." |

### `docs/issues/OVERVIEW.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 5 | "confirmed via automated E2E testing on 2026-04-02 ... re-verified ... 2026-04-16" | current | Historical pinned dates; section is the test-evidence header, not a freshness claim | (Keep.) |
| 65-77 | Implies 8 incomplete features (FEAT-01..FEAT-08) | stale | Underlying file now contains FEAT-01..FEAT-14 | Extend index table to FEAT-09..FEAT-14. |
| 80-87 | Implies 7 architecture issues (ARC-01..ARC-07) | stale | `docs/issues/ARCHITECTURE.md` now includes ARC-08 (Missing `[RemoteService(IsEnabled = false)]` on 3 AppServices, audit 2026-04-13) | Add ARC-08 row to the Architecture table. |
| 74 | "FEAT-06 ... Fixed 2026-04-17 (17 GitHub Actions workflows)" | current | `ls .github/workflows/ | wc -l` -> 17 on this worktree | (Keep.) |

### `docs/issues/BUGS.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 5 | "Twelve confirmed bugs were identified" | current | File still contains BUG-01..BUG-12 only | (Keep.) |
| 100 | "BUG-03 ... Fixed -- verified 2026-04-17 ... no longer present" | current | Inline note already corrects the historical description | (Keep.) |
| 306 | "BUG-08 ... Fixed in `docs/QUICK-START.md` on 2026-04-03" | current | Inline note already corrects | (Keep.) |
| 419 | "BUG-11 ... Fixed -- verified 2026-04-17. The Menu:* keys ... are present" | current | Inline note already corrects | (Keep.) |

### `docs/issues/INCOMPLETE-FEATURES.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 5 | "Eight features are either entirely missing, present only as placeholders" | stale | File now contains FEAT-01..FEAT-14 (14 entries) | "Fourteen features..." |
| 192-199 | "FEAT-06 ... 17 GitHub Actions workflows now present" | current | Workflow count verified 2026-04-24: 17 | (Keep.) |
| 213-218 | "What Is Missing ... .github/workflows/ -- No GitHub Actions workflows" | stale | Historical block contradicts the live "Fixed" status above; will confuse future readers | Move under a "Historical Description" heading or strike, since the Status block above already documents the fix. |
| 223 | "On every pull request: restore, build, and run the 13 existing backend tests" | stale | Live test count: 115 methods across 17 files | "...run the existing backend tests (115 methods on HEAD 3559065)." |
| 233-251 | "FEAT-07: Near-Zero Test Coverage ... 13 unique test methods across the entire application" | stale | 115 [Fact]+[Theory] methods across 17 files; 8 entities have tests | "FEAT-07: Partial Test Coverage. 115 backend test methods cover 8 of 15 entities. Tier 1 complete (2026-04-23); 7 entities still untested." |
| 242-251 | Coverage table: "Doctors 5; Books 3; Appointments 0; Patients 0; Doctor Availabilities 0; External Signup 0; Tenant/Doctor Creation 0; All other entities 0" | stale | Live counts: Appointments ~12, Patients 15, DoctorAvailabilities 18-20, Locations 12-13, ApplicantAttorneys 12, AppointmentAccessors 9, Books 3, Doctors 5, EF Core repo tests 5+ | Replace table with the live per-entity counts; mark ExternalSignup, Tenant creation, and the 7 lookup entities as still 0. |

### `docs/issues/DATA-INTEGRITY.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 7 | "DAT-01 partially confirmed ... DAT-03 confirmed via E6 ... See [TEST-EVIDENCE.md]" | current | Historical pin; refers to 2026-04-02 E2E run | (Keep.) |

### `docs/issues/SECURITY.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 5 | "Five security issues were identified ... All file paths and line numbers are current as of the audit date" | indeterminate | Line numbers in subsequent sections reference 2026-04 audit; not re-checked against HEAD this session | (Re-verify line numbers before edit.) |
| 24 | SEC-01: "Now uses `REPLACE_ME_LOCALLY` placeholder; real value in `appsettings.Local.json`" | current | Inline correction matches code-standards-aligned remediation | (Keep.) |

### `docs/issues/ARCHITECTURE.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 5 | "Seven architectural concerns and code quality issues were identified" | stale | File now contains ARC-01..ARC-08 (8 entries) | "Eight architectural concerns..." |

### `docs/issues/QUESTIONS-FOR-PREVIOUS-DEVELOPER.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| (file scope) | "10 irreversible questions" implied by INDEX.md cross-reference | current | Spot read shows P1..P3 still present; full count not exhaustively verified this session | (Keep.) |

### `docs/issues/TECHNICAL-OPEN-QUESTIONS.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 5 | "258 automated tests + 11 exploratory scenarios run on 2026-04-02" | current | Historical pin | (Keep.) |
| 35 | Q1 "Test B11.1.1 confirms: PUT cannot change status" | indeterminate | Status of this code-level finding not re-checked against HEAD this session | (Re-verify.) |

### `docs/issues/TEST-EVIDENCE.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 3 | "**Date**: 2026-04-02" | current | This is the historical run report; date pin is correct | (Keep; consider adding "Note: subsequent xUnit Tier 1 suite catalogued separately.") |

### `docs/runbooks/DOCKER-DEV.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 223-224 | "Page title shows 'MyProjectName' (cosmetic)" | current | BUG-12 still Open per BUGS.md line 467 | (Keep.) |

### `docs/features/patients/overview.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 1 | "Last synced from src/.../Patients/CLAUDE.md on 2026-04-08" | stale | PatientsAppServiceTests.cs ships 15 [Fact]/[Theory] methods on HEAD; sync date precedes Tier 1 PR-1C merge | "Last synced ... on 2026-04-23 (post Tier 1 PR-1C)." |
| 61 | "3. **No tests**" (in Known Gotchas) | stale | `test/.../Application.Tests/Patients/PatientsAppServiceTests.cs` -> 15 [Fact]/[Theory]; `EntityFrameworkCore.Tests/.../Patients/PatientRepositoryTests.cs` -> 5 [Fact] | "AppService tests live (PR-1C, 15 methods). 2 profile tests skipped pending FEAT-10 (WithCurrentUser helper)." |

### `docs/features/appointments/overview.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 1 | "Last synced from ... Appointments/CLAUDE.md on 2026-04-08" | stale | AppointmentsAppServiceTests.cs ships 12+ methods on HEAD; sync date precedes Tier 1 | "Last synced ... on 2026-04-23." |
| 29 | "No domain methods enforce valid transitions -- any code can set any status directly" | current | FEAT-01 still open per INCOMPLETE-FEATURES.md | (Keep.) |

### `docs/features/locations/overview.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 1 | "Last synced from ... Locations/CLAUDE.md on 2026-04-08" | stale | `test/.../Application.Tests/Locations/LocationsAppServiceTests.cs` -> 12-13 facts; `EntityFrameworkCore.Tests/.../Locations/LocationRepositoryTests.cs` -> 1 fact | "Last synced ... on 2026-04-23." |
| 112 | "3. **No tests** -- no test files found for Locations." | stale | LocationsAppServiceTests.cs and LocationRepositoryTests.cs both present in test tree on HEAD | "AppService and repository tests live (PR-1D); 2 delete-constraint tests skipped pending FEAT-14 (SQLite FK enforcement)." |

### `docs/features/doctors/overview.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 1 | "Last synced from ... Doctors/CLAUDE.md on 2026-04-08" | stale | Tier 1 work landed since; tests count remains accurate but sync header should match | "Last synced ... on 2026-04-23." |

### `docs/features/doctor-availabilities/overview.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 1 | "Last synced from ... DoctorAvailabilities/CLAUDE.md on 2026-04-08" | stale | DoctorAvailabilitiesAppServiceTests.cs -> 18-20 facts on HEAD | "Last synced ... on 2026-04-23." |
| 118 | "4. **No tests** -- no test files found for DoctorAvailabilities." | stale | `test/.../Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs` ships 18-20 [Fact]/[Theory] | "AppService tests live (Tier 1, 18-20 methods including conflict detection and overlap rules)." |

### `docs/devops/TEST-CATALOG.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 5 | "Last Run: 2026-04-02 | 258 automated + 11 exploratory" | stale | Catalog never updated since the xUnit Tier 1 suite (115 methods) shipped 2026-04-23 | "Last Run (PowerShell E2E): 2026-04-02 | xUnit Tier 1 suite: 115 methods, last run 2026-04-23." |
| 13-22 | At-a-glance ASCII diagram showing only Plan A / B / Exploratory | stale | xUnit suite is the primary regression gate now; not represented in the diagram | Add a third lane for the xUnit suite (Application + EF Core test projects, 115 methods). |

### `docs/devops/TESTING-STRATEGY.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 26 | "DoctorsDataSeedContributor ... seed two test Doctor entities with known GUIDs" | stale | Wave-2 seeds added 2026-04-24 (PR #141); `test/.../Application.Tests/SeedContributor/Wave2SeedSanityTests.cs` ships 2 [Fact] | "Wave-1 seeds 2 Doctors; Wave-2 (2026-04-24) extends seeding for additional entities." |
| 40-47 | "DoctorApplicationTests ... tests IDoctorsAppService CRUD operations ... GetListAsync, GetAsync, CreateAsync, UpdateAsync, DeleteAsync" | current | DoctorApplicationTests.cs still ships 5 facts mapping to those CRUD methods | (Keep.) |
| 46 | "BookAppService_Tests ... SampleAppServiceTests ... Baseline" | stale | Application.Tests project now contains AppointmentAccessors, ApplicantAttorneys, Appointments, DoctorAvailabilities, Locations, Patients, SeedContributor.Wave2 directories on HEAD | Add the 7 new Tier 1 test classes to the Application.Tests inventory. |

### `docs/devops/DEVELOPMENT-SETUP.md`

| Line | Claim (verbatim, <=15 words) | Status | Evidence | Proposed fresh text |
| --- | --- | --- | --- | --- |
| 11 | "Node.js" without version pin | indeterminate | No live `node --version` captured this session | (Verify before edit.) |
| 71 | "Default admin credentials: admin@abp.io / see TEST_PASSWORD in .env.local" | current | Matches handover notes; not a freshness claim | (Keep.) |
| 106 | "Never use `ng serve` or `yarn start`" | current | Constraint still load-bearing per CLAUDE.md and ADR-005 | (Keep.) |

---

End of audit.
