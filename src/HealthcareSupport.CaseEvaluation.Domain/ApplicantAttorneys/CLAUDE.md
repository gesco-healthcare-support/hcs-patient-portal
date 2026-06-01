# ApplicantAttorneys -- attorney contact and firm profile; linked to appointments via join entity

Attorney records for workers' comp applicant-side counsel. Each record is tenant-scoped
and ties one `IdentityUser` account to a firm profile. Linked to `Appointment` via
`AppointmentApplicantAttorney` join entity.

## What lives here

| File | Purpose |
|---|---|
| `ApplicantAttorney.cs` | Aggregate root: `IMultiTenant`, 11 string fields + 2 FKs (`StateId`, `IdentityUserId`) |
| `ApplicantAttorneyManager.cs` | DomainService: `CreateAsync` / `UpdateAsync` with `Check.Length` on all string fields |
| `ApplicantAttorneyWithNavigationProperties.cs` | Projection wrapper: `State` + `IdentityUser` nav props |
| `IApplicantAttorneyRepository.cs` | Custom repo: nav-prop queries, filter by FirmName/PhoneNumber/City/StateId/IdentityUserId |

Constants (`FirstName/LastName max 50`, `FirmName 50`, `FirmAddress 100`, `WebAddress 100`,
`PhoneNumber 20`, `FaxNumber 19`, `Street 255`, `City 50`, `ZipCode 10`, `Email 100`) in
`Domain.Shared/ApplicantAttorneys/ApplicantAttorneyConsts.cs`.

## Conventions

### Constructor 4-vs-7 field split (code-gen artifact)

`ApplicantAttorney(id, stateId, identityUserId, firmName, firmAddress, phoneNumber, email)`
sets and validates 4 string fields in-ctor: `FirmName`, `FirmAddress`, `PhoneNumber`, `Email`.
The remaining 7 (`FirstName`, `LastName`, `WebAddress`, `FaxNumber`, `Street`, `City`,
`ZipCode`) are assigned directly in `Manager.CreateAsync()` after the constructor returns.

Consequence: if you bypass the Manager and call the constructor directly, those 7 fields
are null even when values were supplied. Always go through `ApplicantAttorneyManager`.

### Relationship to DefenseAttorney

Both `ApplicantAttorney` and `DefenseAttorney` are full domain entities, each with a matching
appointment join entity (`AppointmentApplicantAttorney` / `AppointmentDefenseAttorney`); the
two attorney sides are structurally symmetric.

Current structural difference: `ApplicantAttorney.IdentityUserId` is `Guid?` (optional)
matching `DefenseAttorney.IdentityUserId`; both entities carry `FirstName`/`LastName` fields
added under BUG-042. No symmetric divergence remains in the entity shape.

### Multi-tenancy

`IMultiTenant: yes`. DbContext config exists in BOTH `CaseEvaluationDbContext` (line 245)
and `CaseEvaluationTenantDbContext` (line 155) -- no `IsHostDatabase()` guard.
`StateId` FK points to the host-scoped `State` entity with `SetNull` on delete.

## Gotchas

- **Length validation runs twice on the 4 in-ctor fields.** Manager validates with
  `Check.Length` before calling the constructor; the constructor re-runs `Check.Length`
  on the same fields. The 7 post-construction fields are validated only by the Manager.
  Do not remove the Manager-side checks thinking the ctor covers them.

- **`IdentityUser` lookup filters by `Email.Contains(filter)`, not by username.**
  `GetIdentityUserLookupAsync` matches `IdentityUser.Email` only. A filter string that
  matches a username but not an email returns no results.

- **Default sort is `CreationTime desc`.** `ApplicantAttorneyConsts.GetDefaultSorting(bool)`
  returns the correct form with or without the entity name prefix for nav-prop queries.

## Related

- Domain layer overview: `src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md`
- Join entity: `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDefenseAttorneys/AppointmentDefenseAttorney.cs`
- Parity flags: `docs/parity/_parity-flags.md`
