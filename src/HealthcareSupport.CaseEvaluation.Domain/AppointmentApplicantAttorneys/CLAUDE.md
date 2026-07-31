# AppointmentApplicantAttorneys -- join entity granting attorney portal access

Links an `Appointment` to an `ApplicantAttorney` and an `IdentityUser`. Membership in
this table IS portal access at MVP: a row grants the attorney visibility into that
appointment; deleting the row revokes it. No separate access-grant layer exists.

No standalone Angular UI -- join rows are created during the Appointments booking flow.

## What lives here

| File | Purpose |
|---|---|
| `AppointmentApplicantAttorney.cs` | Aggregate root: `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`; pure link record (see below) |
| `AppointmentApplicantAttorneyManager.cs` | Domain service: `CreateAsync` / `UpdateAsync`; add invariants here, not in AppService |
| `AppointmentApplicantAttorneyWithNavigationProperties.cs` | Read model bundling Appointment + ApplicantAttorney + IdentityUser (NOT an EF-mapped type) |
| `IAppointmentApplicantAttorneyRepository.cs` | Custom repo: `GetWithNav` / `GetListWithNav` / `GetList` / `GetCount` |

Constructor accepts all 3 FKs + Id. No additional settable fields; this is a pure link record
(`AppointmentId`, `ApplicantAttorneyId`, `IdentityUserId` all required).

## Conventions

- **Mapper location.** Riok.Mapperly partials for this feature are the
  `AppointmentApplicantAttorneyMappers` partial class in `CaseEvaluationApplicationMappers.cs`.
  The mapper class name ends in "Mappers" (plural) -- code-gen artifact, do not rename
  without checking all call sites.
- **Lookup endpoints reuse parent mappers.** The three AppService lookups
  (`GetAppointmentLookupAsync`, `GetApplicantAttorneyLookupAsync`, `GetIdentityUserLookupAsync`)
  use the `LookupDto<Guid>` mappers from their respective parent features. No
  `AppointmentApplicantAttorney`-owned lookup mapper exists.
- **Default sort `Id asc`.** Atypical -- most entities sort `CreationTime desc`. Oldest-first
  ordering in list pages is expected, not a bug.
- **`[RemoteService(IsEnabled = false)]` on AppService.** HTTP entry point is the controller
  only (`api/app/appointment-applicant-attorneys`).

## Gotchas

1. **`IdentityUserId` may duplicate `ApplicantAttorney.IdentityUserId`.** The parent entity
   already carries this FK; storing it again allows drift. Likely a scaffolding artifact.
   Do not treat the duplication as load-bearing without confirming intent.
2. **No DB-level uniqueness on `AppointmentId`.** The "one attorney per appointment" MVP
   rule is product policy, not a schema constraint. Concurrent creates can produce duplicates.
3. **`NoAction` on all 3 FKs.** Deleting a parent Appointment, ApplicantAttorney, or
   IdentityUser while a join row references it will fail at the DB. Remove the join row
   first.
4. **Exists in BOTH DbContexts without an `IsHostDatabase()` guard.** Table lives in host
   and tenant DBs; ABP's tenant data filter controls runtime visibility.
5. **Empty-Guid guard in AppService, not Manager.** `CreateAsync` / `UpdateAsync` reject
   `Guid.Empty` for all 3 FKs with a `UserFriendlyException`. Referenced entity existence
   is NOT verified -- a stale Guid fails at the FK constraint instead.
6. **No tests.** No test directory exists for this feature; coverage gap.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- docs/database/EF-CORE-DESIGN.md
