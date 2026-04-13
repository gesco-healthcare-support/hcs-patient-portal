# Domain.Shared Layer

Shared constants, enums, localization resources, and multi-tenancy configuration. This project is referenced by **every other project** in the solution -- it sits at the bottom of the dependency chain.

## What Lives Here

- **`Enums/`** -- all domain enums (AppointmentStatusEnum, BookingStatusEnum, etc.). Shared across all layers so that DTOs, entities, and repositories agree on values.
- **`Localization/CaseEvaluation/`** -- ABP localization JSON files (`en.json`, etc.). Used by the `L("Key")` helper throughout the application.
- **`MultiTenancy/`** -- `MultiTenancyConsts.cs` defining `IsEnabled`, connection string name, and related tenancy configuration.
- **One folder per feature** (e.g. `Appointments/`) holding feature-scoped constants (max lengths, format strings) -- NOT enums, which live in `Enums/`.
- **Cross-cutting files:**
  - `CaseEvaluationDomainSharedModule.cs` -- ABP module definition
  - `CaseEvaluationDomainErrorCodes.cs` -- error code string constants
  - `CaseEvaluationGlobalFeatureConfigurator.cs` -- ABP global feature setup
  - `CaseEvaluationModuleExtensionConfigurator.cs` -- ABP module extension property config

## Conventions

1. **No business logic, no entity types, no services.** This project only contains constants, enums, and localization resources. Anything with behavior belongs in `Domain/` or higher.
2. **Enums go in `Enums/`.** One enum per file. Name enum types with the `Enum` suffix only when it disambiguates from an entity (e.g. `AppointmentStatusEnum` exists because there is also an `AppointmentStatus` entity).
3. **Constants (max lengths, formats) go in feature folders.** Example: `Appointments/AppointmentConsts.cs` holds things like `ClaimNumberMaxLength`.
4. **Localization is additive.** Add new keys to `Localization/CaseEvaluation/en.json`; do not remove keys referenced elsewhere. `L("Key")` calls throughout the app depend on these strings.
5. **This project must not reference anything else in the solution.** It is the root of the dependency graph.

## Key Files

| File | Purpose |
|------|---------|
| `MultiTenancy/MultiTenancyConsts.cs` | Tenancy configuration (enable flag, connection string name) |
| `Enums/*` | Domain enums shared by all layers |
| `Localization/CaseEvaluation/*.json` | UI and API strings by culture |
| `CaseEvaluationDomainErrorCodes.cs` | Error code constants for business exceptions |

## Related Docs

- [Root CLAUDE.md](../../CLAUDE.md) -- ABP Conventions (Localization)
- [docs/backend/ENUMS-AND-CONSTANTS.md](../../docs/backend/ENUMS-AND-CONSTANTS.md)
- [docs/architecture/MULTI-TENANCY.md](../../docs/architecture/MULTI-TENANCY.md)
