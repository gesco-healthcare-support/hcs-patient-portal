# ClaimExaminers -- reusable claim-examiner master; firm-less, login-optional

Claim-examiner contact records for workers' comp insurance carriers (UM3/UM4, 2026-06-05).
A firm-less, record-based mirror of `ApplicantAttorney` (OBS-8: no firm fields). Tenant-scoped.
Login is optional: an `IdentityUser` is linked later on self-register by email
(`ExternalUserType.ClaimExaminer = 2`). The per-appointment `AppointmentClaimExaminer` stays
free-text; wiring its selector to this master FK is deferred (CI1 coordination).

## What lives here

| File | Purpose |
|---|---|
| `ClaimExaminer.cs` | Aggregate root: `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`; FKs `StateId?`, `IdentityUserId?` (both optional) |
| `ClaimExaminerManager.cs` | DomainService: `CreateAsync` / `UpdateAsync` with `Check.Length` on every string field |
| `ClaimExaminerWithNavigationProperties.cs` | Projection wrapper: `State` + `IdentityUser` nav props |
| `IClaimExaminerRepository.cs` | Custom repo: nav-prop queries + filters |

Constants (`FirstName`/`LastName`, `PhoneNumber`, `FaxNumber`, `Street`, `City`, `ZipCode`,
`Email` max lengths) in `Domain.Shared/ClaimExaminers/ClaimExaminerConsts.cs`. There are NO
firm fields (no `FirmName`/`FirmAddress`/`WebAddress`) -- the deliberate difference from
`ApplicantAttorney` (OBS-8).

## Conventions

### Constructor vs Manager field split (code-gen artifact)

`ClaimExaminer(id, stateId, identityUserId, phoneNumber, email)` validates + sets only
`PhoneNumber` and `Email` in-ctor. The remaining string fields (`FirstName`, `LastName`,
`FaxNumber`, `Street`, `City`, `ZipCode`) are assigned directly in `Manager.CreateAsync()`
after the constructor returns. Bypassing the Manager leaves those fields null even when
values were supplied -- always go through `ClaimExaminerManager`.

### Login-optional (record-based, IP6 model)

`IdentityUserId` is `Guid?`. A claim examiner can exist as a pure reference record with no
login; `ExternalSignupAppService.AutoLinkClaimExaminerAsync` claims the unlinked master by
email when the person self-registers. No appointment back-link is performed (per-appointment
CE is free-text, not the master FK).

### Multi-tenancy

`IMultiTenant: yes`. DbContext config exists in BOTH `CaseEvaluationDbContext` and
`CaseEvaluationTenantDbContext`. `StateId` FK points to the host-scoped `State` entity.

## Gotchas

- **Firm-less by design (OBS-8).** Do not re-add firm fields to mirror `ApplicantAttorney`;
  the examiner is an individual contact, not a firm profile.
- **Per-appointment CE is NOT this entity.** `Appointment.ClaimExaminerName/Email` (CI1) is
  free-text on the appointment; this master is the reusable directory. Linking the two via a
  master FK is intentionally deferred.
