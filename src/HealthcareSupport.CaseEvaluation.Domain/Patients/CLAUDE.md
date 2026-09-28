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

`GetOrCreatePatientForAppointmentBookingAsync` is the canonical entry point, and booking is
**record-only** (IP6, 2026-06-05): it creates a Patient row with no login, mints no
IdentityUser, grants no role and sets no password. The order is: email fast path (a blank
email skips it), then the 3-of-6 duplicate scan, then `PatientManager.FindOrCreateAsync`
with `identityUserId: null`. The patient claims a login later; `ExternalSignupAppService.RegisterAsync`
links the record by email and grants the Patient role. Never replicate this sequence
outside this method.

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

2. **Booking mints no login.** The old path that created patient IdentityUsers with a shared
   default password was removed by deletion in IP6 (2026-06-05); do not reintroduce it. A
   patient's login comes only from registration.

3. **No email uniqueness guard.** `Patient.Email` is NOT NULL but has no unique index, and
   admin `CreateAsync` does not check for a duplicate email before inserting. The booking path
   avoids duplicates through its email fast path and 3-of-6 matching, not through a constraint.
   Two Patient rows with the same email are possible.

4. **Booking update preserves frozen fields.** `UpdatePatientForAppointmentBookingAsync`
   keeps `IdentityUserId`, `TenantId`, `GenderId`, `DateOfBirth`, and `PhoneNumberTypeId`
   from the existing row. Admin `UpdateAsync` does not use these fallbacks. Who may call it
   (#598): internal staff holding `Patients.Edit`, or the patient's own login -- never a party to
   the patient's appointments, because booking makes anyone a party
   (`Application/Patients/PatientBookingEditAccess.cs`).

5. **Profile test suite is incomplete.** `GetMyProfileAsync` / `UpdateMyProfileAsync` tests
   are skipped pending `WithCurrentUser` test infrastructure. Profile endpoints rely on
   `IdentityUserId == CurrentUser.Id`; that coupling is hard to fake without the fixture.

## Related

- docs/security/DATA-FLOWS.md (SSN egress + PHI handling)
- docs/parity/_parity-flags.md
