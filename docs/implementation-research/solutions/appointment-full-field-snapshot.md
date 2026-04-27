# Appointment full-field snapshot + RoleAppointmentType gate

## Source gap IDs

- [G2-10 (track 02)](../../gap-analysis/02-domain-entities-services.md) -- Appointment full-field snapshot. Missing columns per track-02 Delta line 194: `CancelledById`, `RejectedById`, `ReScheduledById`, `AppointmentApproveDate`, `CancellationReason`, `RejectionNotes`, `ReScheduleReason`, `PrimaryResponsibleUserId`, `OriginalAppointmentId`.
  - **Correction**: `AppointmentApproveDate` is already present in NEW `Appointment.cs:38` as `public virtual DateTime? AppointmentApproveDate { get; set; }`. The remaining 8 columns are genuinely missing. Brief scope is 8, not 9, columns.
- [G2-12 (track 02)](../../gap-analysis/02-domain-entities-services.md) -- RoleAppointmentType gate. Track-02 Delta line 196, effort S (1 day).

## NEW-version code read

- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs` lines 19-72: `Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant`. Properties present: `TenantId`, `PanelNumber`, `AppointmentDate`, `IsPatientAlreadyExist`, `RequestConfirmationNumber`, `DueDate`, `InternalUserComments`, `AppointmentApproveDate`, `AppointmentStatus`, `PatientId`, `IdentityUserId`, `AppointmentTypeId`, `LocationId`, `DoctorAvailabilityId`. Constructor signature at line 56 sets these 12 non-Id fields. No setters for any of the missing 8 columns.
- `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` lines 14-59: slim DomainService. `CreateAsync` signature (line 23) accepts no transition-actor fields or reasons. `UpdateAsync` signature (line 39) accepts the same 8 mutable fields; no reason strings and no actor IDs. Concurrency stamp handled at line 57.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`: per feature CLAUDE.md lines 122-136, `UpdateAsync` does NOT accept `AppointmentStatus`, `InternalUserComments`, `AppointmentApproveDate`, or `IsPatientAlreadyExist`. There is no separate status-transition path. Transition-actor IDs and reasons have no code path at all.
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` lines 192-204: `builder.Entity<Appointment>` maps 9 properties. No mapping for `CancelledById`/`RejectedById`/`ReScheduledById`/`CancellationReason`/`RejectionNotes`/`ReScheduleReason`/`PrimaryResponsibleUserId`/`OriginalAppointmentId`. EF would not track them.
- Glob `W:/patient-portal/main/src/**/RoleAppointmentType*` returns zero results. Nothing named `RoleAppointmentType` exists in Domain, Application, Contracts, EF Core, or HttpApi projects.
- Grep on `W:/patient-portal/main/src` for `RoleAppointmentType` returns zero matches. The role-to-AppointmentType allowlist concept is entirely absent.
- Migrations inventory: 22 migration files under `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/`. None add the missing columns (filename scan rules out `Added_Role*` or `*Snapshot*` additions). Most recent Appointment migration is `20260217183357_Added_DocAvailabilityId_Appointment.cs`.
- Track-02 gap doc lines 173-178 documents NEW enforcement: `AppointmentsAppService.CreateAsync:166-194` does existence checks for Guid FKs, `:201-202` marks slot Booked one-way, `:264-292` auto-generates `A#####`. No role-appointment-type gate anywhere in the flow.
- `AppointmentCreateDto` / `AppointmentUpdateDto` (per `Appointments/CLAUDE.md`): neither DTO accepts cancellation reason, rejection notes, or reschedule reason inputs. Consumers today cannot even submit those fields if the columns existed.
- No data seed contributor exists for `RoleAppointmentType` (none could, the entity does not exist) and none exists for any role-permission linkage. `src/HealthcareSupport.CaseEvaluation.Domain/Data/` contains only `CaseEvaluationDbMigrationService.cs`, `CaseEvaluationTenantDatabaseMigrationHandler.cs`, `ICaseEvaluationDbSchemaMigrator.cs`, `NullCaseEvaluationDbSchemaMigrator.cs` -- migration scaffolding only, no per-entity seed.

## Live probes

- `GET https://localhost:44327/swagger/v1/swagger.json` 2026-04-24T22:00 -- HTTP 200. Grep on the payload for `RoleAppointmentType`, `CancellationReason`, `RejectionNotes`, `ReScheduleReason`, `CancelledById`, `RejectedById`, `ReScheduledById`, `PrimaryResponsibleUserId`, `OriginalAppointmentId` returns zero hits. Confirms the gap via API surface. Probe log: [../probes/appointment-full-field-snapshot-2026-04-24T22-00-00.md](../probes/appointment-full-field-snapshot-2026-04-24T22-00-00.md).
- `GET https://localhost:44327/api/app/appointment-types` (via `MaxResultCount=3`) -- used to count current seeded AppointmentType rows, relevant for the RoleAppointmentType seed sizing. Probe recorded in the same log. Expect `totalCount: 0` (seed data not yet loaded -- confirms seed-data ordering: `lookup-data-seeds` must run before the `RoleAppointmentType` seed).
- No state-mutating probes. Per protocol, mutating probes are only permitted to prove `NEW-SEC-02`; this capability is a design gap, not a defect.

## OLD-version reference

- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/Appointment.cs` lines 14-107: full column list. Decorators confirm column types and lengths:
  - `CancelledById` (line 34-35): `Nullable<int>`.
  - `CancellationReason` (line 29-31): `string` with `[MaxLength(500)]`.
  - `RejectedById` (line 86-87): `Nullable<int>`.
  - `RejectionNotes` (line 90-92): `string` with `[MaxLength(500)]`.
  - `ReScheduledById` (line 101-102): `Nullable<int>`.
  - `ReScheduleReason` (line 105-107): `string` with `[MaxLength(500)]`.
  - `PrimaryResponsibleUserId` (line 82-83): `Nullable<int>`.
  - `OriginalAppointmentId` (line 73-74): `Nullable<int>` -- self-reference to the original Appointment when this row is a reschedule clone.
  - `AppointmentApproveDate` (line 18-19): `Nullable<DateTime>` (type `date`). Already in NEW.
  - `CreatedById`/`CreatedDate`/`ModifiedById`/`ModifiedDate` (lines 40, 44-45, 65-66, 69-70): ABP `FullAuditedAggregateRoot` already covers equivalents via `CreatorId`, `CreationTime`, `LastModifierId`, `LastModificationTime`. No port needed.
- `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/RoleAppointmentType.cs` lines 14-45: composite table under `spm` schema, identity PK `RoleAppointmentTypeId`, with `AppointmentTypeId` (FK -> `spm.AppointmentTypes`) and `RoleId` (FK -> `dbo.Roles`). Plain join, no extra columns. No audit fields in OLD.
- `P:/PatientPortalOld/PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs` lines 637-642: gate logic.
  ```csharp
  int currentUserRoleId = Convert.ToInt32(UserClaim.Get(ClaimTypes.Role));
  bool isAuthorizedUserAppointmentType = true;
  if (currentUserRoleId > 0)
  {
      isAuthorizedUserAppointmentType = AppointmentRequestUow.Repository<RoleAppointmentType>()
          .All()
          .Any(x => x.RoleId == currentUserRoleId && x.AppointmentTypeId == appointment.AppointmentTypeId);
  }
  ```
  Fails validation further down if `!isAuthorizedUserAppointmentType`. Single-role-claim model (OLD has a `dbo.Roles` enum-backed table; NEW uses ABP Identity where a user can carry multiple `IdentityRole` rows).
- **Track-10 erratum does not apply.** Grep of `docs/gap-analysis/10-deep-dive-findings.md` for `G2-10`, `G2-12`, `RoleAppointmentType`, `CancelledById`, `RejectedById`, `full field snapshot` returns zero matches. Track-10 leaves G2-10/G2-12 unchanged.

## Constraints that narrow the solution space

- ABP Commercial 10.0.2 on .NET 10; Angular 20; OpenIddict; row-level `IMultiTenant` with ABP `IDataFilter` auto-filter (ADR-004).
- Riok.Mapperly (ADR-001) for DTO mapping; manual controllers with `[RemoteService(IsEnabled = false)]` on every AppService (ADR-002); dual DbContext (ADR-003); no `ng serve` (ADR-005).
- Appointment is multi-tenant (`IMultiTenant`) per the EF mapping and the repo feature CLAUDE.md; the new columns must be tenant-scoped automatically via ABP's data filter. No extra code.
- EF Core migration required to add the 8 columns. Migration must be additive and all columns nullable -- existing rows cannot be retrofit (`CancelledById` for a not-cancelled appointment is genuinely null).
- **HIPAA**: the three reason fields (`CancellationReason`, `RejectionNotes`, `ReScheduleReason`) are free-text up to 500 chars. They can leak PHI (diagnosis, injury detail, patient-name gossip) if users type it. HIPAA policy response: (a) UX warns users not to enter PHI, (b) audit/log paths redact the reason field (do not emit it in structured logs), (c) reason fields are exposed via API only to roles with `CaseEvaluation.Appointments` Default + Edit. Do NOT encrypt-at-rest as a sole mitigation -- SQL Server TDE or equivalent is sufficient for data-at-rest, but the application-level risk is PHI in operational logs.
- Transition-actor IDs (`CancelledById`, `RejectedById`, `ReScheduledById`) are `Guid?` in NEW (since ABP uses `Guid` PKs), pointing at `AbpUsers.Id`. No FK constraint is required (OLD has none either; they are plain nullable int columns that point at `dbo.AbpUsers` by convention). Unconstrained is simpler, matches ABP `CreatorId`/`LastModifierId` convention.
- `OriginalAppointmentId` is a self-reference to `Appointments.Id` and MUST be nullable. No FK constraint is recommended: ABP `SoftDelete` + `OnDelete(NoAction)` is the house pattern, and a self-reference FK with cascade would be risky. Track-02 G2-N12 treats this as post-MVP elsewhere, but G2-10 pulls it in.
- `PrimaryResponsibleUserId` is the doctor's / staff's "primary responsible" user on the case. OLD treats it as a bare nullable int, unconstrained. Map to `Guid?` to `AbpUsers.Id`; no FK.
- `RoleAppointmentType` entity MUST use `Guid` PK per ABP convention. Composite (RoleId, AppointmentTypeId) unique constraint. `RoleId` references `AbpRoles.Id` (not an enum like OLD). The entity is host-scoped because `AppointmentType` is host-scoped -- don't apply `IMultiTenant`.
- The gate must accept a user with MULTIPLE roles. Rule: "user can book type T if ANY of user's roles has a RoleAppointmentType row for T." OLD's single-role-claim model does not apply.
- Couples with `appointment-state-machine` (G2-01): transition methods in the state machine are where `CancelledById`, `RejectedById`, `ReScheduledById`, `CancellationReason`, `RejectionNotes`, `ReScheduleReason` get populated. The brief for G2-10 adds the fields; the brief for G2-01 writes to them.
- Couples with `lookup-data-seeds` (DB-15) and `internal-role-seeds` (DB-16): the `RoleAppointmentType` seed needs both lookup AppointmentTypes and internal Roles to exist. Seeder must run after both.

## Research sources consulted

- [ABP -- Entities](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities) -- accessed 2026-04-24. HIGH confidence. Confirms `Guid` PK convention for aggregates and canonical `FullAuditedAggregateRoot<Guid>` base class with built-in `CreatorId`/`LastModifierId`/`DeleterId` (addresses the OLD `CreatedById`/`ModifiedById` columns without new code).
- [ABP -- EF Core Migrations](https://abp.io/docs/latest/framework/data/entity-framework-core/migrations) -- accessed 2026-04-24. HIGH confidence. Two-DbContext migration workflow (`CaseEvaluationDbContext` is the design-time context; `CaseEvaluationTenantDbContext` inherits most of the model). Confirms nullable-column additions are safe additive migrations with no schema-break risk.
- [ABP -- Data Seeding](https://abp.io/docs/latest/framework/infrastructure/data-seeding) -- accessed 2026-04-24. HIGH confidence. `IDataSeedContributor` pattern: implement `DataSeedAsync(DataSeedContext context)`, run via `DbMigrator`. Tenant-aware; can seed per-tenant. The `RoleAppointmentType` seed is host-only (host-scoped entity) so ignore the tenant parameter.
- [ABP -- Identity Module -- Roles](https://abp.io/docs/latest/modules/identity) -- accessed 2026-04-24. HIGH confidence. `IdentityRole.Id` is `Guid`; users carry 0..N roles via `IdentityUserRole`. Membership query: `CurrentUser.Roles` returns the role NAMES for the current user (string array), not IDs; role IDs can be fetched via `IIdentityRoleRepository.GetListAsync()` or `IIdentityUserRepository.GetRolesAsync(userId)`. This matters for the gate implementation.
- [ABP -- Authorization / IAuthorizationService](https://abp.io/docs/latest/framework/fundamentals/authorization) -- accessed 2026-04-24. MEDIUM confidence. ABP's permission system is not a natural fit: permissions are string constants, but the gate is data-driven (admin must be able to edit which roles can book which types at runtime). Do NOT use `[Authorize(Policy = ...)]` for this.
- [Riok.Mapperly docs](https://github.com/riok/mapperly) -- accessed 2026-04-24. HIGH confidence. Adding new scalar properties to `Appointment` auto-flows into the source-generated mapper when the target DTO has matching names. No mapper changes are needed if `AppointmentDto` gains the same 8 fields.
- [ABP EntityExtensionManager (ExtraProperties)](https://abp.io/docs/latest/framework/architecture/modularity/extending/object-extensions) -- accessed 2026-04-24. HIGH confidence. `ExtraProperties` is JSON-backed; queryable via `b.Property(e => e.ExtraProperties)` with `EF.Functions.JsonValue`. Rejected alternative B relies on this.
- [StackOverflow -- ABP nullable property + EF Core migration](https://stackoverflow.com/questions/75196243) -- accessed 2026-04-24. MEDIUM confidence. Confirms `public virtual DateTime? X { get; set; }` + EF mapping via `b.Property(x => x.X).HasColumnName(...)` produces a nullable column with no extra config. Matches OLD's `[Column(TypeName = "date")]` result.

## Alternatives considered

1. **Add the 8 columns to Appointment + create a new `RoleAppointmentType` entity + `IDataSeedContributor`.** Explicit schema, queryable via EF Core, admin-editable via a future CRUD. Effort S-M (one migration, one entity + EF config, one seed, one gate check in `AppointmentManager`). **chosen.** Reason: matches the house pattern (explicit entities elsewhere in this repo), queryable for reports ("who rejected which appointments in the last 30 days"), HIPAA-compliant audit via ABP's `FullAuditedAggregateRoot` extra-properties, and the RoleAppointmentType row set is admin-editable.
2. **Use ABP `IHasExtraProperties` + `ExtraProperties` JSON bag** for the 8 scalar fields instead of new columns. Zero migration for the Appointment entity; new values carried as a JSON blob. **rejected.** Reasons: (a) reason fields and actor IDs are queried by admin dashboards (e.g., "cancellations per user per month") -- JSON queries via `EF.Functions.JsonValue` work but lose index affinity, (b) `FullAuditedAggregateRoot` already exposes `ExtraProperties` but that is the wrong tool for first-class domain data, (c) Mapperly does not auto-project JSON values onto DTO scalar fields -- extra glue code needed.
3. **Hard-code the role-type allowlist in C# (dictionary literal in code).** Ship a `RolesThatCanBookPqme = [...]` constant. **rejected.** Reasons: (a) not admin-editable, (b) every change requires a deploy, (c) doesn't match NEW's direction of admin-configurable lookup data. Accepted only if Adrian decides G2-12 should be a pure code gate for MVP (flagged as open sub-question).
4. **Use ABP `PermissionDefinitionProvider` permissions (one permission per AppointmentType).** `CaseEvaluation.Appointments.BookType.PQME`, etc. Attach permissions to roles via the ABP admin UI; check via `IAuthorizationService.AuthorizeAsync(permissionName)` in `AppointmentManager.CreateAsync`. **rejected.** Reasons: (a) AppointmentType is data-driven (seeded rows, potentially admin-added), but permission definitions are code-time string constants -- new AppointmentTypes require deploy, (b) the permission tree becomes coupled to data, which violates ABP's design (permissions describe features, not rows), (c) no reduced effort vs chosen.

## Recommended solution for this MVP

Ship a single EF Core migration and a new domain entity.

**Step 1: Extend `Appointment` with 8 nullable columns.**
Edit `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs`. Add 8 properties and widen the constructor + extra setter methods as needed:

- `public virtual Guid? CancelledById { get; set; }`
- `public virtual Guid? RejectedById { get; set; }`
- `public virtual Guid? ReScheduledById { get; set; }`
- `public virtual string? CancellationReason { get; set; }` (max 500)
- `public virtual string? RejectionNotes { get; set; }` (max 500)
- `public virtual string? ReScheduleReason { get; set; }` (max 500)
- `public virtual Guid? PrimaryResponsibleUserId { get; set; }`
- `public virtual Guid? OriginalAppointmentId { get; set; }` (self-reference)

Add matching max-length constants to `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/AppointmentConsts.cs` (e.g., `CancellationReasonMaxLength = 500`).

**Step 2: Map the new columns in the DbContext.**
Edit `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` inside `builder.Entity<Appointment>(b => ...)` block (currently at lines 192-204). For each column:

```csharp
b.Property(x => x.CancelledById).HasColumnName(nameof(Appointment.CancelledById));
b.Property(x => x.RejectedById).HasColumnName(nameof(Appointment.RejectedById));
b.Property(x => x.ReScheduledById).HasColumnName(nameof(Appointment.ReScheduledById));
b.Property(x => x.CancellationReason).HasColumnName(nameof(Appointment.CancellationReason)).HasMaxLength(AppointmentConsts.CancellationReasonMaxLength);
b.Property(x => x.RejectionNotes).HasColumnName(nameof(Appointment.RejectionNotes)).HasMaxLength(AppointmentConsts.RejectionNotesMaxLength);
b.Property(x => x.ReScheduleReason).HasColumnName(nameof(Appointment.ReScheduleReason)).HasMaxLength(AppointmentConsts.ReScheduleReasonMaxLength);
b.Property(x => x.PrimaryResponsibleUserId).HasColumnName(nameof(Appointment.PrimaryResponsibleUserId));
b.Property(x => x.OriginalAppointmentId).HasColumnName(nameof(Appointment.OriginalAppointmentId));
```

No FK constraints added: transition-actor ids and `OriginalAppointmentId` remain bare Guid columns. This matches OLD and matches ABP's `CreatorId`/`LastModifierId` convention.

**Step 3: EF migration.**
Run `dotnet ef migrations add AppointmentSnapshotFields_AndRoleAppointmentType --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host` (from repo root). Keep the migration additive only -- no existing-row defaults, all nullable. Review the generated `.cs` to confirm 8 `AddColumn<...>` calls on `Appointments` plus one `CreateTable` for `RoleAppointmentTypes` (see step 5).

**Step 4: Define the `RoleAppointmentType` entity.**
Create `src/HealthcareSupport.CaseEvaluation.Domain/RoleAppointmentTypes/RoleAppointmentType.cs`:

```csharp
public class RoleAppointmentType : FullAuditedAggregateRoot<Guid>
{
    public Guid RoleId { get; set; }           // AbpRoles.Id
    public Guid AppointmentTypeId { get; set; } // AppointmentTypes.Id

    protected RoleAppointmentType() { }

    public RoleAppointmentType(Guid id, Guid roleId, Guid appointmentTypeId) : base(id)
    {
        RoleId = roleId;
        AppointmentTypeId = appointmentTypeId;
    }
}
```

Host-scoped (no `IMultiTenant`). Create `IRoleAppointmentTypeRepository` in the same folder; register via `services.AddTransient<IRoleAppointmentTypeRepository, EfCoreRoleAppointmentTypeRepository>();` pattern from existing repos.

**Step 5: Map the entity in DbContext inside `IsHostDatabase()` guard.**

```csharp
if (builder.IsHostDatabase())
{
    builder.Entity<RoleAppointmentType>(b =>
    {
        b.ToTable("RoleAppointmentTypes", CaseEvaluationConsts.DbSchema);
        b.ConfigureByConvention();
        b.Property(x => x.RoleId).IsRequired();
        b.Property(x => x.AppointmentTypeId).IsRequired();
        b.HasIndex(x => new { x.RoleId, x.AppointmentTypeId }).IsUnique();
    });
}
```

No FK constraints on `RoleId` or `AppointmentTypeId` at the EF level (to avoid coupling with ABP Identity DbContext); enforce at the application layer via existence checks in the seed + the admin CRUD (future).

**Step 6: Write a host-only data seed contributor.**
Create `src/HealthcareSupport.CaseEvaluation.Domain/RoleAppointmentTypes/RoleAppointmentTypesDataSeedContributor.cs` implementing `IDataSeedContributor, ITransientDependency`. In `SeedAsync`:
- Skip if `TenantId != null` (host-only).
- Fetch role names -> Guids via `IIdentityRoleRepository` for the 4 internal roles (names per `internal-role-seeds`): ItAdmin, StaffSupervisor, ClinicStaff, Adjuster.
- Fetch AppointmentType rows via `IAppointmentTypeRepository` (seeded by `lookup-data-seeds`).
- Default seed rows: grant ALL types to ItAdmin + StaffSupervisor; grant PQME + PQMEREEVAL + AME + AMEREEVAL to ClinicStaff; grant a single "Adjuster-permitted" subset to Adjuster. Exact defaults are an open sub-question (below).
- Idempotent: check for existing `(RoleId, AppointmentTypeId)` row before inserting.

**Step 7: Wire the gate into the booking path.**
The best place in NEW is the `AppointmentsAppService.CreateAsync` existence-check block (currently lines 166-194 per the track-02 doc). Add before the slot-validation block:

```csharp
var currentUserRoles = CurrentUser.Roles; // string[] of role names
if (!await _roleAppointmentTypeRepo.AnyAsync(rat =>
        currentUserRoleIds.Contains(rat.RoleId) &&
        rat.AppointmentTypeId == input.AppointmentTypeId))
{
    throw new UserFriendlyException(L["NotAuthorizedForAppointmentType"]);
}
```

`currentUserRoleIds` is resolved by mapping `CurrentUser.Roles` -> role Guids via `IIdentityRoleRepository.FindByNormalizedNameAsync`. Skip the gate when `CurrentUser.IsInRole("admin")` (host admin by-passes) or when `CurrentUser.Id == null` (unauthenticated, caught earlier by `[Authorize]`).

**Step 8: Update mappers.**
Riok.Mapperly auto-flows new scalar properties into `AppointmentDto` when the DTO carries matching properties. Add the 8 fields to `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentDto.cs`. Do NOT add them to `AppointmentCreateDto` or `AppointmentUpdateDto` -- these fields are set by state-machine transitions (G2-01 brief), not by the caller directly. Mapperly has nothing to regenerate manually; `dotnet build` triggers source-gen.

**Step 9: Update the feature CLAUDE.md.**
`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md` currently documents the entity shape at lines 44-60. Add the 8 new properties to the table and note they are populated by G2-01 transitions, not by AppService Create/Update. Also add a row under "Permissions / Gates" referencing `RoleAppointmentType`.

Folder touches (summary):
- `src/.../Domain.Shared/Appointments/AppointmentConsts.cs` -- add 3 max-length constants.
- `src/.../Domain/Appointments/Appointment.cs` -- add 8 properties.
- `src/.../Domain/RoleAppointmentTypes/*` -- new folder with entity, repo interface, seed contributor.
- `src/.../Application/Appointments/AppointmentsAppService.cs` -- add gate check.
- `src/.../Application.Contracts/Appointments/AppointmentDto.cs` -- add 8 output fields.
- `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` -- 8 property mappings + 1 entity block.
- `src/.../EntityFrameworkCore/RoleAppointmentTypes/EfCoreRoleAppointmentTypeRepository.cs` -- new repo impl.
- `src/.../EntityFrameworkCore/Migrations/<timestamp>_AppointmentSnapshotFields_AndRoleAppointmentType.cs` -- generated.
- `src/.../Domain/Appointments/CLAUDE.md` -- docs.

No Angular changes in this brief. DTO additions surface automatically via `abp generate-proxy`. Form fields for reasons are added in the G2-01 state-machine brief.

## Why this solution beats the alternatives

- **First-class data**: the 8 new columns are queryable with normal SQL + EF LINQ -- essential for the reports brief (`appointment-request-report-export`) and the dashboard (`dashboard-counters`). `ExtraProperties` JSON loses the index path.
- **Admin-editable gate without a deploy**: `RoleAppointmentType` as a table lets admins (via a future CRUD) shift which roles can book which types without a code change. Alternative 3 (code constant) and alternative 4 (code-level permissions) fail this test.
- **Matches the house pattern**: every other lookup/config in this repo is a `FullAuditedAggregateRoot<Guid>` host-scoped entity with an `IDataSeedContributor`. Matches AppointmentType, AppointmentStatus, AppointmentLanguage, Location.
- **Mapperly-friendly**: adding scalar nullable columns auto-flows into the generated mapper. Zero mapper glue.

## Effort (sanity-check vs inventory estimate)

Inventory says G2-10 is S-M (1-2 days) and G2-12 is S (1 day). Combined capability is S-M total.

- ~1 hour: add 8 properties, 3 max-length consts, 8 EF property mappings.
- ~1 hour: add `RoleAppointmentType` entity + repo interface + EF config + repo impl.
- ~1 hour: generate migration, review generated SQL, run DbMigrator locally.
- ~2 hours: `RoleAppointmentTypesDataSeedContributor` with role/type-name lookups and idempotency.
- ~1 hour: gate check in `AppointmentsAppService.CreateAsync` + `CurrentUser.Roles` -> Guid resolution helper.
- ~1 hour: `AppointmentDto` additions + `dotnet build` source-gen verification + quick unit test for the gate.
- ~1 hour: feature CLAUDE.md edits + migration smoke test via `/api/app/appointments/with-navigation-properties` probe.

Total ~1.0 to 1.5 days. **Confirms inventory estimate (S-M, 1-2 days).** Single PR; no split needed.

## Dependencies

- **Blocks** `appointment-state-machine` (G2-01): state-machine transition methods populate `CancelledById`/`RejectedById`/`ReScheduledById`/`CancellationReason`/`RejectionNotes`/`ReScheduleReason`. G2-01 cannot land until these columns exist.
- **Blocks** `appointment-change-requests` (G2-06): change requests record `RejectionNotes`/`ReScheduleReason` on the parent Appointment when a request is approved.
- **Blocks** `appointment-change-log-audit` (G2-13): the audit log compares old-vs-new values of all Appointment fields; the 8 new fields must be on the entity for the diff to include them.
- **Blocks** `appointment-request-report-export` (report over columns) and `dashboard-counters` (cards count cancellations/rejections by actor) -- both consume `CancelledById`/`RejectedById`.
- **Blocked by** `lookup-data-seeds` (DB-15): `RoleAppointmentTypesDataSeedContributor` needs AppointmentType rows to exist. Must run AFTER the lookup seeds.
- **Blocked by** `internal-role-seeds` (DB-16): seed needs ItAdmin/StaffSupervisor/ClinicStaff/Adjuster role Guids. `IDataSeedContributor.DataSeedAsync` is idempotent so the order is a hard requirement: fail-fast if seeds run out of order.
- **Blocked by open question**: none required to ship the entity + migration. The **default role-type allowlist** is a sub-question for the seed rows (below). If Adrian has not answered before build, land the migration + entity + empty seed; populate seed rows in a follow-up.

## Risk and rollback

- **Blast radius**: medium. Appointment is a core entity touched by every booking flow. Adding nullable columns is backward-compatible -- existing rows see NULL in the new columns, every existing query continues to work (no LINQ changes to non-touching code). The RoleAppointmentType gate is additive in `CreateAsync` only; if the gate misfires (wrong role claim resolution), no existing row is corrupted, but new bookings are blocked until fixed. Update and Delete flows are untouched. No cross-tenant leak risk: Appointment remains `IMultiTenant`; `RoleAppointmentType` is host-only and read-only to tenant users.
- **Rollback (full)**: `dotnet ef database update <previous migration name> --project ...EntityFrameworkCore --startup-project ...HttpApi.Host` removes the 8 columns and drops the `RoleAppointmentTypes` table. Revert the commits. No data loss since the columns were nullable and new.
- **Rollback (gate only)**: comment out the gate check in `AppointmentsAppService.CreateAsync` and redeploy; no migration revert needed. The table can remain; no consumer fails.
- **Concurrency risk**: none. New properties are plain nullable primitives; ABP's `ConcurrencyStamp` on `Appointment` already protects against stale writes.
- **HIPAA risk**: the three reason strings must be included in the "do-not-log" allowlist of the logging middleware (not yet implemented). Surface to `cross-cutting-backend` brief; this brief only enforces non-PHI in the UX warning text of the future add/edit forms (G2-01 brief).

## Open sub-questions surfaced by research

- **Default role-type allowlist**: should the seed rows for `RoleAppointmentType` mirror OLD's allowlist (copy from the PROD `spm.RoleAppointmentTypes` snapshot if Adrian can obtain one) or start with admin-gets-all + each internal role gets the obvious types (ItAdmin = all; StaffSupervisor = all; ClinicStaff = PQME + PQMEREEVAL + AME + AMEREEVAL; Adjuster = PQME + AME)? Not MVP-blocking; default set can be updated post-launch via CRUD.
- **Reason field PHI policy**: should the Angular add-edit form (G2-01 brief) enforce a hard non-PHI pattern (regex block for SSN, DOB formats) or rely on a UX warning banner only? Recommend warning-only for MVP with a post-launch audit.
- **Gate bypass for host admin**: should the host `admin` user (seeded by ABP) always bypass the gate? Recommend yes -- the seeded admin is operational and needs to book any type for testing. Implemented by `CurrentUser.IsInRole("admin")` early-return in the gate.
- **Self-reference constraint for `OriginalAppointmentId`**: leave as plain `Guid?` with no EF relationship (matches OLD, matches ABP convention for soft-delete-safe self refs) or add `builder.HasOne<Appointment>().WithMany().HasForeignKey(x => x.OriginalAppointmentId).OnDelete(DeleteBehavior.NoAction)`? Recommend plain `Guid?` for MVP; add relationship only if a future eager-load scenario requires it.
