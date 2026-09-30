# Locations -- per-office physical exam locations

## What lives here

Domain entity, manager, repository interface, and projection wrapper for examination
locations. Under database-per-office, each office owns its own locations; tenant-scoped
DoctorAvailabilities and Appointments reference them.

| File | Purpose |
|---|---|
| `Location.cs` | Aggregate root. `IMultiTenant`, so each office owns its own rows |
| `LocationManager.cs` | Create / update over the repository |
| `ILocationRepository.cs` | Custom repository: nav-prop list and get, joined to `State` and the offered appointment types |
| `LocationWithNavigationProperties.cs` | Read projection: the `Location` plus its optional `State` and its `List<AppointmentType>` |
| `LocationAppointmentType.cs` | M2M join (I3, 2026-06-08) -- which appointment types are offered at a clinic location. Composite PK on (LocationId, AppointmentTypeId) |
| `LocationDataSeedContributor.cs` | Seeds the ONE synthetic clinic of the synthetic TEST office only |

**`LocationAppointmentType` carries no `TenantId`, and that is deliberate** -- unlike
`DoctorAvailabilityAppointmentType`, which does. It does not need one: both sides of the join are
themselves `IMultiTenant`, so a row is only reachable through rows already scoped to the office,
and the composite key is unique inside that office's database.

An earlier version of that class comment justified the absence with "`Location` is not
IMultiTenant". **That was wrong -- `Location` IS `IMultiTenant`**, and the comment has since been
corrected in place (`LocationAppointmentType.cs:12-16`). Worth knowing because the same wrong claim
propagated into several documents before 2026-09-28.

**Seeding is narrower than people expect.** `LocationDataSeedContributor` gives a clinic ONLY to
the synthetic TEST office, so that office is walkable end to end. **Every other office starts with
no location at all** -- a real practice adds its own on the Locations page, and a seeded clinic
there would be a fake address in a real office. Office-scoped and idempotent via a count guard. So
an empty Locations page in a fresh real office is correct, not a seeding failure.

## Entity shape

See `Location.cs`. Key fields: `Name` (required, unique per office), `ParkingFee` (required
decimal, no DB default), `IsActive` (defaults true on CreateDto), `StateId?` (SetNull),
`AppointmentTypeId?` (SetNull), `DoctorLocations` collection.

## Conventions

**Tenant-scoped.** `Location` implements `IMultiTenant` and carries a `TenantId`, so ABP's
automatic tenant filter applies. Its configuration lives in
`CaseEvaluationSharedModelConfiguration` (not inside the `IsHostDatabase()` guard), because
both the host and the per-office contexts need it. `Location.cs` puts it plainly: "Office-owned
list (db-per-office): clinic locations are specific to each office. No seeded defaults; the
office creates its own."

> **This section previously said the opposite** -- that `Location` had no `IMultiTenant`, that
> its config sat inside the host-database guard, and that locations were host-scoped and shared
> across all tenants. All three were wrong, and they matter: a maintainer reasoning from them
> would expect a shared catalogue and might write a cross-office query. Corrected 2026-09-28
> after Adrian confirmed tenant-scoping is the intent. If you find the host-scoped claim
> repeated elsewhere, it is stale too -- `docs/parity-v2/07-admin-master-data.md` is one.

**Name is unique per office.** `LocationManager.EnsureNameIsUniqueAsync` throws
`LocationDuplicateName` on create and on update, excluding the row being edited. (This section
previously said there was no uniqueness check.) `FacilityId` is also unique when non-empty, via
a filtered index.

**ParkingFee is required.** Non-nullable decimal with no database default. Any `CreateAsync`
call that omits it will fail at the DB level. The Angular form enforces `Validators.required`
on this field. The manager additionally rejects a negative value.

**Delete is blocked while referenced.** `LocationManager.DeleteAsync` refuses when an
`Appointment` or a `DoctorAvailability` still references the location, so the FK constraints
below are a backstop rather than the first line of defence.

**FK asymmetry: cascade vs. NoAction.**

- `DoctorLocation.LocationId` -> **Cascade** -- deleting a Location auto-removes doctor
  association join rows.
- `DoctorAvailability.LocationId` -> **NoAction** (required FK) -- a Location with availability
  slots cannot be deleted.
- `Appointment.LocationId` -> **NoAction** (required FK) -- a Location with appointments cannot
  be deleted. The UI offers no pre-delete conflict preview; callers must handle the exception.

**`AppointmentTypeId` semantics are undocumented.** The single optional FK links a Location to
one AppointmentType but no business logic consumes it beyond storage. Intent (for example, a
preferred type per location) is unknown. Do not build on it without asking.

## Gotchas

- FK-edge tests `DeleteAsync_WhenLocationReferencedBy*` are `Skip`ped -- SQLite in-memory
  ignores FK enforcement even with `PRAGMA foreign_keys = ON`. They encode target behaviour and
  will activate once the test infrastructure uses a FK-enforcing driver.
- `LocationToLocationDtoMappers` is plural while sibling mapper classes are singular -- an ABP
  Suite cosmetic artefact; safe to leave.
- `LocationsAppService` injects `IRepository<State>` and `IRepository<AppointmentType>` directly
  instead of their dedicated repository interfaces. Flag when touching that file.
- Cross-office visibility is covered by `MultiOffice/MultiOfficeCatalogResolutionTests`. An
  earlier `LocationsAppServiceTests.LocationsAreVisible_FromTenantContext` was moved there; do
  not go looking for it under the old name.

## Related

- Root: CLAUDE.md
- docs/decisions/004-doctor-per-tenant-model.md
- docs/decisions/017-database-per-office-isolation.md
- docs/database/EF-CORE-DESIGN.md
