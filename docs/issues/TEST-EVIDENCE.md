[Home](../INDEX.md) > [Issues](./) > Test Evidence

# E2E Test Evidence Report

**Date**: 2026-04-02
**Method**: Automated testing via Plan A (data seeding) + Plan B (E2E test suite) + exploratory tests
**Environment**: localhost (AuthServer :44368, API Host :44327, LocalDB)

---

## Test Execution Summary

### Plan A: Data Seeding (Master-Seed.ps1)

| Phase | Records | Status |
|-------|---------|--------|
| A1: Reference Data | 10 states, 6 types, 13 statuses, 12 languages | PASS |
| A2: Locations & WCAB | 8 locations, 7 WCAB offices | PASS |
| A3: Doctors & Users | 5 tenants, 5 doctors, 7 patients, 3 CE, 3 AA, 3 DA | PASS |
| A4: Availability | 209 slots across 5 tenants | PASS |
| A5: Appointments | 28 appointments (T1:13, T2:10, T3:5) covering all 13 statuses | PASS |
| A6: Child Entities | 3 AA entities, 19 employer details, 48 accessors, 17 AA links | PASS |
| **Total** | **~370 records** | **All 6 phases PASS** |

### Plan B: E2E Tests (Master-Test.ps1)

| Phase | Tests | Passed | Failed | Expected | Skipped |
|-------|-------|--------|--------|----------|---------|
| B1: Infrastructure | 18 | 13 | 0 | 0 | 5 |
| B2: Authentication | 17 | 16 | 0 | 0 | 1 |
| B3: Reference Data CRUD | 44 | 44 | 0 | 0 | 0 |
| B4: Locations & WCAB | 19 | 19 | 0 | 0 | 0 |
| B5: Doctors CRUD | 13 | 13 | 0 | 0 | 0 |
| B6: External Signup | 20 | 20 | 0 | 0 | 0 |
| B7: Availability CRUD | 14 | 13 | 0 | 1 | 0 |
| B8: Appointments | 24 | 24 | 0 | 0 | 0 |
| B9: Child Entities | 14 | 14 | 0 | 0 | 0 |
| B10: Business Logic | 20 | 20 | 0 | 0 | 0 |
| B11: Known Gaps | 7 | 2 | 0 | 5 | 0 |
| B12: Multi-Tenancy | 13 | 13 | 0 | 0 | 0 |
| B13: Security | 12 | 12 | 0 | 0 | 0 |
| B14: Data Integrity | 8 | 8 | 0 | 0 | 0 |
| B15: Concurrency | 3 | 3 | 0 | 0 | 0 |
| B16: Credentials | 12 | 11 | 0 | 0 | 1 |
| **TOTAL** | **258** | **246** | **0** | **5** | **7** |

### Exploratory Tests (Exploratory-Tests.ps1)

| Test | Finding | Maps To |
|------|---------|---------|
| E1: Past-date appointment | **ACCEPTED** - No past-date validation | NEW: BUG-09 |
| E2: Custom confirmation number | Auto-generated correctly (CUSTOM99 overridden to A00014) | OK |
| E3: Patient /me endpoint | Works correctly (200) | OK |
| E4: External user lookup | **8 users with PII exposed to Patient role** | SEC-03 confirmed |
| E5: Weak password | Rejected (403 - already exists from prior run) | SEC-05 (policy is relaxed) |
| E6: Slot release after DELETE | **Slot stays Booked(9)** - gap confirmed | DAT-03 / C1.2 |
| E7: fromTime > toTime | **Accepted** - no validation | BUG-10 / C1.7 |
| E8: Anonymous API access | All /api/app/* return 401 | OK |
| E9: Patient privilege escalation | Correctly blocked (403) | OK |
| E10a: Duplicate emails | 8 in AbpUsers (cross-tenant - expected) | OK (tenant-scoped) |
| E10b: Tenant distribution | 3 tenants with appointments (correct) | OK |
| E10c: Null CreatorId | 0 (audit trail complete) | OK |
| E10d: Orphaned slots | 0 (no orphans) | OK |
| E10e: Duplicate conf numbers | 0 (none found) | OK |
| E11: Config secrets | 4 secrets/risks in config files | SEC-01 confirmed |
| E11: CORS Origins | Wildcard `*.CaseEvaluation.com` | SEC-04 confirmed |

---

## Evidence Mapping: Test Results to Known Issues

### Confirmed Issues (reproduced via testing)

| Issue ID | Test Evidence | Status |
|----------|--------------|--------|
| **SEC-01** | B16.2.1-2: StringEncryption passphrase and OpenIddict PFX password found in appsettings.json | **CONFIRMED** |
| **SEC-02** | B16.6.1: `DisablePII: false` found in HttpApi.Host appsettings.json | **CONFIRMED** |
| **SEC-03** | E4: Patient role retrieved 8 users with identityUserId, firstName, lastName, email, userRole | **CONFIRMED** |
| **SEC-04** | E11: CORS Origins `https://*.CaseEvaluation.com` with AllowAnyMethod + AllowCredentials | **CONFIRMED** |
| **DAT-01** | B15.1.1: Simultaneous booking test - one succeeded, one failed (race window exists) | **PARTIALLY CONFIRMED** - EF Core transaction prevented double-booking in this test run, but no distributed lock exists |
| **DAT-02** | B10.4.1-2: Sequential confirmation numbers generated correctly in serial tests. B15 concurrent test didn't trigger duplicate. | **NOT REPRODUCED** in testing but code path analysis confirms vulnerability |
| **DAT-03** | E6: Slot stays Booked(9) after appointment DELETE. B11.2.1 also confirmed. | **CONFIRMED** |
| **DAT-05** | B10.1.1-13: All 13 statuses accepted at creation. B11.1.1: No mechanism to change status via PUT. | **CONFIRMED** - enum and lookup table are disconnected |
| **BUG-02** | B11.1.1: `AppointmentUpdateDto` has no `AppointmentStatus` field. Status frozen at creation. | **CONFIRMED** |
| **ARC-03** | B6.2.1: Patient registration sets Gender=Male(1), DateOfBirth=UtcNow automatically | **CONFIRMED** (observed in seed data) |
| **FEAT-01** | B11.1.1: No status transition mechanism. B10.1.1-13: All 13 statuses set only at creation. | **CONFIRMED** |
| **FEAT-02** | B11.6.1: Claim Examiner role exists but has no dedicated endpoints, entity, or UI | **CONFIRMED** |

### New Issues Found During Testing

| Issue ID | Description | Severity | Evidence |
|----------|-------------|----------|----------|
| **BUG-09** | Past-date appointments accepted without validation | Medium | E1: Created appointment on 2026-01-15 (past) - accepted |
| **BUG-10** | `fromTime > toTime` accepted on slot creation | Medium | E7, B7.4.2: Slot with fromTime=15:00, toTime=14:00 accepted |
| **SEC-05** | Password policy fully relaxed | High | B13.4.1: Password `abc123` (no uppercase, no special char) accepted. All RequireX settings disabled. |

### Issues Not Reproducible in Current Testing

| Issue ID | Notes |
|----------|-------|
| **DAT-02** | Duplicate confirmation numbers not produced in serial testing. Concurrent test (B15) also didn't trigger. Would require higher concurrency load. |
| **DAT-04** | Tenant creation succeeded consistently. Would need failure injection to test. |
| **DAT-06** | `sys.dm_db_missing_index_details` returned 0 missing indexes. May appear under higher load. |
| **BUG-01** | Slot conflict detection logic - not tested via API (requires GeneratePreview with specific overlapping scenarios). Code analysis confirms inverted logic. |
| **BUG-03** | Doctor availability lookup filter - not directly tested. Code analysis confirms `TimeOnly != null` is always true. |

---

## Database State After Testing

Queried via direct SQL on `(LocalDb)\MSSQLLocalDB;Database=CaseEvaluation`:

| Table | Active Records | Notes |
|-------|---------------|-------|
| AppStates | 12 | 10 seeded + 2 from previous runs |
| AppAppointmentTypes | 7 | 6 seeded + 1 test artifact |
| AppAppointmentStatuses | 14 | 13 seeded + 1 test artifact |
| AppAppointmentLanguages | 13 | 12 seeded + 1 test artifact |
| AppLocations | 8 | As seeded |
| AppWcabOffices | 7 | As seeded |
| AppDoctors | 28 | 5 from seeding + 23 from test registrations (including test tenants) |
| AppDoctorAvailabilities | 209 | As seeded (test slots cleaned up) |
| AppAppointments | 27 | 28 seeded - 1 deleted by B11 test |
| AppPatients | 25 | 7 seeded + 18 from test registrations |
| AppApplicantAttorneys | 4 | 3 seeded + 1 from B9 test |
| AppAppointmentEmployerDetails | 20 | 19 seeded + 1 from B9 test |
| AppAppointmentAccessors | 88 | 48 seeded + 40 from test registrations |

**Observations**:
- Soft delete working correctly (IsDeleted filter active)
- All seeded records have non-null CreatorId and CreationTime (audit trail complete)
- No orphaned slots (all have TenantId)
- No duplicate confirmation numbers within same tenant
- Cross-tenant email duplicates exist (expected - emails scoped by tenant)

---

## Test Scripts Location

All test infrastructure in `scripts/`:
- `Master-Seed.ps1` - Plan A orchestrator (data seeding)
- `Master-Test.ps1` - Plan B orchestrator (E2E testing)
- `Exploratory-Tests.ps1` - Additional probing tests
- `Remove-SeedData.ps1` - Cleanup (reverse tier order)
- `Test-Report.md` - Generated test report
- `seed-state.json` - All created entity GUIDs
- `helpers/` - Shared auth, API, assertion, fake data utilities
- `phases/` - A1-A6 seeding phase scripts
- `tests/` - B1-B16 test phase scripts
