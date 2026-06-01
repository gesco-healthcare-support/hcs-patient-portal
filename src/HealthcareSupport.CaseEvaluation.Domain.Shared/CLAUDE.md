# Domain.Shared Layer

Shared constants, enums, localization resources, and multi-tenancy configuration. This project is referenced by **every other project** in the solution -- it sits at the bottom of the dependency chain.

## What Lives Here

- **`Enums/`** -- most domain enums (AppointmentStatusType, BookingStatus, Gender, etc.). 8 enums live in feature subfolders instead (see Gotchas).
- **`Localization/CaseEvaluation/`** -- ABP localization JSON files (`en.json` is the only maintained locale). Used by the `L("Key")` helper throughout the application.
- **`Localization/AbpUiOverride/`** + **`Localization/AccountOverride/`** -- override ABP base-resource strings that `CaseEvaluationResource` inheritance cannot reach (Razor pages do direct base-resource lookups, not inherited ones).
- **`Notifications/Events/`** -- 14 ETOs in namespace `HealthcareSupport.CaseEvaluation.Notifications.Events`. Forward-declared here so Domain and Application handlers can subscribe without circular references. Distinct from the `Appointments/` ETOs (e.g. `AppointmentStatusChangedEto`) which live in `Appointments/` under namespace `...Appointments`.
- **`Extensions/ExtraPropertyConverters.cs`** -- static helpers for reading typed values off ABP `IHasExtraProperties` entities (see Gotchas).
- **`MultiTenancy/`** -- `MultiTenancyConsts.cs` defining `IsEnabled`, connection string name, and related tenancy configuration.
- **One folder per feature** (e.g. `Appointments/`) holding feature-scoped constants (max lengths, format strings) and, in 8 cases, enums that could not live in `Enums/` due to dependency sequencing.
- **Cross-cutting files:**
  - `CaseEvaluationDomainSharedModule.cs` -- ABP module definition
  - `CaseEvaluationDomainErrorCodes.cs` -- error code string constants
  - `CaseEvaluationGlobalFeatureConfigurator.cs` -- ABP global feature setup
  - `CaseEvaluationModuleExtensionConfigurator.cs` -- ABP module extension property config; exposes `IdentityUser` extension property name consts (see Gotchas)

## Conventions

1. **No business logic, no entity types, no services.** This project only contains constants, enums, and localization resources. Anything with behavior belongs in `Domain/` or higher.
2. **New enums go in `Enums/`.** One enum per file. Name enum types with the `Enum` suffix only when it disambiguates from an entity (e.g. `AppointmentStatusEnum` exists because there is also an `AppointmentStatus` entity).
3. **Constants (max lengths, formats) go in feature folders.** Example: `Appointments/AppointmentConsts.cs` holds things like `ClaimNumberMaxLength`.
4. **Localization is additive.** Add new keys to `Localization/CaseEvaluation/en.json`; do not remove keys referenced elsewhere. `L("Key")` calls throughout the app depend on these strings.
5. **This project must not reference anything else in the solution.** It is the root of the dependency graph.

## Gotchas

### Enums in feature subfolders (tolerated deviation)
8 enums live outside `Enums/` by convention because they were authored alongside their feature or moved for dependency reasons. Do NOT "fix" them by relocating -- namespace + using-directive changes ripple into Angular proxy regeneration and seeded test data:
- `ExternalSignups/ExternalUserType.cs`
- `AppointmentDocuments/DocumentStatus.cs`, `PacketGenerationStatus.cs`, `PacketKind.cs`
- `AppointmentChangeRequests/ChangeRequestType.cs`
- `Appointments/Notifications/NotificationKind.cs`, `RecipientRole.cs`
- `Books/BookType.cs`

### IMPORTANT: Reading bool extension properties -- use ExtraPropertyConverters
`entity.GetProperty<bool>("flag")` throws on a freshly reloaded ABP entity because ABP's `TypeHelper.ChangeTypePrimitiveExtended<T>` cannot coerce a `JsonElement` to `bool` (ABP issues 12547, 19430, 23546). Always use:
```csharp
ExtraPropertyConverters.GetBoolOrDefault(entity, CaseEvaluationModuleExtensionConfigurator.IsExternalUserPropertyName)
```
The raw non-generic `GetProperty(string)` + coercion helper in `Extensions/ExtraPropertyConverters.cs` is the approved workaround for all bool extension properties.

### ModuleExtensionConfigurator -- use consts, never inline strings
`CaseEvaluationModuleExtensionConfigurator` exposes these `IdentityUser` extension property names as `public const string`:
- `FirmNamePropertyName`, `FirmEmailPropertyName`
- `IsExternalUserPropertyName`, `IsAccessorPropertyName`
- `UserSignatureBlobNamePropertyName`

Reference the const. Inline string literals will silently diverge if a name ever changes.

### en.json known duplicate-key bug
`Localization/CaseEvaluation/en.json` contains two blocks with keys `Enum:BookingStatus.8/9/10`. The first block (lines ~67-69) has the correct labels: `8=Available, 9=Reserved, 10=Booked`. The second block (lines ~228-230) has 9 and 10 swapped: `9=Booked, 10=Reserved`. JSON parsers use the last occurrence, so the live labels for 9 and 10 are wrong. Do not add a third copy; fix the second block when touching that file.

### AbpUiOverride / AccountOverride localization
ABP Razor pages look up strings directly in ABP's base resources (`AbpUi`, `AbpAccount`), not in `CaseEvaluationResource`. Overrides in `Localization/AbpUiOverride/en.json` and `Localization/AccountOverride/en.json` reach those pages; changes to `CaseEvaluationResource` do not.

## Key Files

| File | Purpose |
|------|---------|
| `MultiTenancy/MultiTenancyConsts.cs` | Tenancy configuration (enable flag, connection string name) |
| `Enums/*` | Most domain enums shared by all layers |
| `Localization/CaseEvaluation/en.json` | Only maintained locale; has known BookingStatus duplicate-key bug |
| `Localization/AbpUiOverride/en.json` | Overrides ABP UI base strings (Register->Sign up, Login->Sign in) |
| `Localization/AccountOverride/en.json` | Overrides ABP Account base strings |
| `Notifications/Events/` | 14 ETOs for notification fan-out (namespace Notifications.Events) |
| `Extensions/ExtraPropertyConverters.cs` | Safe bool read from ABP extension properties |
| `CaseEvaluationModuleExtensionConfigurator.cs` | IdentityUser extension property names as public consts |
| `CaseEvaluationDomainErrorCodes.cs` | Error code constants for business exceptions |

## Related Docs

- docs/backend/ENUMS-AND-CONSTANTS.md
- docs/architecture/MULTI-TENANCY.md
