---
status: draft (decisions resolved 2026-05-20; see readiness check)
issue: doctor-invariant-enforcement
owner: AdrianG
created: 2026-05-15
updated: 2026-05-20
approach: code + tdd (TDD on the AppService guards, code-only on the
  migration, test-after on the SPA error rendering)
sequence: 1 of 7 (slot-generation + doctor-invariant series)
branch: create a new branch off `feat/replicate-old-app`. PR back to
  `feat/replicate-old-app`. Do not merge to `main` until Adrian
  reviews the full series.
readiness-check: docs/plans/SlotGenerationRework/_2026-05-20-doctor-invariant-readiness-check.md
---

> **2026-05-20 decisions locked** (see readiness-check for full context):
> - **Q1 (Option C):** the dependent-bucket probe in `DeleteAsync` checks ONLY
>   the three operational tenant-scope entities — `DoctorAvailability`,
>   `Appointment`, and `DoctorPreferredLocation` (with `IsActive == true`).
>   Host-scope M2M tables (`DoctorLocation`, `DoctorAppointmentType`) are
>   dropped from the probe; their `HasQueryFilter(x => !x.Doctor.IsDeleted)`
>   already hides orphans from every app query.
> - **Q2 (Option A):** `DoctorPreferredLocation` count filters by
>   `x.DoctorId == id && x.IsActive` to avoid the "soft-delete forever
>   blocked" trap (the entity is never hard-deleted in the normal flow).
> - **Q3 (Option C-then-A, affects Phase 2 only):** the parallel-worktree
>   docker work the other session is doing is orthogonal to this plan's
>   Phase 2 `obj/` race fix; coordinate first, default to dropping the
>   Phase 2 bundle if no easy answer.
>
> Six mechanical drift items also resolved (test-file name, error-codes
> insertion point, en.json section absence, AbpExceptionHttpStatusCodeOptions
> block moved to lines 151-194, reference-migration syntax confirmed,
> no migration-name collision). See readiness check for the full diff.

# Doctor-invariant enforcement: one Doctor per tenant (permanent)

## Goal

Codify and enforce the architectural invariant that **exactly one
`Doctor` row exists per tenant**, in three places:

1. `DoctorsAppService.CreateAsync` -- throw on any second create
   inside the same tenant.
2. `DoctorsAppService.DeleteAsync` -- throw when the tenant has any
   downstream data (`DoctorAvailability`, `Appointment`,
   `DoctorPreferredLocation`, `DoctorAppointmentType` rows still
   pointing at the doctor or living inside the tenant).
3. Database -- add a filtered unique index on
   `AppEntity_Doctors(TenantId)` so that even a direct SQL insert
   cannot bypass the AppService gate.

Additionally:

- Declare `DoctorTenantAppService.CreateAsync` the **only**
  supported entry point for net-new Doctor creation (downstream
  AppService is now write-restricted to update / soft-delete-with-
  guards).
- Surface a structured `BusinessException` with a localized message
  on the SPA's existing doctor-management list page when an admin
  attempts a second create.

## Why

`docs/parity/wave-1-parity/_parity-flags.md` PARITY-FLAG-NEW-006
(resolved 2026-05-15) and the new "Invariant: one doctor per tenant
(permanent)" section in
`docs/parity/wave-1-parity/staff-supervisor-doctor-management.md`
both state that the OLD app's per-deploy-DB shape maps to the NEW
app's per-tenant scope. The tenant IS the doctor; the `Doctor` row
is just the profile face of that scope. Downstream entities
(`DoctorAvailability`, `Appointment`) intentionally have no FK to
`Doctor` and reach the doctor through tenant scope.

Today the codebase encodes this invariant only by convention. The
existing `DoctorTenantAppService.CreateAsync` (in
`src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs:52-85`)
correctly seeds one Doctor when a tenant is provisioned. But:

- `DoctorsAppService.CreateAsync`
  (`src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs:112-117`)
  has no guard and will happily insert a second row inside the
  current tenant scope.
- `DoctorsAppService.DeleteAsync` (line 106-110) is a pure
  `DeleteAsync(id)` call that orphans every downstream row.
- No DB constraint prevents direct SQL (or a future code-gen
  regression) from inserting a duplicate.

The audit doc
`docs/parity/_remaining-from-old-audit-2026-05-15.md` calls this out
as a P1 enforcement gap. Without it, the slot-generation rework
(plans 2-7 in this series) cannot rely on "tenant scope == doctor"
because two-doctor tenants would silently produce two parallel slot
calendars.

This plan ships **before** the slot rework so the rework can take
the invariant as load-bearing.

## Non-goals

- No changes to `DoctorTenantAppService.CreateAsync` (it is already
  the canonical entry point).
- No changes to the OLD app (read-only at `P:\PatientPortalOld`).
- No removal of the `Doctor` entity or its M2M collections; the
  schema stays shaped for the eventual multi-doctor future,
  guarded by the invariant for Phase 1.
- No UI work to hide the "Add Doctor" button when the tenant
  already has a doctor. The button stays visible; the server
  throws and the SPA renders the error. Hiding the button is a
  follow-up UX polish (out of scope; would need separate signal in
  `DoctorViewService`).
- No retroactive cleanup of any extant duplicate-Doctor rows.
  The migration's index creation will fail loudly if a tenant
  already has two Doctor rows; that is the desired signal -- the
  operator must dedupe by hand before re-running. We deliberately
  refuse to silently pick a "winner".

## Decisions locked

1. **Invariant scope = tenant.** A Doctor row's `TenantId` is the
   tenant key. Host-scoped Doctor rows (`TenantId IS NULL`) are
   not produced by `DoctorTenantAppService`, do not exist in
   today's data, and are explicitly excluded from the unique
   index via `WHERE TenantId IS NOT NULL`.

2. **Soft-delete-aware.** ABP's `FullAuditedAggregateRoot<Guid>`
   sets `IsDeleted = 1` on logical deletion. The unique index
   filter must include `AND IsDeleted = 0` so a soft-deleted
   doctor does not block a fresh create. This matches every
   other ABP unique index in the project (e.g.
   `IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber`
   from `Phase11f_AppointmentConfirmationNumberUniqueIndex`).

3. **Two new error codes.** Both go into
   `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs`
   following the existing convention:
   - `DoctorOnePerTenantViolated` -- localization key
     `Doctor:OnePerTenantViolated`. Raised by
     `DoctorsAppService.CreateAsync` when the tenant already has
     a non-deleted Doctor row.
   - `DoctorCannotDeleteWithDependents` -- localization key
     `Doctor:CannotDeleteWithDependents`. Raised by
     `DoctorsAppService.DeleteAsync` when any of the four
     dependent counts is > 0. Carries `WithData("entity", "<name>")`
     and `WithData("count", "<n>")` so the SPA can render which
     bucket is non-empty.

4. **HTTP 400 mapping.** Both new codes get an entry in
   `CaseEvaluationHttpApiHostModule.ConfigureHttpStatusCodeMapping`
   so they surface as 400 Bad Request, not the ABP default 403
   Forbidden (which makes the SPA's `ConfigStateService` retry
   the request as a permission issue and confuses the user).

5. **Update path is unchanged.** `DoctorsAppService.UpdateAsync`
   keeps its current behavior. The invariant is about row count;
   an update mutates the existing row and does not violate it.

6. **`DoctorTenantAppService.CreateAsync` keeps its
   replay-on-conflict semantics**
   (file:`src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs:121-141`):
   if the per-email Doctor lookup finds an existing row, it
   updates that row rather than inserting. This is the legitimate
   "tenant re-provisioning" code path and must NOT be broken by
   the new guards. The guard in `DoctorsAppService` does NOT fire
   here because `DoctorTenantAppService` calls the repository
   directly and does not route through the AppService.

## Files touched (with the exact change per file)

### 1. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs`

Append two new public const strings near the existing Doctor-area
codes (line 59-84 region):

```csharp
/// <summary>
/// 2026-05-15 -- raised by <c>DoctorsAppService.CreateAsync</c>
/// when a non-deleted <c>Doctor</c> row already exists for the
/// caller's tenant. Codifies the one-doctor-per-tenant invariant
/// (<c>docs/parity/wave-1-parity/_parity-flags.md</c>
/// PARITY-FLAG-NEW-006). Operators should use the tenant
/// provisioning flow (DoctorTenantAppService.CreateAsync) for
/// net-new doctors; the standard CRUD CreateAsync is reserved
/// for legacy data fixes that should never re-create a duplicate.
/// Localization key <c>Doctor:OnePerTenantViolated</c>.
/// </summary>
public const string DoctorOnePerTenantViolated =
    "CaseEvaluation:Doctor.OnePerTenantViolated";

/// <summary>
/// 2026-05-15 -- raised by <c>DoctorsAppService.DeleteAsync</c>
/// when the tenant still has downstream rows that depend on the
/// doctor (<c>DoctorAvailability</c>, <c>Appointment</c>,
/// <c>DoctorPreferredLocation</c>, <c>DoctorAppointmentType</c>).
/// Forces the operator to drain the schedule and reassign or
/// hard-delete dependents before removing the Doctor profile.
/// Carries <c>entity</c> + <c>count</c> via <c>WithData</c> so the
/// SPA can render which bucket is non-empty.
/// Localization key <c>Doctor:CannotDeleteWithDependents</c>.
/// </summary>
public const string DoctorCannotDeleteWithDependents =
    "CaseEvaluation:Doctor.CannotDeleteWithDependents";
```

### 2. `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs`

**CreateAsync (lines 112-117).** Replace with the guarded
implementation. Pull `IRepository<Doctor, Guid>` (already
injected as `_doctorRepository` via `IDoctorRepository` -- reuse
it) and call `AnyAsync(x => x.TenantId == CurrentTenant.Id)`
against the implicit `IMultiTenant` filter (which already scopes
to the current tenant). The filter excludes soft-deleted rows
by default, so the call is a simple "does the tenant have any
live Doctor row" check.

```csharp
[Authorize(CaseEvaluationPermissions.Doctors.Create)]
public virtual async Task<DoctorDto> CreateAsync(DoctorCreateDto input)
{
    // 2026-05-15 -- one-doctor-per-tenant invariant
    // (PARITY-FLAG-NEW-006). Tenant scope IS the doctor identity;
    // a second Doctor row inside the same tenant breaks every
    // downstream "lookup by tenant scope" path (DoctorAvailability,
    // Appointment, slot generation). DoctorTenantAppService is the
    // canonical net-new path; this AppService is for profile edits.
    if (await _doctorRepository.AnyAsync())
    {
        throw new BusinessException(
            CaseEvaluationDomainErrorCodes.DoctorOnePerTenantViolated)
            .WithData("tenantId", CurrentTenant.Id ?? Guid.Empty);
    }

    var doctor = await _doctorManager.CreateAsync(
        input.AppointmentTypeIds,
        input.LocationIds,
        input.FirstName,
        input.LastName,
        input.Email,
        input.Gender);
    return ObjectMapper.Map<Doctor, DoctorDto>(doctor);
}
```

Notes:
- `IRepository<Doctor, Guid>.AnyAsync()` with no predicate uses
  the queryable's current scope, which already applies the
  `IMultiTenant` filter. We deliberately do NOT add a manual
  `TenantId == CurrentTenant.Id` predicate -- ABP's filter is the
  single source of truth here.
- We do NOT disable the soft-delete filter. A soft-deleted Doctor
  row does not block a new create; the operator workflow is
  "delete then re-create" if the profile metadata is wrong.

**DeleteAsync (lines 106-110).** Wire up three count probes against
operational tenant-scope data only (per Q1 Option C decision
2026-05-20: host-scope M2M tables `DoctorLocation` and
`DoctorAppointmentType` are dropped from the probe; their
`HasQueryFilter(x => !x.Doctor.IsDeleted)` already hides orphans).
Inject three additional repos (kept narrow; we don't pull domain
managers since the guard is pure existence):

```csharp
protected IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
protected IRepository<Appointment, Guid> _appointmentRepository;
protected IRepository<DoctorPreferredLocation, Guid> _doctorPreferredLocationRepository;
```

Constructor adds the three parameters; existing constructor lines
stay. Body of `DeleteAsync`:

```csharp
[Authorize(CaseEvaluationPermissions.Doctors.Delete)]
public virtual async Task DeleteAsync(Guid id)
{
    // 2026-05-15 -- guard the soft-delete against orphaning the
    // schedule. Tenant scope IS the doctor identity; deleting the
    // Doctor row while DoctorAvailability or Appointment rows
    // still exist would leave the tenant with a "ghost calendar"
    // (active slots / appointments with no parent doctor profile).
    // Each probe scopes to the current tenant via ABP's IMultiTenant
    // filter automatically.
    //
    // 2026-05-20 (Q1 Option C, Q2 Option A): host-scope M2M tables
    // (DoctorLocation, DoctorAppointmentType) are intentionally NOT
    // probed -- they are pure profile metadata, already hidden from
    // every app query by HasQueryFilter(x => !x.Doctor.IsDeleted),
    // and blocking delete on them would add friction with no
    // integrity payoff. DoctorPreferredLocation IS probed but only
    // for IsActive=true rows; inactive rows are audit-preserved
    // history (the entity is never hard-deleted in the normal flow,
    // so counting them would create a "soft-delete forever blocked"
    // trap).
    var availabilityCount = await _doctorAvailabilityRepository.CountAsync();
    if (availabilityCount > 0)
    {
        ThrowDependentsExist("DoctorAvailability", availabilityCount);
    }

    var appointmentCount = await _appointmentRepository.CountAsync();
    if (appointmentCount > 0)
    {
        ThrowDependentsExist("Appointment", appointmentCount);
    }

    var activePreferredLocationCount = await _doctorPreferredLocationRepository
        .CountAsync(x => x.DoctorId == id && x.IsActive);
    if (activePreferredLocationCount > 0)
    {
        ThrowDependentsExist("DoctorPreferredLocation", activePreferredLocationCount);
    }

    await _doctorRepository.DeleteAsync(id);
}

private static void ThrowDependentsExist(string entity, long count)
{
    throw new BusinessException(
        CaseEvaluationDomainErrorCodes.DoctorCannotDeleteWithDependents)
        .WithData("entity", entity)
        .WithData("count", count);
}
```

Notes:
- `DoctorAvailability.CountAsync()` and `Appointment.CountAsync()` are
  tenant-scoped automatically via ABP's IMultiTenant filter. They do
  not need a Doctor predicate because the tenant IS the doctor.
- `DoctorPreferredLocation` is tenant-scoped (`IMultiTenant`) AND
  needs a Doctor predicate -- the entity carries DoctorId because
  EF requires it on a join entity even though it is functionally
  derivable from TenantId in the one-doctor-per-tenant model
  (per PARITY-FLAG-NEW-006).
- We surface count (not just existence) so the SPA error message
  can read "Cannot delete: 5 appointment(s) remain".

### 3. `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`

Add two entries to the existing `Configure<AbpExceptionHttpStatusCodeOptions>`
block (search for existing entries like
`options.Map(CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime, HttpStatusCode.BadRequest);`).

```csharp
options.Map(
    CaseEvaluationDomainErrorCodes.DoctorOnePerTenantViolated,
    System.Net.HttpStatusCode.BadRequest);
options.Map(
    CaseEvaluationDomainErrorCodes.DoctorCannotDeleteWithDependents,
    System.Net.HttpStatusCode.BadRequest);
```

### 4. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`

Add the two new keys plus a `Doctor:` namespace if absent. Place
under the existing `Doctor` section (alphabetical -- they fall
between any current `Doctor:*` keys):

```jsonc
"Doctor:OnePerTenantViolated": "This clinic already has a doctor profile. The system supports one doctor per clinic. Edit the existing profile instead, or contact the IT administrator if the clinic needs a different doctor.",
"Doctor:CannotDeleteWithDependents": "This doctor cannot be removed because the clinic still has {count} {entity} record(s). Cancel or reassign the dependent records, then try again."
```

ASCII only. ABP's `BusinessException.WithData` parameters render
through these placeholders by name.

### 5. EF Core migration: `Phase19_DoctorOnePerTenantUniqueIndex`

Add a new migration via:

```bash
DOTNET_ENVIRONMENT=Development \
  dotnet ef migrations add Phase19_DoctorOnePerTenantUniqueIndex \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host \
  --context CaseEvaluationDbContext
```

Then edit the generated `<timestamp>_Phase19_DoctorOnePerTenantUniqueIndex.cs`
to produce a filtered unique index. EF Core's `HasIndex` does
not directly emit a `WHERE` predicate on SQL Server without
`.HasFilter("...")`, so we add the index declaratively to
`CaseEvaluationDbContext.cs` AND let the migration capture it.

#### 5a. Edit `CaseEvaluationDbContext.cs` (the `Doctor` entity block, line 122-132)

Add a new line at the end of the configurator:

```csharp
b.HasIndex(x => x.TenantId)
    .IsUnique()
    .HasFilter("[TenantId] IS NOT NULL AND [IsDeleted] = 0")
    .HasDatabaseName("IX_AppEntity_Doctors_TenantId_Unique");
```

Notes:
- The filter ensures the index only enforces uniqueness on live
  tenant-scoped rows. Host-scoped rows (none expected) and soft-
  deleted rows are excluded.
- SQL Server's filtered-index requires a constant predicate; we
  use the bracketed column names since `dotnet ef`'s emitted SQL
  uses bracketed identifiers.

#### 5b. Verify generated `Up()` / `Down()`

The migration should emit:

```csharp
migrationBuilder.CreateIndex(
    name: "IX_AppEntity_Doctors_TenantId_Unique",
    schema: "AppEntity",
    table: "Doctors",
    column: "TenantId",
    unique: true,
    filter: "[TenantId] IS NOT NULL AND [IsDeleted] = 0");
```

If the auto-generated emit does not include the filter (some
EF Core versions strip it without `[Index(...)]` data annotation
hints), edit the migration by hand to add `filter: "..."` -- this
is the pattern used in
`20260504170956_Phase11f_AppointmentConfirmationNumberUniqueIndex.cs`
(read that migration first to confirm syntax).

`Down()`:

```csharp
migrationBuilder.DropIndex(
    name: "IX_AppEntity_Doctors_TenantId_Unique",
    schema: "AppEntity",
    table: "Doctors");
```

#### 5c. Pre-flight: confirm zero duplicate tenants

Before running `dotnet ef database update`, run a one-shot probe
to confirm there are no existing duplicate `(TenantId)` Doctor
rows (would otherwise abort the migration mid-flight):

```sql
SELECT TenantId, COUNT(*) AS DoctorCount
FROM AppEntity.Doctors
WHERE IsDeleted = 0 AND TenantId IS NOT NULL
GROUP BY TenantId
HAVING COUNT(*) > 1;
```

Adrian runs this against the local LocalDB and the dev SQL
Server. If the probe returns rows, halt the migration and surface
the dupes to Adrian for manual cleanup (this plan does NOT
attempt automated dedupe).

### 6. `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorsAppService.cs` (continued)

Add a single class-level XML doc paragraph above the class
declaration referencing the invariant:

```csharp
/// <summary>
/// Doctor CRUD AppService. Honors the one-doctor-per-tenant
/// invariant (PARITY-FLAG-NEW-006) via two guards: CreateAsync
/// rejects a second row inside the tenant; DeleteAsync rejects
/// removal while DoctorAvailability / Appointment /
/// DoctorPreferredLocation / DoctorAppointmentType dependents
/// exist. <see cref="DoctorTenantAppService"/> remains the
/// canonical net-new path; this service is for profile edits and
/// soft-deletes only.
/// </summary>
```

This is the only doc-comment touch -- not a refactor.

## Test plan (TDD where it pays)

### `test/HealthcareSupport.CaseEvaluation.Application.Tests/Doctors/DoctorApplicationTests.cs`

Existing test class is `DoctorsAppServiceTests<TStartupModule>` (abstract
generic, ABP Suite scaffold pattern). New tests go in the concrete
subclass that already lives in this file. Add six new `[Fact]` tests
(TDD: write the test first, watch it fail with the unguarded
`CreateAsync`/`DeleteAsync`, ship the guard, watch it pass):

| # | Test | Acceptance |
|---|------|------------|
| 1 | `CreateAsync_WhenTenantAlreadyHasDoctor_Throws` | Seed one Doctor in TenantA. Second `CreateAsync` throws `BusinessException` with code `DoctorOnePerTenantViolated`. |
| 2 | `CreateAsync_WhenTenantHasOnlySoftDeletedDoctor_Succeeds` | Seed one Doctor in TenantA, soft-delete it. Second `CreateAsync` succeeds. |
| 3 | `DeleteAsync_WithNoDependents_Succeeds` | Seed one Doctor. No availabilities, no appointments, no preferred locations. `DeleteAsync(id)` returns; Doctor is soft-deleted. |
| 4 | `DeleteAsync_WithDoctorAvailability_Throws` | Seed one Doctor + one `DoctorAvailability`. `DeleteAsync(id)` throws with code `DoctorCannotDeleteWithDependents` and `WithData("entity", "DoctorAvailability")`. |
| 5 | `DeleteAsync_WithAppointment_Throws` | Seed one Doctor + one Appointment chain (Patient + IdentityUser + AppointmentType + Location + DoctorAvailability + Appointment). `DeleteAsync(id)` throws with `entity="Appointment"`. |
| 6 | `DeleteAsync_WithActiveDoctorPreferredLocation_Throws` | Seed one Doctor + one `DoctorPreferredLocation` with `IsActive=true`. `DeleteAsync(id)` throws with `entity="DoctorPreferredLocation"`. |
| 7 | `DeleteAsync_WithOnlyInactivePreferredLocation_Succeeds` | Seed one Doctor + one `DoctorPreferredLocation` with `IsActive=false`. `DeleteAsync(id)` returns successfully (audit-history rows do not block delete). |

Tests 1-5 cover the original plan; tests 6-7 cover the Q2 Option A
decision (filter `DoctorPreferredLocation` count by `IsActive`).

No tests for `DoctorLocation` or `DoctorAppointmentType` -- per
Q1 Option C those are intentionally NOT probed, so there is no
guard behavior to assert against.

### Manual UI verification

After the build passes and migration runs:

1. `docker compose up -d --build`.
2. Log in as the tenant admin on `falkinstein.localhost:4200`.
3. Navigate to `/doctor-management/doctors`.
4. Click "New" -- attempt to add a second doctor.
5. **Expected:** browser shows toast / inline error with the
   `Doctor:OnePerTenantViolated` message. The DevTools network
   tab shows `400 Bad Request` with the error code in the body.
6. Navigate to the doctor profile. Click "Delete".
7. **Expected:** toast / inline error with the
   `Doctor:CannotDeleteWithDependents` message including the
   dependent entity and count.

## Risk and rollback

**Blast radius:**
- Two guarded AppService methods. Update path untouched; lookups
  untouched.
- One DB migration. Rollback is `dotnet ef database update <previous_migration>`
  which drops the index. The new error codes and SPA messages
  are inert if the SPA never receives them, so they are safe to
  ship even if the migration is reverted.

**Rollback:**
- Revert the commit on the feature branch and `dotnet ef
  database update <previous_migration>` against the affected DB.
- Existing Doctor data is unaffected: the migration is index-
  only, no schema change to row shape.

**Risk: dupes in existing data.** The migration WILL FAIL if any
tenant already has two non-deleted Doctor rows. This is the
desired safety net; Adrian gets the failure message, runs the
SQL probe (5c above), dedupes by hand, re-runs.

**Risk: a future tenant-provisioning code path calls the
unguarded `DoctorsAppService.CreateAsync` instead of
`DoctorTenantAppService.CreateAsync`.** Mitigated by: (a) the
class-level XML doc note above the class, (b) the test in plan 7
that asserts `DoctorTenantAppService` is the only caller of
`new Doctor(...)` outside `DoctorManager`.

**Risk: ABP soft-delete + filtered index interaction.** Tested
explicitly via test #2 above. If a deleted-row backlog accumulates
the index continues to permit one live row per tenant; soft-
deleted rows are filtered out by `IsDeleted = 0`.

## Verification

End-to-end test procedure after all changes ship:

1. Clean docker stack: `docker compose down -v`.
2. Rebuild + migrate: `docker compose up -d --build`. Confirm the
   DbMigrator output mentions
   `Phase19_DoctorOnePerTenantUniqueIndex` running.
3. Log in as IT Admin on `admin.localhost:4200`.
4. Create a new tenant via SaaS Host -> Tenants -> New. Confirm
   the tenant Doctor row exists via SQL:
   ```sql
   SELECT TenantId, FirstName, IsDeleted
   FROM AppEntity.Doctors
   WHERE TenantId = '<new-tenant-id>';
   ```
   Exactly one row, `IsDeleted=0`.
5. Switch to the tenant subdomain (`<tenant>.localhost:4200`).
   Log in as the tenant admin.
6. Navigate `/doctor-management/doctors` -> click "New" -> fill
   in any synthetic data -> Save.
7. Expect a 400 with `Doctor:OnePerTenantViolated`. UI renders
   the friendly message.
8. As Staff Supervisor, create one `DoctorAvailability` slot for
   the doctor.
9. Navigate back to the doctor profile -> Delete.
10. Expect a 400 with `Doctor:CannotDeleteWithDependents`
    (`entity=DoctorAvailability`, `count=1`).
11. Delete the slot, then re-try the doctor delete -> succeeds
    (soft-delete).
12. Confirm SQL row now has `IsDeleted=1`.
13. Navigate back to "New" -> create another doctor -> succeeds
    (test #2 in TDD list).

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Do NOT merge upstream to `main` until the slot-rework plans
  (2-7) are merged behind this one. Plans 2-7 assume the
  invariant holds; merging the slot rework before this lands
  would leave a window where a two-doctor tenant could break the
  rework's tenant-scoped queries.
