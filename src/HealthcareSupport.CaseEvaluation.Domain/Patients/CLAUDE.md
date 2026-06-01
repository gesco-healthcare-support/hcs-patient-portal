# Patients -- worker's comp IME patient records and onboarding

Three usage patterns: admin CRUD, inline appointment booking (get-or-create + auto-create
IdentityUser), and self-service profile for the logged-in patient.

## What lives here

| File | Purpose |
|---|---|
| `Patient.cs` | Aggregate root; see Entity Shape below |
| `PatientManager.cs` | Domain service: `CreateAsync`, `UpdateAsync`, `FindOrCreateAsync` (fuzzy match) |
| `PatientWithNavigationProperties.cs` | Projection: Patient + State? + AppointmentLanguage? + IdentityUser? + Tenant? |
| `IPatientRepository.cs` | Custom repo: `GetWithNavigationPropertiesAsync`, `FindBestMatchAsync`, `GetListAsync`/`GetCountAsync` with rich filter set |

## Entity shape

See `Patient.cs` for all fields. Key structural facts:
- `SocialSecurityNumber` is `string? [max 20]`, stored plaintext (PII; see SSN gotcha).
- `RefferedBy` is `string? [max 50]` -- the typo propagates to the DB column name; fixing it
  requires a migration.
- `PhoneNumberTypeId` uses non-sequential legacy values `Work=28 / Home=29`.
- `IdentityUserId` FK is `NoAction` (required); `StateId` and `AppointmentLanguageId` are
  `SetNull` (optional).
- `IMultiTenant` was added via FEAT-09 (ADR-006 T4, 2026-05-05). Cross-tenant visibility
  for host/IT-Admin paths is opt-in via `IDataFilter<IMultiTenant>.Disable()` -- mirroring
  `DoctorsAppService` -- when `CurrentTenant.Id == null`.

## Conventions

### Booking onboarding path

`GetOrCreatePatientForAppointmentBookingAsync` is the canonical entry point. Search by
trimmed email; if found return it; if not, find-or-create an IdentityUser with
`CaseEvaluationConsts.AdminPasswordDefaultValue`, grant the "Patient" role, then call
`PatientManager.CreateAsync`. Never replicate this sequence outside this method.

### SSN never-clear rule

Defined in the Domain layer CLAUDE.md. Do not bypass it.

### Fuzzy match before insert

Normalisation and threshold rules (3 of 6 keys) are defined in the Domain layer CLAUDE.md.
Entry point: `PatientManager.FindOrCreateAsync`.

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
