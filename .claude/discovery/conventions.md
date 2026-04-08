# Conventions — Patient Portal

## Naming Conventions

### DTOs
- `{Entity}CreateDto` — creation input
- `{Entity}UpdateDto` — update input (includes `IHasConcurrencyStamp`)
- `{Entity}Dto` — full output
- `{Entity}WithNavigationPropertiesDto` — rich output with FK entities
- `Get{Entities}Input` — filter/paging input (extends `PagedAndSortedResultRequestDto`)
- **Never** use `CreateUpdate{Entity}Dto`

### Services
- `I{Entities}AppService` — interface in Application.Contracts
- `{Entities}AppService` — implementation in Application
- Always extend `CaseEvaluationAppService` (not `ApplicationService` directly)

### Controllers
- `{Entity}Controller` — in `HttpApi/Controllers/{Feature}/`
- Route: `[Route("api/app/{entity-plural}")]`
- Extends `AbpController` AND implements `I{Entities}AppService`

### Angular
- Feature folder: `angular/src/app/{feature-kebab}/`
- Abstract component: `{feature-kebab}.abstract.component.ts`
- Concrete component: `{feature-kebab}.component.ts`
- Routes: `{feature-kebab}-routes.ts`
- Proxy: `angular/src/app/proxy/{feature-kebab}/` (auto-generated, never edit)

### Permissions
- Nested static class in `CaseEvaluationPermissions.cs`
- Pattern: `CaseEvaluation.{Feature}` (Default), `.Create`, `.Edit`, `.Delete`
- Registered in `CaseEvaluationPermissionDefinitionProvider.cs`

## File Organization

```
src/HealthcareSupport.CaseEvaluation.Domain.Shared/{Feature}/
  {Entity}Consts.cs         — MaxLength values, GetDefaultSorting()

src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/
  {StatusEnum}.cs           — State/type enums (shared across features)

src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/
  {Entity}.cs               — Entity class
  {Entity}Manager.cs        — DomainService (if business rules exist)
  {Entity}WithNavigationProperties.cs  — Projection wrapper
  I{Entity}Repository.cs    — Custom repository interface
  CLAUDE.md                 — Feature documentation

src/HealthcareSupport.CaseEvaluation.Application.Contracts/{Feature}/
  {Entity}CreateDto.cs, {Entity}UpdateDto.cs, {Entity}Dto.cs
  {Entity}WithNavigationPropertiesDto.cs
  Get{Entities}Input.cs
  I{Entities}AppService.cs

src/HealthcareSupport.CaseEvaluation.Application/{Feature}/
  {Entities}AppService.cs

src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/{Feature}/
  EfCore{Entity}Repository.cs

src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/{Feature}/
  {Entity}Controller.cs
```

## Key Patterns

### Riok.Mapperly (NOT AutoMapper)
```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class {Entity}To{Dto}Mapper : MapperBase<{Entity}, {Dto}>
{
    public override partial {Dto} Map({Entity} source);
    public override partial void Map({Entity} source, {Dto} destination);
}
```
- All mappers in `CaseEvaluationApplicationMappers.cs`
- `LookupDto<Guid>` mappers use `AfterMap()` to set `DisplayName`

### AppService Decoration
```csharp
[RemoteService(IsEnabled = false)]  // REQUIRED — prevents duplicate routes
public class {Entities}AppService : CaseEvaluationAppService, I{Entities}AppService
```

### Multi-tenancy
- `IMultiTenant` entities: tenant filter automatic via ABP
- Host-scoped entities: configured inside `if (builder.IsHostDatabase())` guard
- Cross-tenant lookup: `using (_dataFilter.Disable<IMultiTenant>()) { ... }`

### Localization
- JSON files in `Domain.Shared/Localization/CaseEvaluation/`
- Backend: `L("Key")` in permission providers
- Angular: `| abpLocalization` pipe in templates

## Anti-Patterns to Avoid

1. **Never use AutoMapper / `ObjectMapper.Map<>`** — this project uses Riok.Mapperly
2. **Never add `IMultiTenant` to host-scoped lookups** (Location, State, WcabOffice, AppointmentType, AppointmentStatus, AppointmentLanguage)
3. **Never omit `[RemoteService(IsEnabled = false)]`** on AppService classes
4. **Never skip the DomainManager** for Appointment create/update — business rules live there
5. **Never edit proxy files** in `angular/src/app/proxy/`
6. **Never use `ng serve`** — causes duplicate InjectionToken errors with ABP
