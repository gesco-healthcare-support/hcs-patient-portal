# Application.Contracts Layer

DTOs, AppService interfaces, and permission constants. This is the contract surface exposed to HTTP clients (via HttpApi controllers) and to the Angular proxy generator.

## What Lives Here

- **One folder per feature** (mirrors `Domain/`): Appointments, Doctors, Patients, etc. Each folder contains the feature's DTOs and `I{Entity}AppService` interface.
- **`Permissions/`** -- `CaseEvaluationPermissions.cs` (constants) and `CaseEvaluationPermissionDefinitionProvider.cs` (ABP registration)
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
2. **Permissions are nested static classes.** Pattern:
   ```csharp
   public static class Appointments
   {
       public const string Default = GroupName + ".Appointments";
       public const string Create  = Default + ".Create";
       public const string Edit    = Default + ".Edit";
       public const string Delete  = Default + ".Delete";
   }
   ```
   Every new permission must also be registered in `CaseEvaluationPermissionDefinitionProvider.cs` -- otherwise it will not appear in the admin UI.
3. **`Shared/` holds cross-cutting DTOs only.** If a DTO is specific to one feature, put it in that feature folder. Examples of legitimate Shared DTOs: `LookupDto<TKey>`, common filter base classes.
4. **This project references Domain.Shared only.** It must not reference Domain or EntityFrameworkCore -- keeping that separation is what lets the Angular proxy generator compile against contracts without the full backend.

## Key Files

| File | Purpose |
|------|---------|
| `Permissions/CaseEvaluationPermissions.cs` | All permission string constants |
| `Permissions/CaseEvaluationPermissionDefinitionProvider.cs` | Registers permissions with ABP |
| `CaseEvaluationDtoExtensions.cs` | ABP extension property configuration |
| `{Feature}/I{Entity}AppService.cs` | Service interface per feature |
| `Shared/` | Cross-cutting DTOs |

## Related Docs

- [Root CLAUDE.md](../../CLAUDE.md) -- ABP Conventions (DTO naming, Permissions)
- [docs/backend/PERMISSIONS.md](../../docs/backend/PERMISSIONS.md)
- [docs/security/AUTHORIZATION.md](../../docs/security/AUTHORIZATION.md)
