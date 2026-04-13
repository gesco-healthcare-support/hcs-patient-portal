[Home](../INDEX.md) > Test Catalog

# Test Catalog & User Flow Coverage

> **Last Run**: 2026-04-02 | **258 automated + 11 exploratory** | **246 passed, 0 unexpected failures**
> 
> **How to run**: `cd scripts && powershell -File Master-Seed.ps1 -SkipPrerequisites` then `powershell -File Master-Test.ps1 -SkipPrerequisites`

---

## At a Glance

```
Plan A (Seed)       Plan B (Test)         Exploratory
     |                    |                    |
  6 phases           16 phases           11 scenarios
  ~370 records       258 tests           18 findings
  17.5 seconds       16.6 seconds        ~10 seconds
     |                    |                    |
     +--------+-----------+----------+---------+
              |                      |
        seed-state.json        Test-Report.md
```

| Result | Count | Meaning |
|--------|-------|---------|
| PASS | 246 | Test passed as expected |
| FAIL-EXPECTED | 5 | Known handover gap confirmed (documented) |
| SKIP | 7 | Build tests disabled or manual-only |
| FAIL (unexpected) | 0 | Nothing broken beyond known gaps |

---

## Phase Map

Each phase tests a specific layer or user flow. Click to jump to details.

```
B1  Infrastructure    Are the services even running?
B2  Authentication    Can every role log in? Are tokens correct?
 |
B3  Reference CRUD    States, Types, Statuses, Languages
B4  Locations/WCAB    Locations, WCAB Offices + Excel export
B5  Doctors           Nav props, many-to-many, lookups
B6  External Signup   Registration for all 4 user types
 |
B7  Availability      Slot CRUD, filters, preview, tenant isolation
B8  Appointments      <<< CRITICAL PATH >>> Booking, validation, filtering
B9  Child Entities    Employer details, accessors, applicant attorneys
 |
B10 Business Logic    Status creation, confirmation numbers, tenant flow
B11 Known Gaps        Intentional tests for 7 documented gaps
B12 Multi-Tenancy     Tenant isolation, cross-tenant blocking
B13 Security          RBAC matrix, privilege escalation, passwords
B14 Data Integrity    Soft delete, audit trail, FK constraints
B15 Concurrency       Race conditions, optimistic locking
B16 Credentials       Secrets audit, SSL certs, OAuth config
 |
E1-E11 Exploratory    Edge cases, past dates, config audit
```

---

## B1: Infrastructure & Build Verification

> **What it proves**: The environment is correctly set up and all services respond.

| ID | Test | What It Validates |
|----|------|-------------------|
| B1.1.1-5 | Build tests | `dotnet restore`, `dotnet build`, `npm install`, `ng build`, `dotnet test` *(skipped by default)* |
| B1.2.1 | DB schema | All 16 custom `App*` tables exist in LocalDB |
| B1.2.2 | AbpUsers | Identity tables are present |
| B1.2.3 | Admin user | `admin@abp.io` seeded by DbMigrator |
| B1.2.4 | OAuth clients | `CaseEvaluation_App` and `CaseEvaluation_Swagger` registered in OpenIddict |
| B1.2.5 | Custom roles | Patient, Claim Examiner, Applicant Attorney, Defense Attorney exist |
| B1.3.1-2 | AuthServer | OpenID config + token endpoint respond |
| B1.3.3 | API Host | Application configuration endpoint responds |
| B1.3.4 | Health check | `/health-status` returns Healthy |
| B1.3.5 | Swagger | Swagger UI loads at `/swagger/index.html` |
| B1.3.6 | JWKS | JSON Web Key Set endpoint responds |
| B1.4.1-2 | Redis | Confirmed disabled in both AuthServer and API Host |

---

## B2: Authentication & Token Flow

> **What it proves**: Every role can authenticate and tokens contain correct claims.

| ID | Test | User Flow |
|----|------|-----------|
| B2.1.1 | Host admin login | `admin@abp.io` gets access + refresh tokens |
| B2.1.2 | Tenant admin login | Doctor's tenant admin gets token with `tenantid` claim |
| B2.1.3 | Patient login | Patient user gets token with `Patient` role |
| B2.1.4 | Attorney login | Applicant Attorney gets valid token |
| B2.1.5 | Token refresh | Refresh token produces new access token |
| B2.1.6-8 | Failure cases | Wrong password, fake user, empty username all return 400 |
| B2.2.1-4 | JWT claims | `sub`, `role`, `tenantid` claims verified per role |
| B2.3.1 | No auth | Unauthenticated request gets 401 |
| B2.3.2 | Admin access | Host admin can access all entities |
| B2.3.3 | Patient blocked | Patient cannot delete reference data |
| B2.3.4 | Tenant header | ABP uses `__tenant` header for tenant resolution |

---

## B3: Reference Data CRUD

> **What it proves**: All 4 lookup entities support full CRUD with validation.

**Tested for each of**: States (10), AppointmentTypes (6), AppointmentStatuses (13), AppointmentLanguages (12)

| Pattern | What It Validates |
|---------|-------------------|
| GET list | Seeded count matches expected |
| GET by ID | Single record retrieval works |
| POST create | New record created with generated ID |
| PUT update | Name change persists |
| DELETE | Soft delete works |
| GET deleted | Returns 404 (filtered by IsDeleted) |
| Filter | `filterText` query parameter works |
| Pagination | Page 2 has different items than page 1 |
| Empty name | Returns 400 validation error |
| Too-long name | Behavior documented (some entities accept, some reject) |

**11 tests x 4 entities = 44 tests**

---

## B4: Locations & WCAB Offices

> **What it proves**: Location management with isActive filter, navigation properties, Excel export.

| ID | Test | What It Validates |
|----|------|-------------------|
| B4.1.1-2 | List + GET | 8 locations, single retrieval |
| B4.1.3-4 | Active filter | `isActive=true` returns 6, `isActive=false` returns 1 |
| B4.1.5 | Nav properties | `with-navigation-properties` includes State object |
| B4.1.6 | State lookup | Dropdown data endpoint for location forms |
| B4.1.7-9 | CRUD cycle | Create with null FK, delete, verify 404 |
| B4.1.10-11 | Name boundary | 50 chars OK, 51 chars rejected |
| B4.2.1-4 | WCAB CRUD | List, GET, create, active filter |
| B4.2.5-6 | Excel export | `download-token` + `as-excel-file` endpoints |
| B4.2.7-8 | Abbreviation | 50 chars OK, 51 chars rejected |

---

## B5: Doctors

> **What it proves**: Doctor entity with many-to-many relationships (types, locations) works correctly.

| ID | Test | What It Validates |
|----|------|-------------------|
| B5.1.1 | List | 5 doctors visible from host context |
| B5.1.2-3 | T1 nav props | AppointmentTypes and Locations populated |
| B5.1.4 | T4 edge case | Single-location doctor has exactly 1 |
| B5.1.5 | T5 edge case | All-locations doctor has all 6 |
| B5.2.1-4 | M:M update | Change types, verify, restore original state |
| B5.3.1-3 | Lookups | identity-user, appointment-type, location lookups |
| B5.4.1 | Email max | EmailMaxLength = 49 (unusual constraint documented) |

---

## B6: External Signup

> **What it proves**: Public registration flow for all 4 user types with failure handling.

| ID | Test | User Flow |
|----|------|-----------|
| B6.1.1-2 | Tenant options | Anonymous endpoint returns doctor tenant list |
| B6.1.3 | Filter | Filter by name works |
| B6.1.4 | Tenant scope | With `__tenant` header returns empty (expected) |
| B6.2.1-4 | Register all types | Patient, Claim Examiner, Applicant Attorney, Defense Attorney |
| B6.2.5-8 | Verify created | Each user exists in identity system with correct role |
| B6.3.1 | Duplicate email | Returns 4xx error in same tenant |
| B6.3.2 | Cross-tenant | Same email succeeds in different tenant |
| B6.3.3-6 | Failures | Invalid email, short password, empty name, bad userType |
| B6.4.1-2 | User lookup | Authenticated lookup with filter |

---

## B7: Doctor Availability

> **What it proves**: Slot management with filters, preview, and tenant isolation.

| ID | Test | What It Validates |
|----|------|-------------------|
| B7.1.1-6 | CRUD | List, GET, create, update, delete, 404 |
| B7.2.1-4 | Filters | By status, location, date range |
| B7.3.1 | Preview | GeneratePreview returns slots without creating them |
| B7.4.1 | Reserved | Can create slot with Reserved(10) status |
| B7.4.2 | **Gap C1.7** | `fromTime > toTime` accepted (no validation) |
| B7.5.1 | Tenant isolation | T2 context shows only T2 slots |

---

## B8: Appointment Booking (Critical Path)

> **What it proves**: The core business flow -- finding a slot, booking it, validation, filtering.

### Happy Path
| ID | Test | Step in User Flow |
|----|------|-------------------|
| B8.1.1 | POST booking | Staff selects slot, patient, type, location -- appointment created |
| B8.1.2 | Confirmation # | Auto-generated as A + 5 digits (e.g., A00014) |
| B8.1.3 | Slot booked | Selected slot changes from Available(8) to Booked(9) |
| B8.1.4 | Nav properties | Appointment includes Patient, User, Type, Location, Slot |

### Validation (12 negative tests)
| ID | Test | What Breaks |
|----|------|-------------|
| B8.2.1-5 | Empty GUIDs | Each required FK field returns 4xx when empty |
| B8.2.6-10 | Fake GUIDs | Non-existent patient, user, type, location, slot |
| B8.2.11 | Wrong location | Slot's location doesn't match selected location |
| B8.2.12 | Wrong date | Appointment date doesn't match slot's available date |

### List Filtering
| B8.3.1-4 | Filters | By identityUserId, appointmentTypeId, date range, empty tenant |

### Attorney Linking
| B8.4.1-2 | AA lookup | GET attorney for appointment, details-for-booking endpoint |

### Update
| B8.5.1 | Change panel | PUT updates PanelNumber successfully |
| B8.5.2 | Stale stamp | Concurrent update with old stamp returns 409 |

---

## B9: Child Entities

> **What it proves**: Appointment extensions (employer, accessors, attorneys) CRUD correctly.

| Entity | Tests | Key Validations |
|--------|-------|-----------------|
| EmployerDetails | 7 | CRUD + boundary (255 OK, 256 fail) |
| Accessors | 3 | GET by appointment, POST View(23), DELETE |
| ApplicantAttorneys | 4 | CRUD + firmName boundary (50 OK, 51 fail) |

---

## B10: Business Logic

> **What it proves**: Core business rules work -- status creation, numbering, tenant provisioning, patient management.

| ID | Test | Business Rule |
|----|------|---------------|
| B10.1.1-13 | Status creation | All 13 statuses accepted at creation time |
| B10.4.1-2 | Confirmation #s | Sequential, unique, format A#####, custom value ignored |
| B10.5.1-3 | Tenant flow | Create tenant -> doctor auto-created -> cleanup |
| B10.6.1-2 | GetOrCreate | New patient created, same email returns existing (idempotent) |

---

## B11: Known Handover Gaps

> **What it proves**: Documented gaps are real, not just theoretical.

| ID | Gap | Result |
|----|-----|--------|
| B11.1.1 | **C1.1** No status transitions | FAIL-EXPECTED: PUT has no status field |
| B11.1.2 | AppointmentApproveDate | PASS: Null even on Approved appointments |
| B11.2.1 | **C1.2** No slot release | CONFIRMED: Slot stays Booked(9) after DELETE |
| B11.4.1 | **C1.4** Orphaned fields | PASS: InternalUserComments always null |
| B11.5.1 | **C1.5** No file upload | FAIL-EXPECTED: No endpoint exists |
| B11.6.1 | **C1.6** CE no logic | FAIL-EXPECTED: No CE-specific anything |
| B11.7.1 | **C1.7** Time validation | FAIL-EXPECTED: fromTime > toTime not validated |

---

## B12: Multi-Tenancy & Data Isolation

> **What it proves**: Tenant boundaries hold -- data can't leak between doctors.

| ID | Test | Isolation Rule |
|----|------|----------------|
| B12.1.1-3 | Appointment isolation | T1 sees only T1, T2 sees only T2, no overlap |
| B12.1.4 | Header override | `__tenant` header determines context (ABP design) |
| B12.2.1-3 | Host data shared | States, Locations, Types visible from all tenants |
| B12.3.1-3 | Email scoping | Same email allowed in different tenants, blocked in same |
| B12.4.1 | Cross-tenant booking | T2 slot rejected in T1 booking context |
| B12.5.1-2 | Tenant options | Empty with header, full list without |

---

## B13: Security & Authorization

> **What it proves**: Role permissions enforced, endpoints protected, policy documented.

| ID | Test | Security Check |
|----|------|----------------|
| B13.1.1 | No auth | All /api/app/* return 401 |
| B13.1.2 | Public endpoint | Tenant options accessible anonymously |
| B13.1.3-6 | Patient role | Can read own data, blocked from admin actions |
| B13.1.7 | AA role | Can read appointments |
| B13.1.8 | DA role | Cannot delete appointments |
| B13.2.1 | CSRF bypass | Anonymous registration works (intentional) |
| B13.3.1-2 | Concurrency | Correct stamp -> 200, stale stamp behavior documented |
| B13.4.1 | Password policy | Weak passwords accepted (HIGH risk documented) |

---

## B14: Data Integrity

> **What it proves**: Database-level correctness via direct SQL queries.

| ID | Test | What It Verifies |
|----|------|-------------------|
| B14.1.1-3 | Soft delete | DELETE sets IsDeleted=1, row persists, API returns 404 |
| B14.2.1-2 | Audit trail | CreationTime and CreatorId populated on all records |
| B14.3.1 | FK behavior | Soft delete on referenced entity doesn't cascade |
| B14.4.1-2 | Distribution | Appointments span 3+ tenants, 20+ total |

---

## B15: Concurrency & Race Conditions

> **What it proves**: How the system behaves under simultaneous requests.

| ID | Test | What Happened |
|----|------|---------------|
| B15.1.1 | Dual booking | Two jobs tried same slot -- one succeeded, one failed (EF Core transaction won) |
| B15.3.1-2 | Dual update | First PUT succeeded, second PUT also succeeded (no 409) |

---

## B16: Credentials & License Audit

> **What it proves**: Configuration state and security posture of the deployment.

| ID | Test | Finding |
|----|------|---------|
| B16.1.1-2 | ABP license | NuGet API key present, CLI reports login |
| B16.2.1-2 | Secrets | Encryption passphrase and PFX password in source (HIGH risk) |
| B16.3.1-3 | SSL certs | `openiddict.pfx` exists in both projects, loadable with configured password |
| B16.4.1-2 | OAuth | Both OpenIddict clients registered |
| B16.5.1 | Locking | ConfigureDistributedLocking registered without IsDevelopment guard |
| B16.6.1 | PII | `DisablePII: false` -- sensitive data appears in logs |

---

## Exploratory Tests

> **Beyond B1-B16**: Edge cases and probes that don't fit in any phase.

| ID | Scenario | Finding |
|----|----------|---------|
| E1 | Past-date appointment | **Accepted** -- no date validation (BUG-09) |
| E2 | Custom confirmation # | Correctly overridden to auto-generated |
| E3 | Patient /me | Works correctly |
| E4 | User lookup exposure | **8 users with PII exposed** to Patient role (SEC-03) |
| E5 | Weak password | Policy relaxed -- `123456` accepted (SEC-05) |
| E6 | Slot release | **Stays Booked** after DELETE (DAT-03/C1.2) |
| E7 | Invalid time range | **fromTime > toTime accepted** (BUG-10/C1.7) |
| E8 | Anonymous access | All /api/app/* correctly return 401 |
| E9 | Privilege escalation | Patient correctly blocked from admin endpoints |
| E10 | SQL integrity | 0 orphans, 0 null audit fields, 0 duplicate conf#s |
| E11 | Config audit | 4 secrets in config, wildcard CORS |

---

## What's NOT Tested (Frontend / Manual)

These require browser testing and are NOT covered by the automated suite:

- [ ] Angular login/logout flow with AuthServer redirect
- [ ] Appointment booking multi-step form (patient search, slot selection, attorney lookup)
- [ ] Availability generation UI (date range, preview, conflict highlighting)
- [ ] Patient self-service profile page
- [ ] Role-specific home page views (admin vs patient vs attorney)
- [ ] ABP admin modules (Identity, SaaS, Audit Logs, Settings)
- [ ] Excel export download in browser
- [ ] Sidebar visibility based on role (internal vs external)

---

## How to Add New Tests

1. Create a new `.ps1` file in `scripts/tests/` following the B-prefix pattern
2. Accept params: `$TestResults` (ArrayList), `$SeedState` (hashtable), `$ApiBaseUrl`, `$AuthServerUrl`
3. Use `Assert-*` functions from `scripts/helpers/Assert-Response.ps1`
4. Use `Invoke-TestApiCall` for API calls (captures status codes instead of throwing)
5. Add the phase to `Master-Test.ps1`'s `Invoke-TestPhase` chain
6. Run: `powershell -File Master-Test.ps1 -StartFromPhase BXX`
