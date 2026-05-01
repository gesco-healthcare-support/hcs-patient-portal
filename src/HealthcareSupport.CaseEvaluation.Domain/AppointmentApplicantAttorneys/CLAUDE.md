# AppointmentApplicantAttorneys

Join entity that links an Appointment to an ApplicantAttorney and an IdentityUser. At MVP this join IS the portal access mechanism: an attorney's presence in this table grants them portal visibility into the appointment. Created during the appointment booking flow; no standalone Angular UI.

See product intent: [docs/product/appointment-applicant-attorneys.md](/docs/product/appointment-applicant-attorneys.md).

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyConsts.cs` | Default sort: `Id asc` (atypical -- most entities sort by CreationTime desc) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/AppointmentApplicantAttorney.cs` | Aggregate root: FullAuditedAggregateRoot<Guid>, IMultiTenant, 3 required FKs |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyManager.cs` | DomainService -- CreateAsync / UpdateAsync (FK-only mutation surface) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyWithNavigationProperties.cs` | Read model bundling Appointment + ApplicantAttorney + IdentityUser |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/IAppointmentApplicantAttorneyRepository.cs` | Custom repo: GetWithNav / GetListWithNav / GetList / GetCount |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyDto.cs` | DTO -- FullAuditedEntityDto<Guid>, IHasConcurrencyStamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyCreateDto.cs` | Create input (3 Guids, no concurrency stamp) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyUpdateDto.cs` | Update input (3 Guids + ConcurrencyStamp) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyWithNavigationPropertiesDto.cs` | Nav-bundle DTO returned by GetList / GetWithNav |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentApplicantAttorneys/GetAppointmentApplicantAttorneysInput.cs` | Paged query: filter by AppointmentId / ApplicantAttorneyId / IdentityUserId |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentApplicantAttorneys/IAppointmentApplicantAttorneysAppService.cs` | AppService contract -- 9 methods |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/AppointmentApplicantAttorneys/AppointmentApplicantAttorneysAppService.cs` | CRUD + 3 lookups; class-level [Authorize], per-method Create/Edit/Delete checks |
| EntityFrameworkCore | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/AppointmentApplicantAttorneys/EfCoreAppointmentApplicantAttorneyRepository.cs` | EF Core repo implementation |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentApplicantAttorneys/AppointmentApplicantAttorneyController.cs` | 9 endpoints under `api/app/appointment-applicant-attorneys` |
| Mappers | `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` (lines ~363-373) | Riok.Mapperly partials: Entity->Dto, WithNav->WithNavDto |

## Entity Shape

```
AppointmentApplicantAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
- TenantId            : Guid?  (tenant isolation, settable)
- AppointmentId       : Guid   (FK -> Appointment, required)
- ApplicantAttorneyId : Guid   (FK -> ApplicantAttorney, required)
- IdentityUserId      : Guid   (FK -> IdentityUser, required)
```

Constructor accepts all 3 FKs + Id. No additional settable fields. No state enum -- this is a pure link record.

## Relationships

| FK Property | Target Entity | Required | Delete Behavior | Configured In |
|---|---|---|---|---|
| `AppointmentId` | Appointment | Yes | NoAction | Both DbContexts (no IsHostDatabase guard) |
| `ApplicantAttorneyId` | ApplicantAttorney | Yes | NoAction | Both DbContexts (no IsHostDatabase guard) |
| `IdentityUserId` | IdentityUser (Volo.Abp.Identity) | Yes | NoAction | Both DbContexts (no IsHostDatabase guard) |

`NoAction` everywhere: deleting a referenced Appointment / ApplicantAttorney / IdentityUser will fail at the database if a join row points at it. Delete the join row first, then the referenced parent.

`AppointmentApplicantAttorneyWithNavigationProperties` is a read-only DTO companion (NOT an EF-mapped owned type) used by the custom repo to materialise the three nav records alongside the join row.

## Multi-tenancy

**IMultiTenant: Yes.** The entity declares `Volo.Abp.MultiTenancy.IMultiTenant` and a settable `TenantId`. `builder.Entity<AppointmentApplicantAttorney>(...)` is configured outside any `IsHostDatabase()` guard in BOTH `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext`, so the table exists in both host and tenant DBs and rows are filtered by ABP's tenant data filter at runtime.

## Mapper Configuration

Riok.Mapperly partial classes in `CaseEvaluationApplicationMappers.cs`:

- `AppointmentApplicantAttorneyToAppointmentApplicantAttorneyDtoMappers : MapperBase<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>` (note: trailing "s" on the class name -- code-gen artifact, do not "fix" without checking referencing call sites)
- `AppointmentApplicantAttorneyWithNavigationPropertiesToAppointmentApplicantAttorneyWithNavigationPropertiesDtoMapper : MapperBase<AppointmentApplicantAttorneyWithNavigationProperties, AppointmentApplicantAttorneyWithNavigationPropertiesDto>`

No mapper to `LookupDto<Guid>` is defined for this entity itself -- the AppService's three lookup endpoints (`GetAppointmentLookupAsync`, `GetApplicantAttorneyLookupAsync`, `GetIdentityUserLookupAsync`) reuse the lookup mappers belonging to Appointment / ApplicantAttorney / IdentityUser. No `AfterMap` overrides on the two AppointmentApplicantAttorney mappers.

## Permissions

Constants in `CaseEvaluationPermissions.cs` (lines 126-132):

```
CaseEvaluation.AppointmentApplicantAttorneys           (Default -- class-level [Authorize])
CaseEvaluation.AppointmentApplicantAttorneys.Create
CaseEvaluation.AppointmentApplicantAttorneys.Edit
CaseEvaluation.AppointmentApplicantAttorneys.Delete
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` (lines 71-74). Localised label in `en.json`: `"Permission:AppointmentApplicantAttorneys": "Appointment Applicant Attorneys"`.

`AppointmentApplicantAttorneysAppService` has class-level `[Authorize(...Default)]`, plus per-method `[Authorize(...Create|Edit|Delete)]` on `CreateAsync`, `UpdateAsync`, `DeleteAsync`. Read methods (GetList / GetAsync / GetWithNav / 3 Lookups) require Default only.

`[RemoteService(IsEnabled = false)]` on the AppService -- the controller (with its own `[RemoteService]` attribute) is the only HTTP entry point.

## Business Rules

- **One applicant attorney per appointment at MVP.** Enforced in product intent, not in code. The DB schema does NOT have a unique constraint on `AppointmentId`; nothing prevents inserting a second join row for the same appointment. If co-counsel ever lands, this constraint becomes the spec change, not a bug. (Source: docs/product/appointment-applicant-attorneys.md)
- **Membership equals portal access.** A row in this table is the only thing granting an attorney portal visibility into the linked appointment at MVP -- there is no separate access-grant layer. Deleting a row revokes access. (Source: docs/product/appointment-applicant-attorneys.md)
- **Empty-Guid validation in AppService.** `CreateAsync` and `UpdateAsync` reject `Guid.Empty` for all three FKs with a `UserFriendlyException` before delegating to the Manager. Existence of the referenced entities is NOT verified -- a stale Guid will fail at the FK constraint instead.
- **Standard CRUD otherwise.** No uniqueness, no auto-generated fields, no frozen fields, no one-way state changes. All three FKs are mutable on update.
- **Default sort `Id asc`.** Atypical for this codebase. Most entities default to `CreationTime desc`. If list pages start showing oldest-first ordering unexpectedly, this is the cause.

## Angular UI Surface

No Angular UI -- this entity is managed via API only. Join rows are created during the Appointments booking flow (the canonical caller is the appointment attorney upsert path; precise trigger pending product interview, see `docs/product/appointment-applicant-attorneys.md`).

## Test Coverage

No backend tests yet. No tests directory exists at `test/HealthcareSupport.CaseEvaluation.Domain.Tests/AppointmentApplicantAttorneys/` or in any sibling test project. Coverage gap.

## Known Gotchas

1. **`IdentityUserId` may be redundant with `ApplicantAttorney.IdentityUserId`.** The parent `ApplicantAttorney` already carries an `IdentityUserId`. Storing it again on the join row creates a possibility for the two to drift. Likely a code-gen artifact from when the join was scaffolded. Product intent flags this as `[UNKNOWN]` pending interview -- do not assume the duplication is load-bearing without confirming.
2. **No DB-level uniqueness on `AppointmentId`.** The "one attorney per appointment" MVP rule is product policy, not a schema invariant. Concurrent creates can produce duplicate join rows.
3. **`NoAction` on all FKs.** Deleting a parent Appointment, ApplicantAttorney, or IdentityUser while a join row references it will fail at the DB. Callers must remove the join first.
4. **Mapper class name has a stray plural.** `AppointmentApplicantAttorneyToAppointmentApplicantAttorneyDtoMappers` ends in "Mappers" (plural). Code-gen output -- consistent with sibling features that have the same quirk.
5. **No domain manager logic.** `AppointmentApplicantAttorneyManager` is a thin pass-through to the repo (Check.NotNull + entity hydrate + Insert/Update). Any future invariant (e.g., uniqueness, access-grant side-effects) belongs HERE, not in the AppService.
6. **No tests.** Listed under Test Coverage above; repeated here so it shows up in audits.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Product intent: [docs/product/appointment-applicant-attorneys.md](/docs/product/appointment-applicant-attorneys.md)
- Sibling entities: [ApplicantAttorneys](/docs/product/applicant-attorneys.md), [Appointments](/docs/product/appointments.md), [AppointmentAccessors](/docs/product/appointment-accessors.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
