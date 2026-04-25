---
last-verified: 2026-04-24
---

[Home](../INDEX.md) > [Testing](./) > Coverage Status

# Backend Test Coverage Status

This document is the single source of truth for the live state of the backend
test suite. Older docs (executive-summary.md, INCOMPLETE-FEATURES.md, the root
CLAUDE.md, TEST-EVIDENCE.md, README.md) historically carried scattered, drifted
counts ("13 unique test methods", "Only Doctors and Books"). All those claims
are stale. New docs and refreshed pointers should link here instead of
restating numbers inline so there is exactly one place to update when the
suite grows.

## Headline Numbers (verified 2026-04-24)

- **115 backend test methods** total: **113 `[Fact]`** + **2 `[Theory]`**.
- Spread across **17 test files** under `test/`.
- Covers **8 of 15** entities. **7 entities still have no backend tests.**
- Verified by Grep on worktree HEAD (commit on branch
  `docs/freshness-audit-2026-04-24`).

## Per-Entity Coverage Rollup

### Covered (8 entities)

| Entity | Test files | `[Fact]` + `[Theory]` |
|---|---|---|
| Appointments | `Application.Tests/Appointments/AppointmentsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/Appointments/EfCoreAppointmentsAppServiceTests.cs` | 17 |
| DoctorAvailabilities | `Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/DoctorAvailabilities/EfCoreDoctorAvailabilitiesAppServiceTests.cs` | 20 |
| Doctors | `Application.Tests/Doctors/DoctorApplicationTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/Doctors/EfCoreDoctorsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/Doctors/DoctorRepositoryTests.cs` | 7 |
| Patients | `Application.Tests/Patients/PatientsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/Patients/EfCorePatientsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/Patients/PatientRepositoryTests.cs` | 23 (16 + 2 Theory + 5) |
| Books (legacy scaffold) | `Application.Tests/Books/BookAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/Books/EfCoreBookAppServiceTests.cs` | 3 |
| AppointmentAccessors | `Application.Tests/AppointmentAccessors/AppointmentAccessorsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/AppointmentAccessors/EfCoreAppointmentAccessorsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/AppointmentAccessors/AppointmentAccessorRepositoryTests.cs` | 11 |
| ApplicantAttorneys | `Application.Tests/ApplicantAttorneys/ApplicantAttorneysAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/ApplicantAttorneys/EfCoreApplicantAttorneysAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/ApplicantAttorneys/ApplicantAttorneyRepositoryTests.cs` | 13 |
| Locations | `Application.Tests/Locations/LocationsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/Locations/EfCoreLocationsAppServiceTests.cs`, `EntityFrameworkCore.Tests/EntityFrameworkCore/Domains/Locations/LocationRepositoryTests.cs` | 16 |

Plus framework-scaffold tests in `Domain.Tests/Samples/SampleDomainTests.cs`,
`Application.Tests/Samples/SampleAppServiceTests.cs`,
`EntityFrameworkCore.Tests/EntityFrameworkCore/Samples/SampleRepositoryTests.cs`,
and the seed-sanity tests `Application.Tests/SeedContributor/Wave2SeedSanityTests.cs`
+ its EfCore mirror.

### Not yet covered (7 entities)

- AppointmentApplicantAttorneys
- AppointmentEmployerDetails
- AppointmentLanguages
- AppointmentStatuses
- AppointmentTypes
- States
- WcabOffices

These are mostly reference-data lookups + appointment-child join entities; they
carry the lowest blast radius but should be added before MVP closure.

## E2E / Integration Test Layer

Separate from the xUnit suite above, the Plan-A/Plan-B PowerShell harness in
`scripts/Master-Seed.ps1` + `Master-Test.ps1` measured **258 automated tests +
11 exploratory** on **2026-04-02** (246 PASS, 5 FAIL-EXPECTED, 7 SKIP, 0
unexpected failures). That number has not been re-measured against the
post-Tier-2 codebase. Historical baseline lives in
[docs/issues/TEST-EVIDENCE.md](../issues/TEST-EVIDENCE.md). Re-running the
harness is tracked separately and is out of scope for this doc.

## How To Re-Verify

From the repo root, in Git Bash:

```
# [Fact] count -- expect ~113 currently
grep -rcP '\[Fact' test/ --include='*.cs' | awk -F: '{s+=$2} END{print s}'

# [Theory] count -- expect ~2 currently
grep -rcP '\[Theory' test/ --include='*.cs' | awk -F: '{s+=$2} END{print s}'

# Distinct test files
find test -name '*Tests.cs' | wc -l
```

Or, equivalently in this worktree, run the Grep tool with pattern `\[Fact` /
`\[Theory` against `test/` and `*.cs` glob, count mode. When numbers change,
update the headline + per-entity table above and bump `last-verified` in the
frontmatter.

## History

- **Pre-2026-04-23 (B-phase closure):** ~13 backend test methods total,
  covering Doctors and the framework scaffold sample only. The "Only Doctors
  and Books have tests" claim originates from this snapshot.
- **2026-04-23 (B-6 Tier 1):** Added Application.Tests for Appointments,
  DoctorAvailabilities, Patients, AppointmentAccessors, ApplicantAttorneys,
  Locations. Brought total from 13 -> ~70 methods.
- **2026-04-23/24 (B-6 Tier 2):** Added EfCore.Tests mirrors plus repository-
  layer tests for Doctors, Patients, AppointmentAccessors, ApplicantAttorneys,
  Locations. Brought total to **115 methods across 17 files**, current as of
  2026-04-24.

## Related

- [docs/issues/INCOMPLETE-FEATURES.md FEAT-07](../issues/INCOMPLETE-FEATURES.md#feat-07-test-coverage-gaps-mostly-obsolete) -- issue tracker entry, downgraded from Critical to Medium.
- [docs/issues/TEST-EVIDENCE.md](../issues/TEST-EVIDENCE.md) -- frozen E2E baseline (2026-04-02).
- [docs/devops/TESTING-STRATEGY.md](../devops/TESTING-STRATEGY.md) -- structure of the test projects.
- [docs/devops/TEST-CATALOG.md](../devops/TEST-CATALOG.md) -- E2E phase map.
