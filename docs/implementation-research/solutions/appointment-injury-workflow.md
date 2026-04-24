# Appointment injury details + body parts + claim examiner + primary insurance

## Source gap IDs

- [G2-07 (track 02)](../../gap-analysis/02-domain-entities-services.md) -- "AppointmentInjuryDetail + 3 sub-entities (Body Parts + Claim Examiners + Primary Insurance)". Severity MVP-blocking, inventory effort L (7+ days).
- [DB-07 (track 01)](../../gap-analysis/01-database-schema.md) -- "Claim examiner linkage". Severity MVP-blocking, inventory effort M (5 story points). Evidence: `P:/PatientPortalOld/.../Models/AppointmentClaimExaminer.cs`; no NEW table.
- [DB-08 (track 01)](../../gap-analysis/01-database-schema.md) -- "Primary insurance on appointment". Severity MVP-blocking, inventory effort S (3 story points).
- [DB-09 (track 01)](../../gap-analysis/01-database-schema.md) -- "Injury details + body parts". Severity MVP-blocking, inventory effort M (5-8 story points).
- [03-G04 (track 03)](../../gap-analysis/03-application-services-dtos.md) -- "AppointmentInjuryDetail service + sub-collections". Severity MVP-blocking, inventory effort 4-6 days.
- [A8-07 (track 08)](../../gap-analysis/08-angular-proxy-services-models.md) -- Angular proxy/service missing. Severity MVP-blocking, inventory effort S.
- [G-API-11 (track 04)](../../gap-analysis/04-rest-api-endpoints.md) -- REST API parity (4 endpoints). Severity MVP-blocking, "Medium (MVP-critical for workers-comp)".

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs:19-72` -- aggregate root. No `InjuryDetail`, no injury sub-collection, no `ClaimNumber`, `DateOfInjury`, `BodyParts`, `ClaimExaminer` or `PrimaryInsurance` property. The entity exposes only 13 non-Id scalar/FK fields. No navigation property would reach any of the 4 OLD tables.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md:25-77` -- documents 14 entity properties; "Relationships" table lists only 5 FKs and 3 inbound links (`AppointmentEmployerDetail`, `AppointmentAccessor`, `AppointmentApplicantAttorney`). No mention of injury details.
- `Grep "InjuryDetail|BodyPart|ClaimExaminer|PrimaryInsurance"` under `src/` -- zero matches for the entities. Four incidental matches under `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/` and `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/ExternalUserType.cs:6` are the enum VALUE `ClaimExaminer = 2` used as an external-user role name, not the entity. Independent evidence that the entity family is wholly absent.
- `Grep "InjuryDetail|BodyPart|ClaimExaminer|PrimaryInsurance"` under `angular/` -- zero matches. No Angular module, service, model, form, or route references any of the 4 concepts.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs:24-44` -- `DbSet<T>` declarations for 14 entities: AppointmentApplicantAttorneys, ApplicantAttorneys, AppointmentAccessors, AppointmentEmployerDetails, Appointments, Patients, DoctorAvailabilities, WcabOffices, Doctors, Locations, AppointmentLanguages, AppointmentStatuses, AppointmentTypes, States. No `DbSet<AppointmentInjuryDetail>` (or Body Parts / Claim Examiners / Primary Insurance).
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/` (ls filter `-v Designer | grep -v Snapshot`) -- 24 migrations from `20260131164316_Initial.cs` through `20260302064409_Added_AppointmentApplicantAttorney.cs`. None carries a name containing `Injury`, `BodyPart`, `ClaimExaminer`, `PrimaryInsurance`, or `WorkerCompensation`. Schema truly lacks the tables.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:1-133` -- permission constants defined for 15 entities; no `AppointmentInjuryDetails` nested static class. The permission tree is silent on the capability.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` (per `Appointments/CLAUDE.md` and track 02 cross-reference) has no injury-loading path. `GetWithNavigationPropertiesAsync` composes 5 LEFT JOINs (Patient + IdentityUser + AppointmentType + Location + DoctorAvailability); the join-set would have to extend by 4 tables + 1 nested body-part collection to cover the OLD read path.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/` -- 5 enum files (`Gender`, `BookingStatus`, `AccessType`, `PhoneNumberType`, `AppointmentStatusType`). No `IsCumulativeInjury`-supporting flag or enumerated body-part taxonomy. OLD stored `IsCumulativeInjury` as a plain `bool` column.
- `angular/src/app/proxy/index.ts` re-export list (per track-08 inventory) names 13 feature subfolders; none of them is `appointment-injury-details`. The generated proxy set is exhaustive: running `abp generate-proxy` today would not add injury types because the backend emits no injury schema into Swagger (confirmed by live probe below).

## Live probes

- Probe 1 (2026-04-24T22:10 local, HTTPS) -- `GET https://localhost:44327/swagger/v1/swagger.json` -> HTTP 200, body contains 317 paths. Python filter for path segments containing `injury`, `body-part`, `claim-examiner`, or `primary-insurance` returned `0` matches. Proves the backend exposes no HTTP surface for the capability today. Full log: [../probes/appointment-injury-workflow-2026-04-24T22-10-00.md](../probes/appointment-injury-workflow-2026-04-24T22-10-00.md).
- Probe 2 (2026-04-24T22:10 local, HTTPS) -- same swagger payload scanned for component schemas containing `Injury`, `BodyPart`, `ClaimExaminer`, or `PrimaryInsurance`. Zero matches. Confirms no DTO shape for the capability leaks through any other endpoint either. Same log file.
- Neither probe mutates state. No Bearer required for schema scan. No credential is written into the brief.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentInjuryDetail.cs:12-109` -- `[Table("AppointmentInjuryDetails", Schema = "spm")]`. Columns: `AppointmentInjuryDetailId` (identity PK), `BodyParts` (required plain `string` -- a free-text denormalised summary), `ClaimNumber` (required, 50 chars), `DateOfInjury` (date, required), `ToDateOfInjury` (date, nullable -- end of cumulative range), `IsCumulativeInjury` (bool, required), `WcabAdj` (50 chars, nullable), `CreatedById`/`CreatedDate`/`ModifiedById`/`ModifiedDate` (audit), `AppointmentId` (FK, required), `WcabOfficeId` (FK, nullable). Aggregate owner via `InverseProperty` collections for `AppointmentInjuryBodyPartDetails`, `AppointmentClaimExaminers`, and `AppointmentPrimaryInsurance` (note the singular table name for that third collection -- OLD uses table name `AppointmentPrimaryInsurance` -- but the nav property name is `AppointmentPrimaryInsurance`).
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentInjuryBodyPartDetail.cs:12-43` -- `[Table("AppointmentInjuryBodyPartDetails", Schema = "spm")]`. Columns: `BodyPartDescription` (required, 500 chars -- typed per body part, NOT an enum), `BodyPartId` (identity PK), `AppointmentInjuryDetailId` (FK, required). Minimal shape. No audit fields. No FK to a `BodyPart` master lookup table -- free text.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentClaimExaminer.cs:12-109` -- `[Table("AppointmentClaimExaminers", Schema = "spm")]`. Columns: `City` (50), `ClaimExaminerId` (identity PK), `ClaimExaminerNumber` (255), `Email` (255), `Fax` (15), `IsActive` (required), `Name` (50), `PhoneNumber` (20), `Street` (100), `Zip` (10), `AppointmentInjuryDetailId` (FK, required -- NOT `AppointmentId`), `StateId` (FK, nullable), plus audit columns. Notable: 11 scalar contact fields -- one examiner row carries a full address + phone + fax + email + an internal examiner ID number.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentPrimaryInsurance.cs:12-108` -- `[Table("AppointmentPrimaryInsurance", Schema = "spm")]` (singular table). Columns: `Attention` (string, no max), `City` (50), `FaxNumber` (20), `InsuranceNumber` (255), `IsActive` (required), `Name` (50), `PhoneNumber` (12), `Street` (255), `Zip` (10), `PrimaryInsuranceId` (identity PK), plus `AppointmentInjuryDetailId` (FK, required -- again via InjuryDetail, NOT Appointment), `StateId` (FK, nullable), and audit columns. Differences from ClaimExaminer: `Attention` line, `InsuranceNumber` label instead of `ClaimExaminerNumber`, no `Email` column, tighter `PhoneNumber` limit (12 vs 20).
- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentInjuryDetailDomain.cs:19` (per track-02 line 38) -- "Injury + body parts + claim examiners + primary insurance" orchestration. All 4 writes go through this single domain service using `AppointmentRequestUow`; body parts, examiners, and insurance are attached to the InjuryDetail row, not to the Appointment row. Track-02 line 38 records the orchestrator scope but does not include a line number; the domain service's responsibility is create/update across all 4 aggregate members via a single UnitOfWork commit.
- OLD API surface (track-04 inventory + track-03 line 140) -- `AppointmentInjuryDetailsController.cs:31-71` exposes the nested surface under `api/appointments/{appointmentId}/AppointmentInjuryDetails/*` with CRUD + search; sub-expansion (body parts, examiners, insurance) is handled implicitly via a `WithNavigationProperties` read shape. There is one controller for the aggregate, not four.
- **Track-10 errata applicable: none.** Search of `docs/gap-analysis/10-deep-dive-findings.md` for `G2-07`, `DB-07`, `DB-08`, `DB-09`, `A8-07`, `G-API-11`, `InjuryDetail`, `BodyPart`, `ClaimExaminer`, `PrimaryInsurance` returned zero matches. Track 10 does not revise any claim about this capability. Brief proceeds on tracks 01-08 evidence without correction.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10; Angular 20; row-level `IMultiTenant` (ADR-004). Manual controllers with `[RemoteService(IsEnabled = false)]` on every AppService (ADR-002). Dual DbContext (ADR-003). Riok.Mapperly (ADR-001). No `ng serve` (ADR-005).
- **Multi-tenancy: all 4 new entities are `IMultiTenant`.** They are appointment-children and an Appointment is `IMultiTenant` (`Appointments/CLAUDE.md` Multi-tenancy section: "IMultiTenant: Yes."). Per ADR-003 the DbContext config for all 4 entities must sit OUTSIDE any `IsHostDatabase()` guard so they replicate into the tenant DbContext as well.
- **Aggregate shape preserved from OLD.** `AppointmentInjuryDetail` is the aggregate root; body parts, claim examiners, and primary insurance FK to it via `AppointmentInjuryDetailId`, NOT directly to `Appointment`. This matches OLD and is the right design: injury-claim identity (claim number + dates + cumulative flag) is the unifying anchor for the stakeholder records.
- **HIPAA**: injury details are PHI. `DateOfInjury`, `ToDateOfInjury`, `BodyParts`, `BodyPartDescription`, `ClaimNumber`, `WcabAdj`, `ClaimExaminerNumber`, `InsuranceNumber` all qualify (they are case-identifying or medical-condition-identifying). Logging middleware must redact these. ABP `FullAuditedAggregateRoot` provides per-row `CreatorId`/`LastModifierId` sufficient for HIPAA access-attribution; no bespoke audit row is required for MVP (that is the job of `appointment-change-log-audit`). Row-level encryption is out of scope for MVP -- SQL Server TDE at the database level is the expected control for data-at-rest.
- **Body parts: keep OLD's denormalised-string `BodyParts` column AND the child collection.** OLD ships both (`AppointmentInjuryDetail.BodyParts` is a required string and `AppointmentInjuryBodyPartDetails` is an `ICollection`). The denormalised string is a human-readable aggregate label ("Lower back, Left knee, Right shoulder") while the child rows are one-per-body-part. Either an off-the-shelf body-part picker produces the string concatenation on save, or it writes child rows and the server computes the concat. Porting both paths matches OLD behavior and avoids breaking future data imports that carry only the string.
- **No body-part master lookup in MVP.** OLD stores `BodyPartDescription` as free text (500 chars), not an FK to a lookup. Porting verbatim; a lookup table can be added later (ties to `lookup-data-seeds` if and when Adrian wants a curated list).
- **Claim Examiner and Primary Insurance carry full contact details.** Both include full address + phone + (fax/email). Not a separate "person/organization" lookup. Each Appointment-injury may bind to its own claim examiner record; the same examiner may be re-entered for different appointments unless Adrian chooses to promote ClaimExaminer to a tenant-level aggregate later (see open sub-question).
- **Aggregate is queried as a single eager-load.** OLD's controller returns a WithNavigationProperties shape covering injury + body parts + 0..N examiners + 0..N insurance rows. NEW pattern for this is `AppointmentInjuryDetailWithNavigationPropertiesDto` populated by an explicit LINQ join in `EfCoreAppointmentInjuryDetailRepository` (consistent with `EfCoreAppointmentRepository` per `EntityFrameworkCore/CLAUDE.md`).
- **FK to WcabOffice + State is nullable.** OLD `AppointmentInjuryDetail.WcabOfficeId` and claim examiner / primary insurance `StateId` are nullable. NEW must match. `WcabOffice` and `State` are host-scoped (`CLAUDE.md` Multi-tenancy Rules section); the FK from a tenant-scoped child into a host entity is already a known pattern in this codebase (e.g., `AppointmentTypeId` on `Appointment`). Preserve `OnDelete(NoAction)` to stay consistent with the house convention.
- **OLD `AppointmentWorkerCompensation` is explicitly OUT OF SCOPE for this brief.** Track-02 line 213 (`G2-N10`) treats `AppointmentWorkerCompensation enum + row` as non-MVP (S, 1 day). The Q4 wording bundles "Injury details + Primary Insurance + WorkerCompensation" together, but the inventory and the gap-IDs assigned to this brief (G2-07, DB-07, DB-08, DB-09, 03-G04, A8-07, G-API-11) do not include a WorkerCompensation row. This brief covers only the 4 entities in G2-07.
- **NEW-SEC-02 must be respected.** The new AppService classes for this capability MUST carry method-level `[Authorize(...Create/Edit/Delete)]` attributes from day one, not rely on class-level `[Authorize]` only. The NEW-side security brief is landing in parallel; the injury brief should land with compliant attributes.

## Research sources consulted

- [ABP -- Entities](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities) -- accessed 2026-04-24. HIGH. Aggregate root + owned sub-entity patterns; `FullAuditedAggregateRoot<Guid>` base; protected parameterless constructor for EF Core proxying.
- [ABP -- Aggregates](https://abp.io/docs/latest/framework/architecture/domain-driven-design/aggregates) -- accessed 2026-04-24. HIGH. Guidance that sub-entities belong conceptually to one root aggregate; confirms `AppointmentInjuryDetail` is the correct aggregate root with body parts / examiners / insurance as owned children. Advises "keep aggregates small" but the aggregate here is naturally 4-part and OLD evidence shows the domain warrants unitary read+write.
- [ABP -- Multi-Tenancy (`IMultiTenant`)](https://abp.io/docs/latest/framework/architecture/multi-tenancy) -- accessed 2026-04-24. HIGH. Confirms auto-filter on `TenantId`; child aggregates must also carry `IMultiTenant` to participate in the filter. Not inherited through FK.
- [ABP -- EF Core Repositories and Custom Queries](https://abp.io/docs/latest/framework/data/entity-framework-core/custom-repositories) -- accessed 2026-04-24. HIGH. Pattern for custom repository with `IDbContextProvider<T>` and LINQ-compose shapes; directly applicable to `EfCoreAppointmentInjuryDetailRepository.GetWithNavigationPropertiesAsync(Guid id)` which must compose 4 tables.
- [ABP -- EF Core Migrations](https://abp.io/docs/latest/framework/data/entity-framework-core/migrations) -- accessed 2026-04-24. HIGH. Adding 4 tables in a single migration is safe; dual-DbContext consideration (ADR-003) means migration must be created against `CaseEvaluationDbContext` (`--startup-project HttpApi.Host`).
- [ABP -- Data Seeding](https://abp.io/docs/latest/framework/infrastructure/data-seeding) -- accessed 2026-04-24. HIGH. Only relevant for an optional body-part taxonomy seed; otherwise no seed contributor is needed.
- [Riok.Mapperly docs -- References to nested collections](https://mapperly.riok.app/docs/configuration/object-references/) -- accessed 2026-04-24. HIGH. `[Mapper]` auto-resolves owned collections by name; the `AppointmentInjuryDetailWithNavigationPropertiesDto` can be produced from the matching source projection without manual mapping glue. Matches ADR-001.
- [ABP -- Authorization + `[Authorize]` attribute](https://abp.io/docs/latest/framework/fundamentals/authorization) -- accessed 2026-04-24. HIGH. Required for NEW-SEC-02 compliance on the new AppService methods.
- [OLD source-of-truth for OLD aggregate shape] -- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentInjuryDetail.cs` + siblings, read 2026-04-24. HIGH, primary source.

## Alternatives considered

- **A. Full aggregate port: 4 ABP entities + aggregate-root Manager + AppService surface per entity + dedicated Angular feature module.** Matches OLD. Keeps the aggregate root + 3 children structure verbatim; uses one AppointmentInjuryDetailsAppService for the root write-path + three child-entity AppServices (Body Parts / Claim Examiners / Primary Insurance) for list-and-mutate surfaces that OLD exposes. One migration adds 4 tables + 1 FK per child. **Chosen.** Reasons: (1) preserves OLD's proven read path (the WithNavigationProperties join set maps 1:1 to the OLD stored-proc result); (2) each child has its own CRUD because UI needs to add/remove individual body parts / examiners / insurance rows without re-posting the parent; (3) matches existing repo patterns (`AppointmentEmployerDetails`, `AppointmentAccessors`, `AppointmentApplicantAttorneys` are all their own AppServices -- the repo has no "aggregate with internal-only children" precedent); (4) Riok.Mapperly auto-flows collections when DTO matches source.
- **B. Fold all 4 tables into `Appointment.ExtraProperties` (ABP `IHasExtraProperties` JSON bag).** Zero new tables; the injury aggregate becomes a JSON blob on Appointment. **Rejected.** Reasons: (1) not queryable -- the dashboard + reports briefs need counts by `DateOfInjury`, `ClaimNumber`, `IsCumulativeInjury`, which `EF.Functions.JsonValue` can express but without index coverage; (2) breaks on large aggregates (a patient with 5 body parts + 2 examiners + 2 insurance rows is ~25 fields of JSON per Appointment); (3) Mapperly does not project JSON values onto DTO scalars without glue; (4) HIPAA audit story worsens -- per-field changes are invisible unless the audit serializes the whole bag.
- **C. Defer to post-MVP.** **Rejected.** Workers-comp IME cannot be scheduled without the injury anchor -- `ClaimNumber` and `DateOfInjury` are what the referring adjuster and the WCAB board match on. OLD ships it in the booking form; NEW's intake form (`angular/src/app/appointments/appointment-add.component.ts`) is already a ~30-field workers-comp intake per `Appointments/CLAUDE.md` line 197. Omitting injury would ship a demo, not an MVP.
- **D. Port OLD's 4 OLD `v*` view-tables as ABP `Shared/` DTO `WithNavigationPropertiesDto` classes without backing entities.** Read-only projection layer only. **Rejected.** Writes are required (add/remove body parts, edit examiner contact). A read-only projection cannot satisfy `G-API-11` (4 CRUD endpoints per OLD controller).
- **E. Promote ClaimExaminer + PrimaryInsurance to tenant-level aggregates (not appointment-children) reusable across appointments.** Appointments would link via a join table. **Rejected for MVP** (conditional for post-MVP). Reasons: (1) OLD shape is per-injury, not per-tenant -- no existing PROD data would fit; (2) data-migration and de-duplication are non-trivial; (3) YAGNI until Adrian hears from clinics that examiners repeat enough to warrant a master record. Record as open sub-question.

## Recommended solution for this MVP

Ship one EF migration, four ABP entities, four AppServices, four manual controllers, regenerated Angular proxy, one Angular feature module mirroring `appointment-employer-details/`. Aggregate shape matches OLD.

**Step 1 -- Domain entities + folders** (mirror `src/.../Domain/AppointmentEmployerDetails/` pattern):

- `src/.../Domain/AppointmentInjuryDetails/AppointmentInjuryDetail.cs` -- `FullAuditedAggregateRoot<Guid>, IMultiTenant` with props: `TenantId`, `AppointmentId` (Guid, required FK), `DateOfInjury` (DateTime, required, date-only), `ToDateOfInjury` (DateTime?, nullable), `ClaimNumber` (string, required, max 50), `IsCumulativeInjury` (bool, required), `WcabAdj` (string?, max 50), `BodyPartsSummary` (string?, max 500 -- the denormalised text summary), `WcabOfficeId` (Guid?, nullable FK to host `WcabOffice`). Constructor takes the required set; all non-required setters are virtual.
- `src/.../Domain/AppointmentBodyParts/AppointmentBodyPart.cs` -- `FullAuditedEntity<Guid>, IMultiTenant` with `TenantId`, `AppointmentInjuryDetailId` (required Guid FK), `BodyPartDescription` (string, required, max 500).
- `src/.../Domain/AppointmentClaimExaminers/AppointmentClaimExaminer.cs` -- `FullAuditedAggregateRoot<Guid>, IMultiTenant`. Props: `TenantId`, `AppointmentInjuryDetailId` (required), `Name` (max 50), `ClaimExaminerNumber` (max 255), `Email` (max 255), `PhoneNumber` (max 20), `Fax` (max 15), `Street` (max 100), `City` (max 50), `StateId` (Guid?, FK to host `State`), `Zip` (max 10), `IsActive` (bool, required).
- `src/.../Domain/AppointmentPrimaryInsurances/AppointmentPrimaryInsurance.cs` -- `FullAuditedAggregateRoot<Guid>, IMultiTenant`. Props: `TenantId`, `AppointmentInjuryDetailId` (required), `Name` (max 50), `InsuranceNumber` (max 255), `Attention` (max 255), `PhoneNumber` (max 12), `FaxNumber` (max 20), `Street` (max 255), `City` (max 50), `StateId` (Guid?, FK to host `State`), `Zip` (max 10), `IsActive` (bool, required).

Max-length constants live in `src/.../Domain.Shared/AppointmentInjuryDetails/AppointmentInjuryDetailConsts.cs` and sibling folders, per the house convention in `Appointments/AppointmentConsts.cs`.

**Step 2 -- DbContext config** (in `CaseEvaluationDbContext.OnModelCreating`, outside any `IsHostDatabase()` guard because entities are `IMultiTenant`):

```csharp
builder.Entity<AppointmentInjuryDetail>(b =>
{
    b.ToTable("AppointmentInjuryDetails", CaseEvaluationConsts.DbTablePrefix);
    b.ConfigureByConvention();
    b.Property(x => x.ClaimNumber).IsRequired().HasMaxLength(50);
    b.Property(x => x.BodyPartsSummary).HasMaxLength(500);
    b.Property(x => x.WcabAdj).HasMaxLength(50);
    b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
    b.HasOne<WcabOffice>().WithMany().HasForeignKey(x => x.WcabOfficeId).OnDelete(DeleteBehavior.NoAction);
});
// similar blocks for AppointmentBodyPart, AppointmentClaimExaminer, AppointmentPrimaryInsurance
```

Add matching `DbSet<>` declarations to both `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext` (per ADR-003). State FK on claim examiner and primary insurance configures `OnDelete(NoAction)` (matches sibling pattern).

**Step 3 -- EF migration.**

```
dotnet ef migrations add Added_AppointmentInjuryDetails \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
```

Migration adds 4 tables + 1 FK per child + 1 FK to Appointment + 1 nullable FK to WcabOffice + 2 nullable FKs to State. Review generated `.cs` for `CreateTable` ordering (parent tables before child FKs).

**Step 4 -- Domain managers** (thin, per house pattern -- business rules that cross entities go in AppService):

- `AppointmentInjuryDetailManager.CreateAsync/UpdateAsync` -- `Check.NotNull`/`Check.Length` on the required string + concurrency stamp. No state machine, no multi-entity orchestration (that lives in the app service).
- `AppointmentBodyPartManager`, `AppointmentClaimExaminerManager`, `AppointmentPrimaryInsuranceManager` -- same shape.

**Step 5 -- Application.Contracts DTOs** (per-feature folder, per-entity):

Every feature folder carries `{Entity}Dto.cs`, `{Entity}CreateDto.cs`, `{Entity}UpdateDto.cs`, `{Entity}WithNavigationPropertiesDto.cs` (for the root), `Get{Entities}Input.cs`, `I{Entity}AppService.cs`. Standard shapes per `Application.Contracts/CLAUDE.md`. `AppointmentInjuryDetailWithNavigationPropertiesDto` exposes `AppointmentInjuryDetail` + `List<AppointmentBodyPart>` + `List<AppointmentClaimExaminer>` + `List<AppointmentPrimaryInsurance>` + optional `WcabOffice` and `State` navigation entries. The base read shape for NEW uses actual entity types in the nav DTO (see `AppointmentWithNavigationPropertiesDto`), not secondary DTOs.

**Step 6 -- Permissions** (`Application.Contracts/Permissions/CaseEvaluationPermissions.cs`):

Add 4 nested static classes following the house pattern (Default/Create/Edit/Delete):

```csharp
public static class AppointmentInjuryDetails { public const string Default = GroupName + ".AppointmentInjuryDetails"; public const string Create = Default + ".Create"; public const string Edit = Default + ".Edit"; public const string Delete = Default + ".Delete"; }
public static class AppointmentBodyParts { /* same */ }
public static class AppointmentClaimExaminers { /* same */ }
public static class AppointmentPrimaryInsurances { /* same */ }
```

Register all four in `CaseEvaluationPermissionDefinitionProvider.cs`. Localization keys added to `src/.../Domain.Shared/Localization/CaseEvaluation/en.json`.

**Step 7 -- AppServices** (`src/.../Application/{Feature}/{Entity}AppService.cs`):

- `AppointmentInjuryDetailsAppService` -- standard CRUD (`GetAsync`, `GetListAsync`, `GetWithNavigationPropertiesAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`) plus a single `GetByAppointmentIdAsync(Guid appointmentId)` convenience method (returns 0..1 rows since OLD stores one injury-detail row per appointment). Class attribute `[Authorize(CaseEvaluationPermissions.AppointmentInjuryDetails.Default)]`; method-level `[Authorize(...Create)]`/`...Edit`/`...Delete` on the three mutations (NEW-SEC-02 compliance).
- `AppointmentBodyPartsAppService`, `AppointmentClaimExaminersAppService`, `AppointmentPrimaryInsurancesAppService` -- standard CRUD scoped by `AppointmentInjuryDetailId`. `GetList` filter takes `AppointmentInjuryDetailId` as a filter column so the UI can load children without loading the parent's full nav-props DTO on every interaction.

Riok.Mapperly partial classes go into `CaseEvaluationApplicationMappers.cs` per ADR-001: `AppointmentInjuryDetail -> AppointmentInjuryDetailDto`, `AppointmentBodyPart -> AppointmentBodyPartDto`, etc. `WithNavigationProperties` mapper has `AfterMap` only if the sub-collections need custom order (none do for MVP).

**Step 8 -- Manual controllers** (ADR-002; every AppService gets one at `src/.../HttpApi/Controllers/AppointmentInjuryDetails/...Controller.cs`):

- `AppointmentInjuryDetailController : AbpController, IAppointmentInjuryDetailsAppService` route `api/app/appointment-injury-details` + delegation.
- 3 sibling controllers for body parts / examiners / insurance at `api/app/appointment-body-parts`, `api/app/appointment-claim-examiners`, `api/app/appointment-primary-insurances`.

Each AppService carries `[RemoteService(IsEnabled = false)]`.

**Step 9 -- Custom repository on the root** (matches `EfCoreAppointmentRepository` pattern):

`src/.../EntityFrameworkCore/AppointmentInjuryDetails/EfCoreAppointmentInjuryDetailRepository.cs` implements `IAppointmentInjuryDetailRepository.GetWithNavigationPropertiesAsync(Guid id)` composing a single query with `.Include(x => x.BodyParts)`-style joins (or explicit LINQ `from` joins if the custom-repo convention in the repo is LINQ-only, which per `EntityFrameworkCore/CLAUDE.md` lines 23-25 it is). Returns an `AppointmentInjuryDetailWithNavigationProperties` projection holding all 4 entities + `WcabOffice` + joined `State` rows.

**Step 10 -- Angular proxy regenerate** (orchestrator only, Phase 1.5 / final build step).

Run `abp generate-proxy` after backend is green. Produces `angular/src/app/proxy/appointment-injury-details/`, `.../appointment-body-parts/`, `.../appointment-claim-examiners/`, `.../appointment-primary-insurances/` -- 4 folders each with a `{entity}.service.ts` + `models.ts`. `index.ts` re-exports via new entries. Nothing in `angular/src/app/proxy/` is hand-edited (CLAUDE.md line 33).

**Step 11 -- Angular feature module** (`angular/src/app/appointment-injury-details/`):

Mirror `appointment-employer-details/` layout: one list component + one detail/modal component + abstract service + detail-abstract service + route provider + base routes. The detail modal owns a reactive form for the root fields and three inline sub-lists (body parts / examiners / insurance), each with add/remove inline editors. The route is `/appointment-injury-details` behind `authGuard + permissionGuard('CaseEvaluation.AppointmentInjuryDetails')`. Add a deep-link from the `appointment-view/:id` and `appointment-add` pages: an "Injury details" accordion panel that loads `GetByAppointmentIdAsync(appointmentId)`.

**Step 12 -- CLAUDE.md for the feature** (`src/.../Domain/AppointmentInjuryDetails/CLAUDE.md`):

Standard per-feature documentation template covering file map, entity shape, aggregate diagram, multi-tenancy, mapper, permissions, business rules, inbound FKs, Angular UI, gotchas -- mirror `Appointments/CLAUDE.md`.

Folder touches (summary):

- `src/.../Domain.Shared/AppointmentInjuryDetails/*`, `AppointmentBodyParts/*`, `AppointmentClaimExaminers/*`, `AppointmentPrimaryInsurances/*` -- const files.
- `src/.../Domain/{AppointmentInjuryDetails,AppointmentBodyParts,AppointmentClaimExaminers,AppointmentPrimaryInsurances}/*` -- entity, manager, repo interface, CLAUDE.md.
- `src/.../Application.Contracts/...` -- per-feature DTO + AppService interfaces + permission constants + localization.
- `src/.../Application/...` -- AppService impls + Mapperly mappers (4 new partial classes in `CaseEvaluationApplicationMappers.cs`).
- `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` + `CaseEvaluationTenantDbContext.cs` -- 4 DbSets + 4 entity config blocks each.
- `src/.../EntityFrameworkCore/{AppointmentInjuryDetails}/EfCoreAppointmentInjuryDetailRepository.cs` -- custom repo (others use the default generic repo).
- `src/.../EntityFrameworkCore/Migrations/<timestamp>_Added_AppointmentInjuryDetails.cs` -- generated.
- `src/.../HttpApi/Controllers/...` -- 4 manual controllers.
- `angular/src/app/appointment-injury-details/*` -- new feature module + routes + service wrappers + list/detail components.
- `angular/src/app/proxy/*` -- auto-regenerated.

## Why this solution beats the alternatives

- **Queryable, indexed first-class schema**: the 4 tables + explicit FK columns support dashboard counts ("cumulative-injury appointments per month") and search filtering (claim-number exact match) without JSON predicates. Alternative B loses this.
- **Matches the house pattern exactly**: `AppointmentEmployerDetails`, `AppointmentAccessors`, `AppointmentApplicantAttorneys` are all first-class entities with dedicated AppServices. No new paradigm; the next developer can copy-paste an existing feature folder.
- **Respects the OLD read path**: OLD's `AppointmentInjuryDetailsController` returns the full aggregate on GET; porting the same shape via `GetWithNavigationPropertiesAsync` keeps the UI form shape unchanged from the clinic staff's perspective.
- **Complies with NEW-SEC-02 from day one**: new AppServices ship with method-level `[Authorize(...Create/Edit/Delete)]` attributes; we do not compound the existing defect.
- **Single migration**: one atomic EF migration adds all 4 tables; rollback is a single `database update <previous>`.
- **Mapperly-friendly**: Riok.Mapperly auto-flows scalar + nested-collection mappings when DTO matches entity names. Zero manual mapper glue beyond the 4 `[Mapper]` attribute classes.

## Effort (sanity-check vs inventory estimate)

Inventory says L (7+ days) on G2-07, with DB/03/04/08 rows summing compatibly (DB-07 M, DB-08 S, DB-09 M, 03-G04 4-6 days, G-API-11 Medium, A8-07 S). My estimate: **5-7 days**, which lands in the lower half of L. Breakdown:

- ~1 day: Domain.Shared consts + 4 entity classes + 4 manager classes.
- ~1 day: DbContext (both contexts) + migration + migration review + DbMigrator run.
- ~1 day: 4 AppService classes + 4 manual controllers + Mapperly partials + permissions registration + localization stubs.
- ~1 day: custom root repository `GetWithNavigationPropertiesAsync` + `GetByAppointmentIdAsync` + repository interface.
- ~1 day: `abp generate-proxy` + Angular feature module scaffold (list component + detail modal + abstract services + routes) following `appointment-employer-details/`.
- ~1 day: integration test fixture (xUnit + Shouldly, EF SQLite in-memory) for the aggregate + deep-link accordion wired into the Appointment view/add pages.
- ~0.5-1 day: feature CLAUDE.md + swagger probe re-run verifying 4+ new path entries + Angular e2e smoke.

**Confirms inventory L**, on the low end. Split suggested by Adrian's inventory (1 day entities + migration; 2 days managers + services; 2 days Angular; 1 day tests) is workable; I would collapse "managers + services" into one 2-day chunk and pull 0.5 day out for the custom repository.

## Dependencies

- **Blocks** `appointment-full-field-snapshot` (G2-10): only in the sense that the Appointment aggregate might need an `InjuryDetailId` FK if we ever want to require a one-injury-per-appointment invariant at the schema level. For MVP, leave the FK off Appointment and treat injury-detail as an optional child addressed by `AppointmentId` on the child side -- matches OLD. Soft dependency only.
- **Blocks** `appointment-search-listview` (G-API-19 / UI-07): a claim-number search column on the Appointment list is a cross-aggregate join that depends on this capability. The list brief can land first with no claim-number column; adding the column requires this brief merged.
- **Blocks** `appointment-request-report-export` (03-G07): reports by injury type / cumulative flag / claim number require the tables to exist.
- **Blocks** `appointment-change-log-audit` (G2-13): the audit brief includes injury-aggregate columns in the diff. Must land after this brief to avoid auditing a non-existent schema.
- **Blocked by** `lookup-data-seeds` (DB-15): the claim examiner and primary insurance entities carry nullable `StateId` FKs; end-to-end testing (form persistence + UI dropdown) requires seeded State rows. The migration itself is not blocked.
- **Blocked by** `lookup-data-seeds` for an optional `WcabOffice` dropdown (same logic).
- **Blocked by open question**: verbatim from `docs/gap-analysis/README.md:233-234`:
  - Q3: `"Claim Examiner sub-entity on Appointment: required for workers-comp tracking?"`
  - Q4: `"Primary Insurance, Injury details (incl. body parts), WorkerCompensation: required for workers-comp IME?"`
- Implementation must not begin until Adrian answers both. If Q3/Q4 answer is "yes to injury details + body parts + primary insurance, no to claim examiner" (unlikely but possible), the brief collapses to 3 entities -- schema drops the `AppointmentClaimExaminers` table and associated AppService. If the answer is "yes to all four", the brief proceeds as specified. If Q4 additionally scopes in `AppointmentWorkerCompensation`, that is a separate brief (`G2-N10`, non-MVP today).

## Risk and rollback

- **Blast radius**: moderate. Four new tables + four new AppServices + four new controllers + one Angular feature module. No existing behavior changes because existing paths never reference the new entities. The only cross-cutting touch is the two DbContext classes (add DbSet + config for each entity). A misconfigured `IsHostDatabase()` guard would be the highest-risk miss: all 4 entities are tenant-scoped; the guard MUST NOT wrap them (ADR-003). The migration is additive; existing Appointment rows see no effect. No change to `AppointmentsAppService` read or write paths except the Angular `appointment-view` adding an accordion panel that loads lazily.
- **Rollback (schema)**: `dotnet ef database update <previous migration> --project ...EntityFrameworkCore --startup-project ...HttpApi.Host` drops the 4 tables. Data is lost -- acceptable because the capability is net-new.
- **Rollback (code only)**: revert the feature branch; no existing service is left in an inconsistent state. The Angular feature module uninstalls cleanly because its route + providers live only in its own folder + proxy `index.ts` re-exports.
- **HIPAA risk**: PHI in the 4 tables. Mitigate via (a) standard ABP multi-tenancy filter (same as existing Appointment + Patient rows), (b) tag every new AppService method with `[Authorize(...)]`, (c) ensure logging middleware redacts `ClaimNumber`, `DateOfInjury`, `BodyPartsSummary`, `BodyPartDescription`, `ClaimExaminerNumber`, `InsuranceNumber` fields before emitting structured logs -- surface to the cross-cutting logging brief (not this one). Audit-by-default is already provided by `FullAuditedAggregateRoot` (creator + modifier captured).
- **Multi-tenancy drift risk**: if any of the 4 entities accidentally omits `IMultiTenant`, a tenant could see another tenant's claim numbers. Mitigate with a unit test that queries the entities with a non-matching `CurrentTenant.Id` and asserts zero rows (pattern borrowed from ABP multi-tenant test examples; surfaced to NEW-QUAL-01).
- **Concurrency**: ABP's `ConcurrencyStamp` on aggregate roots covers optimistic concurrency on the injury-detail root. Body parts / examiners / insurance child rows individually inherit concurrency stamps from `FullAuditedAggregateRoot` or `FullAuditedEntity` as applicable.

## Open sub-questions surfaced by research

- **Should ClaimExaminer be promoted to a tenant-level (or host-level) reusable master record?** OLD embeds full address + phone + fax + email on every injury-detail row; clinics may repeatedly type the same examiner. Recommend NO for MVP (ship as appointment-scoped embedded record per OLD) and flag as a post-MVP refactor once Adrian has a volume estimate. Same question applies to PrimaryInsurance. Neither blocks MVP.
- **Should body parts be a curated lookup (dropdown) or free text?** OLD ships free text (`BodyPartDescription` max 500). Recommend keep free text for MVP; a `BodyPart` master-lookup table + seed + dropdown is a post-MVP enhancement (1 day).
- **Should `BodyPartsSummary` (the denormalised text string on InjuryDetail) be computed server-side or user-typed?** OLD stores it as a required string with no computation evidence -- the UI likely concatenates. Recommend server-side concatenation on save (sort child rows by CreationTime, comma-join). Surface to the Angular brief if Adrian prefers UI-typed. Makes no schema difference.
- **Should `AppointmentInjuryDetail.AppointmentId` be unique (one injury-detail per appointment)?** OLD's schema does not enforce uniqueness (no unique index on the FK). Recommend add a unique index for MVP -- every observable OLD usage pattern is one injury-detail per appointment, and a uniqueness violation is a data-quality bug we should catch at write-time. If Adrian needs multiple injuries per appointment (e.g., cumulative + acute on the same case), remove the unique index and keep the FK.
- **Should the migration be named `Added_AppointmentInjuryDetails` (singular-aggregate) or `Added_AppointmentInjuryWorkflow` (cross-cutting 4-table add)?** Recommend `Added_AppointmentInjuryDetails` to match the repo's migration naming convention (`Added_AppointEmployerDetails`, `Added_ApplicantAttorney`).
- **Permission tree leaf granularity**: should `AppointmentBodyParts`, `AppointmentClaimExaminers`, `AppointmentPrimaryInsurances` each have their own permission nested class (4 permissions each) or roll up under `AppointmentInjuryDetails.*`? Recommend per-entity nested classes (follows house convention even for closely coupled entities like `AppointmentApplicantAttorneys` + `ApplicantAttorneys`). Four new nested classes, 16 new permission constants, 12 lines in the provider.
- **Do we need a `BookingStage` flag on InjuryDetail?** OLD ships the injury section as always-editable regardless of appointment status. Recommend same for MVP; future state-machine work (G2-01 brief) may gate edits post-Approved. Surface a sub-question there, not here.
- **WcabAdj field semantics**: OLD column name is `WcabAdj` (max 50, nullable) -- likely a WCAB adjudication location code or adjuster name. Confirm with Adrian; default to "free-text WCAB adjuster identifier" verbatim.
