# Common Development Tasks

[Home](../INDEX.md) > [Onboarding](./) > Common Tasks

---

This guide walks through the most common development tasks in the Patient Portal. Every example uses real file paths and code patterns from this codebase. When in doubt, trace the Appointments feature end-to-end -- it's the reference implementation documented in `.claude/discovery/reference-pattern.md`.

## How to Add a New Entity

Adding a new entity requires files in 7 layers, following ABP's DDD structure. The order matters because each layer depends on the one before it.

### Layer 1: Domain.Shared (enums, constants)

Create `src/HealthcareSupport.CaseEvaluation.Domain.Shared/{Feature}/{Entity}Consts.cs`:

```csharp
// Real example from src/.../Domain.Shared/Locations/LocationConsts.cs
namespace HealthcareSupport.CaseEvaluation.Locations;

public static class LocationConsts
{
    public const int NameMaxLength = 50;
    public const int AddressMaxLength = 100;
    // ...
    
    private const string DefaultSorting = "{0}Name asc";
    
    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "Location." : string.Empty);
    }
}
```

If the entity has a status or type enum, create it in `Domain.Shared/Enums/`.

### Layer 2: Domain (entity, manager, repository interface)

Create the entity in `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/{Entity}.cs`:

```csharp
// Real pattern from src/.../Domain/States/State.cs
public class State : FullAuditedAggregateRoot<Guid>
{
    [NotNull]
    public virtual string Name { get; set; }

    protected State() { }

    public State(Guid id, string name)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Name = name;
    }
}
```

**Key decisions:**
- `FullAuditedAggregateRoot<Guid>` -- soft delete + audit fields (most entities use this)
- Add `IMultiTenant` if the entity should be tenant-scoped (see [Multi-Tenancy](../architecture/MULTI-TENANCY.md))
- Host-scoped lookups (State, Location, AppointmentType) do NOT implement `IMultiTenant`

Create the domain manager in `src/.../Domain/{Feature}/{Entity}Manager.cs` if business rules exist. Even simple entities have managers in this project.

Create the repository interface in `src/.../Domain/{Feature}/I{Entity}Repository.cs`.

### Layer 3: Application.Contracts (DTOs, interfaces, permissions)

Create DTOs following the naming convention:
- `{Entity}CreateDto.cs` -- creation input (never `CreateUpdate{Entity}Dto`)
- `{Entity}UpdateDto.cs` -- implements `IHasConcurrencyStamp`
- `{Entity}Dto.cs` -- extends `FullAuditedEntityDto<Guid>`, implements `IHasConcurrencyStamp`
- `Get{Entities}Input.cs` -- extends `PagedAndSortedResultRequestDto`
- `I{Entities}AppService.cs` -- service interface

Add permissions in `CaseEvaluationPermissions.cs`:
```csharp
public static class YourFeature
{
    public const string Default = GroupName + ".YourFeature";
    public const string Create = Default + ".Create";
    public const string Edit = Default + ".Edit";
    public const string Delete = Default + ".Delete";
}
```

Register in `CaseEvaluationPermissionDefinitionProvider.cs`.

### Layer 4: Application (AppService, mappers)

Create the AppService. **Critical: always add `[RemoteService(IsEnabled = false)]`:**

```csharp
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.YourFeature.Default)]
public class YourFeaturesAppService : CaseEvaluationAppService, IYourFeaturesAppService
```

Add Riok.Mapperly mappers in `CaseEvaluationApplicationMappers.cs` (NOT AutoMapper):
```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EntityToEntityDtoMappers : MapperBase<Entity, EntityDto>
{
    public override partial EntityDto Map(Entity source);
    public override partial void Map(Entity source, EntityDto destination);
}
```

### Layer 5: EntityFrameworkCore (DbContext, repository, migration)

Configure the entity in `CaseEvaluationDbContext.cs`. If host-scoped, wrap in `if (builder.IsHostDatabase())`. If tenant-scoped, also configure in `CaseEvaluationTenantDbContext.cs`.

Create the EF Core repository in `src/.../EntityFrameworkCore/{Feature}/EfCore{Entity}Repository.cs`.

Create a migration:
```bash
dotnet ef migrations add Add{Entity} \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
```

### Layer 6: HttpApi (controller)

Create `src/.../HttpApi/Controllers/{Feature}/{Entity}Controller.cs`. Controllers are NOT auto-wired in this project -- you must manually delegate every method:

```csharp
[RemoteService]
[Area("app")]
[ControllerName("YourEntity")]
[Route("api/app/your-entities")]
public class YourEntityController : AbpController, IYourEntitiesAppService
{
    protected IYourEntitiesAppService _appService;
    
    public YourEntityController(IYourEntitiesAppService appService)
    {
        _appService = appService;
    }
    
    [HttpGet]
    public virtual Task<PagedResultDto<YourEntityDto>> GetListAsync(GetYourEntitiesInput input)
    {
        return _appService.GetListAsync(input);
    }
    // ... delegate every method
}
```

### Layer 7: Angular (regenerate proxies, create UI)

```bash
cd angular
abp generate-proxy
```

This regenerates the TypeScript proxy files in `angular/src/app/proxy/`. **Never edit proxy files manually.**

Create your Angular components following the abstract/concrete pattern in `angular/src/app/{feature-kebab}/`.

## How to Add a Field to an Existing Entity

1. Add the property to the entity class
2. Update the Manager's `CreateAsync`/`UpdateAsync` parameters
3. Add the field to `CreateDto`, `UpdateDto`, and `Dto`
4. Update the AppService to pass the new field
5. If the field needs a max length: add to `{Entity}Consts.cs` and `[StringLength]` on DTOs
6. Configure in DbContext if needed (max length, required, index)
7. Create a migration: `dotnet ef migrations add Add{Field}To{Entity} --project src/...EntityFrameworkCore --startup-project src/...HttpApi.Host`
8. Update the controller if the method signature changed
9. Regenerate Angular proxies: `cd angular && abp generate-proxy`
10. Update the Angular form template

## How to Run Database Migrations

```bash
# Create a new migration
dotnet ef migrations add <MigrationName> \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host

# Apply migrations (run the DbMigrator)
dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator
```

See [Migration Guide](../database/MIGRATION-GUIDE.md) for rollback procedures and common issues.

## How to Run Tests

```bash
# All tests
dotnet test

# Just AppService tests
dotnet test test/HealthcareSupport.CaseEvaluation.Application.Tests

# Just EF Core tests
dotnet test test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests

# Single test method
dotnet test --filter "FullyQualifiedName~DoctorsAppServiceTests.GetListAsync"

# Angular tests
cd angular && npm test
```

To add tests for a new feature, see [Testing Strategy](../devops/TESTING-STRATEGY.md) and `.claude/discovery/test-patterns.md` for the base class chain and seed contributor pattern.

## How to Regenerate Angular Proxies

After ANY backend API change (new endpoint, changed DTO, renamed method):

```bash
cd angular
abp generate-proxy
```

This updates files in `angular/src/app/proxy/`. Never edit these files manually -- your changes will be overwritten on the next proxy generation.

## How to Add Localization Text

Add keys to `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`:

```json
{
  "Permission:YourFeature": "Your Feature",
  "Permission:Create": "Create",
  "Menu:YourFeature": "Your Feature"
}
```

Use in C#: `L("Permission:YourFeature")`
Use in Angular: `{{ '::Menu:YourFeature' | abpLocalization }}`

## Quick Command Reference

| Task | Command |
|------|---------|
| Restore .NET packages | `dotnet restore` |
| Install Angular deps | `cd angular && npm install` |
| Start SQL Server | Docker: `docker start sql-server` / LocalDB: `sqllocaldb start MSSQLLocalDB` |
| Run AuthServer | `dotnet run --project src/...AuthServer` |
| Run API Host | `dotnet run --project src/...HttpApi.Host` |
| Build Angular | `cd angular && npx ng build --configuration development` |
| Serve Angular | `npx serve -s dist/CaseEvaluation/browser -p 4200` |
| Add migration | `dotnet ef migrations add <Name> --project src/...EntityFrameworkCore --startup-project src/...HttpApi.Host` |
| Apply migrations | `dotnet run --project src/...DbMigrator` |
| Regenerate proxies | `cd angular && abp generate-proxy` |
| Run all tests | `dotnet test` |

---

**Related:**
- [Getting Started](GETTING-STARTED.md) -- first-time setup
- [Development Setup](../devops/DEVELOPMENT-SETUP.md) -- detailed environment configuration
- [DDD Layers](../architecture/DDD-LAYERS.md) -- understanding the layer structure
- [ABP Framework](../architecture/ABP-FRAMEWORK.md) -- ABP-specific patterns and conventions
- [Reference Pattern](../../.claude/discovery/reference-pattern.md) -- Appointments traced end-to-end
