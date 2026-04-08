---
name: design-tests
description: "Read a feature's CLAUDE.md, identify testable surfaces, and write test files using the project's xUnit + Shouldly conventions and synthetic data only."
argument-hint: "<FeatureName> (e.g. Appointments, DoctorAvailabilities)"
---

# design-tests

Generates test files for a feature by reading its CLAUDE.md to understand the entity shape,
business rules, and API surface, then writing tests following the existing xUnit + Shouldly
patterns established by the Doctors and Books test suites.

---

## Step 1 — LOAD CONTEXT

1. Read the feature's CLAUDE.md:
   `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md`
   - If not found: stop and tell the user to run `/generate-feature-doc {Feature}` first

2. Read `.claude/discovery/test-patterns.md` for:
   - Base class chain (CaseEvaluationTestBase → CaseEvaluationApplicationTestBase)
   - EF Core collection attribute pattern
   - Data seed contributor pattern
   - Test method pattern (Arrange/Act/Assert with Shouldly)
   - Approved synthetic data patterns

3. Read existing test files for the reference features:
   - `test/.../Application.Tests/Doctors/DoctorApplicationTests.cs` — AppService test pattern
   - `test/.../Domain.Tests/Doctors/DoctorsDataSeedContributor.cs` — seed contributor pattern
   - `test/.../EntityFrameworkCore.Tests/.../Doctors/EfCoreDoctorsAppServiceTests.cs` — EF Core test pattern

4. Check if tests already exist for this feature:
   - Glob `test/**/*{Feature}*` and `test/**/*{EntitySingular}*`
   - If tests exist: read them to understand current coverage and avoid duplicating

---

## Step 2 — IDENTIFY TESTABLE SURFACES

From the feature's CLAUDE.md, build a test matrix:

### From Entity Shape section:
- Required fields → test that CreateAsync fails without them
- Max length constraints → test that values exceeding max length are rejected
- Default values → test that defaults are set correctly on creation
- Enum fields → test valid enum values; test invalid values are rejected

### From Business Rules section:
- Auto-generated values → test that the AppService computes them correctly (e.g., confirmation numbers)
- Frozen fields → test that UpdateAsync does NOT change frozen fields
- One-way operations → test that state changes are applied correctly
- Validation rules → test that invalid input is rejected
- Lookup filtering → test that filtered lookups return correct subsets

### From Relationships section:
- FK references → test that creating with invalid FK IDs fails gracefully
- Navigation property loading → test that GetListWithNavigationProperties returns related data

### From Permissions section:
- Test that authorized calls succeed
- Test that unauthorized calls are rejected (if the CLAUDE.md documents permission gaps, note them)

### From Known Gotchas section:
- Each gotcha may suggest a specific edge case test

---

## Step 3 — GENERATE TEST CODE

### 3a. Data Seed Contributor

Create `test/HealthcareSupport.CaseEvaluation.Domain.Tests/{Feature}/{Entity}DataSeedContributor.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;
using HealthcareSupport.CaseEvaluation.{Feature};

namespace HealthcareSupport.CaseEvaluation.{Feature};

public class {Entity}DataSeedContributor : IDataSeedContributor, ISingletonDependency
{
    private bool IsSeeded = false;
    private readonly I{Entity}Repository _{entityCamel}Repository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public {Entity}DataSeedContributor(I{Entity}Repository {entityCamel}Repository, IUnitOfWorkManager unitOfWorkManager)
    {
        _{entityCamel}Repository = {entityCamel}Repository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (IsSeeded) return;

        // Use hardcoded GUIDs so tests can assert against specific IDs
        await _{entityCamel}Repository.InsertAsync(new {Entity}(
            id: Guid.Parse("{guid1}"),
            // ... constructor params with SYNTHETIC data only
            // Use random hex strings for string fields
            // Use default enum values
        ));

        await _{entityCamel}Repository.InsertAsync(new {Entity}(
            id: Guid.Parse("{guid2}"),
            // ... second seed record with different synthetic values
        ));

        await _unitOfWorkManager!.Current!.SaveChangesAsync();
        IsSeeded = true;
    }
}
```

**Synthetic data rules:**
- Generate 2 new GUIDs for the seed records (use `Guid.NewGuid()` once to generate, then hardcode)
- All string values: random hex strings matching the entity's max length constraints
- All emails: `"{randomhex}@{randomhex}.com"`
- All enums: use `default` or the first enum value
- All dates: `DateTime.Parse("1990-01-01")` or similar obviously fake date
- All nullable FKs: `null` (unless the test specifically needs a relationship)

### 3b. Application Tests

Create `test/HealthcareSupport.CaseEvaluation.Application.Tests/{Feature}/{Entity}ApplicationTests.cs`:

```csharp
using System;
using System.Linq;
using Shouldly;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.{Feature};

public abstract class {Entity}AppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly I{Entities}AppService _{entitiesCamel}AppService;
    private readonly IRepository<{Entity}, Guid> _{entityCamel}Repository;

    public {Entity}AppServiceTests()
    {
        _{entitiesCamel}AppService = GetRequiredService<I{Entities}AppService>();
        _{entityCamel}Repository = GetRequiredService<IRepository<{Entity}, Guid>>();
    }

    [Fact]
    public async Task GetListAsync()
    {
        var result = await _{entitiesCamel}AppService.GetListAsync(new Get{Entities}Input());
        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
        result.Items.Any(x => x.{IdAccessor} == Guid.Parse("{guid1}")).ShouldBe(true);
        result.Items.Any(x => x.{IdAccessor} == Guid.Parse("{guid2}")).ShouldBe(true);
    }

    [Fact]
    public async Task GetAsync()
    {
        var result = await _{entitiesCamel}AppService.GetAsync(Guid.Parse("{guid1}"));
        result.ShouldNotBeNull();
        result.Id.ShouldBe(Guid.Parse("{guid1}"));
    }

    [Fact]
    public async Task CreateAsync()
    {
        var input = new {Entity}CreateDto
        {
            // ... all required fields with SYNTHETIC values
        };
        var serviceResult = await _{entitiesCamel}AppService.CreateAsync(input);
        var result = await _{entityCamel}Repository.FindAsync(c => c.Id == serviceResult.Id);
        result.ShouldNotBe(null);
        // Assert each field matches input
    }

    [Fact]
    public async Task UpdateAsync()
    {
        var input = new {Entity}UpdateDto()
        {
            // ... all updatable fields with different SYNTHETIC values
        };
        var serviceResult = await _{entitiesCamel}AppService.UpdateAsync(Guid.Parse("{guid1}"), input);
        var result = await _{entityCamel}Repository.FindAsync(c => c.Id == serviceResult.Id);
        result.ShouldNotBe(null);
        // Assert each field matches the updated values
    }

    [Fact]
    public async Task DeleteAsync()
    {
        await _{entitiesCamel}AppService.DeleteAsync(Guid.Parse("{guid1}"));
        var result = await _{entityCamel}Repository.FindAsync(c => c.Id == Guid.Parse("{guid1}"));
        result.ShouldBeNull();
    }
}
```

The `{IdAccessor}` depends on whether the DTO is flat (`Id`) or wrapped in a nav props DTO
(`{Entity}.Id`). Check the CLAUDE.md's GetListAsync return type.

### 3c. EF Core Test Wrapper

Create `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/{Feature}/EfCore{Entity}AppServiceTests.cs`:

```csharp
using HealthcareSupport.CaseEvaluation.{Feature};
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Applications.{Feature};

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCore{Entity}AppServiceTests : {Entity}AppServiceTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
```

This inherits all test methods and runs them against the SQLite in-memory database.

### 3d. Business Rule Tests (if applicable)

If the CLAUDE.md Business Rules section lists specific rules, add dedicated test methods:

```csharp
[Fact]
public async Task CreateAsync_ShouldAutoGenerateConfirmationNumber()
{
    // Test that auto-generated values are set correctly
}

[Fact]
public async Task UpdateAsync_ShouldNotChangeFrozenFields()
{
    // Test that frozen fields retain their original values after update
}
```

---

## Step 4 — PHI-SPECIFIC TESTS

If the feature's Entity Shape contains PHI fields (patient name, date of birth, phone,
address, email on Patient/Appointment/AppointmentEmployerDetail entities), add:

```csharp
[Fact]
public async Task CreateAsync_ShouldNotExposePhiInErrorMessages()
{
    // Attempt to create with invalid data
    // Verify error message does not contain PHI field values
}
```

---

## Step 5 — WRITE TEST FILES

Write all generated test files to the appropriate directories:
- Seed contributor: `test/HealthcareSupport.CaseEvaluation.Domain.Tests/{Feature}/`
- Application tests: `test/HealthcareSupport.CaseEvaluation.Application.Tests/{Feature}/`
- EF Core wrapper: `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/EntityFrameworkCore/Applications/{Feature}/`

If tests already exist for this feature, add new test methods to existing files
rather than creating duplicates. Never overwrite existing tests.

---

## Step 6 — VERIFY SYNTHETIC DATA

After writing all test files, scan them for prohibited patterns:
- Grep the written files for patterns that look like real identifiers
- Verify all string test values are random hex strings or TEST- prefixed
- Verify all emails use random hex domains (not real domains)

If any prohibited patterns found: replace with approved synthetic patterns.

---

## Step 7 — REPORT

```
Tests designed for {Feature}:
  Seed contributor: test/.../Domain.Tests/{Feature}/{Entity}DataSeedContributor.cs
  Application tests: test/.../Application.Tests/{Feature}/{Entity}ApplicationTests.cs
  EF Core wrapper: test/.../EntityFrameworkCore.Tests/.../Applications/{Feature}/EfCore{Entity}AppServiceTests.cs

  Test cases: {N} total
    CRUD: 5 (GetList, Get, Create, Update, Delete)
    Business rules: {N}
    PHI-specific: {N}

  Synthetic data: verified clean

  Run: dotnet test --filter "FullyQualifiedName~{Feature}"
```

---

## Constraints

- **ALL test data MUST be synthetic** — see approved patterns in test-patterns.md
- **Never overwrite existing tests** — add alongside them
- **Follow the existing pattern exactly** — abstract class in Application.Tests,
  concrete wrapper in EntityFrameworkCore.Tests with `[Collection]` attribute
- **Use hardcoded GUIDs in seed contributors** — tests assert against specific IDs
- **Test the AppService, not the repository directly** — the AppService is the public API
