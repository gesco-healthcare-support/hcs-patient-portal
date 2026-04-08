# HCS Case Evaluation Portal - E2E Test Report
**Date**: 2026-04-02 16:56:33
**Tester**: Automated (Plan B)
**Seed State**: scripts/seed-state.json

## Summary

| Category | Total | Passed | Failed Unexpected | Failed Expected | Skipped |
|----------|-------|--------|-------------------|-----------------|---------|
| B1 | 18 | 13 | 0 | 0 | 5 |
| B10 | 20 | 20 | 0 | 0 | 0 |
| B11 | 7 | 3 | 0 | 4 | 0 |
| B12 | 13 | 13 | 0 | 0 | 0 |
| B13 | 12 | 12 | 0 | 0 | 0 |
| B14 | 8 | 8 | 0 | 0 | 0 |
| B15 | 3 | 3 | 0 | 0 | 0 |
| B16 | 12 | 11 | 0 | 0 | 8 |
| B2 | 17 | 16 | 0 | 0 | 8 |
| B3 | 44 | 44 | 0 | 0 | 0 |
| B4 | 19 | 19 | 0 | 0 | 0 |
| B5 | 13 | 13 | 0 | 0 | 0 |
| B6 | 20 | 20 | 0 | 0 | 0 |
| B7 | 14 | 13 | 0 | 8 | 0 |
| B8 | 24 | 24 | 0 | 0 | 0 |
| B9 | 14 | 14 | 0 | 0 | 0 |
| **TOTAL** | **258** | **246** | **0** | **5** | **7** |

## UNEXPECTED FAILURES - Investigate / Contact Previous Developer

None! All non-gap tests passed.

## EXPECTED FAILURES - Confirmed Handover Gaps

| Test ID | Gap ID | Description | Details |
|---------|--------|-------------|---------|
| B7.4.2 | C1.7 | fromTime > toTime behavior | Status: 200. fromTime > toTime should fail but may succeed (C1.7) |
| B11.1.1 | C1.1 | AppointmentUpdateDto lacks AppointmentStatus field | Confirmed: PUT cannot change appointment status. Status is set only at creation. |
| B11.5.1 | C1.5 | No file upload endpoint | Status: 400. File upload endpoint responded with 400 |
| B11.6.1 | C1.6 | Claim Examiner has no CE-specific endpoints | CE role exists but has no dedicated entity, service, or UI. Functions as a viewer role. |
| B11.7.1 | C1.7 | FromTime > ToTime validation gap | CreateAsync does not validate fromTime vs toTime. Only GeneratePreview validates this. |

## Detailed Test Results

### B1

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B1.1.1 | dotnet restore | SKIP | IncludeBuildTests not set |
| B1.1.2 | dotnet build | SKIP | IncludeBuildTests not set |
| B1.1.3 | npm install | SKIP | IncludeBuildTests not set |
| B1.1.4 | Angular build | SKIP | IncludeBuildTests not set |
| B1.1.5 | dotnet test | SKIP | IncludeBuildTests not set |
| B1.2.1 | All 16 App* tables exist | PASS | Found 17 tables. Missing:  |
| B1.2.2 | AbpUsers table exists | PASS |  |
| B1.2.3 | admin@abp.io user exists | PASS |  |
| B1.2.4 | OpenIddict clients registered | PASS | CaseEvaluation_App: True, CaseEvaluation_Swagger: True |
| B1.2.5 | Custom roles seeded | PASS | Actual: 112 >= Expected: 4 |
| B1.3.1 | AuthServer OpenID configuration | PASS | Status: 200 |
| B1.3.2 | AuthServer token_endpoint present | PASS | Value present |
| B1.3.3 | API Host application configuration | PASS | Status: 200 |
| B1.3.4 | Health check endpoint | PASS | Status: 200 |
| B1.3.5 | Swagger UI loads | PASS | Status: 200 |
| B1.3.6 | JWKS endpoint responds | PASS | Status: 200 |
| B1.4.1 | AuthServer Redis disabled | PASS | Checked appsettings.json for Redis.IsEnabled: false |
| B1.4.2 | API Host Redis disabled | PASS | Checked appsettings.json for Redis.IsEnabled: false |

### B10

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B10.1.1 | Create with status Pending(1) | PASS | Status: 200 |
| B10.1.2 | Create with status Approved(2) | PASS | Status: 200 |
| B10.1.3 | Create with status Rejected(3) | PASS | Status: 200 |
| B10.1.4 | Create with status NoShow(4) | PASS | Status: 200 |
| B10.1.5 | Create with status CancelledNoBill(5) | PASS | Status: 200 |
| B10.1.6 | Create with status CancelledLate(6) | PASS | Status: 200 |
| B10.1.7 | Create with status RescheduledNoBill(7) | PASS | Status: 200 |
| B10.1.8 | Create with status RescheduledLate(8) | PASS | Status: 200 |
| B10.1.9 | Create with status CheckedIn(9) | PASS | Status: 200 |
| B10.1.10 | Create with status CheckedOut(10) | PASS | Status: 200 |
| B10.1.11 | Create with status Billed(11) | PASS | Status: 200 |
| B10.1.12 | Create with status RescheduleRequested(12) | PASS | Status: 200 |
| B10.1.13 | Create with status CancellationRequested(13) | PASS | Status: 200 |
| B10.4.1 | Confirmation numbers unique | PASS | Expected: 3 |
| B10.4.2 | Confirmation format A+5 digits | PASS | All valid: A00014, A00015, A00016 |
| B10.5.1 | Create test tenant | PASS | Status: 200 |
| B10.5.2 | New tenant has doctor | PASS | Actual: 28 >= Expected: 1 |
| B10.5.3 | Delete test tenant | PASS | Status: 200 |
| B10.6.1 | GetOrCreatePatient - new | PASS | Status: 200 |
| B10.6.2 | GetOrCreatePatient - idempotent | PASS | Expected: b54a6a6f-40e0-18ee-487d-3a2062abdc40 |

### B11

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B11.1.1 | AppointmentUpdateDto lacks AppointmentStatus field | FAIL-EXPECTED | Confirmed: PUT cannot change appointment status. Status is set only at creation. |
| B11.1.2 | AppointmentApproveDate is null | PASS | Value is null as expected |
| B11.2.1 | Slot stays Booked after appointment DELETE | PASS | Expected: 9 |
| B11.4.1 | InternalUserComments never populated | PASS | Value is null as expected |
| B11.5.1 | No file upload endpoint | FAIL-EXPECTED | Status: 400. File upload endpoint responded with 400 |
| B11.6.1 | Claim Examiner has no CE-specific endpoints | FAIL-EXPECTED | CE role exists but has no dedicated entity, service, or UI. Functions as a viewer role. |
| B11.7.1 | FromTime > ToTime validation gap | FAIL-EXPECTED | CreateAsync does not validate fromTime vs toTime. Only GeneratePreview validates this. |

### B12

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B12.1.1 | T1 sees T1 appointments | PASS | Actual: 12 >= Expected: 1 |
| B12.1.2 | T2 sees T2 appointments | PASS | Actual: 10 >= Expected: 1 |
| B12.1.3 | T1 IDs not visible in T2 | PASS | Expected: 0 |
| B12.1.4 | T1 token + T2 header: ABP uses __tenant header | PASS | ABP tenant resolution uses __tenant header. Status: 200, Count: 12 |
| B12.2.1 | States visible from T1 context | PASS | Actual: 12 >= Expected: 10 |
| B12.2.2 | Locations visible from T1 | PASS | Actual: 8 >= Expected: 8 |
| B12.2.3 | AppointmentTypes visible from T2 | PASS | Actual: 7 >= Expected: 6 |
| B12.3.1 | Register email in T1 | PASS | Status: 200 |
| B12.3.2 | Same email in T2 succeeds | PASS | Status: 200 |
| B12.3.3 | Duplicate in T1 returns 4xx error | PASS | Status: 403 |
| B12.4.1 | Cross-tenant booking blocked (400) | PASS | Status: 400 |
| B12.5.1 | Tenant options with __tenant returns empty | PASS | Expected: 0 |
| B12.5.2 | Tenant options without header >= 5 | PASS | Actual: 5 >= Expected: 5 |

### B13

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B13.1.1 | No auth returns 401 | PASS | Status: 401 |
| B13.1.2 | Anonymous on public endpoint returns 200 | PASS | Status: 200 |
| B13.1.3 | Patient can GET appointments | PASS | Status: 200 |
| B13.1.4 | Patient cannot DELETE states (403) | PASS | Status: 403 |
| B13.1.5 | Patient cannot POST availability (403) | PASS | Status: 403 |
| B13.1.6 | Patient can GET /patients/me | PASS | Status: 200 |
| B13.1.7 | Applicant Attorney can GET appointments | PASS | Status: 200 |
| B13.1.8 | Defense Attorney cannot DELETE appointments (403) | PASS | Status: 403 |
| B13.2.1 | Anonymous registration without CSRF token | PASS | Status: 200 |
| B13.3.1 | Correct concurrencyStamp -> 200 | PASS | Status: 200 |
| B13.3.2 | Stale concurrencyStamp behavior documented | PASS | Status: 200. No conflict on same-data PUT |
| B13.4.1 | Weak password policy check | PASS | Password 'abc123' rejected (Status: 403). Policy may be stricter than expected. |

### B14

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B14.1.1 | State DELETE returns success | PASS |  |
| B14.1.2 | Soft-deleted row exists with IsDeleted=1 | PASS | Rows: 1, IsDeleted: True |
| B14.1.3 | Soft-deleted returns 404 via API | PASS | Status: 404 |
| B14.2.1 | Appointment CreationTime populated | PASS | CreationTime: 04/02/2026 16:55:52 |
| B14.2.2 | Appointment CreatorId populated | PASS |  |
| B14.3.1 | DELETE California (FK referenced) | PASS | Status: 200. Soft delete succeeded despite FK references (ABP global filter) (restored via SQL) |
| B14.4.1 | Appointments distributed across tenants | PASS | Actual: 3 >= Expected: 2 |
| B14.4.2 | Total appointments >= 20 | PASS | Actual: 27 >= Expected: 20 |

### B15

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B15.1.1 | Simultaneous slot booking | PASS | One succeeded, one failed (correct behavior). Job1: 200, Job2: 403 |
| B15.3.1 | First concurrent update succeeds | PASS | Status: 200 |
| B15.3.2 | Second concurrent update behavior | PASS | Status: 200. No conflict on same-data PUT |

### B16

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B16.1.1 | NuGet.Config has ABP API key | PASS | Key present: True. MEDIUM risk - key in source control. |
| B16.1.2 | ABP CLI login-info | PASS | Output: ABP CLI 10.2.0  Login info:  |
| B16.2.1 | StringEncryption passphrase in source | PASS | Placeholder 'REPLACE_ME_LOCALLY' found in appsettings.json. Set real value in appsettings.Local.json. |
| B16.2.2 | OpenIddict PFX passphrase in source | PASS | HIGH RISK: Certificate passphrase in appsettings.json. |
| B16.2.3 | Docker SA password | SKIP | docker-compose.yml not found |
| B16.3.1 | openiddict.pfx exists in AuthServer | PASS | Path: P:\Patient Appointment Portal\hcs-case-evaluation-portal\src\HealthcareSupport.CaseEvaluation.AuthServer\openiddic... |
| B16.3.2 | openiddict.pfx exists in HttpApi.Host | PASS | Path: P:\Patient Appointment Portal\hcs-case-evaluation-portal\src\HealthcareSupport.CaseEvaluation.HttpApi.Host\openidd... |
| B16.3.3 | PFX loadable with configured password | PASS | Subject: CN=localhost, Expires: 03/31/2027 19:26:24 |
| B16.4.1 | CaseEvaluation_App client registered | PASS |  |
| B16.4.2 | CaseEvaluation_Swagger client registered | PASS |  |
| B16.5.1 | Distributed locking configuration | PASS | ConfigureDistributedLocking: True, IsDevelopment guard: False WARNING: No IsDevelopment guard. Lock operations may fail ... |
| B16.6.1 | PII logging enabled (DisablePII: false) | PASS | HIGH RISK: PII (emails, names, SSNs) will appear in logs. Change to true for production. |

### B2

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B2.1.1 | Host admin password grant | PASS | Token length: 1396 |
| B2.1.2 | Tenant admin password grant | PASS | Tenant: T1 |
| B2.1.3 | Patient user token | PASS | Patient: min.wilson@hcs.test |
| B2.1.4 | Applicant Attorney user token | PASS | AA: robert.wang@hcs.test |
| B2.1.5 | Refresh token flow | PASS | New token acquired via refresh |
| B2.1.6 | Wrong password returns error | PASS | Status: 400 |
| B2.1.7 | Non-existent user returns error | PASS | Status: 400 |
| B2.1.8 | Empty username returns error | PASS | Status: 400 |
| B2.2.1 | Host admin token has 'sub' claim | PASS | Value present |
| B2.2.2 | Host admin token has 'admin' role | PASS | Roles: admin |
| B2.2.3 | Tenant admin token has 'tenantid' claim | PASS | Value present |
| B2.2.4 | Patient token has 'Patient' role | PASS | Roles: Patient |
| B2.3.1 | Unauthenticated request returns 401 | PASS | Status: 401 |
| B2.3.2 | Admin can access entities | PASS | Status: 200 |
| B2.3.3 | Patient cannot delete states (403) | PASS | Status: 403 |
| B2.3.4 | Cross-tenant: __tenant header overrides token context | PASS | ABP uses __tenant header for tenant resolution. Status: 200, Count: 13 |
| B2.4.1 | Swagger OAuth flow | SKIP | Manual test - verify in browser |

### B3

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B3.1.1 | States list count >= 10 | PASS | Actual: 12 >= Expected: 10 |
| B3.1.2 | States GET by ID | PASS | Status: 200 |
| B3.1.3 | States POST create | PASS | Status: 200 |
| B3.1.4 | States PUT update | PASS | Status: 200 |
| B3.1.5 | States GET shows updated name | PASS | Expected: B3UpdatedState |
| B3.1.6 | States DELETE | PASS | Status: 200 |
| B3.1.7 | States GET deleted returns 404 | PASS | Status: 404 |
| B3.1.8 | States GET with filter | PASS | Status: 200 |
| B3.1.9 | States pagination works | PASS | Page1: 3 items, Page2: 3 items |
| B3.1.10 | States POST empty name returns 400 | PASS | Status: 400 |
| B3.1.11 | States POST too-long name behavior | PASS | Status: 200. No max-length constraint |
| B3.2.1 | AppointmentTypes list count >= 6 | PASS | Actual: 7 >= Expected: 6 |
| B3.2.2 | AppointmentTypes GET by ID | PASS | Status: 200 |
| B3.2.3 | AppointmentTypes POST create | PASS | Status: 200 |
| B3.2.4 | AppointmentTypes PUT update | PASS | Status: 200 |
| B3.2.5 | AppointmentTypes GET shows updated name | PASS | Expected: B3UpdatedType |
| B3.2.6 | AppointmentTypes DELETE | PASS | Status: 200 |
| B3.2.7 | AppointmentTypes GET deleted returns 404 | PASS | Status: 404 |
| B3.2.8 | AppointmentTypes GET with filter | PASS | Status: 200 |
| B3.2.9 | AppointmentTypes pagination works | PASS | Page1: 3 items, Page2: 3 items |
| B3.2.10 | AppointmentTypes POST empty name returns 400 | PASS | Status: 400 |
| B3.2.11 | AppointmentTypes POST too-long name behavior | PASS | Status: 400. Validation applied |
| B3.3.1 | AppointmentStatuses list count >= 13 | PASS | Actual: 14 >= Expected: 13 |
| B3.3.2 | AppointmentStatuses GET by ID | PASS | Status: 200 |
| B3.3.3 | AppointmentStatuses POST create | PASS | Status: 200 |
| B3.3.4 | AppointmentStatuses PUT update | PASS | Status: 200 |
| B3.3.5 | AppointmentStatuses GET shows updated name | PASS | Expected: B3UpdatedStatus |
| B3.3.6 | AppointmentStatuses DELETE | PASS | Status: 200 |
| B3.3.7 | AppointmentStatuses GET deleted returns 404 | PASS | Status: 404 |
| B3.3.8 | AppointmentStatuses GET with filter | PASS | Status: 200 |
| B3.3.9 | AppointmentStatuses pagination works | PASS | Page1: 3 items, Page2: 3 items |
| B3.3.10 | AppointmentStatuses POST empty name returns 400 | PASS | Status: 400 |
| B3.3.11 | AppointmentStatuses POST too-long name behavior | PASS | Status: 400. Validation applied |
| B3.4.1 | AppointmentLanguages list count >= 12 | PASS | Actual: 13 >= Expected: 12 |
| B3.4.2 | AppointmentLanguages GET by ID | PASS | Status: 200 |
| B3.4.3 | AppointmentLanguages POST create | PASS | Status: 200 |
| B3.4.4 | AppointmentLanguages PUT update | PASS | Status: 200 |
| B3.4.5 | AppointmentLanguages GET shows updated name | PASS | Expected: B3UpdatedLanguage |
| B3.4.6 | AppointmentLanguages DELETE | PASS | Status: 200 |
| B3.4.7 | AppointmentLanguages GET deleted returns 404 | PASS | Status: 404 |
| B3.4.8 | AppointmentLanguages GET with filter | PASS | Status: 200 |
| B3.4.9 | AppointmentLanguages pagination works | PASS | Page1: 3 items, Page2: 3 items |
| B3.4.10 | AppointmentLanguages POST empty name returns 400 | PASS | Status: 400 |
| B3.4.11 | AppointmentLanguages POST too-long name behavior | PASS | Status: 400. Validation applied |

### B4

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B4.1.1 | Locations count >= 8 | PASS | Actual: 8 >= Expected: 8 |
| B4.1.2 | Location GET by ID | PASS | Status: 200 |
| B4.1.3 | Active locations >= 6 | PASS | Actual: 7 >= Expected: 6 |
| B4.1.4 | Inactive locations >= 1 | PASS | Actual: 1 >= Expected: 1 |
| B4.1.5 | Location nav props include State | PASS | State present: True |
| B4.1.6 | Location state-lookup | PASS | Status: 200 |
| B4.1.7 | Location POST with null appointmentTypeId | PASS | Status: 200 |
| B4.1.8 | Location DELETE cleanup | PASS | Status: 200 |
| B4.1.9 | Location GET deleted returns 404 | PASS | Status: 404 |
| B4.1.10 | Location name 50 chars OK | PASS | Status: 200 |
| B4.1.11 | Location name 51 chars fails (400) | PASS | Status: 400 |
| B4.2.1 | WCAB Offices count >= 7 | PASS | Actual: 7 >= Expected: 7 |
| B4.2.2 | WCAB GET by ID | PASS | Status: 200 |
| B4.2.3 | WCAB POST create | PASS | Status: 200 |
| B4.2.4 | Active WCAB offices >= 6 | PASS | Actual: 6 >= Expected: 6 |
| B4.2.5 | WCAB download-token endpoint | PASS | Status: 200 |
| B4.2.6 | WCAB as-excel-file endpoint | PASS | Status: 200 |
| B4.2.7 | WCAB abbreviation 50 chars OK | PASS | Status: 200 |
| B4.2.8 | WCAB abbreviation 51 chars fails (400) | PASS | Status: 400 |

### B5

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B5.1.1 | Doctor list count >= 5 | PASS | Actual: 27 >= Expected: 5 |
| B5.1.2 | T1 doctor has AppointmentTypes | PASS | Types count: 4 |
| B5.1.3 | T1 doctor has Locations | PASS | Locations: 3 |
| B5.1.4 | T4 doctor has 1 location | PASS | Expected: 1 |
| B5.1.5 | T5 doctor has all locations | PASS | Actual: 6 >= Expected: 6 |
| B5.2.1 | Doctor PUT change types | PASS | Status: 200 |
| B5.2.2 | Doctor types updated correctly | PASS | Expected: 1 |
| B5.2.3 | Doctor restore original state | PASS | Status: 200 |
| B5.2.4 | Doctor types restored | PASS | Expected: 4 |
| B5.3.1 | Doctor identity-user-lookup | PASS | Status: 200 |
| B5.3.2 | Doctor appointment-type-lookup | PASS | Status: 200 |
| B5.3.3 | Doctor location-lookup | PASS | Status: 200 |
| B5.4.1 | Doctor EmailMaxLength = 49 documented | PASS | Constraint verified in DoctorConsts.EmailMaxLength = 49. Unusual constraint - may be a bug. |

### B6

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B6.1.1 | Tenant options returns 200 (anonymous) | PASS | Status: 200 |
| B6.1.2 | Tenant options returns >= 5 tenants | PASS | Actual: 5 >= Expected: 5 |
| B6.1.3 | Tenant options with filter | PASS | Status: 200 |
| B6.1.4 | Tenant-scoped options returns empty | PASS | Expected: 0 |
| B6.2.1 | Register Patient user | PASS | Status: 200, Email: b6.test.patient@b-test.hcs.test |
| B6.2.5 | Verify Patient user exists | PASS | Found: True |
| B6.2.2 | Register ClaimExaminer user | PASS | Status: 200, Email: b6.test.claimexaminer@b-test.hcs.test |
| B6.2.6 | Verify ClaimExaminer user exists | PASS | Found: True |
| B6.2.3 | Register ApplicantAttorney user | PASS | Status: 200, Email: b6.test.applicantattorney@b-test.hcs.test |
| B6.2.7 | Verify ApplicantAttorney user exists | PASS | Found: True |
| B6.2.4 | Register DefenseAttorney user | PASS | Status: 200, Email: b6.test.defenseattorney@b-test.hcs.test |
| B6.2.8 | Verify DefenseAttorney user exists | PASS | Found: True |
| B6.3.1 | Duplicate email returns 4xx error | PASS | Status: 403 |
| B6.3.2 | Same email different tenant succeeds | PASS | Status: 200 |
| B6.3.3 | Invalid email returns 400 | PASS | Status: 400 |
| B6.3.4 | Short password returns 400 | PASS | Status: 400 |
| B6.3.5 | Empty firstName returns 400 | PASS | Status: 400 |
| B6.3.6 | Invalid userType returns 4xx error | PASS | Status: 403 |
| B6.4.1 | External user lookup returns 200 | PASS | Status: 200 |
| B6.4.2 | External user lookup with filter | PASS | Status: 200 |

### B7

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B7.1.1 | T1 availability slots >= 10 | PASS | Actual: 64 >= Expected: 10 |
| B7.1.2 | Availability GET by ID | PASS | Status: 200 |
| B7.1.3 | Availability POST create | PASS | Status: 200 |
| B7.1.4 | Availability PUT update | PASS | Status: 200 |
| B7.1.5 | Availability DELETE | PASS | Status: 200 |
| B7.1.6 | Availability GET deleted 404 | PASS | Status: 404 |
| B7.2.1 | Filter by Available status | PASS | Status: 200 |
| B7.2.2 | Filter by Booked status | PASS | Status: 200 |
| B7.2.3 | Filter by locationId | PASS | Status: 200 |
| B7.2.4 | Filter by date range | PASS | Status: 200 |
| B7.3.1 | Availability preview | PASS | Status: 200 |
| B7.4.1 | Create Reserved(10) slot | PASS | Status: 200 |
| B7.4.2 | fromTime > toTime behavior | FAIL-EXPECTED | Status: 200. fromTime > toTime should fail but may succeed (C1.7) |
| B7.5.1 | T2 slots visible in T2 context | PASS | Actual: 63 >= Expected: 1 |

### B8

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B8.1.1 | Appointment POST booking | PASS | Status: 200 |
| B8.1.2 | Confirmation number format A+5 digits | PASS | Value 'A00014' matches /^A\d{5}$/ |
| B8.1.3 | Slot becomes Booked(9) | PASS | Expected: 9 |
| B8.1.4 | Appointment nav props populated | PASS | Patient present: True |
| B8.2.1 | Empty patientId returns 4xx | PASS | Status: 403 |
| B8.2.2 | Empty identityUserId returns 4xx | PASS | Status: 403 |
| B8.2.3 | Empty appointmentTypeId returns 4xx | PASS | Status: 403 |
| B8.2.4 | Empty locationId returns 4xx | PASS | Status: 403 |
| B8.2.5 | Empty doctorAvailabilityId returns 4xx | PASS | Status: 403 |
| B8.2.6 | Non-existent patientId returns 4xx | PASS | Status: 403 |
| B8.2.7 | Non-existent identityUserId returns 4xx | PASS | Status: 403 |
| B8.2.8 | Non-existent appointmentTypeId returns 4xx | PASS | Status: 403 |
| B8.2.9 | Non-existent locationId returns 4xx | PASS | Status: 403 |
| B8.2.10 | Non-existent doctorAvailabilityId returns 4xx | PASS | Status: 403 |
| B8.2.11 | Location mismatch returns 4xx | PASS | Status: 403 |
| B8.2.12 | Date mismatch returns 4xx | PASS | Status: 403 |
| B8.3.1 | Filter by identityUserId | PASS | Actual: 13 >= Expected: 1 |
| B8.3.2 | Filter by appointmentTypeId | PASS | Status: 200 |
| B8.3.3 | Filter by date range | PASS | Status: 200 |
| B8.3.4 | T4 empty appointment list | PASS | Expected: 0 |
| B8.4.1 | GET applicant attorney for appointment | PASS | Status: 200 |
| B8.4.2 | AA details-for-booking non-existent returns 200 | PASS | Status: 200 |
| B8.5.1 | PUT change PanelNumber | PASS | Status: 200 |
| B8.5.2 | Stale concurrencyStamp returns 409 | PASS | Status: 409 |

### B9

| Test ID | Name | Status | Details |
|---------|------|--------|---------|
| B9.1.1 | EmployerDetails GET by appointmentId | PASS | Status: 200 |
| B9.1.2 | EmployerDetails POST create | PASS | Status: 200 |
| B9.1.3 | EmployerDetails PUT update | PASS | Status: 200 |
| B9.1.4 | EmployerDetails DELETE | PASS |  |
| B9.1.5 | EmployerDetails GET deleted 404 | PASS | Status: 404 |
| B9.1.6 | EmployerName 255 chars OK | PASS | Status: 200 |
| B9.1.7 | EmployerName 256 chars fails (400) | PASS | Status: 400 |
| B9.2.1 | Accessors GET by appointmentId | PASS | Status: 200 |
| B9.2.2 | Accessor POST View(23) | PASS | Status: 200 |
| B9.2.3 | Accessor DELETE | PASS |  |
| B9.3.1 | ApplicantAttorneys GET list | PASS | Status: 200 |
| B9.3.2 | ApplicantAttorney POST create | PASS | Status: 200 |
| B9.3.3 | FirmName 50 chars OK | PASS | Status: 200 |
| B9.3.4 | FirmName 51 chars fails (400) | PASS | Status: 400 |

## B17: Frontend UI Testing (Manual Checklist)

### B17.1 Authentication Flow
- [ ] Navigate to http://localhost:4200 - Home loads
- [ ] Click Login - Redirect to AuthServer
- [ ] Enter admin@abp.io / <TEST_PASSWORD> - Logged in
- [ ] Logout works correctly
- [ ] Route guard redirects to login when not authenticated

### B17.2 Role-Specific Views
- [ ] Admin: Generic home page
- [ ] Patient: Patient appointment list + Book button
- [ ] Applicant Attorney: Filtered appointment view
- [ ] External user: Sidebar hidden
- [ ] Internal user: Full sidebar visible

### B17.3 CRUD Pages
- [ ] States: List, Create, Edit, Delete
- [ ] Appointment Types: List, Create, Edit, Delete
- [ ] Appointment Statuses: List, Create, Edit, Delete
- [ ] Appointment Languages: List, Create, Edit, Delete
- [ ] Locations: List, Create, Edit, Delete
- [ ] WCAB Offices: List, Create, Edit, Delete
- [ ] Doctors: List, Create, Edit, Delete
- [ ] Doctor Availabilities: List, Create, Edit, Delete
- [ ] Patients: List, Create, Edit, Delete
- [ ] Appointments: List, Create, Edit, Delete

### B17.4 Appointment Booking Flow
- [ ] Patient search by email works
- [ ] Location dropdown shows tenant locations only
- [ ] Date picker shows available dates
- [ ] Time slot selector shows available slots
- [ ] Booking creates appointment with confirmation number

### B17.5 ABP Admin Modules
- [ ] Identity Users and Roles
- [ ] SaaS Tenants list
- [ ] Audit Logs visible
- [ ] Settings page loads
- [ ] Excel exports work


