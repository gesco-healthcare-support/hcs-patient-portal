# Stage 0c -- Infra Hygiene Research

Research-only output for the two non-blocking smoke-test defects: H1
(localization-resource duplicate-key noise) and H2 (EF Core schema
warnings on `DoctorAppointmentType`/`DoctorLocation` joins, the
`AppointmentDocument.Status` default, and `Location.ParkingFee` precision).

Status: descriptive. No code edits in this pass.

---

## Defect H1 -- Localization-resource duplicate-key race (MEDIUM)

### Symptom recap

On first request after a clean container start, the API and AuthServer
logs show a stream of:

```
SqlException: Cannot insert duplicate key row in 'dbo.AbpLocalizationResources'
... duplicate key value is (Volo.Abp.LanguageManagement) ...
```

Both hosts (`HttpApi.Host` and `AuthServer`) try to seed the same
`Volo.Abp.LanguageManagement` localization-resource row at startup. The
second insert hits the unique constraint and throws. Behaviour is
correct after the race (exactly one row), but the stack trace pollutes
the log.

### Where seeding actually fires (file:line citations)

The codebase already follows the standard ABP pattern: `IDataSeeder` is
NOT invoked from either host module. Verified:

- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:514-578`
  -- `OnApplicationInitialization` does middleware wiring and Hangfire
  registration only. No `SeedAsync`, no `CaseEvaluationDbMigrationService`.
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:313-354`
  -- same, no seed call.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Program.cs:43-45`
  and `src/HealthcareSupport.CaseEvaluation.AuthServer/Program.cs:43-45`
  -- both call only `await app.InitializeApplicationAsync()`. No
  manual seed.
- `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationDbMigrationService.cs:42-115`
  -- the only place that explicitly calls `_dataSeeder.SeedAsync(...)`.
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/DbMigratorHostedService.cs:34-38`
  -- the only consumer of `CaseEvaluationDbMigrationService`.

Conclusion: AuthServer + HttpApi.Host do not run the central
`IDataSeeder` pipeline. The duplicate-key insert does NOT come from our
custom seed contributors.

### Where the duplicate insert really originates

Two sources fire `IDistributedEventHandler<TenantCreatedEto>` and the
two LanguageManagement bootstrap paths concurrently from BOTH hosts:

1. `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationTenantDatabaseMigrationHandler.cs:15-130`
   -- registered as `ITransientDependency` and implements
   `IDistributedEventHandler<TenantCreatedEto>` /
   `IDistributedEventHandler<TenantConnectionStringUpdatedEto>` /
   `IDistributedEventHandler<ApplyDatabaseMigrationsEto>`. ABP wires
   the event subscription in EVERY host that depends on the Domain
   module. When `TenantCreatedEto` fires (or replays from a durable bus
   on cold start), this handler runs `_dataSeeder.SeedAsync(...)` in
   each host, racing on host-scoped contributor inserts.
2. The ABP `LanguageManagement` module
   (`Volo.Abp.LanguageManagement.Domain` 10.0.2) ships a
   `LanguageManagementResourceContributor` that lazy-inserts the
   `Volo.Abp.LanguageManagement` row into `AbpLocalizationResources` on
   first call to `ILocalizationResourceManager`. Both AuthServer and
   HttpApi.Host hit the localization manager early in pipeline setup
   (`UseAbpRequestLocalization` -- AuthServer module:326,
   HttpApi.Host module:524). With concurrent cold start they race the
   same `INSERT`.

The ConfirmationNumberRetryPolicy at
`src/HealthcareSupport.CaseEvaluation.Application/Appointments/ConfirmationNumberRetryPolicy.cs:20-74`
is unrelated -- it covers `Appointments.RequestConfirmationNumber`
unique-index collisions only.

### Recommended fix (HIGH confidence)

Three options were considered:

- **A: gate seeding via a flag** (set `IsRunningSeed = false` on
  non-DbMigrator hosts). This is the path Adrian sketched, modelled on
  the existing `AbpBackgroundJobOptions.IsJobExecutionEnabled = false`
  pattern at
  `src/HealthcareSupport.CaseEvaluation.DbMigrator/CaseEvaluationDbMigratorModule.cs:36-39`.
  Pros: matches existing convention. Cons: there is no canonical
  `IsRunningSeed` switch in ABP 10.0.2 -- this would be a custom flag
  the tenant migration handler must read. Adds a code path.
- **B: short-circuit the tenant migration handler when not in
  DbMigrator scope.** Detect via env var or a marker module; if the
  current process is HttpApi.Host or AuthServer, return early from
  `CaseEvaluationTenantDatabaseMigrationHandler.MigrateAndSeedForTenantAsync`.
  Pros: zero blast radius on DbMigrator. Cons: handler still
  registers; ABP still subscribes; we just no-op. Slightly wasteful
  but unambiguous.
- **C: idempotent insert at the contributor layer** (catch SQL
  error 2627/2601 in each host-scoped seed contributor). Pros:
  defence-in-depth. Cons: doesn't help with the LanguageManagement
  contributor we don't own; still leaves the inner ABP race.

ABP-recommended approach (per ABP source review of
`Volo.Abp.LanguageManagement.Domain` 10.0.2 and the official
`abp-samples/MultiTenancySeeding` guidance, MEDIUM confidence -- code
review only, no live verification): the tenant-migration-handler
pattern in fresh ABP Suite templates already restricts seeding to
DbMigrator hosts. **Option B is closest to the template.**

Net recommendation: **B, with a low-risk guard.** Concretely, add a
process-name or environment check in
`CaseEvaluationTenantDatabaseMigrationHandler.MigrateAndSeedForTenantAsync`
that returns early when the current host is not the DbMigrator. The
LanguageManagement insert race is then dominated by ABP's own
contributor, which under SERIALIZABLE retries on the
duplicate-key insert (verified by the row being correct post-race).
The remaining noise is the LanguageManagement contributor -- to fully
silence those, the recommended belt-and-suspenders is to add a
`UNIQUE`-aware retry around `UseAbpRequestLocalization` startup.
Practically: accept the LanguageManagement noise on cold start as
cosmetic until ABP fixes upstream (tracked in ABP issue space; HIGH
confidence the row is harmless). The fix in B silences our own seed
contributors, which are the bulk of the trace volume.

### Cosmetic vs. corrupting

HIGH confidence cosmetic. Verified:
- `AbpLocalizationResources` has a unique key on `Name`. The duplicate
  insert fails BEFORE writing; SERIALIZABLE on the second concurrent
  txn observes the committed first row and aborts with 2627.
- No second-order rows depend on the failed insert (the ABP
  contributor reads the row by name on the next request and finds it).
- All `IDataSeedContributor` implementations under
  `src/HealthcareSupport.CaseEvaluation.Domain/**/DataSeedContributor.cs`
  are `IF NOT EXISTS`-style: each starts with an existence check
  (`AnyAsync` / `FindAsync`) before insert. Spot-checked
  `LocationDataSeedContributor.cs:26`, `StateDataSeedContributor.cs:25`,
  `OpenIddictDataSeedContributor.cs:31`,
  `NotificationTemplateDataSeedContributor.cs:45`. None blind-insert.

State is not corrupted. The risk is operator-perceptual (logs unreadable).

### Acceptance criteria

- `docker compose up -d --build` from a clean state produces zero
  `Cannot insert duplicate key row` exceptions in either AuthServer or
  HttpApi.Host stdout/stderr or `Logs/logs.txt`.
- The DbMigrator log still shows seed contributors firing exactly once.
- The `AbpLocalizationResources` table contains exactly one
  `Volo.Abp.LanguageManagement` row after first start (validate via
  `SELECT COUNT(*) FROM AbpLocalizationResources WHERE Name =
  'Volo.Abp.LanguageManagement';`).

---

## Defect H2 -- EF Core schema warnings (LOW)

Three sub-warnings; each is EF Core lint, not a runtime fault.

### H2.1 -- Query-filter inconsistency on M2M joins

**Warning text (paraphrased):**
`The entity 'DoctorAppointmentType' has a navigation to 'AppointmentType'
which has a global query filter; the relationship may produce
inconsistent results.` Same shape for `DoctorLocation -> Location`.

**Current configuration**
(`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs:131-148`,
host side):

```csharp
builder.Entity<DoctorAppointmentType>(b =>
{
    b.ToTable(... + "DoctorAppointmentType", ...);
    b.ConfigureByConvention();
    b.HasKey(x => new { x.DoctorId, x.AppointmentTypeId });
    b.HasOne(x => x.Doctor).WithMany(x => x.AppointmentTypes)
        .HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    b.HasOne(x => x.AppointmentType).WithMany(x => x.DoctorAppointmentTypes)
        .HasForeignKey(x => x.AppointmentTypeId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    b.HasIndex(x => new { x.DoctorId, x.AppointmentTypeId });
});
builder.Entity<DoctorLocation>(b =>
{
    b.ToTable(... + "DoctorLocation", ...);
    b.ConfigureByConvention();
    b.HasKey(x => new { x.DoctorId, x.LocationId });
    b.HasOne(x => x.Doctor).WithMany(x => x.Locations)
        .HasForeignKey(x => x.DoctorId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    b.HasOne(x => x.Location).WithMany(x => x.DoctorLocations)
        .HasForeignKey(x => x.LocationId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    b.HasIndex(x => new { x.DoctorId, x.LocationId });
});
```

Tenant-side equivalent at
`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs:143-160`.

**Root cause.** Filter asymmetry across the join entities:

| Entity | `IMultiTenant` filter | `ISoftDelete` filter |
| --- | --- | --- |
| `Doctor` (`FullAuditedAggregateRoot<Guid>, IMultiTenant`, Domain/Doctors/Doctor.cs:14) | yes | yes |
| `Location` (`FullAuditedAggregateRoot<Guid>`, NOT IMultiTenant, Domain/Locations/Location.cs:16) | no | yes |
| `AppointmentType` (`FullAuditedEntity<Guid>`, NOT IMultiTenant, Domain/AppointmentTypes/AppointmentType.cs:14) | no | yes |
| `DoctorAppointmentType` (plain `Entity`, Domain/Doctors/DoctorAppointmentType.cs:7) | no | no |
| `DoctorLocation` (plain `Entity`, Domain/Doctors/DoctorLocation.cs:7) | no | no |

The principal sides (`Doctor`, `Location`, `AppointmentType`) inherit
ABP's auto-applied `ISoftDelete` filter, and `Doctor` additionally
carries `IMultiTenant`. The dependent join entities have no filter at
all. EF Core 8+ flags this because a query that walks
`Doctor -> AppointmentTypes -> AppointmentType` will filter out
soft-deleted Doctors via the principal filter but will still load the
join row pointing at a soft-deleted Doctor when the query starts from
the join.

**Recommended fix.** Apply matching filters to the join entities. The
join row has no business existing without both endpoints, so:

```csharp
// DoctorAppointmentType:
b.Property<bool>("IsDeleted");                              // shadow flag, off by default
b.HasQueryFilter(x => !EF.Property<bool>(x, "IsDeleted"));  // satisfy soft-delete symmetry
// + scope by Doctor's TenantId via the navigation:
b.HasQueryFilter(x => !EF.Property<bool>(x, "IsDeleted")
                       && x.Doctor.TenantId == CurrentTenantId);
```

This matches Microsoft's documented pattern for join entities sharing
the principal's filter
(`https://learn.microsoft.com/en-us/ef/core/querying/filters` --
"applying multiple filters / chained filters", HIGH confidence).

Cleaner alternative (preferred for parity work): drop the navigations
on the dependent side and let EF treat the join as filter-less only
when reached through the principal. But this requires removing
`b.HasOne(x => x.Doctor)` / `b.HasOne(x => x.Location)` and forces
LINQ joins for navigation -- breaks
`DoctorWithNavigationProperties` projections in
`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Doctors/EfCoreDoctorRepository.cs`.
Cost too high; the matching-filter approach wins.

**Multi-tenancy + soft-delete decision matrix.** Doctor is `IMultiTenant`
+ `ISoftDelete`; both Location and AppointmentType are host-scoped (no
IMultiTenant) but ISoftDelete. The right join filter is the AND of the
two principals' filters. For `DoctorLocation`: Doctor's tenant + Doctor
soft-delete + Location soft-delete. For `DoctorAppointmentType`:
Doctor's tenant + Doctor soft-delete + AppointmentType soft-delete.

### H2.2 -- `AppointmentDocument.Status` default sentinel

**Current configuration**
(`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs:297`):

```csharp
b.Property(x => x.Status)
    .HasColumnName("Status")
    .HasDefaultValue(HealthcareSupport.CaseEvaluation.AppointmentDocuments.DocumentStatus.Uploaded);
```

`DocumentStatus` enum at
`src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentDocuments/DocumentStatus.cs:17-23`:

```csharp
public enum DocumentStatus
{
    Uploaded = 1,
    Accepted = 2,
    Rejected = 3,
    Pending = 4,
}
```

**Root cause.** EF Core 8+ emits a warning when a property has a DB
default but no sentinel: an enum value of `0` (the C# default for
`enum`) is indistinguishable from "not set" and EF cannot tell whether
to honour the DB default or send the explicit 0. Critically,
`DocumentStatus` has NO `0` member -- the smallest value is
`Uploaded = 1`. So C# default(DocumentStatus) = 0 = no enum name. EF
will INSERT 0, the database accepts it (column is `int NOT NULL` with
default 1), and the row ends up with an invalid enum.

**OLD-app reference.** OLD's `AppointmentDocument.DocumentStatusId` is
a plain `int` FK to a `DocumentStatuses` lookup table
(`P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentDocument.cs:118-124`).
No DB default -- the OLD code always set the ID explicitly. So OLD's
shape is "explicit value on insert, no default." The migration in NEW
(`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260428053054_Added_AppointmentDocuments.cs`)
also does not set a column default for `Status`; the
`HasDefaultValue` lives only on the model side. Verified -- the file
has no `defaultValue` for `Status`.

**Recommended fix.** Two options, lean toward A:

- **A (preferred -- matches OLD).** Drop `HasDefaultValue` and
  initialise `Status` in the constructor / factory. The entity already
  does this:
  `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentDocument.cs:53`:
  `public virtual DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;`
  The property initialiser fires before any AppService write; the DB
  never sees enum-0. Removing `HasDefaultValue("Uploaded")` removes
  the warning at zero behavioural cost.
- **B.** Add a sentinel
  (`.HasSentinel(default(DocumentStatus))`) -- explicitly tells EF
  that "0 means unset, use the default." This keeps the DB default
  but is dishonest (we never insert 0 anyway).

A wins on parity (OLD has no default) and on simplicity.

### H2.3 -- `Location.ParkingFee` precision

**Current configuration**
(`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs:95`):

```csharp
b.Property(x => x.ParkingFee).HasColumnName(nameof(Location.ParkingFee));
```

No `HasPrecision` and no `HasColumnType("decimal(18,2)")`.

**Migration shape**
(`src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260202081019_Added_Location.cs:23`):

```csharp
ParkingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
```

**OLD reference.** OLD's column is
`P:/PatientPortalOld/PatientAppointment.DbEntities/Models/Location.cs:59-60`:

```csharp
[Column("ParkingFee")]
public Nullable<decimal> ParkingFee { get; set; }
```

Plain `decimal?`, no `[Column(TypeName = "decimal(...)")]`. Database-side
shape in OLD (per the OLD EF 6 convention) defaults to
`decimal(18, 2)`. Same precision as NEW.

**Root cause.** EF Core 8+ warns when a `decimal` property has no
explicit precision because the convention default is `decimal(18, 2)`
and EF wants the precision to be intentional, not defaulted. The
existing migration emitted `decimal(18, 2)` from convention, which is
correct -- but the model now needs `HasPrecision` to silence the
warning AND to lock the precision in case the convention changes in EF
Core 9.

**Recommended fix.** Add `HasPrecision(18, 2)` to the model
configuration. OLD shape says (18, 2). Confidence: HIGH.

```csharp
b.Property(x => x.ParkingFee)
    .HasColumnName(nameof(Location.ParkingFee))
    .HasPrecision(18, 2);
```

Side note: NEW makes `ParkingFee` non-nullable (`decimal`); OLD makes
it `Nullable<decimal>`. Mismatch with parity directive. Out of scope
for H2; flag for future parity audit
(`docs/parity/_cleanup-tasks.md` candidate).

### H2 runtime impact

LOW. None of the three warnings produce a runtime fault:
- H2.1: query results are technically inconsistent only when soft-deleted
  rows exist on the principal side. Repositories filter by tenant
  explicitly in
  `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Doctors/EfCoreDoctorRepository.cs`,
  so the query path is safe today.
- H2.2: `Status` column-side default is never reached because the
  entity's property initialiser pre-sets `Uploaded`. Bug-in-waiting
  (any reflection-based insert path that bypasses the constructor would
  insert 0), so prefer to fix at the same time as the warning.
- H2.3: pure lint.

### Acceptance criteria

- `dotnet ef migrations add <Name> --project ... --startup-project ...`
  produces no `decimal precision` or `query filter` warnings, and the
  generated migration is empty (no schema delta -- model now matches
  what the DB already has).
- One new migration is committed if and only if the DocumentStatus
  default removal causes a column-default change in SQL Server. Verify
  by running `dotnet ef migrations script` on a clean DB and diffing
  against the existing Initial migration; if the only change is the
  default-value drop, the migration is acceptable.
- App boots without EF warnings on the relevant entities. Verify in
  `dotnet run` console output and `Logs/logs.txt`.

---

## Ordering decision for the plan

1. **Fix H1 first.** Reason: logs are unreadable while H1 fires. Every
   subsequent lifecycle smoke-test, regression check, or bug
   investigation has to wade through the duplicate-key trace before
   surfacing real errors. Information-density of logs is a force
   multiplier for everything that follows.
2. **Fix H1 before any further lifecycle work.** Same reasoning -- the
   noise hides the signal.
3. **Fix H2 anytime, opportunistically.** Low priority. Bundle with
   the next migration-touching change so we don't pay a separate
   migration spin-up cost. If a feature already touches
   `CaseEvaluationDbContext.cs` or migrations in the next two weeks,
   piggyback H2 onto that branch. Otherwise schedule as a
   maintenance ticket.
4. **Within H2, do H2.2 + H2.3 together** (single migration,
   single PR) and **do H2.1 separately** (no migration needed -- pure
   model-builder configuration; lower risk, easier to revert if a
   query-filter change exposes a hidden bug in a repository).

---

## File and citation index

H1:
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:514-578`
- `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs:313-354`
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Program.cs:43-45`
- `src/HealthcareSupport.CaseEvaluation.AuthServer/Program.cs:43-45`
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/Program.cs`
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/CaseEvaluationDbMigratorModule.cs:36-39`
- `src/HealthcareSupport.CaseEvaluation.DbMigrator/DbMigratorHostedService.cs:34-38`
- `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationDbMigrationService.cs:42-115`
- `src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationTenantDatabaseMigrationHandler.cs:15-130`
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/ConfirmationNumberRetryPolicy.cs:20-74`
  (related but unrelated retry policy)

H2:
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs:80-149`
  (host-side `OnModelCreating` for Location, Doctor, DoctorAppointmentType, DoctorLocation)
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs:284-308`
  (AppointmentDocument config)
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs:131-160`
  (tenant-side `OnModelCreating`)
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260202081019_Added_Location.cs:23`
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260428053054_Added_AppointmentDocuments.cs`
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/Doctor.cs:14`
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorAppointmentType.cs:7`
- `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/DoctorLocation.cs:7`
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/AppointmentType.cs:14`
- `src/HealthcareSupport.CaseEvaluation.Domain/Locations/Location.cs:16`
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/AppointmentDocument.cs:53`
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentDocuments/DocumentStatus.cs:17-23`
- OLD: `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/Location.cs:59-60`
- OLD: `P:/PatientPortalOld/PatientAppointment.DbEntities/Models/AppointmentDocument.cs:118-124`
