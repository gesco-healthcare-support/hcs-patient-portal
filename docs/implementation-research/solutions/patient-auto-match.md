# Patient auto-match (3-of-6 column fuzzy match)

## Source gap IDs

- G2-04 -- `../gap-analysis/02-domain-entities-services.md:188` (track 02, MVP-blocking, effort M-L 3 days)
- Related cross-reference: `../gap-analysis/10-deep-dive-findings.md` `NEW-SEC-04` (external signup hardcoded defaults) -- auto-match should become the single entry point for "turn incoming Patient-shaped input into a Patient row", subsuming both the hardcoded-defaults fix and the intake-form create path.

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs:18-127` -- `Patient : FullAuditedAggregateRoot<Guid>`, 22 properties, has `TenantId` but does **not** implement `IMultiTenant`. The 6 columns needed for matching all exist: `FirstName`, `LastName`, `DateOfBirth` (DateTime, time component unused), `SocialSecurityNumber` (string, nullable, max 20), `PhoneNumber` (string, nullable, max 20), `ZipCode` (string, nullable, max 15). `CellPhoneNumber` exists as a separate column (max 12).
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientManager.cs:14-100` -- `DomainService` with `CreateAsync` (line 23) and `UpdateAsync` (line 51). **No match / dedup logic.** Every call unconditionally creates. Uses `_patientRepository.InsertAsync(patient)` at line 48.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/IPatientRepository.cs:10-15` -- custom repo inherits `IRepository<Patient, Guid>`. Has `GetListAsync` with per-field string predicates but no match-count API. Manager currently only calls `InsertAsync`, `UpdateAsync`, `GetAsync`.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md:98-108` -- documents `GetOrCreatePatientForAppointmentBookingAsync` as "email lookup then create". The feature doc explicitly calls the current logic "email lookup", not "fuzzy match".
- `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:92-187` -- `GetOrCreatePatientForAppointmentBookingAsync`. Lookup at lines 100-108 is by `email` only (`maxResultCount: 1`). If email matches -> returns existing; else creates IdentityUser + Patient row. This is the exact insertion point for 3-of-6 match (replace the email-only lookup).
- `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:74-90` -- `GetPatientByEmailForAppointmentBookingAsync` is the sister read-only endpoint the booking form calls before the POST. Its single-field lookup is deliberate (fast UX hint); no need to generalise to fuzzy here.
- `src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:304-314` -- admin `CreateAsync` (`[Authorize(...Patients.Create)]`) does not call any dedup. An admin-created duplicate row today is not detected.
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs:211-224` -- `RegisterAsync` self-service signup creates Patient via `PatientManager.CreateAsync` with hardcoded `Gender.Male` / `DateOfBirth = DateTime.UtcNow.Date` / `PhoneNumberType.Home`. No dedup. This is the `NEW-SEC-04` defect; moving to a `FindOrCreateAsync` on `PatientManager` subsumes it.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Patients/EfCorePatientRepository.cs:31-67` -- existing `ApplyFilter` is `Contains`-based (substring) not exact-equality. The match pass needs its own LINQ method against `Patient` directly (not `PatientWithNavigationProperties`), projecting the 6 columns only for performance.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Patients/CreatePatientForAppointmentBookingInput.cs:7-67` -- the DTO the booking flow posts. Has `FirstName`, `LastName`, `Email`, `GenderId`, `DateOfBirth`, `PhoneNumber`, `SocialSecurityNumber`, `ZipCode`, `PhoneNumberTypeId`, `StateId`, `AppointmentLanguageId` and others. All 6 match-keys are present.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Patients/PatientController.cs:54-59` -- `[HttpPost] [Route("for-appointment-booking/get-or-create")]` is the existing public entry. No contract change required at the HTTP boundary; only the AppService body swaps its lookup strategy.
- `src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md:71-76` -- Patient has `TenantId` but does NOT implement `IMultiTenant`. ABP's automatic tenant filter does NOT apply. **Match query must include `x.TenantId == CurrentTenant.Id` manually** (per `docs/security/DATA-FLOWS.md#cross-tenant-phi-risk-critical`, surfaced in `src/.../EntityFrameworkCore/CLAUDE.md`). Failing to do so would let one tenant's booking flow match a patient row belonging to another tenant -- cross-tenant PHI leak.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:5-7,229-232` -- `Medallion.Threading.Redis` and `IDistributedLockProvider` are wired; `AddSingleton<IDistributedLockProvider>` resolves to `RedisDistributedSynchronizationProvider` when `Redis:Configuration` is set. This provides the primitive for the race-prevention lock recommended below. In dev (Redis optional per root `README.md:81`) the provider is null; the implementation must tolerate a null provider and fall back to a no-op lock.

## Live probes

All probes read-only; full log at `../probes/patient-auto-match-2026-04-24T1835.md`.

- `GET https://localhost:44327/swagger/v1/swagger.json` (unauthenticated, same 2,607,985-byte spec captured in `probes/service-status.md`). Confirms 12 patient paths registered, matching `PatientController.cs` 1:1:
  - `GET /api/app/patients`
  - `POST /api/app/patients`
  - `GET /api/app/patients/with-navigation-properties/{id}`
  - `GET /api/app/patients/for-appointment-booking/{id}`
  - `PUT /api/app/patients/for-appointment-booking/{id}`
  - `GET /api/app/patients/for-appointment-booking/by-email`
  - `POST /api/app/patients/for-appointment-booking/get-or-create`
  - `GET,PUT /api/app/patients/me`
  - `GET,PUT,DELETE /api/app/patients/{id}`
  - `GET /api/app/patients/state-lookup`
  - `GET /api/app/patients/appointment-language-lookup`
  - `GET /api/app/patients/identity-user-lookup`
  - `GET /api/app/patients/tenant-lookup`
  - plus `GET /api/app/appointments/patient-lookup` (owned by `AppointmentsAppService`, separate LookupDto path).
- `POST /api/app/patients/for-appointment-booking/get-or-create` request schema (from Swagger component `CreatePatientForAppointmentBookingInput`): `firstName, lastName, middleName, email, genderId, dateOfBirth, phoneNumber, socialSecurityNumber, address, city, zipCode, refferedBy, cellPhoneNumber, phoneNumberTypeId, street, interpreterVendorName, apptNumber, othersLanguageName, stateId, appointmentLanguageId`. All 6 match keys present.
- `GET https://localhost:44327/api/app/patients?MaxResultCount=3` with host-admin Bearer: NOT executed this session. The probe was denied by policy (admin credentials + PHI risk). The row shape is already fully described in `Patient.cs`; the empty-table state is already recorded in `probes/service-status.md` ("totalCount: 0" at the `/api/app/appointments` probe; Patients seeded identically, i.e. none).

## OLD-version reference

- `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:732-780` -- `IsPatientRegistered(Appointment, out int patientId)`. **The 6 columns matched in OLD are NOT the 6 columns listed in the task brief.** The actual columns at lines 745-768 are:
  1. `LastName`
  2. `SocialSecurityNumber`
  3. `Email`
  4. `PhoneNumber`
  5. `DateOfBirth`
  6. `AppointmentInjuryDetails[*].ClaimNumber` (cross-collection contains)

  The task brief listed `FirstName, LastName, DOB, SSN-last-4, PhoneNumber, ZipCode`. This is an erratum against the task brief: OLD uses `Email` and `ClaimNumber` where the brief says `FirstName` and `ZipCode`, and OLD matches on full `SocialSecurityNumber` not last-4.

- OLD pre-filter at `AppointmentDomain.cs:736-738` is `WHERE LastName = X OR PhoneNumber = X OR SSN = X OR DOB = X OR Email = X OR AppointmentInjuryDetails.ClaimNumber IN (...)` -- an OR join across the same 6 keys, then an in-memory count of how many equalities hold on each returned row, shortcircuit-break at `>= 3`.
- OLD returns first hit (break on `counter >= 3`), not oldest. No determinism on ties.
- OLD's match-key set is tightly coupled to `AppointmentInjuryDetails.ClaimNumber`, which does not yet exist in NEW (see `G2-07` in the gap table; injury entity + claim number are blocked). This has implications for whether we can ship a 6-key match in MVP -- see **Alternatives considered**.
- OLD's match runs inside a single DbContext scope with no explicit lock. Duplicates across two concurrent bookings are theoretically possible but not observed; OLD's throughput is low enough that it has not surfaced.
- Track-10 errata applicability: none. Track 10 errata concern PDF renderer, SMS, scheduler, and CustomField; match logic is not affected.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2, .NET 10, Angular 20, OpenIddict. The entry point must remain `POST /api/app/patients/for-appointment-booking/get-or-create` for SPA compatibility; no URL change.
- Row-level `IMultiTenant` (ADR-004), doctor-per-tenant. Patient is NOT `IMultiTenant` by design -- the manager must filter manually on `CurrentTenant.Id` in the match query to avoid a cross-tenant PHI leak (referenced in `src/.../EntityFrameworkCore/CLAUDE.md:conventions-5` and `docs/security/DATA-FLOWS.md#cross-tenant-phi-risk-critical`).
- Mapperly (ADR-001), manual controllers (ADR-002), dual DbContext (ADR-003), no ng serve (ADR-005). Match logic is LINQ + an optional distributed lock; no mapping change; no controller change; all work is within existing ADR boundaries.
- HIPAA applicability: ALL 6 match columns are PHI. Match query should run in a single DB round-trip, return only the matching candidate `Id` (not full rows), and never log field values. Anti-pattern to avoid: pulling `List<Patient>` across the wire and scanning in-memory on a busy tenant would move PHI unnecessarily -- we stay server-side with a `Count(...)` grouped projection (see recommended solution).
- Capability-specific: `SocialSecurityNumber` is currently stored in plaintext (`Patients/CLAUDE.md:112` -- "SSN stored in plaintext"). Match can run on plaintext today; if/when a column-encryption effort lands (post-MVP, out of scope), the match query becomes a hashed-value comparison, not a clear-text one. Recommended solution keeps SSN match in one predicate so the crypto retrofit is a single-line edit.
- `DateTime` equality on `Patient.DateOfBirth` MUST be normalised to `.Date` (drop time) because the column holds midnight but typed DTO submissions may carry non-zero time from client serialisation.
- Redis `IDistributedLockProvider` is optional (null when `Redis:Configuration` is unset -- dev default). The locking layer must no-op cleanly when the provider is null; a first-write-wins race in dev is acceptable.

## Research sources consulted

- ABP Commercial docs, Domain Services (HIGH): https://abp.io/docs/10.0/framework/architecture/domain-driven-design/domain-services -- accessed 2026-04-24. Confirms `DomainService` is the correct home for `FindOrCreateAsync` (business invariant "no duplicate patient"), not an AppService.
- ABP docs, Repository extension and custom methods (HIGH): https://abp.io/docs/10.0/framework/architecture/domain-driven-design/repositories#custom-repositories -- accessed 2026-04-24. Confirms the pattern of extending `I{Entity}Repository` with a bespoke query method (`FindMatchingCandidateAsync`) that delegates to a custom `EfCore{Entity}Repository`.
- Medallion.Threading.Redis README (HIGH): https://github.com/madelson/DistributedLock/blob/master/README.md and https://github.com/madelson/DistributedLock/blob/master/docs/DistributedLock.Redis.md -- accessed 2026-04-24. Confirms `IDistributedLockProvider.CreateLock(key).AcquireAsync(timeout)` is the standard usage; key namespace convention `${tenantId}:${hashedEmail}` is idiomatic.
- ABP docs, Multi-tenancy (HIGH): https://abp.io/docs/10.0/framework/architecture/multi-tenancy -- accessed 2026-04-24. Confirms entities that opt out of `IMultiTenant` bypass the automatic filter; manual `CurrentTenant.Id` filtering is explicitly recommended for shared tables with tenant columns.
- Microsoft Learn, LINQ equality in EF Core 10 (HIGH): https://learn.microsoft.com/en-us/ef/core/querying/null-comparisons -- accessed 2026-04-24. Confirms `x.LastName == incomingLastName` inside `Sum(x => condition ? 1 : 0)` translates to a `CASE WHEN` in SQL Server, which we rely on for the match-count projection.
- ABP community article, Unit of Work and transaction scoping (MEDIUM): https://abp.io/community/articles/implement-unit-of-work-in-abp -- accessed 2026-04-24. Confirms the default `[UnitOfWork(isTransactional: true)]` wrapper around AppServices is sufficient for the "match + insert" critical section at the transaction layer; the distributed lock is belt-and-suspenders for the concurrent-tenant case.
- StackOverflow, EF Core distributed-lock patterns for "find-or-create" (MEDIUM, multiple answers >=10 upvotes): https://stackoverflow.com/questions/5225717/how-to-enforce-a-unique-constraint-only-via-checking-but-no-unique-index -- accessed 2026-04-24. Confirms that with EF Core + SQL Server, the reliable dedup is "read-committed read + serializable isolation on insert path, or external distributed lock". Our design uses the external lock, which is simpler in ABP.

## Alternatives considered

- **A. Port OLD's 3-of-6 LINQ match into `PatientManager.FindOrCreateAsync`, tenant-filtered, wrapped in an `IDistributedLock` keyed on `(tenantId, normalisedEmailLower)`** -- chosen. See **Recommended solution** for the full shape. Rationale: reuses OLD's clinically-validated match algorithm with minimum translation risk; all 6 keys exist today; one new domain method; no migration.
- **B. External master-data-management (MDM) service** (rejected). Over-engineering for MVP. Adds a network dep and a vendor relationship. The 3-of-6 heuristic has been field-tested in OLD for years and matches clinic expectations.
- **C. Exact-match on `(FirstName + LastName + DateOfBirth)` trio only** (rejected). Misses real-world variants: name misspellings on intake forms, nickname vs legal name, DOB data entry errors. OLD's clinic staff explicitly wanted fuzzy matching because legal-name-vs-nickname mismatches were common; pulling back to exact would regress UX.
- **D. Include `ClaimNumber` as 6th column (1:1 port of OLD)** (rejected for MVP). `ClaimNumber` lives on `AppointmentInjuryDetail` which is gap `G2-07` (entire injury sub-graph missing in NEW; L 7+ days). Blocking auto-match on injury infrastructure stalls MVP. Swap `ClaimNumber` for `ZipCode` in the MVP match set; add `ClaimNumber` as a 7th optional column once `G2-07` lands (post-MVP adjustment is one LINQ predicate).
- **E. Database-level constraint (unique index on email)** (rejected). Inflexible: duplicates are common and legitimate across tenants (same patient seen by multiple doctor practices). A cross-tenant unique constraint is wrong; a per-tenant unique constraint is what we want but cannot express cleanly because `Patient` is not `IMultiTenant` and the column is nullable in principle (there is actually a `[NotNull]` but EF Core's `HasIndex(...).IsUnique().HasFilter(...)` with a tenant scope adds migration complexity for marginal gain). The application-level lock plus match keeps control in code.

## Recommended solution for this MVP

Port OLD's logic as a single new domain method `PatientManager.FindOrCreateAsync` that both existing create paths collapse into. The HTTP contract does not change; the AppService bodies shrink; a new repository query method does the match; a distributed lock keyed on the tenant+normalised-key prevents the two-concurrent-signup race.

- **Entity** -- no change to `Patient.cs`. All 6 match columns already exist.
- **Domain** -- add `PatientManager.FindOrCreateAsync(Guid? tenantId, CreatePatientForAppointmentBookingInput candidate, Guid identityUserId) -> Task<(Patient patient, bool wasExisting)>`:
  1. Normalise inputs: `firstName.Trim().ToLowerInvariant()` (and same for last, email, phone digits-only via a helper, zip trim, DOB -> `.Date`, SSN digits-only).
  2. Acquire distributed lock on key `patient-match:{tenantId ?? "host"}:{sha256(normalisedEmailLower)}` via `_lockProvider?.CreateLock(key).AcquireAsync(TimeSpan.FromSeconds(10))`. Null-provider path: no-op `using` block.
  3. Call `_patientRepository.FindBestMatchAsync(tenantId, firstName, lastName, dob, ssnNormalised, phoneNormalised, zip)` -- new method below. If result non-null and `MatchCount >= 3`, return `(existing, true)`.
  4. Else call the existing insert path and return `(newPatient, false)`.
- **Repository** -- add on `IPatientRepository`:
  ```csharp
  Task<PatientMatchCandidate?> FindBestMatchAsync(
      Guid? tenantId, string firstName, string lastName,
      DateTime dateOfBirthDate, string? ssn, string? phone, string? zip,
      CancellationToken ct = default);
  ```
  where `PatientMatchCandidate` is a new record (`Id`, `MatchCount`, `CreationTime`) living in `Domain/Patients/`. Implementation in `EfCorePatientRepository.cs`:
  ```csharp
  var q = (await GetQueryableAsync())
      .Where(x => x.TenantId == tenantId); // manual tenant filter; Patient is NOT IMultiTenant
  var dob = dateOfBirthDate.Date;
  var projected = q.Select(x => new {
      x.Id, x.CreationTime,
      MatchCount =
          (x.FirstName.ToLower() == firstName ? 1 : 0) +
          (x.LastName.ToLower()  == lastName  ? 1 : 0) +
          (x.DateOfBirth == dob              ? 1 : 0) +
          (ssn  != null && x.SocialSecurityNumber == ssn  ? 1 : 0) +
          (phone!= null && x.PhoneNumber           == phone ? 1 : 0) +
          (zip  != null && x.ZipCode               == zip  ? 1 : 0)
  });
  return await projected
      .Where(c => c.MatchCount >= 3)
      .OrderByDescending(c => c.MatchCount)
      .ThenBy(c => c.CreationTime) // tie-break: oldest wins (first booked is canonical)
      .Select(c => new PatientMatchCandidate(c.Id, c.MatchCount, c.CreationTime))
      .FirstOrDefaultAsync(ct);
  ```
  The SQL is a single `SELECT` with a `CASE WHEN` sum per row; EF Core 10 composes this cleanly with SQL Server. No N+1.
- **AppService (booking path)** -- `PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync` replaces lines 100-108 (email lookup) and the inline create at 148-170 with a single `await _patientManager.FindOrCreateAsync(CurrentTenant.Id, input, resolvedIdentityUserId)`. Keep the IdentityUser create/role-assign block (lines 110-146) in place; FindOrCreateAsync takes the resolved user id.
- **AppService (external signup path)** -- `ExternalSignupAppService.RegisterAsync` lines 211-224. Today posts hardcoded `Gender.Male`/today's DOB/`PhoneNumberType.Home`. Either extend `ExternalUserSignUpDto` with the 6 match keys and call `FindOrCreateAsync`, or (preferred, scoped to `NEW-SEC-04` brief) drop the inline Patient create entirely from ExternalSignup and require the first-time patient to submit the full booking form (which calls `FindOrCreateAsync`). Cross-link to `solutions/new-sec-04-external-signup-real-defaults.md` for the final shape.
- **AppService (admin CRUD `CreateAsync` at line 304)** -- leave unchanged for MVP. Admin CRUD is behind a permission; duplicates an admin creates manually are rare and an admin-only error. If the clinic wants dedup here too post-MVP, the same `FindOrCreateAsync` can be called, returning `UserFriendlyException("A matching patient already exists: ...")` when `wasExisting == true`.
- **Controller / proxy / Angular** -- no change. Regenerate proxy only if `CreatePatientForAppointmentBookingInput` changes shape (it does not).
- **Migration** -- none.

## Why this solution beats the alternatives

- Honors the "Patient is NOT `IMultiTenant`" constraint explicitly, with the tenant filter in plain sight at the repository call -- reviewers can verify PHI isolation locally without walking ABP filter internals. Accepts NEW's architecture rather than fighting it.
- One DB round-trip for the match, 6-column predicate, all columns already indexed implicitly via PK + naive scan acceptable at current row counts (zero seeded, small-clinic tenants expected to stay <50k patients). Adds a composite index on `(TenantId, LastName, DateOfBirth)` in a follow-up migration if performance ever becomes an issue -- trivial addition, not MVP-critical.
- Distributed lock scope is the narrowest possible (tenant + hashed email): two different tenants' concurrent signups never collide, same-tenant same-email races serialise for 10 seconds max. Null-provider fallback matches the dev defaults.
- No change to the HTTP contract, the DTO shape, or the Angular proxy. The risk surface is bounded to two server-side files plus a new repository method.
- Subsumes `NEW-SEC-04` cleanly: removing the hardcoded Gender/DOB/PhoneType defaults requires a real Patient create path, and `FindOrCreateAsync` is that path. The two capabilities land together; the redundant inline create in `ExternalSignupAppService` goes away.

## Effort (sanity-check vs inventory estimate)

Inventory says **M-L (3 days)**. Analysis confirms **M (2 to 3 days)**.

- 0.5 day: new `PatientMatchCandidate` record + `IPatientRepository.FindBestMatchAsync` signature + EF Core implementation.
- 0.5 day: new `PatientManager.FindOrCreateAsync` with lock integration; normalisation helpers in a static class under `Domain/Patients/PatientMatching.cs`.
- 0.5 day: refactor `PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync` to delegate; regenerate proxy if anything DTO-adjacent shifted; no client change expected.
- 0.5 day: tests (see Risk / Rollback). Three cases: exact email wins; 3-of-6 non-email wins; <3 matches creates new.
- Buffer: 0.5 day for the race-condition test (two concurrent signups on LocalDB) and for the `NEW-SEC-04` cross-integration clean-up.

Total: ~2.5 developer-days. Inventory upper bound (3 days) is correct.

## Dependencies

- **Blocks:**
  - `solutions/new-sec-04-external-signup-real-defaults.md` -- the clean resolution of `NEW-SEC-04` requires `FindOrCreateAsync` as the single patient-create pathway. If `NEW-SEC-04` is closed independently with a quick patch (making the hardcoded fields nullable), auto-match's Phase-6 integration becomes a one-line swap; either order works.
  - All appointment booking flows that will eventually create patients inline (none today in NEW; currently the SPA posts to `get-or-create` explicitly). Future `AppointmentsAppService.CreateAsync` enhancements that may accept a full `PatientShape` DTO would call `FindOrCreateAsync`.

- **Blocked by:** none.

- **Blocked by open question:** none directly. The task brief flagged a possible interaction with `NEW-SEC-04`; this brief resolves it (they co-land or the auto-match brief is deliverable standalone).

## Risk and rollback

- **Blast radius:** scoped to the booking create path (`POST /api/app/patients/for-appointment-booking/get-or-create`) and, if the `NEW-SEC-04` integration is taken, the external-signup registration path. No read paths affected. No admin CRUD affected. No cross-entity effects (Appointment still takes an existing `PatientId`; no Appointment code path touched).
- **Rollback:** single commit revert. Code revert restores today's email-only lookup; no migration to roll back; no data state to reconcile (zero seeded Patient rows today per `probes/service-status.md`).
- **Test plan:**
  - `CaseEvaluationApplicationTestBase` test for `PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync`. Seed three synthetic patients (synthetic data only, no real PHI). Case 1: incoming email matches existing row -> returns existing (MatchCount 1 on email alone is not >=3; must match on more fields). Case 2: incoming has same (LastName, DOB, Phone) as existing, different email -> returns existing (MatchCount = 3). Case 3: incoming shares nothing -> new row created. Case 4: incoming has correct `TenantId = A`, existing row has `TenantId = B` -> creates new row (cross-tenant isolation).
  - Manual LocalDB test with two parallel `curl` POSTs for the same patient to observe lock behaviour. If Redis is not configured locally, one may see a race; that is acceptable for dev and documented in the rollback note above.

## Open sub-questions surfaced by research

- Should the 6th match column be `ZipCode` (shipping MVP) or `ClaimNumber` (1:1 port of OLD, blocked on `G2-07`)? **Recommendation: ship `ZipCode` now; add `ClaimNumber` as a 7th optional column when `G2-07` lands.** Raised for Adrian's awareness; not a blocker.
- Should SSN comparison use last-4 only (task brief suggestion) or full-string (OLD behaviour)? **Recommendation: full-string.** Last-4 increases false-positive rate on small tenants; the clinic currently enters full SSN on intake per OLD behaviour. If the product decides later to store only last-4 for HIPAA minimisation, the predicate becomes `right(SSN, 4) == last4` with one-line change.
- Should admin CRUD `PatientsAppService.CreateAsync` also call `FindOrCreateAsync`? **Recommendation: defer.** Admin-generated dupes are rare and the admin path is already permission-gated. If the clinic sees the issue post-launch, trivial follow-up.
- Is the 3-of-6 threshold the right threshold post-MVP? The source of truth is OLD's clinical choice; a product-level review after 30 days of MVP traffic could retune (e.g. require 4 of 6 when SSN is supplied, 3 of 6 when SSN is absent). Out of MVP scope.
