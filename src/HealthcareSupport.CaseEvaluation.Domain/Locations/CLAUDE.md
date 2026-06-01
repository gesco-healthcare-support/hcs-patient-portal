# Locations -- host-scoped physical exam offices shared across all tenants

## What lives here

Domain entity, manager, repository interface, and projection wrapper for examination
locations. Host admins manage; tenant-scoped DoctorAvailabilities and Appointments
reference them via cross-context FKs.

## Entity shape

```
Location : FullAuditedAggregateRoot<Guid>  (NO IMultiTenant)
  Name              string  [max 50, required]
  Address           string? [max 100]
  City              string? [max 50]
  ZipCode           string? [max 15]
  ParkingFee        decimal  (required, no DB default -- callers must supply it)
  IsActive          bool     (defaults true on CreateDto)
  StateId           Guid?    (FK -> State, SetNull on delete)
  AppointmentTypeId Guid?    (FK -> AppointmentType, SetNull on delete)
  DoctorLocations   ICollection<DoctorLocation>
```

## Conventions

**Host-only scoping.** `Location` has no `IMultiTenant`. Its DbContext config sits
inside `if (builder.IsHostDatabase())` in `CaseEvaluationDbContext.OnModelCreating`
(lines 51-66) and is absent from `CaseEvaluationTenantDbContext` for the entity itself.
Tenant users can still read Locations via `GetListAsync` -- confirmed by
`LocationsAreVisible_FromTenantContext` -- because EF resolves the host table through
the shared connection, not TenantId filtering.

**ParkingFee is required.** Non-nullable decimal with no database default. Any `CreateAsync`
call that omits it will fail at the DB level. The Angular form enforces `Validators.required`
on this field.

**FK asymmetry: cascade vs. NoAction.**
- `DoctorLocation.LocationId` -> **Cascade** -- deleting a Location auto-removes doctor
  association join rows. Configured in both host and tenant DbContexts.
- `DoctorAvailability.LocationId` -> **NoAction** (both DbContexts, required FK) --
  a Location with availability slots cannot be deleted.
- `Appointment.LocationId` -> **NoAction** (both DbContexts, required FK) --
  a Location with appointments cannot be deleted.
  The UI offers no pre-delete conflict preview; callers must handle the DB exception.

**No uniqueness check.** `LocationManager` does not guard against duplicate `Name` values.
Two Locations may legally share the same name.

**`AppointmentTypeId` semantics are undocumented.** The single optional FK links a Location
to one AppointmentType but no business logic consumes it beyond storage. Intent (e.g.,
preferred type per location) is unknown; replicate as-is and mark for product clarification.

## Gotchas

- FK-edge tests `DeleteAsync_WhenLocationReferencedBy*` are `Skip`ped -- SQLite in-memory
  ignores FK enforcement even with `PRAGMA foreign_keys = ON`. They encode target behavior
  and will activate once test infra switches to a FK-enforcing driver.
- `LocationToLocationDtoMappers` is plural while sibling mapper classes are singular --
  ABP Suite cosmetic artefact; safe to leave.
- `LocationsAppService` injects `IRepository<State>` and `IRepository<AppointmentType>`
  directly instead of their dedicated repository interfaces. Flag when touching that file.

## Related

- Root: CLAUDE.md
- docs/decisions/004-doctor-per-tenant-model.md
- docs/database/EF-CORE-DESIGN.md
