---
status: draft
issue: slot-rework-phase-1-schema
owner: AdrianG
created: 2026-05-15
revised: 2026-05-20 (drift check + locked decisions baked in -- see
  `_2026-05-20-slot-phase-1-readiness-check.md`)
approach: code + tdd (TDD on entity construction + mapper output;
  code-only on the migration and DbContext config)
sequence: 2 of 7 (slot-generation + doctor-invariant series)
depends-on: 2026-05-15-doctor-invariant-enforcement.md (plan 1 must
  ship first so the one-doctor-per-tenant invariant is enforced)
branch: create a new branch off `feat/replicate-old-app`. PR back
  to `feat/replicate-old-app`. Do not merge to `main` until
  plans 2 through 7 are merged together.
decisions-locked-2026-05-20:
  Q1 (constructor parameter order): A -- breaking change, no
    [Obsolete] shim; compiler surfaces every call site.
  Q2 (backfill soft-deleted slots): A -- include them in the
    INSERT so a future un-soft-delete preserves type semantics.
    The join's HasQueryFilter mirrors the parent's soft-delete
    so backfilled rows on soft-deleted slots stay hidden.
  Q4 (Mapperly converter): A -- try the default [MapProperty]
    first; only add the manual converter if source-gen warns.
---

# Slot rework Phase 1: Capacity + multi-type schema

## Goal

Move `DoctorAvailability` from "single-appointment-type per slot,
one appointment per slot" to "capacity-based slot with a set of
permitted appointment types":

1. Add `Capacity int NOT NULL DEFAULT 1` to `DoctorAvailability`.
2. Add a new join entity `DoctorAvailabilityAppointmentType` with
   composite PK `(DoctorAvailabilityId, AppointmentTypeId)` and
   mirrored `TenantId` for IMultiTenant scoping.
3. Drop `DoctorAvailability.AppointmentTypeId` AFTER backfilling
   existing rows into the join table.
4. Update every DTO that surfaced the old `AppointmentTypeId`
   shape (`DoctorAvailabilityCreateDto`, `UpdateDto`, `Dto`,
   `WithNavigationPropertiesDto`, the navigation-properties
   projection POCO) to surface `AppointmentTypeIds : List<Guid>`
   instead, with `Capacity : int`.
5. Update Riok.Mapperly mappers to project the new shape.
6. Keep the existing
   `DoctorAvailability` aggregate-root invariants (Available /
   Booked / Reserved enum, tenant filter, FullAuditedAggregateRoot).

This plan ships **schema + DTO surface only**. It deliberately
leaves the AppService method bodies and the slot-bookable
predicate to plan 3 (domain logic). The AppService keeps
compiling against the new DTOs in this plan by treating the new
`Capacity` field as a passthrough and the new `AppointmentTypeIds`
list as a 1-or-0-element collection (preserving today's behavior
exactly).

## Why

The slot-generation rework plan
(`W:\patient-portal\main\docs\plans\2026-05-15-slot-generation-rework.md`)
locks the target model in section "Phase 1 -- Schema":

> `bookable(slot, requestedType, now) =
>   slot.AvailableDate >= now+leadTime
>   && BookingStatusId == Available
>   && (Capacity - activeAppointmentCount) > 0
>   && (AppointmentTypes empty OR contains requestedType)`

A slot must therefore carry (a) a numeric `Capacity` for
overbooking control and (b) a SET of permitted appointment
types so a single 60-minute slot can cover both AME and PQME
without producing two stored rows. Today the single
`AppointmentTypeId : Guid?` (nullable, single-valued) cannot
express the SET semantics; the generator works around it by
emitting one row per type, which then can't share capacity.

Splitting schema (this plan) from logic (plan 3) keeps each PR
small and reviewable. Plans 4 and 5 (UI) consume the new DTO
surface; plan 6 (booking-form picker) reads `Capacity`; plan 7
(tests) wires the full HRD coverage.

## Non-goals

- No changes to AppService method bodies beyond passing the new
  fields through. The capacity-aware `bookable` predicate, the
  active-count probe, the new error codes, and removing the
  `BookingStatusId = Booked` flip ALL belong to plan 3.
- No changes to the Angular generation UI or booking picker UI
  in this plan. The proxy regeneration that emits the new TS
  shape IS part of this plan (so plan 4/5 can consume it), but
  hand-written Angular code lands in plans 4-6.
- No removal of `BookingStatus.Reserved`. Its repurposing (manual
  close override) is finalized in plan 3.
- No backfilling of `Capacity > 1` for existing slots. Existing
  rows keep `Capacity = 1`, preserving today's "one appointment
  per slot" semantics until an operator deliberately raises it.
- No changes to `Appointment.DoctorAvailabilityId`. The FK stays
  with `OnDelete=NoAction`; the planned `IAppointmentRepository.
  GetActiveCountForSlotAsync` is plan 3.

## Decisions locked

1. **`Capacity` is `int NOT NULL DEFAULT 1`**, not nullable.
   Reason: the no-row-yet default and the no-input-yet UI
   default both resolve to 1, which matches today's "one
   appointment per slot" semantics. A nullable column would
   require every read site to coalesce `?? 1` which spreads the
   default everywhere.

2. **Minimum `Capacity = 1`** at the entity layer. The
   `DoctorAvailability` constructor accepts an `int capacity`
   parameter and `Check.Range(capacity, nameof(capacity), 1,
   int.MaxValue)`. Zero or negative capacity is rejected at
   construction time, not at the AppService layer, so any
   future caller (data migration, seeder, external service)
   gets the same invariant.

3. **No upper bound on `Capacity`.** OLD has no published
   maximum; locking one in the schema would force a future
   migration. Validation against per-tenant policy belongs at
   the AppService.

4. **Join entity name = `DoctorAvailabilityAppointmentType`** (not
   `DoctorAvailabilityAcceptedTypes` or similar). Mirrors the
   existing `DoctorAppointmentType` join naming and the ABP M2M
   convention of `<Parent><Child>`.

5. **Join entity is `Entity` (no Guid Id), composite PK
   `(DoctorAvailabilityId, AppointmentTypeId)`.** Same pattern as
   `DoctorAppointmentType.cs` and `DoctorLocation.cs`. Carries
   `TenantId : Guid?` for IMultiTenant scoping (see decision 6).

6. **Join entity implements `IMultiTenant`** with `TenantId`
   mirrored from the parent `DoctorAvailability` at insert time.
   This is the project convention for tenant-scoped join entities
   (see also `AppointmentAccessor`). ABP's data filter applies
   automatically; cross-tenant queries on the join surface
   nothing.

7. **Empty `AppointmentTypeIds` list = "any type accepted"**.
   This preserves OLD's loose-or-strict mode (slot with null
   `AppointmentTypeId` = any type; slot with specific type =
   only that type). Implemented in plan 3's bookable predicate;
   exposed in this plan via DTO shape (empty list = no filter).

8. **`AppointmentTypeId` is dropped after backfill.** The
   migration runs in two steps inside `Up()`:
   - Step A: for every `DoctorAvailability` row with
     `AppointmentTypeId IS NOT NULL`, insert one
     `DoctorAvailabilityAppointmentType` row with mirrored
     `TenantId`.
   - Step B: drop the `AppointmentTypeId` column (and the
     existing FK).

9. **`Capacity` storage is plain `int`, not a value object.**
   Reason: simplicity. A value object would force every read site
   through a wrapper; the slot rework already carries enough
   conceptual weight.

10. **DTO shape mirrors entity shape.** Output DTO surfaces
    `Capacity : int` and `AppointmentTypeIds : List<Guid>`. Input
    DTOs (`CreateDto`, `UpdateDto`) accept the same.
    `WithNavigationPropertiesDto` surfaces both the IDs and the
    materialized `AppointmentType` rows for UI display.

11. **Pre-flight: fix the docker bind-mount obj/ race before
    running this plan's migration.** AuthServer + API both
    bind-mount `./src` and run `dotnet watch`; they race on
    `obj/.../ref/<asm>.dll` writes during cold start, causing
    AuthServer to fail every health check until manually
    restarted. The fix (Directory.Build.props redirect +
    per-service named volumes in docker-compose) ships as the
    first work item of this plan. See "Pre-flight: fix the
    docker bind-mount obj/ race" section below for details.

    **Status as of 2026-05-20 (readiness-check D5):** the
    Doctor invariant readiness check Q3 was locked as "C then
    A" -- coordinate with the other session about parallel-
    worktree docker FIRST, then default to dropping this pre-
    flight bundle if they touch `Directory.Build.props` or
    relocate `obj/`. If the other session ships their fix
    BEFORE this plan starts, this pre-flight is OPTIONAL --
    re-verify a cold `docker compose up -d --build` reaches
    port 44368 and 44327 within 3 minutes, and skip the
    Directory.Build.props edits entirely. If the other session
    has NOT shipped by the time this plan starts, the pre-
    flight section below applies as written.

## Pre-flight: fix the docker bind-mount obj/ race

### Symptom (observed 2026-05-15)

A cold `docker compose up -d --build` of the dev stack left
`authserver` stuck failing health checks for 44 consecutive
probes; port 8080 never opened. The container's `dotnet watch`
MSBuild was deadlocked on an `IOException` against
`src/.../HealthcareSupport.CaseEvaluation.Domain.Shared/obj/Debug/net10.0/ref/HealthcareSupport.CaseEvaluation.Domain.Shared.dll`.
The `api` container's `dotnet watch` (started in parallel by
docker compose) had already grabbed the lock on the same path,
because both services bind-mount `./src` (see `docker-compose.yml`
lines 171 and 235) and `dotnet watch` writes intermediate output
into `obj/` under the bind-mounted source tree.

User-visible effect: Angular at `localhost:4200` returned 200,
but the login redirect to `falkinstein.localhost:44368` refused
the connection because AuthServer was never listening.

Tactical un-block: `docker compose restart authserver` once the
api container finished its first build; the lock cleared and
AuthServer rebuilt cleanly on the second attempt. This is a
manual workaround the operator hits every cold start.

This plan is the **first** in the slot-rework + invariant series
that triggers heavy rebuilds through docker -- the migration
work in step 8 (`Phase20_DoctorAvailabilityCapacityAndTypeSet`)
forces a from-scratch container rebuild that hits the race
deterministically. The latent issue must be closed before this
plan's verification can run reliably; plans 3-7 inherit the fix.

### Root cause

Two services -- `authserver` and `api` -- bind-mount the same
host `./src` into `/app/src` inside the container and both run
`dotnet watch run`. MSBuild writes its intermediate output to
`obj/` next to each `.csproj`. Because the directory tree is
shared:

- Both `dotnet watch` instances try to rebuild
  `HealthcareSupport.CaseEvaluation.Domain.Shared` on startup
  (it is a transitive dependency of both AuthServer and API).
- The ref-assembly emit step (`obj/Debug/net10.0/ref/<asm>.dll`)
  uses an exclusive write lock.
- One container wins the lock; the other throws `IOException`
  and dies. `dotnet watch` does NOT retry the failed build; it
  sits idle until manually restarted.

This is independent of the order in which docker-compose starts
the services. `depends_on` with a health-check gate would help
slightly (sequence the API after AuthServer is healthy), but
does not fix the underlying co-mutation problem -- a hot-reload
of any shared-project file later will recreate the race.

### Fix: redirect intermediate output OUTSIDE the bind-mount

Per service, point MSBuild's `BaseIntermediateOutputPath` and
`BaseOutputPath` at a container-local directory that is NOT
part of the host bind-mount. Each service's obj/bin tree lives
in its own named volume; the host's `./src` stays clean of
build droppings AND the two containers stop racing.

Microsoft docs reference:
- `BaseIntermediateOutputPath` / `BaseOutputPath` semantics:
  `learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties`.
- Setting MSBuild properties from environment variables: the
  MSBuild engine reads any environment variable as a property
  by default (this is on by SDK convention).

#### Edit 1: `Directory.Build.props`

Insert a new conditional `PropertyGroup` before the existing
NuGet-lock-files PropertyGroup. The condition checks for an
environment variable injected per-container; host-side builds
(no env var set) are unaffected and keep writing to the
default `obj/` and `bin/` next to each `.csproj`.

```xml
<!--
  2026-05-15 -- dev-stack obj/ bind-mount race fix.

  When two docker services (authserver + api) bind-mount the
  same host ./src and both run `dotnet watch`, they race on
  obj/Debug/.../ref/<asm>.dll. Setting per-container
  BaseIntermediateOutputPath via the BUILD_OUTPUT_ROOT
  environment variable steers each container's intermediate
  output to its own location OUTSIDE the bind-mounted tree.

  Host-side builds (no env var) keep the default ./obj/ and
  ./bin/ layout; only docker containers with BUILD_OUTPUT_ROOT
  set get the redirect.
-->
<PropertyGroup Condition="'$(BUILD_OUTPUT_ROOT)' != ''">
  <BaseIntermediateOutputPath>$(BUILD_OUTPUT_ROOT)\$(MSBuildProjectName)\obj\</BaseIntermediateOutputPath>
  <BaseOutputPath>$(BUILD_OUTPUT_ROOT)\$(MSBuildProjectName)\bin\</BaseOutputPath>
</PropertyGroup>
```

Place the block as the FIRST PropertyGroup in the file --
`BaseIntermediateOutputPath` participates in MSBuild's
"intermediate path resolved early" path; later evaluation can
miss it. .NET SDK 8+ (we are on 10) honors Directory.Build.props
placement reliably, but earliest is safest.

#### Edit 2: `docker-compose.yml` -- authserver and api services

Add the env var + a named volume per service. Two parallel
edits, one per service block:

**authserver** (around lines 129-198):

```yaml
authserver:
  # ... existing config ...
  environment:
    # ... existing entries ...
    BUILD_OUTPUT_ROOT: /tmp/build-output/authserver
  volumes:
    - ./src:/app/src
    - .\Directory.Build.props:/app/Directory.Build.props:ro
    - .\HealthcareSupport.CaseEvaluation.slnx:/app/HealthcareSupport.CaseEvaluation.slnx:ro
    # NEW: 2026-05-15 -- per-container obj/bin tree, outside the bind-mount.
    - authserver_build_output:/tmp/build-output
    # ... existing entries (appsettings.secrets.json, etc.) ...
```

**api** (around lines 200-260):

```yaml
api:
  # ... existing config ...
  environment:
    # ... existing entries ...
    BUILD_OUTPUT_ROOT: /tmp/build-output/api
  volumes:
    - ./src:/app/src
    - .\Directory.Build.props:/app/Directory.Build.props:ro
    - .\HealthcareSupport.CaseEvaluation.slnx:/app/HealthcareSupport.CaseEvaluation.slnx:ro
    # NEW: 2026-05-15 -- per-container obj/bin tree, outside the bind-mount.
    - api_build_output:/tmp/build-output
    # ... existing entries ...
```

**bottom of file** -- register the two named volumes alongside
the existing `sqldata` / `miniodata` entries:

```yaml
volumes:
  sqldata:
  miniodata:
  # NEW: 2026-05-15 -- per-service intermediate-output trees.
  authserver_build_output:
  api_build_output:
```

#### Edit 3: `.dockerignore` (sanity check, no change expected)

Confirm `.dockerignore` continues to ignore `**/obj/` and
`**/bin/` so cold image builds do not slurp the host's stale
output into the image. If the file does not contain those
patterns, add them; the fix above only redirects RUNTIME
output, not image-build context.

### Why not the alternatives

- **Stagger service start with `depends_on: condition:
  service_healthy`**: latent. The first build is sequenced, but
  any subsequent hot-reload of a shared project file
  (`Domain.Shared`, `Application.Contracts`, etc.) recreates
  the race because both `dotnet watch` instances react to the
  file event simultaneously.
- **Read-only `./src` bind-mount**: `dotnet watch` writes its
  scratch state into `obj/` and cannot tolerate a read-only
  source tree. Breaks hot-reload entirely.
- **Single shared "builder" container that compiles for both**:
  larger refactor, defeats `dotnet watch`'s per-process
  hot-reload semantics, and requires teaching docker-compose
  about a non-service builder phase. Out of scope.

### Verification (run as part of plan 2's verification block)

1. `docker compose down -v` to clear stale volumes (the named
   volumes that were never created before this fix do not need
   removal; the `-v` is a habit to clear `sqldata`).
2. `docker compose up -d --build` from a clean tree.
3. Watch:
   ```bash
   docker compose ps
   docker compose logs -f authserver | grep -E "Build succeeded|Build FAILED|listening"
   docker compose logs -f api        | grep -E "Build succeeded|Build FAILED|listening"
   ```
4. Both services reach "Now listening on:" within ~3 minutes
   on a cold tree; neither logs `IOException` against an
   `obj/.../ref/*.dll`.
5. SQL probe inside the authserver container confirms its
   intermediate output landed in the named volume, not the
   bind-mount:
   ```bash
   docker compose exec authserver ls /tmp/build-output/authserver | head
   docker compose exec authserver ls /app/src/HealthcareSupport.CaseEvaluation.AuthServer/obj 2>&1 | head
   ```
   The first command lists per-project subdirs (`HealthcareSupport.CaseEvaluation.AuthServer`,
   `HealthcareSupport.CaseEvaluation.Domain.Shared`, etc.).
   The second returns either an empty listing or
   `cannot access ...: No such file or directory` -- proof the
   bind-mounted obj/ is no longer written.
6. Touch a `Domain.Shared` file (e.g.,
   `CaseEvaluationDomainErrorCodes.cs`); both `dotnet watch`
   instances detect the change and rebuild independently
   without either dying. Verified by re-running the
   `Build succeeded` grep above.

### Acceptance criteria for this pre-flight

- A cold `docker compose up -d --build` reliably reaches
  AuthServer port 44368 + API port 44327 within 3 minutes
  without operator intervention.
- A `touch src/.../CaseEvaluationDomainErrorCodes.cs` does not
  knock either container off its health check.
- Host-side `dotnet build src/.../HealthcareSupport.CaseEvaluation.HttpApi.Host`
  (no docker) continues to write `obj/` under the project
  directory as before (the env var is unset on the host).

### Risk and rollback for the pre-flight

- **Blast radius**: 1 properties file + 1 compose file. No code
  paths. No DB changes. No image rebuild needed if the
  `BUILD_OUTPUT_ROOT` env var is unset (the conditional in
  Directory.Build.props no-ops).
- **Rollback**: revert the two edits. Docker stack returns to
  the pre-fix racing behavior; the tactical
  `docker compose restart authserver` workaround stays
  available.
- **Risk**: the named volumes can accumulate cruft across
  rebuilds (stale obj/bin trees from old branches). Mitigated
  by occasional `docker compose down -v` -- the same hygiene
  command the project's CLAUDE.md already recommends.
- **Risk**: a future CI runner that mounts `./src` for tests
  will inherit the env var if the test compose overlay sets
  one. Mitigated: keep the env var unset in test overlays;
  the conditional cleanly no-ops.

This pre-flight is the FIRST work item of this plan. Land it,
verify, then proceed to step 1 (entity changes).

---

## Files touched (with the exact change per file)

### 1. `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs`

Add `Capacity` property and a navigation collection. Drop the
constructor's `appointmentTypeId` parameter and replace with the
collection seed pattern (constructor body initialises an empty
`AppointmentTypes` collection; the manager adds rows via
`AddAppointmentType`).

```csharp
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailability : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual DateTime AvailableDate { get; set; }

    public virtual TimeOnly FromTime { get; set; }

    public virtual TimeOnly ToTime { get; set; }

    public virtual BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// 2026-05-15 -- max simultaneous appointments this slot can hold.
    /// Minimum 1. The capacity-aware bookable predicate (plan 3)
    /// compares this to the active appointment count for the slot.
    /// Default 1 preserves today's "one appointment per slot"
    /// semantics. No upper bound enforced at the entity layer.
    /// </summary>
    public virtual int Capacity { get; set; } = 1;

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentType ids this slot accepts.
    /// Empty (no join rows) means "any type accepted" -- matches
    /// OLD's null-AppointmentTypeId loose mode. Plan 3's bookable
    /// predicate consumes this set.
    /// </summary>
    public virtual ICollection<DoctorAvailabilityAppointmentType> AppointmentTypes { get; protected set; }
        = new Collection<DoctorAvailabilityAppointmentType>();

    protected DoctorAvailability()
    {
    }

    public DoctorAvailability(
        Guid id,
        Guid locationId,
        DateTime availableDate,
        TimeOnly fromTime,
        TimeOnly toTime,
        BookingStatus bookingStatusId,
        int capacity = 1)
    {
        Id = id;
        Check.Range(capacity, nameof(capacity), 1, int.MaxValue);
        AvailableDate = availableDate;
        FromTime = fromTime;
        ToTime = toTime;
        BookingStatusId = bookingStatusId;
        LocationId = locationId;
        Capacity = capacity;
        AppointmentTypes = new Collection<DoctorAvailabilityAppointmentType>();
    }

    public virtual void AddAppointmentType(Guid appointmentTypeId)
    {
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        if (IsInAppointmentTypes(appointmentTypeId))
        {
            return;
        }
        AppointmentTypes.Add(new DoctorAvailabilityAppointmentType(Id, appointmentTypeId, TenantId));
    }

    public virtual void RemoveAppointmentType(Guid appointmentTypeId)
    {
        Check.NotNull(appointmentTypeId, nameof(appointmentTypeId));
        AppointmentTypes.RemoveAll(x => x.AppointmentTypeId == appointmentTypeId);
    }

    public virtual void RemoveAllAppointmentTypesExceptGivenIds(List<Guid> appointmentTypeIds)
    {
        Check.NotNull(appointmentTypeIds, nameof(appointmentTypeIds));
        AppointmentTypes.RemoveAll(x => !appointmentTypeIds.Contains(x.AppointmentTypeId));
    }

    public virtual void RemoveAllAppointmentTypes()
    {
        AppointmentTypes.RemoveAll(x => x.DoctorAvailabilityId == Id);
    }

    private bool IsInAppointmentTypes(Guid appointmentTypeId)
    {
        return AppointmentTypes.Any(x => x.AppointmentTypeId == appointmentTypeId);
    }
}
```

Notes:
- `AppointmentTypeId` is REMOVED. No call site reads it from the
  entity anymore -- callers project the join collection.
- `RemoveAll` extension is from `System.Collections.ObjectModel` /
  `System.Collections.Generic`; if Mapperly trips on it, swap to
  a manual `var to-remove = AppointmentTypes.Where(...).ToList();
  foreach (var x in to-remove) AppointmentTypes.Remove(x);`
  -- matches `Doctor.cs`'s existing pattern.

### 2. NEW FILE `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityAppointmentType.cs`

Join entity with composite PK and tenant mirror. Mirrors
`DoctorAppointmentType.cs`'s shape, with `IMultiTenant` added.

```csharp
using System;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-05-15 -- M2M join between <see cref="DoctorAvailability"/>
/// and <see cref="AppointmentType"/>. Composite primary key on
/// (DoctorAvailabilityId, AppointmentTypeId); TenantId is mirrored
/// from the parent slot at insert time so ABP's IMultiTenant
/// filter scopes correctly. Empty set on a slot means "any
/// AppointmentType accepted" -- the loose-or-strict-mode parity
/// rule from OLD's slot generation.
/// </summary>
public class DoctorAvailabilityAppointmentType : Entity, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid DoctorAvailabilityId { get; protected set; }

    public Guid AppointmentTypeId { get; protected set; }

    public virtual DoctorAvailability DoctorAvailability { get; protected set; } = null!;

    public virtual AppointmentType AppointmentType { get; protected set; } = null!;

    protected DoctorAvailabilityAppointmentType()
    {
    }

    public DoctorAvailabilityAppointmentType(
        Guid doctorAvailabilityId,
        Guid appointmentTypeId,
        Guid? tenantId)
    {
        DoctorAvailabilityId = doctorAvailabilityId;
        AppointmentTypeId = appointmentTypeId;
        TenantId = tenantId;
    }

    public override object[] GetKeys()
    {
        return new object[] { DoctorAvailabilityId, AppointmentTypeId };
    }
}
```

### 3. `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs`

Update the manager to thread `Capacity` and the
`appointmentTypeIds` collection through Create / Update. The
existing `CreateAsync` and `UpdateAsync` method signatures
change. Callers (the AppService) move to a Mapperly-style
collection-sync.

```csharp
public virtual async Task<DoctorAvailability> CreateAsync(
    Guid locationId,
    List<Guid> appointmentTypeIds,
    DateTime availableDate,
    TimeOnly fromTime,
    TimeOnly toTime,
    BookingStatus bookingStatusId,
    int capacity)
{
    Check.NotNull(locationId, nameof(locationId));
    Check.NotNull(availableDate, nameof(availableDate));
    Check.NotNull(bookingStatusId, nameof(bookingStatusId));
    Check.Range(capacity, nameof(capacity), 1, int.MaxValue);

    var doctorAvailability = new DoctorAvailability(
        GuidGenerator.Create(),
        locationId,
        availableDate,
        fromTime,
        toTime,
        bookingStatusId,
        capacity);

    if (appointmentTypeIds != null)
    {
        foreach (var id in appointmentTypeIds.Distinct())
        {
            doctorAvailability.AddAppointmentType(id);
        }
    }

    return await _doctorAvailabilityRepository.InsertAsync(doctorAvailability);
}

public virtual async Task<DoctorAvailability> UpdateAsync(
    Guid id,
    Guid locationId,
    List<Guid> appointmentTypeIds,
    DateTime availableDate,
    TimeOnly fromTime,
    TimeOnly toTime,
    BookingStatus bookingStatusId,
    int capacity,
    [CanBeNull] string? concurrencyStamp = null)
{
    Check.NotNull(locationId, nameof(locationId));
    Check.NotNull(availableDate, nameof(availableDate));
    Check.NotNull(bookingStatusId, nameof(bookingStatusId));
    Check.Range(capacity, nameof(capacity), 1, int.MaxValue);

    // Load with the M2M collection so we can sync via
    // RemoveAllExceptGivenIds + AddAppointmentType.
    var queryable = await _doctorAvailabilityRepository
        .WithDetailsAsync(x => x.AppointmentTypes);
    var query = queryable.Where(x => x.Id == id);
    var doctorAvailability = await AsyncExecuter.FirstOrDefaultAsync(query)
        ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(DoctorAvailability), id);

    doctorAvailability.LocationId = locationId;
    doctorAvailability.AvailableDate = availableDate;
    doctorAvailability.FromTime = fromTime;
    doctorAvailability.ToTime = toTime;
    doctorAvailability.BookingStatusId = bookingStatusId;
    doctorAvailability.Capacity = capacity;

    if (appointmentTypeIds == null || appointmentTypeIds.Count == 0)
    {
        doctorAvailability.RemoveAllAppointmentTypes();
    }
    else
    {
        var distinct = appointmentTypeIds.Distinct().ToList();
        doctorAvailability.RemoveAllAppointmentTypesExceptGivenIds(distinct);
        foreach (var typeId in distinct)
        {
            doctorAvailability.AddAppointmentType(typeId);
        }
    }

    doctorAvailability.SetConcurrencyStampIfNotNull(concurrencyStamp);
    return await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);
}
```

### 4. `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityWithNavigationProperties.cs`

Drop the single `AppointmentType` property; add a `List<AppointmentType>` for the materialized
M2M rows. Field surface for the repository's join projection.

```csharp
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityWithNavigationProperties
{
    public DoctorAvailability DoctorAvailability { get; set; } = null!;

    public Location? Location { get; set; }

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentTypes this slot accepts,
    /// materialized via the EF repository's join projection.
    /// Empty list means "any type accepted".
    /// </summary>
    public List<AppointmentType> AppointmentTypes { get; set; } = new();
}
```

### 5. `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/DoctorAvailabilities/EfCoreDoctorAvailabilityRepository.cs`

Update the navigation-properties projection to materialize the
M2M list:

```csharp
public virtual async Task<DoctorAvailabilityWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
{
    var dbContext = await GetDbContextAsync();
    return await (await GetDbSetAsync())
        .Where(b => b.Id == id)
        .Select(doctorAvailability => new DoctorAvailabilityWithNavigationProperties
        {
            DoctorAvailability = doctorAvailability,
            Location = dbContext.Set<Location>()
                .FirstOrDefault(c => c.Id == doctorAvailability.LocationId),
            AppointmentTypes = (
                from join in dbContext.Set<DoctorAvailabilityAppointmentType>()
                join atype in dbContext.Set<AppointmentType>()
                    on join.AppointmentTypeId equals atype.Id
                where join.DoctorAvailabilityId == doctorAvailability.Id
                select atype
            ).ToList(),
        })
        .FirstOrDefaultAsync(cancellationToken);
}

protected virtual async Task<IQueryable<DoctorAvailabilityWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
{
    var dbContext = await GetDbContextAsync();
    return from doctorAvailability in (await GetDbSetAsync())
           join location in dbContext.Set<Location>()
               on doctorAvailability.LocationId equals location.Id into locations
           from location in locations.DefaultIfEmpty()
           select new DoctorAvailabilityWithNavigationProperties
           {
               DoctorAvailability = doctorAvailability,
               Location = location,
               AppointmentTypes = (
                   from join in dbContext.Set<DoctorAvailabilityAppointmentType>()
                   join atype in dbContext.Set<AppointmentType>()
                       on join.AppointmentTypeId equals atype.Id
                   where join.DoctorAvailabilityId == doctorAvailability.Id
                   select atype
               ).ToList()
           };
}
```

Drop the `appointmentTypeId` filter parameter from the
`ApplyFilter(IQueryable<DoctorAvailabilityWithNavigationProperties>, ...)`
overload. Plan 3 reintroduces a different filter ("any of the
requested types is in the slot's set") on the booking-form picker
endpoint; the admin list filter does not need it.

### 6. `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs`

(Corrected 2026-05-20 from readiness-check D1.) The interface
carries `appointmentTypeId` on TWO method signatures only:
`GetListWithNavigationPropertiesAsync` and `GetCountAsync`.
(`GetWithNavigationPropertiesAsync(Guid id, ...)` and
`GetListAsync(...)` already lack it.) Drop the parameter from
those two signatures.

Mirror the change in the EF Core repository:
- `EfCoreDoctorAvailabilityRepository.GetListWithNavigationPropertiesAsync`
- `EfCoreDoctorAvailabilityRepository.GetCountAsync`
- The shared
  `ApplyFilter(IQueryable<DoctorAvailabilityWithNavigationProperties>, ..., Guid? appointmentTypeId)`
  overload at line 52 -- drop the `appointmentTypeId` parameter
  and the trailing `WhereIf(appointmentTypeId != null && ..., ...)`
  clause.
- The flat `ApplyFilter(IQueryable<DoctorAvailability>, ...)`
  overload at line 57 already lacks `appointmentTypeId`.

Plan 3 adds a NEW signature on `IAppointmentRepository` for the
active-count probe; this repo stays unchanged otherwise.

### 7. `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs`

Update the `DoctorAvailability` config block (line 189-200):

```csharp
builder.Entity<DoctorAvailability>(b =>
{
    b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DoctorAvailabilities", CaseEvaluationConsts.DbSchema);
    b.ConfigureByConvention();
    b.Property(x => x.TenantId).HasColumnName(nameof(DoctorAvailability.TenantId));
    b.Property(x => x.AvailableDate).HasColumnName(nameof(DoctorAvailability.AvailableDate));
    b.Property(x => x.FromTime).HasColumnName(nameof(DoctorAvailability.FromTime));
    b.Property(x => x.ToTime).HasColumnName(nameof(DoctorAvailability.ToTime));
    b.Property(x => x.BookingStatusId).HasColumnName(nameof(DoctorAvailability.BookingStatusId));
    b.Property(x => x.Capacity)
        .HasColumnName(nameof(DoctorAvailability.Capacity))
        .IsRequired()
        .HasDefaultValue(1);
    b.HasOne<Location>().WithMany().IsRequired().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
    // AppointmentTypeId removed -- see DoctorAvailabilityAppointmentType join.
});

builder.Entity<DoctorAvailabilityAppointmentType>(b =>
{
    b.ToTable(CaseEvaluationConsts.DbTablePrefix + "DoctorAvailabilityAppointmentType", CaseEvaluationConsts.DbSchema);
    b.ConfigureByConvention();
    b.Property(x => x.TenantId).HasColumnName(nameof(DoctorAvailabilityAppointmentType.TenantId));
    b.HasKey(x => new { x.DoctorAvailabilityId, x.AppointmentTypeId });
    b.HasOne(x => x.DoctorAvailability)
        .WithMany(x => x.AppointmentTypes)
        .HasForeignKey(x => x.DoctorAvailabilityId)
        .IsRequired()
        .OnDelete(DeleteBehavior.Cascade);
    b.HasOne(x => x.AppointmentType)
        .WithMany()
        .HasForeignKey(x => x.AppointmentTypeId)
        .IsRequired()
        .OnDelete(DeleteBehavior.NoAction);
    b.HasIndex(x => new { x.DoctorAvailabilityId, x.AppointmentTypeId });
    // Mirror parent's soft-delete filter -- slot is soft-deletable.
    b.HasQueryFilter(x => !x.DoctorAvailability.IsDeleted);
});
```

Repeat the join entity config block in `CaseEvaluationTenantDbContext.cs`
with `DeleteBehavior.NoAction` instead of `Cascade` (per the
project's host-vs-tenant cascade convention -- see
`DoctorAppointmentType` for the canonical example).

### 8. EF Core migration: `Phase20_DoctorAvailabilityCapacityAndTypeSet`

Generate:

```bash
DOTNET_ENVIRONMENT=Development \
  dotnet ef migrations add Phase20_DoctorAvailabilityCapacityAndTypeSet \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host \
  --context CaseEvaluationDbContext
```

EF Core's auto-generated migration will:
- Add `Capacity` column with default value `1`.
- Create `AppEntity.DoctorAvailabilityAppointmentType` table.
- Drop the `AppointmentTypeId` FK and column.

**Critical:** the auto-generated `Up()` must be edited to
backfill the join rows BEFORE dropping the column. Insert a
hand-written `migrationBuilder.Sql(...)` block between the
table creation and the column drop:

```csharp
public partial class Phase20_DoctorAvailabilityCapacityAndTypeSet : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add Capacity with default 1.
        migrationBuilder.AddColumn<int>(
            name: "Capacity",
            schema: "AppEntity",
            table: "DoctorAvailabilities",
            type: "int",
            nullable: false,
            defaultValue: 1);

        // 2. Create the join table.
        migrationBuilder.CreateTable(
            name: "DoctorAvailabilityAppointmentType",
            schema: "AppEntity",
            columns: table => new
            {
                DoctorAvailabilityId = table.Column<Guid>(nullable: false),
                AppointmentTypeId = table.Column<Guid>(nullable: false),
                TenantId = table.Column<Guid>(nullable: true),
                ExtraProperties = table.Column<string>(nullable: true),
                ConcurrencyStamp = table.Column<string>(maxLength: 40, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_DoctorAvailabilityAppointmentType",
                    x => new { x.DoctorAvailabilityId, x.AppointmentTypeId });
                table.ForeignKey(
                    "FK_DoctorAvailabilityAppointmentType_DoctorAvailabilities_DoctorAvailabilityId",
                    x => x.DoctorAvailabilityId,
                    "AppEntity.DoctorAvailabilities",
                    "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    "FK_DoctorAvailabilityAppointmentType_AppointmentTypes_AppointmentTypeId",
                    x => x.AppointmentTypeId,
                    "AppEntity.AppointmentTypes",
                    "Id",
                    onDelete: ReferentialAction.NoAction);
            });

        migrationBuilder.CreateIndex(
            "IX_DoctorAvailabilityAppointmentType_DoctorAvailabilityId_AppointmentTypeId",
            schema: "AppEntity",
            table: "DoctorAvailabilityAppointmentType",
            columns: new[] { "DoctorAvailabilityId", "AppointmentTypeId" });

        // 3. Backfill: every existing slot with AppointmentTypeId IS NOT NULL
        // gets one join row. TenantId is mirrored from the parent slot.
        // Slots with AppointmentTypeId IS NULL stay with an empty set
        // (loose-mode parity).
        migrationBuilder.Sql(@"
            INSERT INTO [AppEntity].[DoctorAvailabilityAppointmentType]
                ([DoctorAvailabilityId], [AppointmentTypeId], [TenantId])
            SELECT
                da.[Id], da.[AppointmentTypeId], da.[TenantId]
            FROM [AppEntity].[DoctorAvailabilities] da
            WHERE da.[AppointmentTypeId] IS NOT NULL
              AND da.[IsDeleted] = 0;
        ");

        // 4. Drop the existing FK on DoctorAvailabilities.AppointmentTypeId,
        // then drop the column itself.
        // FK name auto-generated as 'FK_AppEntity.DoctorAvailabilities_AppEntity.AppointmentTypes_AppointmentTypeId'
        // -- confirm by running 'sp_fkeys' before editing this migration.
        migrationBuilder.DropForeignKey(
            name: "FK_AppEntity.DoctorAvailabilities_AppEntity.AppointmentTypes_AppointmentTypeId",
            schema: "AppEntity",
            table: "DoctorAvailabilities");
        migrationBuilder.DropIndex(
            name: "IX_AppEntity.DoctorAvailabilities_AppointmentTypeId",
            schema: "AppEntity",
            table: "DoctorAvailabilities");
        migrationBuilder.DropColumn(
            name: "AppointmentTypeId",
            schema: "AppEntity",
            table: "DoctorAvailabilities");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Down: re-add the column, restore the FK, copy back the FIRST type
        // for each slot (lossy -- multi-type slots collapse to the first row).
        migrationBuilder.AddColumn<Guid>(
            name: "AppointmentTypeId",
            schema: "AppEntity",
            table: "DoctorAvailabilities",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql(@"
            UPDATE da
            SET da.[AppointmentTypeId] = j.[AppointmentTypeId]
            FROM [AppEntity].[DoctorAvailabilities] da
            CROSS APPLY (
                SELECT TOP 1 [AppointmentTypeId]
                FROM [AppEntity].[DoctorAvailabilityAppointmentType]
                WHERE [DoctorAvailabilityId] = da.[Id]
                ORDER BY [AppointmentTypeId]
            ) j;
        ");

        migrationBuilder.CreateIndex(
            "IX_AppEntity.DoctorAvailabilities_AppointmentTypeId",
            schema: "AppEntity",
            table: "DoctorAvailabilities",
            column: "AppointmentTypeId");
        migrationBuilder.AddForeignKey(
            name: "FK_AppEntity.DoctorAvailabilities_AppEntity.AppointmentTypes_AppointmentTypeId",
            schema: "AppEntity",
            table: "DoctorAvailabilities",
            column: "AppointmentTypeId",
            principalSchema: "AppEntity",
            principalTable: "AppointmentTypes",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.DropTable(
            name: "DoctorAvailabilityAppointmentType",
            schema: "AppEntity");

        migrationBuilder.DropColumn(
            name: "Capacity",
            schema: "AppEntity",
            table: "DoctorAvailabilities");
    }
}
```

Important: confirm the auto-generated FK name by running
`EXEC sp_fkeys 'DoctorAvailabilities', 'AppEntity'` against the
local DB BEFORE editing the migration. If the name differs from
the literal above, the `DropForeignKey` call must match exactly.

### 9. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/`

#### 9a. `DoctorAvailabilityCreateDto.cs`

```csharp
using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityCreateDto
{
    public DateTime AvailableDate { get; set; }

    public TimeOnly FromTime { get; set; }

    public TimeOnly ToTime { get; set; }

    public BookingStatus BookingStatusId { get; set; } = Enum.GetValues<BookingStatus>()[0];

    public Guid LocationId { get; set; }

    /// <summary>
    /// 2026-05-15 -- the set of AppointmentType ids this slot will
    /// accept. Empty list means "any type accepted" (loose mode).
    /// </summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>
    /// 2026-05-15 -- max simultaneous appointments. Minimum 1.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Capacity { get; set; } = 1;
}
```

#### 9b. `DoctorAvailabilityUpdateDto.cs`

Mirror the create DTO; keep `ConcurrencyStamp`.

#### 9c. `DoctorAvailabilityDto.cs`

Add `List<Guid> AppointmentTypeIds = new();` and `int Capacity`.
Drop `Guid? AppointmentTypeId`.

#### 9d. `DoctorAvailabilityWithNavigationPropertiesDto.cs`

Replace `AppointmentTypeDto? AppointmentType` with
`List<AppointmentTypeDto> AppointmentTypes = new();`. Add
`int Capacity` -- wait, this DTO embeds the inner
`DoctorAvailabilityDto` so `Capacity` flows through there.

#### 9e. `GetDoctorAvailabilitiesInput.cs`

Drop `Guid? AppointmentTypeId` (matches IDoctorAvailabilityRepository).

#### 9f. `DoctorAvailabilitySlotPreviewDto.cs` and `DoctorAvailabilitySlotsPreviewDto.cs`

Replace `AppointmentTypeId : Guid?` with `AppointmentTypeIds :
List<Guid>` on the slot preview row. Add `Capacity : int`. Plan
3 fills in the preview generation logic; this plan only updates
the shape so the compile passes.

### 10. Mappers (`src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs`)

(2026-05-20 from readiness-check D4.) The existing
`DoctorAvailability*` mappers use bare `[Mapper]` instead of the
project convention `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`.
Do NOT change the attribute form in this PR; a separate cleanup
PR can align all mappers to the convention. Mixing the
convention upgrade with the schema rework would broaden the
diff and risk surfacing latent unrelated mapping gaps.

Update the existing partial classes so Mapperly produces the
new shape. Three changes:

1. `DoctorAvailabilityToDoctorAvailabilityDtoMappers`:
   Add explicit mapping from `AppointmentTypes`
   collection -> `AppointmentTypeIds : List<Guid>`:
   ```csharp
   [MapProperty(nameof(DoctorAvailability.AppointmentTypes), nameof(DoctorAvailabilityDto.AppointmentTypeIds))]
   public partial DoctorAvailabilityDto Map(DoctorAvailability source);
   ```
   Add a converter method:
   ```csharp
   private static List<Guid> MapAppointmentTypeIds(
       ICollection<DoctorAvailabilityAppointmentType> source)
       => source.Select(x => x.AppointmentTypeId).ToList();
   ```

2. `DoctorAvailabilityWithNavigationPropertiesToDoctorAvailabilityWithNavigationPropertiesDtoMapper`:
   Replace the single AppointmentType mapping with a list
   mapping; ensure the inner DTO mapper for `AppointmentType`
   is reused.

3. `DoctorAvailabilityToLookupDtoGuidMapper` AfterMap: no change
   (still uses `AvailableDate` + time range).

### 11. Auto-proxy regeneration

After backend compiles + tests pass:

```bash
cd angular
yarn nswag refresh
```

This regenerates `angular/src/app/proxy/doctor-availabilities/models.ts`
and the service to reflect `appointmentTypeIds` + `capacity`.
The proxy update is committed in this plan's PR. Plans 4-6 read
the new TS shape.

### 12. Update existing call sites that still pass `AppointmentTypeId`

Grep for `AppointmentTypeId` under
`src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/`
and replace with `AppointmentTypeIds` (single-list passthrough)
or, where the parameter was load-bearing for filter logic, drop
the filter (admin list path) or move to a list-overlap check
(no admin list site needs the filter today; the booking-form
picker site is plan 3).

Specific call sites to update (added 2026-05-20 from readiness-
check D3):

- **Admin list path** -- `DoctorAvailabilitiesAppService.GetListAsync`
  at lines 55-56 currently passes `input.AppointmentTypeId` into
  `GetCountAsync` and `GetListWithNavigationPropertiesAsync`.
  After `GetDoctorAvailabilitiesInput.AppointmentTypeId` is
  removed (section 9e) and the repo signatures lose the param
  (section 6), simply DROP the argument from both calls. No
  overlap-check filter is added here; the admin list does not
  need one today.

In `DoctorAvailabilitiesAppService.GeneratePreviewAsync`,
update the slot preview construction at line 264-275 to set
`AppointmentTypeIds = item.AppointmentTypeIds`. The conflict
detection logic stays IDENTICAL in this plan (no semantic
change; the rework lands in plan 3).

In `DoctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync`
(line 374-411), the existing loose-or-strict mode logic lives
INSIDE the `if (input.AppointmentTypeId.HasValue) { ... }`
guard at line 398-405. (Corrected 2026-05-20 from readiness-
check D2.) PRESERVE the guard; only swap the inner `Where`:

```csharp
// Current (line 398-405):
if (input.AppointmentTypeId.HasValue)
{
    var typeId = input.AppointmentTypeId.Value;
    query = query.Where(x => x.AppointmentTypeId == null || x.AppointmentTypeId == typeId);
}
```

becomes:

```csharp
if (input.AppointmentTypeId.HasValue)
{
    var typeId = input.AppointmentTypeId.Value;
    query = query.Where(x =>
        !x.AppointmentTypes.Any()
        || x.AppointmentTypes.Any(at => at.AppointmentTypeId == typeId));
}
```

The semantics is preserved: empty set = any type accepted;
non-empty set = type must be in the set. Callers that omit
`AppointmentTypeId` still see all available slots
(unfiltered-by-type lookup).

In `DoctorAvailabilitiesAppService.CreateAsync` and
`UpdateAsync`, update the call to the manager to pass the new
parameter list:

```csharp
var doctorAvailability = await _doctorAvailabilityManager.CreateAsync(
    input.LocationId,
    input.AppointmentTypeIds,
    input.AvailableDate,
    input.FromTime,
    input.ToTime,
    input.BookingStatusId,
    input.Capacity);
```

## Test plan

### `test/HealthcareSupport.CaseEvaluation.Domain.Tests/DoctorAvailabilities/DoctorAvailabilityTests.cs` (NEW)

TDD entity-level invariants:

| # | Test | Acceptance |
|---|------|------------|
| 1 | `Ctor_WhenCapacityIsZero_Throws` | `new DoctorAvailability(..., capacity: 0)` throws `AbpException` with `Range` message. |
| 2 | `Ctor_WhenCapacityIsNegative_Throws` | Same for -1. |
| 3 | `Ctor_WhenCapacityOmitted_Defaults1` | Default param = 1; entity property equals 1. |
| 4 | `AddAppointmentType_Twice_Idempotent` | Adding the same id twice produces a single join row. |
| 5 | `RemoveAllAppointmentTypesExceptGivenIds_KeepsOnlyListed` | Seed 3 types, call with [type1, type3], remaining set is {type1, type3}. |
| 6 | `AddAppointmentType_MirrorsTenantId` | `slot.TenantId = X`; after `slot.AddAppointmentType(...)`, join row's `TenantId == X`. |

### Update `test/HealthcareSupport.CaseEvaluation.Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs`

Drop the now-invalid `AppointmentTypeId == null` assertions on
the preview output. Where existing tests reference the old
`AppointmentTypeId` field on entities or DTOs, switch to
`AppointmentTypeIds`.

Add 2 facts:

| # | Test | Acceptance |
|---|------|------------|
| 7 | `CreateAsync_WhenCapacityIsZero_ThrowsValidation` | Calls `CreateAsync` with `Capacity = 0`. Throws ABP validation (`Range`). |
| 8 | `CreateAsync_WithThreeAppointmentTypes_PersistsAll` | Seed 3 AppointmentType rows + Location. Call `CreateAsync` with all three ids. Re-fetch via `GetWithNavigationPropertiesAsync` -- the dto's `AppointmentTypes` list has length 3. |

### Manual verification

1. `docker compose down -v && docker compose up -d --build`.
2. Confirm DbMigrator output mentions
   `Phase20_DoctorAvailabilityCapacityAndTypeSet`.
3. SQL probe:
   ```sql
   SELECT COUNT(*) AS PreMigrationSlotCount FROM AppEntity.DoctorAvailabilities;
   SELECT COUNT(*) AS JoinRowCount FROM AppEntity.DoctorAvailabilityAppointmentType;
   -- JoinRowCount should equal the number of pre-migration slots that had AppointmentTypeId IS NOT NULL.
   ```
4. Open the Angular generate page (`/doctor-management/doctor-availabilities/generate`).
   It loads. The capacity field is absent (UI work is plan 4),
   but the page renders without console errors.
5. Open DevTools Network. Click "Generate" with default form
   values. The request body now carries `appointmentTypeIds: []`
   (empty list) and `capacity: 1`. The 200 response contains the
   same shape on each slot preview row.

## Risk and rollback

**Blast radius:**
- One new entity, one column added, one column dropped, one
  migration. EF Core's `OnDelete=Cascade` on the join means
  deleting a slot cascades the join rows; deleting an
  `AppointmentType` is `NoAction` (won't compile / runtime
  blocks if join rows remain).
- DTO shape changes affect 11 sites (`DoctorAvailabilitiesAppService`
  + `CaseEvaluationApplicationMappers` + 6 DTO files +
  `IDoctorAvailabilityRepository` + EF repo + Angular proxy).
  Compilation surfaces every site.

**Rollback:**
- Revert the commit. Run `dotnet ef database update <previous
  migration>` against the affected DB. The `Down()` block
  re-adds `AppointmentTypeId` (lossy on multi-type slots) and
  drops both the join table and the `Capacity` column.

**Risk: FK name mismatch in `DropForeignKey`.** Mitigated by the
pre-flight `EXEC sp_fkeys` step.

**Risk: existing tests reference `AppointmentTypeId` on
entities/DTOs.** Mitigated by the bulk grep in step 12 plus the
test updates.

**Risk: Mapperly fails to generate the collection mapping.**
Mitigated by the explicit converter method in step 10. If
Mapperly's source-gen does not emit the call, the build error
points to the exact `[MapProperty]` line and the converter can
be wired manually.

## Verification

End-to-end:

0. (Pre-flight) `docker compose down -v && docker compose up -d
   --build` -- AuthServer reaches port 44368 + API reaches port
   44327 within 3 minutes on a cold tree without operator
   intervention. No `IOException` on `obj/.../ref/*.dll` in
   either container's log. See "Pre-flight: fix the docker
   bind-mount obj/ race" -> Verification block above for the
   detailed check sequence.
1. Fresh DB; migrate; seed AppointmentType rows.
2. Create a slot with `AppointmentTypeIds = []`, `Capacity = 2`
   via the proxy `service.create(...)` directly from a test
   harness. Re-fetch; both fields read back correctly.
3. Update the slot to `AppointmentTypeIds = [t1, t2]`. Re-fetch;
   `AppointmentTypeIds` contains both ids; `Capacity = 2`.
4. Delete the slot; SQL probe `SELECT COUNT(*) FROM
   AppEntity.DoctorAvailabilityAppointmentType WHERE
   DoctorAvailabilityId = <id>` returns 0 (cascade fired).
5. Existing booking flow (plan 3 hasn't run yet) still works
   end-to-end because plan 1 treats `AppointmentTypeIds` as a
   passthrough; the loose-or-strict-mode logic in
   `GetDoctorAvailabilityLookupAsync` is updated to read the
   new collection, and the conflict detection in
   `GeneratePreviewAsync` is unchanged.

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Plans 3-7 sequentially depend on this; merge order is strict.
