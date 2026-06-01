# Doctors -- IME physician profiles, one per tenant

Each tenant represents exactly one doctor's office; the schema allows N doctors per tenant
but the product enforces 1:1. `DoctorAvailability` and `Appointment` carry NO `DoctorId`
FK -- they reach the doctor via tenant scope.

## What lives here

- `Doctor.cs` -- `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`; owns M2M collections
  `AppointmentTypes` / `Locations` via join entities `DoctorAppointmentType` /
  `DoctorLocation` (composite PK, no own Guid).
- `DoctorManager.cs` -- `CreateAsync` / `UpdateAsync`; `SetAppointmentTypesAsync` /
  `SetLocationsAsync` do replace-all collection sync (partial list REMOVES omitted IDs).
- `IDoctorRepository.cs` -- nav-prop queries filtered by name, email, identityUserId,
  appointmentTypeId, locationId.
- `DoctorWithNavigationProperties.cs` -- projection: `Doctor` + `IdentityUser?` +
  `List<AppointmentType>` + `List<Location>`.

Entity shape: `FirstName`/`LastName` max 50, `Email` max 49 (see Gotchas), `Gender`
(Male=1/Female=2/Other=3), optional `IdentityUserId` FK.

## Conventions

### UpdateAsync syncs the linked IdentityUser

When `IdentityUserId.HasValue`, `DoctorsAppService.UpdateAsync` calls
`_userManager.UpdateAsync(user)` setting `Name`, `Surname`, and `Email`. Failures throw
`UserFriendlyException`. This side-effect is NOT visible in the method signature.

### Collection sync is replace-all

`SetAppointmentTypesAsync` / `SetLocationsAsync` verify each ID exists, then call
`RemoveAllExceptGivenIds` followed by `Add`. Sending a partial list on update drops the
rest. If every supplied ID is missing from the lookup table the method returns without
clearing -- it does NOT wipe existing rows.

### Tenant provisioning seed

`DoctorTenantAppService.CreateAsync` overrides ABP SaaS `TenantAppService.CreateAsync`.
After `base.CreateAsync(input)`, it calls `CurrentTenant.Change(tenantId)` and then:
creates/updates an admin `IdentityUser`, creates a `Doctor` row (`firstName = input.Name`,
`lastName = ""`, `gender = Male`), and ensures a "Doctor" role exists. If a Doctor with
that `IdentityUserId` already exists it is updated, not duplicated. `lastName = ""`
satisfies `Check.Length(min=0)` but will fail the standard form validation if re-saved
without a real last name.

### Host vs Tenant GetList filter

`DoctorsAppService.GetListAsync` disables `IDataFilter<IMultiTenant>` only when
`CurrentTenant.Id == null` (host context). `GetAsync` and `UpdateAsync` do NOT disable the
filter -- from host context they require `_currentTenant.Change(tenantId)`.

## Gotchas

1. Email max 49, not 50/256. `DoctorConsts.EmailMaxLength = 49`; code-gen artifact.
2. Cascade vs NoAction. Host DB uses `Cascade` for `DoctorAppointmentType` and
   `DoctorLocation` join FK; tenant DB uses `NoAction`. Deleting a Doctor in tenant context
   fails at the DB level if join rows exist -- delete join rows first.
3. Explicit `HasOne<Tenant>()` FK on host only. `CaseEvaluationDbContext` adds
   `HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(SetNull)`. The
   tenant `DbContext` omits this FK entirely.
4. No uniqueness guard. Neither `DoctorManager` nor `DoctorsAppService` checks for
   duplicate Email or IdentityUserId at create time.
5. Two AppServices, two proxies. `DoctorsAppService` (CRUD) and `DoctorTenantAppService`
   (tenant provisioning, extends `Volo.Saas.Host.TenantAppService`, no `[Authorize]` of
   its own). Keep them separate -- the tenant service is host-only infrastructure.

## Related

- docs/decisions/004-doctor-per-tenant-model.md
- docs/database/EF-CORE-DESIGN.md
