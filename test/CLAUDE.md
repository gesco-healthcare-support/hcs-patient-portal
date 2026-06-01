# Test Suite

Five test projects that cover the domain, application, EF Core, and a console test app for the HTTP API client. All use xUnit + Shouldly + Autofac DI. Coverage is currently thin -- only Doctors, Books, and framework services have meaningful tests.

## What Lives Here

- **`HealthcareSupport.CaseEvaluation.TestBase/`** -- shared infrastructure, base classes, seed contributors, localization test data
- **`HealthcareSupport.CaseEvaluation.Domain.Tests/`** -- domain entity and domain service unit tests
- **`HealthcareSupport.CaseEvaluation.Application.Tests/`** -- AppService tests, uses in-memory SQLite
- **`HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/`** -- repository tests, uses in-memory SQLite
- **`HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/`** -- manual console app for ad-hoc HTTP API exploration (not automated)

## Conventions

1. **Base class chain:** Concrete tests inherit from `CaseEvaluationApplicationTestBase` or `CaseEvaluationDomainTestBase`, which in turn inherit from `CaseEvaluationTestBase<TModule>`. Do not skip the chain -- it wires up Autofac, `appsettings.json`, and test data seed contributors.

2. **EF-backed test classes MUST use the shared collection.** Annotate EF-backed test classes with the shared `[Collection]` (`CaseEvaluationTestConsts.CollectionDefinitionName`); without it, tests corrupt the shared in-memory SQLite DB because xUnit runs them in parallel.

3. **SQLite in-memory, not real SQL Server.** The test infrastructure uses `AbpEntityFrameworkCoreSqliteModule` via `CaseEvaluationTestBaseModule`. Tests run fast and require no SQL Server instance.

4. **Autofac, not default .NET DI.** Replace / substitute dependencies with `application.ServiceProvider.GetRequiredService<T>()` or via Autofac `IContainer` overrides inside the module override pattern.

5. **Test data via seed contributors.** New test data goes through classes like `DoctorsDataSeedContributor` (which uses hardcoded GUIDs that tests assert against). Don't insert test data manually in test methods -- add a seed contributor instead.

6. **HIPAA: never use real patient data.** All test fixtures must use synthetic data (fake names, fake DOBs, fake SSNs). This is enforced by `.claude/rules/hipaa-data.md` and `.claude/rules/test-data.md`.

7. **Known coverage gaps:** Patients, Appointments, Locations, DoctorAvailabilities, and all host-only lookup entities have no tests. New features touching these should add tests, but existing code modifications do not require back-filling tests.

## Key Files

| File | Purpose |
|------|---------|
| `HealthcareSupport.CaseEvaluation.TestBase/CaseEvaluationTestBase.cs` | Generic base for all tests |
| `HealthcareSupport.CaseEvaluation.TestBase/CaseEvaluationTestConsts.cs` | Shared constants incl. collection name |
| `HealthcareSupport.CaseEvaluation.TestBase/Data/*DataSeedContributor.cs` | Seed contributors with hardcoded test data |
| `HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/CaseEvaluationEntityFrameworkCoreTestBase.cs` | Base for repo tests (SQLite) |
| `HealthcareSupport.CaseEvaluation.Application.Tests/CaseEvaluationApplicationTestBase.cs` | Base for AppService tests |

## Running Tests

See docs/devops/TESTING-STRATEGY.md for the full test strategy. To run: `dotnet test` (all projects), `dotnet test test/<ProjectName>` (one project), or `dotnet test --filter "FullyQualifiedName~MethodName"` (single test).

## Related Docs

- [Root CLAUDE.md](../CLAUDE.md) -- Testing section
- [docs/devops/TESTING-STRATEGY.md](../docs/devops/TESTING-STRATEGY.md)
- [Project HIPAA Rules](../.claude/rules/hipaa-data.md)
- [Project Test Data Rules](../.claude/rules/test-data.md)
