# Patients -- worker's comp IME patient records and onboarding

Three usage patterns: admin CRUD, inline appointment booking (get-or-create + auto-create
IdentityUser), and self-service profile for the logged-in patient.

## What lives here

| File | Purpose |
|---|---|
| `Patient.cs` | Aggregate root; see Entity Shape below |
| `PatientManager.cs` | Domain service: `CreateAsync`, `UpdateAsync`, `FindOrCreateAsync` (fuzzy match) |
| `PatientWithNavigationProperties.cs` | Projection: Patient + State? + AppointmentLanguage? + IdentityUser? + Tenant? |
| `IPatientRepository.cs` | Custom repo: `GetWithNavigationPropertiesAsync`, `FindBestMatchAsync`, GetList/Count (18+ filters) |

## Entity Shape

```
Patient : FullAuditedAggregateRoot<Guid>, IMultiTenant
+-- TenantId              : Guid?   (IMultiTenant; ABP auto-filter scopes reads by CurrentTenant.Id)
+-- FirstName             : string  [max 50, req]
+-- LastName              : string  [max 50, req]
+-- MiddleName            : string? [max 50]
+-- Email                 : string  [max 50, req]
+-- GenderId              : Gender  (Male=1, Female=2, Other=3)
+-- DateOfBirth           : DateTime
+-- PhoneNumber           : string? [max 20]
+-- SocialSecurityNumber  : string? [max 20]  (PII; plaintext; see SSN gotcha)
+-- Address               : string? [max 100]
+-- City                  : string? [max 50]
+-- ZipCode               : string? [max 15]
+-- RefferedBy            : string? [max 50]  (typo; propagates to DB column; needs migration to fix)
+-- CellPhoneNumber       : string? [max 12]
+-- PhoneNumberTypeId     : PhoneNumberType   (Work=28, Home=29; non-sequential, inherited from legacy)
+-- Street                : string? [max 255]
+-- InterpreterVendorName : string? [max 255]
+-- ApptNumber            : string? [max 100]
+-- OthersLanguageName    : string? [max 100]
+-- StateId               : Guid?   (FK -> State, SetNull)
+-- AppointmentLanguageId : Guid?   (FK -> AppointmentLanguage, SetNull)
+-- IdentityUserId        : Guid    (FK -> IdentityUser, NoAction, required)
```

IMultiTenant was added via FEAT-09 (ADR-006 T4, 2026-05-05). Cross-tenant visibility for
host/IT-Admin paths is opt-in via `IDataFilter<IMultiTenant>.Disable()` -- mirroring
`DoctorsAppService` -- when `CurrentTenant.Id == null`.

## Conventions

### Booking onboarding path

`GetOrCreatePatientForAppointmentBookingAsync` is the canonical entry point. Search by
trimmed email; if found return it; if not, find-or-create an IdentityUser with
`CaseEvaluationConsts.AdminPasswordDefaultValue`, grant the "Patient" role, then call
`PatientManager.CreateAsync`. Never replicate this sequence outside this method.

### SSN never-clear rule

`PatientManager.UpdateAsync` only overwrites `SocialSecurityNumber` when the incoming value
is non-empty. Sending blank on admin update, profile update, or booking update leaves the
stored value unchanged. The layer CLAUDE.md covers this rule; do NOT bypass it.

### Fuzzy match before insert

Call `PatientMatching.Normalise` / `NormaliseSsn` / `NormalisePhone` BEFORE
`IPatientRepository.FindBestMatchAsync`. The layer CLAUDE.md defines the threshold
(3 of 6 keys). `PatientManager.FindOrCreateAsync` is the entry point.

### Length validation is double-enforced

Both the `Patient` constructor and `PatientManager.CreateAsync`/`UpdateAsync` run
`Check.Length` on all 15 string fields. This is a code-gen artifact, not a bug; do not
remove either layer.

## Gotchas

1. **SSN stored plaintext.** No encryption at rest (the column is `nvarchar`). Display masking
   is applied at the DTO/UI layer (SsnVisibility.MaskToLast4 + audited reveal), not in storage.
   At-rest encryption is a deferred decision -- none scheduled.

2. **Hardcoded default password (Q-12).** Auto-created patient IdentityUsers get
   `AdminPasswordDefaultValue`. Combined with the relaxed password policy (SEC-05), accounts
   are trivially guessable. Intent: replace with invite-token flow.

3. **No email uniqueness guard.** Admin `CreateAsync` does not check for a duplicate Patient
   email before inserting; only the booking flow's `_userManager.CreateAsync` enforces
   IdentityUser-side uniqueness. Two Patient rows with the same email are possible.

4. **Booking update preserves frozen fields.** `UpdatePatientForAppointmentBookingAsync`
   keeps `IdentityUserId`, `TenantId`, `GenderId`, `DateOfBirth`, and `PhoneNumberTypeId`
   from the existing row. Admin `UpdateAsync` does not use these fallbacks.

5. **Profile test suite is incomplete.** `GetMyProfileAsync` / `UpdateMyProfileAsync` tests
   are skipped pending `WithCurrentUser` test infrastructure. Profile endpoints rely on
   `IdentityUserId == CurrentUser.Id`; that coupling is hard to fake without the fixture.

## Related

- docs/security/DATA-FLOWS.md (SSN egress + PHI handling)
- docs/parity/_parity-flags.md
