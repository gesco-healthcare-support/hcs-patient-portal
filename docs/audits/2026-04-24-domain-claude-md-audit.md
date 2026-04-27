# Domain CLAUDE.md Audit -- 2026-04-24

## Context

Freshness audit of the 15 Domain CLAUDE.md files in `src/HealthcareSupport.CaseEvaluation.Domain/<Entity>/` against current code and the B-6 Tier 1 test corpus merged to main today (#141, #142). 8 entities now have backend tests under `test/HealthcareSupport.CaseEvaluation.Application.Tests/<Entity>/`; the CLAUDE.md files for those entities still say "No tests" in many places, which is the highest-volume drift signal in this audit. 8 entities have product-intent docs at `docs/product/<entity>.md`; the other 7 are deferred. No commit, no push -- this file is read-only output for the next CLAUDE.md rewrite PR.

## Summary

| Entity | Has product-intent doc? | Has tests? | Stale claims | Missing invariants | Current claims |
| --- | --- | --- | --- | --- | --- |
| Appointments | yes | yes | 0 | 5 | 5 |
| ApplicantAttorneys | yes | yes | 1 | 4 | 4 |
| AppointmentAccessors | yes | yes | 1 | 3 | 4 |
| Doctors | yes | yes | 0 | 1 | 5 |
| DoctorAvailabilities | yes | yes | 1 | 7 | 5 |
| Patients | yes | yes | 1 | 6 | 6 |
| Books | no | yes | 0 | 1 | 3 |
| Locations | no | yes | 2 | 5 | 5 |
| AppointmentApplicantAttorneys | yes | no | 0 | 0 | 3 |
| AppointmentEmployerDetails | yes | no | 0 | 0 | 3 |
| AppointmentLanguages | no | no | 0 | 0 | 3 |
| AppointmentStatuses | no | no | 0 | 0 | 4 |
| AppointmentTypes | no | no | 0 | 0 | 3 |
| States | no | no | 0 | 0 | 4 |
| WcabOffices | no | no | 0 | 0 | 3 |

Total stale: 6. Total missing invariants: 32.

## Per-entity findings

### Appointments

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md`
- **Last apparent sync**: unknown (no front-matter; file references current state machine)
- **Stale claims**: none -- the gap and slot-booking notes still match current `AppointmentsAppService` and `AppointmentManager`.
- **Missing invariants** (Tier-1 tests now document):
  - test `AppointmentsAppServiceTests.cs:40-90` covers "Create requires non-empty PatientId/IdentityUserId/AppointmentTypeId/LocationId/DoctorAvailabilityId" -- not captured; CLAUDE.md only enumerates FKs as required, never says AppService rejects empty Guids.
  - test `AppointmentsAppServiceTests.cs:114` covers "Create rejects unknown PatientId" -- not captured.
  - test `AppointmentsAppServiceTests.cs:132-180` covers same five empty-Guid checks on Update path -- not captured.
  - `AppointmentManager.UpdateAsync` enforces `RequestConfirmationNumberMaxLength` and `PanelNumberMaxLength` -- not stated as a domain-layer guard.
  - `AppointmentManager.UpdateAsync` does NOT touch `AppointmentStatus` / `InternalUserComments` / `AppointmentApproveDate` / `IsPatientAlreadyExist` -- this IS in CLAUDE.md as "Update freezes key fields" but the corresponding test (`UpdateAsync_*_Empty_Throws`) implicitly confirms it; worth promoting to "Tier-1 verified".
- **Current claims worth confirming**:
  - line 67: "No domain methods enforce valid transitions"
  - line 122: "`Appointments.Edit` permission ... never checked in the AppService"
  - line 126: "RequestConfirmationNumber is auto-generated"
  - line 128: "Slot booking is one-way"
  - line 178: "console.log debug statement left in appointment-add.component.ts"
- **Suggested-diff-in-prose**: Add a new "Verified Invariants" subsection under Business Rules listing the five empty-Guid rejections and the unknown-PatientId rejection, each cross-referenced to `AppointmentsAppServiceTests.cs:<line>`. Keep the existing "permission gap" and "slot booking is one-way" callouts -- both are still accurate and their absence from the test suite is itself worth noting as an open Tier-2 risk.

### ApplicantAttorneys

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/ApplicantAttorneys/CLAUDE.md`
- **Last apparent sync**: unknown (likely pre-#141)
- **Stale claims**:
  - line 140: "No tests -- no test files found for ApplicantAttorneys" -- evidence `test/.../ApplicantAttorneys/ApplicantAttorneysAppServiceTests.cs` exists with 12 facts -- proposed: "Tier-1 covered: 12 facts in `ApplicantAttorneysAppServiceTests.cs` covering CRUD, tenant scoping, length validation, IdentityUserId required, IdentityUser lookup filter."
- **Missing invariants**:
  - test `:204` covers "Manager.CreateAsync rejects FirmName over max" -- not captured (CLAUDE.md mentions max in shape but not that Manager enforces it).
  - test `:216` covers "Manager.CreateAsync rejects WebAddress over max" -- not captured.
  - test `:235,247` covers "Tenant filter isolates attorneys per tenant" -- not captured.
  - test `:265` covers "GetIdentityUserLookupAsync filters by Email, not username" -- partly captured (line 97) but worth pinning to a verified test.
- **Current claims worth confirming**:
  - line 91: "IdentityUserId is required ... throw UserFriendlyException if default"
  - line 138: "Constructor sets 3/8 string fields; remaining set post-construction by Manager"
  - line 60: "DbContext config is outside `IsHostDatabase()` block"
  - line 142: "Proxy has unused file operations"
- **Suggested-diff-in-prose**: Strike "No tests" gotcha, replace with a "Tier-1 verified invariants" block enumerating the 12 facts. Promote line 91 (IdentityUserId required) and line 97 (lookup filters by email) into the verified block with `:172,:185,:265` line refs. Tenant scoping (`:235,:247`) is the most valuable addition -- it confirms the multi-tenancy section is not just doc-claim but is enforced end-to-end.

### AppointmentAccessors

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentAccessors/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**:
  - line 63: "No tests" -- evidence `AppointmentAccessorsAppServiceTests.cs` has 9 facts -- proposed: "Tier-1 covered: tenant isolation (`:50,:127,:139`), CRUD, lookup filter by email, AppointmentLookup tenant scoping."
- **Missing invariants**:
  - test `:62` covers "CreateAsync persists new accessor as host-scoped" -- not captured; CLAUDE.md says "FullAuditedEntity, NOT AggregateRoot" but does not document the host-scoped insertion behavior.
  - test `:139` covers "Host context with filter disabled returns accessors from both tenants" -- not captured.
  - test `:171` covers "GetAppointmentLookupAsync returns appointments-in-tenant only" -- not captured (no AppointmentLookup mention in CLAUDE.md).
- **Current claims worth confirming**:
  - line 23: AccessTypeId values View=23, Edit=24
  - line 48: "Permissions are defined but NOT used on AppService methods"
  - line 60: "FullAuditedEntity, not AggregateRoot"
  - line 62: "No Angular UI"
- **Suggested-diff-in-prose**: Strike "No tests". Add a Verified Invariants block for the 9 facts. The "Permissions are defined but NOT used" gotcha is the biggest drift target -- the test suite does not check this either, so flag it as a known coverage gap (Tier-2 candidate). Promote AppointmentLookup tenant scoping to a stated invariant since it is now exercised by `:171`.

### AppointmentApplicantAttorneys

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none.
- **Missing invariants**: none -- entity is untested and CLAUDE.md correctly states "No tests".
- **Current claims worth confirming**:
  - line 52: "Default sort is by Id asc -- unusual"
  - line 48: "All properly enforced on AppService methods"
  - line 56: "No Angular UI"
- **Suggested-diff-in-prose**: No changes required this PR cycle. When this entity gets Tier-2 coverage, add a Verified Invariants block; until then, the "No tests" gotcha is accurate. The unusual `Id asc` default sort is the single most interesting fact; keep it visible.

### AppointmentEmployerDetails

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none.
- **Missing invariants**: none -- entity is untested.
- **Current claims worth confirming**:
  - line 52: "CreateAsync and UpdateAsync use generic `[Authorize]` ... Only DeleteAsync uses the specific permission"
  - line 56: "EmployerName and Occupation are required -- the only two required string fields"
  - line 64: "No Angular UI"
- **Suggested-diff-in-prose**: No changes required. The mixed-auth gotcha is the most ship-relevant claim and should remain prominent when Tier-2 tests arrive -- expect a test like `CreateAsync_WhenAnonymous_Throws` and `CreateAsync_WhenAuthenticatedNonAdmin_Succeeds` to confirm or refute the gotcha.

### AppointmentLanguages

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none.
- **Missing invariants**: none -- untested.
- **Current claims worth confirming**:
  - line 20: "FullAuditedEntity<Guid> (NO IMultiTenant -- host-scoped)"
  - line 43: "CreateDto defaults Name to 'English'"
  - line 27: "Patient.AppointmentLanguageId -> SetNull"
- **Suggested-diff-in-prose**: No changes required. Note that the "default English" claim lives in the DTO not the AppService -- worth tightening the wording when next touched ("CreateDto defaults Name to 'English' but AppService does not enforce it") to remove ambiguity for a cold reader.

### Appointments (already covered above -- see first entry)

### AppointmentStatuses

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none.
- **Missing invariants**: none -- untested.
- **Current claims worth confirming**:
  - line 3: "separate from the AppointmentStatusType enum"
  - line 73: "AppointmentStatus entity vs AppointmentStatusType enum" naming gotcha
  - line 74: "No FK from Appointment"
  - line 47: "uses `AppointmentStatusManager` with basic create/update (no special validation)"
- **Suggested-diff-in-prose**: No changes required. The naming-collision gotcha is the most valuable bit of this file and should remain front-and-center; consider adding a one-line "Do not delete this entity even though it appears unused -- the seed data still references it" once the team confirms current usage.

### AppointmentTypes

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none.
- **Missing invariants**: none -- untested.
- **Current claims worth confirming**:
  - line 22: "Has Description field -- unlike most lookup entities"
  - line 64: "Cascade (host) / NoAction (tenant)" cascade behavior on DoctorAppointmentType
  - line 32: "Appointment.AppointmentTypeId NoAction; Location/DoctorAvailability SetNull"
- **Suggested-diff-in-prose**: No changes required. The cascade asymmetry between host and tenant DBs is the highest-value claim; when this entity gets Tier-2 coverage, that asymmetry should be the first thing exercised.

### Books

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/Books/CLAUDE.md`
- **Last apparent sync**: unknown (vestigial demo)
- **Stale claims**: none.
- **Missing invariants**:
  - test `BookAppServiceTests.cs:55` covers "Should_Not_Create_A_Book_Without_Name" -- not captured (CLAUDE.md just says "no max length constraint").
- **Current claims worth confirming**:
  - line 18: "AuditedAggregateRoot (NO FullAudited, NO soft delete, NO IMultiTenant)"
  - line 53: "Not a real feature -- demo entity from ABP scaffolding"
  - line 56: "Combined CreateUpdateBookDto -- violates project convention"
- **Suggested-diff-in-prose**: This file is intentionally slim because Books is vestigial. Add one sentence noting that the existing 3 tests (List/Create/Create-without-Name) are demo-quality coverage and should not be treated as a Tier-1 reference. Consider scheduling Books for deletion in a future cleanup PR.

### Doctors

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none -- line 155 already says "Tests exist".
- **Missing invariants**:
  - tests at `DoctorApplicationTests.cs:27-110` cover GetList/Get/Create/Update/Delete with full happy-path body assertions -- CLAUDE.md says "Tests exist" but does not enumerate what they verify; cold reader cannot tell whether collection-sync (M2M) is exercised.
- **Current claims worth confirming**:
  - line 102: "UpdateAsync syncs IdentityUser ... Name, Surname, Email"
  - line 104: "Collection sync uses 'except given IDs' pattern"
  - line 73: "Host vs Tenant DB difference: Host uses Cascade for join table FKs; Tenant uses NoAction"
  - line 110: "IDataFilter usage for cross-tenant lookups"
  - line 112: "Email max length is 49 -- unusual number"
- **Suggested-diff-in-prose**: Replace the bare "Tests exist" line with a Verified Invariants subsection enumerating the 5 facts. The IdentityUser-sync side effect (line 102) is the highest-risk undocumented behavior and should be promoted to its own bolded item -- a cold reader updating Doctor today would not expect IdentityUser writes. Note that the existing tests do NOT exercise the M2M collection sync, which is a Tier-2 gap.

### DoctorAvailabilities

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**:
  - line 149: "No tests -- no test files found for DoctorAvailabilities" -- evidence `DoctorAvailabilitiesAppServiceTests.cs` has 17 facts -- proposed: "Tier-1 covered: 17 facts in `DoctorAvailabilitiesAppServiceTests.cs` covering empty-Guid validation on CRUD/DeleteBySlot/DeleteByDate, GeneratePreview boundary cases (zero/negative duration, inverted ranges, 60min/30min splits, multi-day, exact-fit boundary)."
- **Missing invariants**:
  - test `:50,:62,:74,:91,:134` covers "All slot operations reject empty LocationId" -- not captured.
  - test `:110` covers "GeneratePreviewAsync(null) throws AbpValidation" -- not captured.
  - test `:121` covers "GeneratePreviewAsync(empty list) returns empty preview" -- not captured.
  - test `:146,:158` covers "Duration must be positive" -- not captured (CLAUDE.md says default is 15 but not that <=0 is rejected).
  - test `:170` covers "ToDate before FromDate is rejected" -- not captured.
  - test `:183,:196` covers "ToTime <= FromTime is rejected" -- not captured.
  - test `:216,:235,:255,:272,:289` covers "slot count math: 60min/1h=1, 30min/1h=2, multi-day=1-per-day, duration>range=0, exact fit=1" -- not captured.
- **Current claims worth confirming**:
  - line 57: "Slots transition from Available to Booked when an Appointment is created, but deleting the Appointment does NOT release the slot back to Available"
  - line 110: "BookingStatus is mutable on update ... no validation prevents changing a Booked slot back to Available"
  - line 102: "GeneratePreviewAsync ... returns a preview with IsConflict flags"
  - line 106: "AppointmentDurationMinutes defaults to 15"
  - line 145: "Race conditions possible if two users generate for the same location/date simultaneously"
- **Suggested-diff-in-prose**: This file is the highest-drift entity in the audit -- 7 freshly-tested invariants land in one PR's worth of edits. Strike "No tests" and replace with a Verified Invariants subsection grouped by surface (CRUD validation, GeneratePreview validation, GeneratePreview math). Keep all five Business Rules intact -- they are the strategic claims that the test suite confirms but does not replace. The race-condition gotcha (line 145) should remain prominent because no test exercises it.

### Doctors (already covered above)

### Locations

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/Locations/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**:
  - line 134: "No tests -- no test files found for Locations" -- evidence `LocationsAppServiceTests.cs` has 14 facts -- proposed: "Tier-1 covered: 14 facts including FK-blocked deletes, filter combinations, bulk delete, tenant visibility."
  - line 132: "Cascade delete risk -- Deleting a Location cascades to DoctorLocation ... but DoctorAvailability and Appointment FKs use NoAction, so delete will fail" -- evidence test `:269,:303` confirms this is now Tier-1 verified -- proposed: rephrase as "Verified: delete-with-references throws (`:269` DoctorAvailability, `:303` Appointment); cascade to DoctorLocation is documented but not yet test-covered."
- **Missing invariants**:
  - test `:139` covers "Filter by Name returns matching location" -- not captured.
  - test `:153` covers "Filter by ParkingFee range" -- not captured.
  - test `:168` covers "Filter by IsActive=false returns inactive only" -- not captured (CLAUDE.md says IsActive defaults to true but never says it is filterable).
  - test `:225` covers "DeleteAllAsync respects filter" -- not captured (CLAUDE.md just says "DeleteAll deletes all matching a filter" without confirming it).
  - test `:349` covers "Locations are visible from tenant context" -- not captured (host-scoped + tenant-readable is implied but not stated as an invariant).
- **Current claims worth confirming**:
  - line 91: "Name is required -- the only required string field"
  - line 92: "IsActive defaults to true"
  - line 95: "ParkingFee is always present -- non-nullable decimal"
  - line 130: "No controller for Location -- Actually there IS a controller" (the meta-correction itself; consider deleting since it just adds noise)
  - line 137: "AppointmentTypeId on Location is unusual -- no business logic enforces or uses this relationship"
- **Suggested-diff-in-prose**: Strike "No tests" plus the meta-self-correction at line 130 (it documents an exploration mistake, not a fact about the code). Promote line 132's cascade-delete gotcha into a verified item with `:269,:303` refs. Add filter coverage (Name, ParkingFee range, IsActive) as verified invariants. The line 137 "unused FK" claim is the highest-value strategic item and should remain because the test suite does not exercise that relationship.

### Patients

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**:
  - line 155: "No tests -- no test files found for Patients" -- evidence `PatientsAppServiceTests.cs` has 16 facts -- proposed: "Tier-1 covered: 16 facts including string-field max validation (parametrized), tenant scoping, GetByEmail-for-booking null/empty handling."
- **Missing invariants**:
  - test `:102` covers "UpdateAsync changes mutable fields but does NOT change IdentityUserId" -- not captured (CLAUDE.md does not state IdentityUserId is locked post-create).
  - test `:146,:156` covers "Create/Update reject empty IdentityUserId" -- not captured (Manager-level required is implied by `Guid` non-nullable type but the AppService-level UserFriendlyException is the actual user-facing guard).
  - test `:194,:218` covers "PatientManager rejects every string field over its consts max" via `[Theory]` parametrization -- not captured.
  - test `:241,:259` covers "Host context sees both tenants; tenant context sees only its own" -- not captured (CLAUDE.md explicitly notes Patient does NOT implement IMultiTenant, which makes this test result the most surprising verified fact in the file).
  - test `:276,:286,:295` covers "GetPatientByEmailForAppointmentBooking: found returns patient, not-found returns null, empty email returns null" -- partly captured (line 106) but worth pinning to verified status.
  - test `:58,:70` covers "Filter by FirstName / Email" -- not captured.
- **Current claims worth confirming**:
  - line 73: "IMultiTenant: No ... However, Patient has a manual TenantId property"
  - line 102: "GetOrCreate for appointment booking ... creates a new IdentityUser with 'Patient' role"
  - line 110: "PhoneNumberType values are non-sequential -- Work=28, Home=29"
  - line 112: "SSN stored in plaintext -- PII concern"
  - line 151: "Typo: RefferedBy -- should be ReferredBy"
  - line 159: "Menu placement -- under DoctorManagement parent"
- **Suggested-diff-in-prose**: Strike "No tests". Add a Verified Invariants subsection with the 6 missing items. The most strategically valuable addition is the "manual tenant filtering works in practice" pair (`:241,:259`) -- it should silence any future doubt about whether the non-IMultiTenant design causes leakage. Keep all 5 named gotchas in line 151+ intact. The SSN-plaintext PII concern (line 112) and the RefferedBy typo (line 151) remain the two most ship-relevant claims and the audit confirms neither is fixed yet.

### States

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/States/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none.
- **Missing invariants**: none -- untested.
- **Current claims worth confirming**:
  - line 88: "StateManager.CreateAsync validates Check.NotNullOrWhiteSpace(name) -- no uniqueness check"
  - line 127: "Most-referenced entity -- 5 other entities have FK to State (all SetNull)"
  - line 129: "No NameMaxLength ... no `HasMaxLength()` in EF config"
- **Suggested-diff-in-prose**: No changes required. The "no max length" and "no uniqueness" pair is the most strategic content; preserve it for the team that will eventually add a unique constraint and migration. State has no docs/product/ coverage -- flag for follow-up.

### WcabOffices

- **CLAUDE.md path**: `src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/CLAUDE.md`
- **Last apparent sync**: unknown
- **Stale claims**: none.
- **Missing invariants**: none -- untested.
- **Current claims worth confirming**:
  - line 60: "Excel export is `[AllowAnonymous]` ... Potential security concern"
  - line 23: "Name and Abbreviation required"
  - line 47: "Delete permission covers Delete, DeleteByIds, DeleteAll"
- **Suggested-diff-in-prose**: No changes required. The `[AllowAnonymous]` Excel export is the highest-priority security item across all 7 untested entities and should remain the first gotcha listed. WcabOffices has no docs/product/ coverage -- flag for follow-up.

## Aggregate notes

- Total stale: 6 (5 of them the same "No tests" claim flipped by today's #141/#142 merges; 1 is a Locations cascade-claim that is now Tier-1 verified).
- Total missing invariants: 32 (concentrated in DoctorAvailabilities=7, Patients=6, Appointments=5, Locations=5, ApplicantAttorneys=4, AppointmentAccessors=3, Books=1, Doctors=1).
- Most-drift entity: DoctorAvailabilities -- 17 freshly merged facts, none referenced in CLAUDE.md, plus the existing "No tests" line.
- Surprisingly-current entities: Appointments (CLAUDE.md correctly captures the `[Authorize]` permission gap and the one-way slot booking; tests merely validate the "FK required" half of the picture). AppointmentApplicantAttorneys, AppointmentEmployerDetails, AppointmentLanguages, AppointmentStatuses, AppointmentTypes, States, WcabOffices -- all 7 untested entities are accurate and need no edits this cycle.
- Entities deferred (no docs/product/ coverage): AppointmentLanguages, AppointmentStatuses, AppointmentTypes, Books, Locations, States, WcabOffices -- TODO: add `docs/product/<entity>.md` once Tier-2 backend tests land for the first 4 (Books is vestigial and can stay uncovered).
