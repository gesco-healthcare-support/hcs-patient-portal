# Application.Contracts Layer

DTOs, AppService interfaces, and permission constants. This is the contract surface exposed to HTTP clients (via HttpApi controllers) and to the Angular proxy generator.

## What Lives Here

- **One folder per feature** (mirrors `Domain/`): Appointments, Doctors, Patients, etc. Each folder contains the feature's DTOs and `I{Entity}AppService` interface.
- **`Permissions/`** -- `CaseEvaluationPermissions.cs` (constants) and `CaseEvaluationPermissionDefinitionProvider.cs` (ABP registration)
- **`Notifications/`** -- `INotificationDispatcher` and `INotificationTemplateRenderer` (in-process interfaces; see Gotchas)
- **`Shared/`** -- cross-cutting DTOs (lookup DTOs, shared filters) used across multiple features
- **`CaseEvaluationApplicationContractsModule.cs`** -- ABP module definition
- **`CaseEvaluationDtoExtensions.cs`** -- ABP extension property wiring

## Conventions

1. **DTO naming is strict:**
   - `{Entity}CreateDto` -- input for `CreateAsync`
   - `{Entity}UpdateDto` -- input for `UpdateAsync`
   - `{Entity}Dto` -- read model
   - `{Entity}WithNavigationPropertiesDto` -- read model with joined related entities
   - `Get{Entities}Input` -- list/filter input
   - **Do not** use `CreateUpdate{Entity}Dto` -- that is ABP's older combined pattern; this project keeps create and update separate.
2. **Permissions are nested static classes.** See `CaseEvaluationPermissions.cs` for the
   nested-static pattern. Every new permission must also be registered in
   `CaseEvaluationPermissionDefinitionProvider.cs` -- otherwise it will not appear in the
   admin UI.
3. **`Shared/` holds cross-cutting DTOs only.** If a DTO is specific to one feature, put it in that feature folder. Examples of legitimate Shared DTOs: `LookupDto<TKey>`, common filter base classes.
4. **This project references Domain.Shared only.** It must not reference Domain or EntityFrameworkCore -- keeping that separation is what lets the Angular proxy generator compile against contracts without the full backend.

## Gotchas

**Dashboard permissions have no `Default` parent.** `Dashboard.Host` and `Dashboard.Tenant`
are registered directly on the permission group via `myGroup.AddPermission(...)` with
`MultiTenancySides.Host` / `MultiTenancySides.Tenant`. The standard `Default -> Create/Edit/Delete`
pattern does NOT apply here. Do not add a `Default` child.

**`Notifications/` interfaces are in-process only, not HTTP endpoints.** The folder lives
in Application.Contracts because multiple Application-layer features consume it, but
`INotificationDispatcher` and `INotificationTemplateRenderer` are injected in-process --
no controller exposes them. Do not add an HttpApi controller for these interfaces.

**`IUserSignatureAppService` does NOT extend `IApplicationService`.** It is a plain C#
interface. The public methods (`GetInfoAsync`, `UploadAsync`, `DownloadAsync`, `DeleteAsync`)
are HTTP-exposed via the manual controller. `GetBytesByUserIdAsync` is an in-process method
called by the packet resolver; the implementation carries `[RemoteService(IsEnabled = false)]`
to prevent ABP from auto-routing it.

**`PatientDto.SocialSecurityNumber` carries only the masked last-4** in all standard
payloads (e.g., `GET api/app/patients/{id}`). The full SSN is returned exclusively by
`IPatientsAppService.GetFullSsnAsync(Guid id)` as `SsnRevealDto`, gated by
`Patients.RevealSsn` permission plus the internal-or-owner check (`SsnRevealAccess`).
Each call is captured in ABP's HTTP audit log.

**`Books/CreateUpdateBookDto.cs` is scaffolding residue -- do not copy it.** It uses the
banned combined create+update pattern (`CreateUpdateBookDto`). All new features must use
separate `{Entity}CreateDto` and `{Entity}UpdateDto` classes.

## Key Files

| File | Purpose |
|------|---------|
| `Patients/SsnRevealDto.cs` | Full-SSN payload for the audited reveal endpoint (non-obvious -- see Gotchas) |

## Related Docs

- docs/backend/PERMISSIONS.md
- docs/security/AUTHORIZATION.md
